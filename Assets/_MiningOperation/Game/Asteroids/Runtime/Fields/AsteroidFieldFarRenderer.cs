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
        private int warmCursor;
        private double simulationSeconds;
        public int VisibleCount { get; private set; }
        public int CachedMeshes => uniqueMeshes.Count;
        public bool Ready => material != null && snapshot != null;

        public AsteroidFieldFarRenderer(Transform fieldRoot, AsteroidFieldSettings fieldSettings)
        {
            root = fieldRoot;
            settings = fieldSettings;
        }

        public void Initialize(AsteroidFieldSnapshot field)
        {
            Dispose();
            if (field == null || field.asteroids == null || field.asteroids.Count == 0 ||
                settings == null || settings.asteroidSettings == null || !settings.useGpuFarField)
                return;
            snapshot = field;
            material = AsteroidStableMaterials.Create(settings.distantMaterial,
                settings.allowExperimentalRockShaders, new Color(0.69f, 0.68f, 0.64f),
                "MO Exact Far Asteroids (runtime)");
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
                Mesh mesh = AsteroidGeometryGenerator.Generate(body, settings.asteroidSettings,
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
                if (rec == null || rec.asteroid == null || detailed.Contains(rec.index) ||
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

        public void Render(double timeSeconds)
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
                if (rec == null || rec.asteroid == null || detailed.Contains(rec.index) ||
                    !uniqueMeshes.TryGetValue(rec.index, out Mesh mesh) || mesh == null) continue;
                Quaternion rotation = settings.enableAsteroidRotation
                    ? AsteroidSpin.Evaluate(rec.localRotation, rec.asteroid.seed,
                        rec.asteroid.diameterMeters, simulationSeconds, settings.rotationSpeedMultiplier)
                    : rec.localRotation;
                Matrix4x4 objectToWorld = parent * Matrix4x4.TRS(rec.localPosition,
                    rotation, Vector3.one);
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
            visibleIndices.Clear();
            detailed.Clear();
            visibilityGrid = null;
            snapshot = null;
            warmCursor = 0;
            VisibleCount = 0;
            if (material != null) AsteroidGeneration.DestroyTemporary(material);
            material = null;
        }
    }
}
