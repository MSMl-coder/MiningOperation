using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Non-GameObject far-field renderer. Draws shared low-poly variants using GPU instancing.
    /// Authoritative identities and mineable data always live in AsteroidFieldSnapshot.
    /// </summary>
    internal sealed class AsteroidFieldFarRenderer : IDisposable
    {
        private const int BatchSize = 511; // Unity's standard 2-matrix per-instance budget.
        private readonly Transform root;
        private readonly AsteroidFieldSettings settings;
        private readonly List<Mesh> variants = new List<Mesh>();
        private Matrix4x4[][] matrices;
        private int[] counts;
        private Material material;
        private int capacity;
        private bool warned;
        private AsteroidVisibilityGrid visibilityGrid;
        private readonly List<int> visibleIndices = new List<int>();

        public int VisibleCount { get; private set; }
        public bool Ready => variants.Count > 0 && material != null && SystemInfo.supportsInstancing;

        public AsteroidFieldFarRenderer(Transform rootTransform, AsteroidFieldSettings fieldSettings)
        {
            root = rootTransform;
            settings = fieldSettings;
        }

        public void Initialize(AsteroidFieldSnapshot snapshot)
        {
            Dispose();
            if (snapshot == null || snapshot.asteroids == null || snapshot.asteroids.Count == 0 ||
                settings == null || settings.asteroidSettings == null || !settings.useGpuFarField)
                return;
            if (!SystemInfo.supportsInstancing)
            {
                WarnOnce("[Mining Operation] GPU instancing unsupported: only detailed nearby asteroids will render.");
                return;
            }
            Shader fallback = Shader.Find("Mining Operation/Asteroid Far URP");
            if (fallback == null) fallback = Shader.Find("Universal Render Pipeline/Lit");
            if (settings.distantMaterial != null)
                material = new Material(settings.distantMaterial);
            else if (fallback != null)
                material = new Material(fallback);
            else if (settings.asteroidMaterial != null)
                material = new Material(settings.asteroidMaterial);
            if (material == null)
            {
                WarnOnce("[Mining Operation] No material/shader available for asteroid far field.");
                return;
            }
            material.name = "MO Asteroids Far (runtime)";
            material.enableInstancing = true;
            material.hideFlags = HideFlags.DontSave;

            int variantCount = Mathf.Clamp(settings.distantMeshVariants, 1, snapshot.asteroids.Count);
            int subdivisions = Mathf.Clamp(settings.distantSubdivisions, 0, 3);
            // Spread samples across the field, not just the first few records.
            try
            {
                for (int i = 0; i < variantCount; ++i)
                {
                    int sample = (int)((long)i * snapshot.asteroids.Count / variantCount);
                    var rock = snapshot.asteroids[sample]?.asteroid;
                    if (rock == null || rock.diameterMeters <= 0f) continue;
                    var shape = new AsteroidData
                    {
                        seed = rock.seed, generatorVersion = rock.generatorVersion,
                        diameterMeters = rock.diameterMeters,
                        geometryProfile = rock.geometryProfile
                    };
                    Mesh mesh = AsteroidGeometryGenerator.Generate(shape, settings.asteroidSettings, subdivisions);
                    // Shared template is built in unit-diameter space, scaled per asteroid at draw time.
                    Vector3[] points = mesh.vertices;
                    float inv = 1f / rock.diameterMeters;
                    for (int j = 0; j < points.Length; ++j) points[j] *= inv;
                    mesh.vertices = points;
                    // V1 meshes predate vertex-color geology. Keep old saves visible.
                    if (mesh.colors32 == null || mesh.colors32.Length != mesh.vertexCount)
                    {
                        var colors = new Color32[mesh.vertexCount];
                        for (int k = 0; k < colors.Length; k++)
                            colors[k] = new Color32(255, 255, 255, 255);
                        mesh.colors32 = colors;
                    }
                    mesh.RecalculateBounds();
                    mesh.name = "MO_AsteroidFarVariant_" + i;
                    mesh.hideFlags = HideFlags.DontSave;
                    variants.Add(mesh);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Dispose();
                return;
            }
            if (variants.Count == 0) { Dispose(); return; }
            visibilityGrid = new AsteroidVisibilityGrid(650f);
            for (int i = 0; i < snapshot.asteroids.Count; ++i)
            {
                var record = snapshot.asteroids[i];
                if (record?.asteroid != null)
                    visibilityGrid.Add(i, record.localPosition, record.asteroid.diameterMeters * 0.65f);
            }
            capacity = snapshot.asteroids.Count;
            matrices = new Matrix4x4[variants.Count][];
            counts = new int[variants.Count];
            for (int i = 0; i < variants.Count; i++) matrices[i] = new Matrix4x4[capacity];
        }

        public void Refresh(AsteroidFieldSnapshot snapshot, Vector3 viewer, HashSet<int> detailedIds, Camera camera = null)
        {
            VisibleCount = 0;
            if (!Ready || snapshot == null || matrices == null) return;
            Array.Clear(counts, 0, counts.Length);
            float maxSq = settings.distantDrawDistanceMeters * settings.distantDrawDistanceMeters;
            Matrix4x4 parent = root.localToWorldMatrix;
            visibilityGrid?.CollectVisible(root, viewer, camera, settings.distantDrawDistanceMeters, visibleIndices);
            foreach (int i in visibleIndices)
            {
                AsteroidFieldRecord item = snapshot.asteroids[i];
                if (item?.asteroid == null || (detailedIds != null && detailedIds.Contains(item.index)) ||
                    (settings.hideDepletedAsteroids && item.asteroid.remainingMassKg <= 0d)) continue;
                Vector3 pos = root.TransformPoint(item.localPosition);
                if ((pos - viewer).sqrMagnitude > maxSq) continue;
                uint stable = unchecked((uint)item.asteroid.seed);
                int variantIndex = (int)((stable ^ (stable >> 16)) % (uint)variants.Count);
                int count = counts[variantIndex];
                if (count >= capacity) continue;
                matrices[variantIndex][count] = parent * Matrix4x4.TRS(
                    item.localPosition, item.localRotation, Vector3.one * item.asteroid.diameterMeters);
                counts[variantIndex] = count + 1;
                VisibleCount++;
            }
        }

        public void Render()
        {
            if (!Ready || counts == null || VisibleCount == 0) return;
            var parameters = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            for (int i = 0; i < variants.Count; i++)
            {
                for (int start = 0; start < counts[i]; start += BatchSize)
                {
                    int batchCount = Mathf.Min(BatchSize, counts[i] - start);
                    Graphics.RenderMeshInstanced(parameters, variants[i], 0, matrices[i], batchCount, start);
                }
            }
        }

        public void Dispose()
        {
            VisibleCount = 0;
            foreach (Mesh mesh in variants) AsteroidGeneration.DestroyTemporary(mesh);
            variants.Clear();
            if (material != null) AsteroidGeneration.DestroyTemporary(material);
            material = null;
            matrices = null;
            counts = null;
            capacity = 0;
            visibilityGrid = null;
            visibleIndices.Clear();
        }

        private void WarnOnce(string message)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning(message, root);
        }
    }
}
