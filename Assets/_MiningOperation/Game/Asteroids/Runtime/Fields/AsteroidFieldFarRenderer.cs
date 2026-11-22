using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// v2.2: one LOW-detail mesh per mineable asteroid, generated from the EXACT
    /// persisted seed/profile/version (never a shared unrelated rock preset).
    /// No GameObjects. Visible meshes are generated in small frame-budgeted steps.
    /// Tradeoff: Graphics.RenderMesh makes one draw per visible mineable rock;
    /// decorative debris still uses GPU instancing. This favors correct silhouettes.
    /// </summary>
    internal sealed class AsteroidFieldFarRenderer : IDisposable
    {
        private readonly Transform root;
        private readonly AsteroidFieldSettings settings;
        private readonly Dictionary<int, Mesh> uniqueMeshes = new Dictionary<int, Mesh>();
        private readonly HashSet<int> failures = new HashSet<int>();
        private readonly List<int> visibleIndices = new List<int>();
        private readonly HashSet<int> detailed = new HashSet<int>();
        private AsteroidVisibilityGrid visibilityGrid;
        private AsteroidFieldSnapshot snapshot;
        private Material material;
        private readonly Material controllerMaterial;
        // One property block per asteroid. Graphics.RenderMesh may defer submission;
        // do not mutate a shared block before the GPU has consumed the previous draw.
        private readonly Dictionary<int, MaterialPropertyBlock> perAsteroidProperties = new Dictionary<int, MaterialPropertyBlock>();
        private int warmCursor;
        private double simulationSeconds;
        public int VisibleCount { get; private set; }
        public int CachedMeshes => uniqueMeshes.Count;
        public bool Ready => material != null && snapshot != null;

        public AsteroidFieldFarRenderer(Transform fieldRoot, AsteroidFieldSettings fieldSettings, Material sharedSurface = null)
        {
            root = fieldRoot;
            settings = fieldSettings;
            controllerMaterial = sharedSurface;
        }

        public void Initialize(AsteroidFieldSnapshot field)
        {
            Dispose();
            if (field == null || field.asteroids == null || field.asteroids.Count == 0 ||
                settings == null || settings.asteroidSettings == null || !settings.useGpuFarField)
                return;
            snapshot = field;
            material = controllerMaterial != null ? controllerMaterial : AsteroidStableMaterials.Create(
                settings.unifiedAsteroidMaterial != null ? settings.unifiedAsteroidMaterial :
                    ((field.contentType == MiningFieldContent.Ice || field.contentType == MiningFieldContent.IceShelf) && settings.iceMaterial != null
                        ? settings.iceMaterial : settings.asteroidMaterial),
                settings.allowExperimentalRockShaders,
                (field.contentType == MiningFieldContent.Ice || field.contentType == MiningFieldContent.IceShelf)
                    ? new Color(.76f, .89f, 1f, 1f) : new Color(.83f, .82f, .78f),
                "MO Exact Far Asteroids (runtime)",
                (field.contentType == MiningFieldContent.Ice || field.contentType == MiningFieldContent.IceShelf) ? null : settings.rockAlbedoTexture,
                (field.contentType == MiningFieldContent.Ice || field.contentType == MiningFieldContent.IceShelf) ? null : settings.rockNormalTexture,
                settings.rockNormalStrength);
            if (material == null) { snapshot = null; return; }
            visibilityGrid = new AsteroidVisibilityGrid(650f);
            for (int i = 0; i < snapshot.asteroids.Count; ++i)
            {
                AsteroidFieldRecord record = snapshot.asteroids[i];
                if (record != null && record.asteroid != null)
                    visibilityGrid.Add(i, record.localPosition,
                        record.asteroid.diameterMeters * 0.65f);
            }
        }

        /// <summary>For the near renderer: borrow this very same low-detail mesh at handoff.</summary>
        public Mesh GetOrCreateMesh(AsteroidFieldRecord record)
        {
            if (!Ready || record == null || record.asteroid == null) return null;
            if (uniqueMeshes.TryGetValue(record.index, out Mesh existing) && existing != null)
                return existing;
            if (failures.Contains(record.index)) return null;
            try
            {
                var body = new AsteroidData
                {
                    seed = record.asteroid.seed,
                    generatorVersion = record.asteroid.generatorVersion,
                    diameterMeters = record.asteroid.diameterMeters,
                    geometryProfile = record.asteroid.geometryProfile
                };
                AsteroidGeneratorSettings generation = (snapshot.contentType == MiningFieldContent.Ice || snapshot.contentType == MiningFieldContent.IceShelf) &&
                    settings.iceGeneratorSettings != null ? settings.iceGeneratorSettings : settings.asteroidSettings;
                Mesh mesh = AsteroidGeometryGenerator.Generate(body, generation,
                    Mathf.Clamp(settings.exactFarSubdivisions, 1, 3));
                // Explicitly NOT the generated V2 prefix: AsteroidInstance must not
                // destroy this borrowed mesh when switching to a detailed one.
                mesh.name = "MO_FarExact_" + record.asteroid.asteroidId;
                mesh.hideFlags = HideFlags.DontSave;
                uniqueMeshes[record.index] = mesh;
                return mesh;
            }
            catch (Exception e)
            {
                failures.Add(record.index);
                UnityEngine.Debug.LogWarning("[MO] Exact far mesh failed for " +
                    record.asteroid.asteroidId + ": " + e.Message);
                return null;
            }
        }

        public void Refresh(AsteroidFieldSnapshot field, Vector3 viewer,
            HashSet<int> detailedIndices, Camera camera = null)
        {
            visibleIndices.Clear();
            detailed.Clear();
            VisibleCount = 0;
            if (!Ready || !ReferenceEquals(field, snapshot) || visibilityGrid == null) return;
            if (detailedIndices != null) detailed.UnionWith(detailedIndices);
            visibilityGrid.CollectVisible(root, viewer, camera,
                settings.distantDrawDistanceMeters, visibleIndices);
            float maxDistanceSq = settings.distantDrawDistanceMeters * settings.distantDrawDistanceMeters;
            for (int i = visibleIndices.Count - 1; i >= 0; --i)
            {
                AsteroidFieldRecord rec = snapshot.asteroids[visibleIndices[i]];
                if (rec == null || rec.asteroid == null ||
                    (detailed.Contains(rec.index) && (!AsteroidStableMaterials.IsUnified(material) ||
                     (root.TransformPoint(rec.localPosition) - viewer).magnitude <
                         Mathf.Min(settings.spawnDistanceMeters, settings.detailedDistanceMeters) * .85f)) ||
                    (settings.hideDepletedAsteroids && rec.asteroid.remainingMassKg <= 0) ||
                    (root.TransformPoint(rec.localPosition) - viewer).sqrMagnitude > maxDistanceSq)
                    visibleIndices.RemoveAt(i);
            }
            // Build nearest not-yet-ready silhouettes first.
            visibleIndices.Sort((a, b) =>
            {
                float da = (root.TransformPoint(snapshot.asteroids[a].localPosition) - viewer).sqrMagnitude;
                float db = (root.TransformPoint(snapshot.asteroids[b].localPosition) - viewer).sqrMagnitude;
                return da.CompareTo(db);
            });
            warmCursor = 0;
        }

        public void Render(double timeSeconds, Vector3 viewerPosition)
        {
            if (!Ready || visibleIndices.Count == 0) { VisibleCount = 0; return; }
            simulationSeconds = timeSeconds;
            WarmVisibleMeshes();
            Matrix4x4 parent = root.localToWorldMatrix;
            var rp = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true
            };
            int drawn = 0;
            foreach (int i in visibleIndices)
            {
                if (drawn >= settings.maxFarRenderDraws) break;
                AsteroidFieldRecord rec = snapshot.asteroids[i];
                if (rec == null || rec.asteroid == null ||
                    (detailed.Contains(rec.index) && !AsteroidStableMaterials.IsUnified(material)) ||
                    !uniqueMeshes.TryGetValue(rec.index, out Mesh mesh) || mesh == null) continue;
                Quaternion rotation = settings.enableAsteroidRotation
                    ? AsteroidSpin.Evaluate(rec.localRotation, rec.asteroid.seed,
                        rec.asteroid.diameterMeters, simulationSeconds, settings.rotationSpeedMultiplier * settings.mineableRotationMultiplier)
                    : rec.localRotation;
                Matrix4x4 objectToWorld = parent * Matrix4x4.TRS(rec.localPosition,
                    rotation, Vector3.one);
                if (AsteroidStableMaterials.IsUnified(material))
                {
                    float distance = Vector3.Distance(root.TransformPoint(rec.localPosition), viewerPosition);
                    float handoff = Mathf.Min(settings.spawnDistanceMeters, settings.detailedDistanceMeters);
                    float nearAlpha = detailed.Contains(rec.index) ?
                        Mathf.Clamp01((handoff - distance) / Mathf.Max(35f, handoff * .15f)) : 0f;
                    if (!perAsteroidProperties.TryGetValue(rec.index, out MaterialPropertyBlock props))
                    {
                        props = new MaterialPropertyBlock();
                        perAsteroidProperties.Add(rec.index, props);
                    }
                    props.SetFloat("_MO_ObjectRadius", Mathf.Max(.001f, rec.asteroid.diameterMeters * .5f));
                    props.SetFloat("_MO_SurfaceSeed", (unchecked((uint)rec.asteroid.seed) & 65535u) / 65535f);
                    props.SetFloat("_MO_LODVisibility", 1f - nearAlpha);
                    rp.matProps = props;
                }
                Graphics.RenderMesh(rp, mesh, 0, objectToWorld);
                drawn++;
            }
            VisibleCount = drawn;
        }

        private void WarmVisibleMeshes()
        {
            Stopwatch watch = Stopwatch.StartNew();
            int builds = 0;
            int budget = Mathf.Clamp(settings.exactFarBuildsPerFrame, 1, 16);
            while (warmCursor < visibleIndices.Count && builds < budget)
            {
                int i = visibleIndices[warmCursor++];
                AsteroidFieldRecord rec = snapshot.asteroids[i];
                if (uniqueMeshes.ContainsKey(rec.index) || failures.Contains(rec.index)) continue;
                GetOrCreateMesh(rec);
                builds++;
                // Limit each frame's blocking geometry work, but always build at least one.
                if (watch.Elapsed.TotalMilliseconds >= settings.farBuildBudgetMs) break;
            }
        }

        public void Dispose()
        {
            foreach (Mesh mesh in uniqueMeshes.Values)
                AsteroidGeneration.DestroyTemporary(mesh);
            uniqueMeshes.Clear();
            failures.Clear();
            perAsteroidProperties.Clear();
            visibleIndices.Clear();
            detailed.Clear();
            visibilityGrid = null;
            snapshot = null;
            warmCursor = 0;
            VisibleCount = 0;
            if (material != null && !ReferenceEquals(material, controllerMaterial))
                AsteroidGeneration.DestroyTemporary(material);
            material = null;
        }
    }
}
