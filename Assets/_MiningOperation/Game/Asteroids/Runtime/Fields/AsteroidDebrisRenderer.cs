using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Entirely visual layer: deterministic from a saved field snapshot, not part of
    /// authoritative mineable data. No per-fragment GameObject, collider or save record.
    /// Separate size tiers: small rocks and micro fragments. Fully disposable/rebuildable.
    /// </summary>
    internal sealed class AsteroidDebrisRenderer : IDisposable
    {
        private const int BatchSize = 511;
        private struct Fragment
        {
            public Vector3 localPosition;
            public Quaternion rotation;
            public float diameter;
            public byte variant;
        }
        private readonly Transform root;
        private readonly AsteroidFieldSettings settings;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Fragment> fragments = new List<Fragment>();
        private Matrix4x4[][] batches;
        private int[] counts;
        private Material material;
        private AsteroidVisibilityGrid visibilityGrid;
        private readonly List<int> visibleIndices = new List<int>();
        public int VisibleCount { get; private set; }

        public AsteroidDebrisRenderer(Transform fieldRoot, AsteroidFieldSettings fieldSettings)
        {
            root = fieldRoot;
            settings = fieldSettings;
        }

        public void Initialize(AsteroidFieldSnapshot snapshot)
        {
            Dispose();
            if (snapshot?.asteroids == null || snapshot.asteroids.Count == 0 ||
                settings == null || !settings.enableDecorativeDebris || !SystemInfo.supportsInstancing) return;
            Shader shader = Shader.Find("Mining Operation/Asteroid Far URP");
            if (settings.distantMaterial != null) material = new Material(settings.distantMaterial);
            else if (shader != null) material = new Material(shader);
            if (material == null) return;
            material.enableInstancing = true;
            material.hideFlags = HideFlags.DontSave;
            material.name = "MO Decorative Fragments (runtime)";
            int variantCount = Mathf.Clamp(settings.debrisMeshVariants, 1, 24);
            for (int i = 0; i < variantCount; ++i)
            {
                var rock = new AsteroidData
                {
                    seed = AsteroidIdUtility.DeriveSeed(snapshot.fieldSeed ^ 0x2940DE52L, i + 123),
                    generatorVersion = 3,
                    diameterMeters = 1f,
                    geometryProfile = AsteroidGeometryProfile.FromSettings(settings.asteroidSettings)
                };
                Mesh mesh = AsteroidGeometryGenerator.Generate(rock, settings.asteroidSettings,
                    1);
                mesh.name = "MO_Debris_Variant_" + i;
                mesh.hideFlags = HideFlags.DontSave;
                meshes.Add(mesh);
            }
            int total = Mathf.Clamp(settings.decorativeRockCount, 0, 16000) +
                Mathf.Clamp(settings.microFragmentCount, 0, 30000);
            var rng = new AsteroidRandom(unchecked((uint)AsteroidIdUtility.DeriveSeed(
                snapshot.fieldSeed, snapshot.asteroids.Count + 43812)));
            // Pick a nearby primary body as a pocket center. This matches the authored
            // density pattern and avoids uniform noise covering the whole scene.
            for (int i = 0; i < total; ++i)
            {
                AsteroidFieldRecord host = snapshot.asteroids[rng.Range(0, snapshot.asteroids.Count)];
                if (host?.asteroid == null) continue;
                bool micro = i >= settings.decorativeRockCount;
                float lo = micro ? 0.12f : settings.fragmentMinDiameter;
                float hi = micro ? Mathf.Min(2.0f, settings.fragmentMaxDiameter) : settings.fragmentMaxDiameter;
                hi = Mathf.Max(lo, hi);
                float diameter = Mathf.Exp(Mathf.Lerp(Mathf.Log(lo), Mathf.Log(hi),
                    Mathf.Pow(Mathf.Max(0.00001f, rng.Value()), 1.5f)));
                Vector3 direction = rng.UnitVector();
                // A near/far mixture gives clustered boulders with a diffuse dust halo.
                float spread = settings.debrisSpreadMeters * (rng.Value() < 0.80f ? 1f : 3.2f);
                float distance = Mathf.Max(host.asteroid.diameterMeters * 0.60f + diameter,
                    Mathf.Pow(rng.Value(), 0.65f) * spread);
                Vector3 center = host.localPosition + Vector3.Scale(direction,
                    new Vector3(distance, distance * 0.50f, distance));
                fragments.Add(new Fragment
                {
                    localPosition = center,
                    rotation = Quaternion.Euler(rng.Range(0f,360f), rng.Range(0f,360f), rng.Range(0f,360f)),
                    diameter = diameter,
                    variant = (byte)rng.Range(0, meshes.Count)
                });
            }
            visibilityGrid = new AsteroidVisibilityGrid(500f);
            for (int i = 0; i < fragments.Count; ++i)
                visibilityGrid.Add(i, fragments[i].localPosition, fragments[i].diameter * 0.7f);
            batches = new Matrix4x4[meshes.Count][];
            counts = new int[meshes.Count];
            int perVariant = Mathf.Max(32, (fragments.Count / meshes.Count + 64) * 2);
            for (int i = 0; i < batches.Length; i++) batches[i] = new Matrix4x4[perVariant];
        }

        public void Refresh(Vector3 viewerWorldPosition, Camera camera = null)
        {
            VisibleCount = 0;
            if (material == null || batches == null) return;
            Array.Clear(counts, 0, counts.Length);
            float maxSqr = settings.debrisDrawDistance * settings.debrisDrawDistance;
            Matrix4x4 parent = root.localToWorldMatrix;
            visibilityGrid?.CollectVisible(root, viewerWorldPosition, camera,
                settings.debrisDrawDistance, visibleIndices);
            foreach (int index in visibleIndices)
            {
                Fragment fragment = fragments[index];
                Vector3 world = root.TransformPoint(fragment.localPosition);
                if ((world - viewerWorldPosition).sqrMagnitude > maxSqr) continue;
                int variant = fragment.variant;
                int count = counts[variant];
                if (count == batches[variant].Length)
                    Array.Resize(ref batches[variant], Mathf.Min(fragments.Count, count * 2));
                batches[variant][count] = parent * Matrix4x4.TRS(fragment.localPosition,
                    fragment.rotation, Vector3.one * fragment.diameter);
                counts[variant]++;
                VisibleCount++;
            }
        }

        public void Render()
        {
            if (material == null || batches == null || VisibleCount == 0) return;
            var param = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            for (int i = 0; i < meshes.Count; ++i)
                for (int offset = 0; offset < counts[i]; offset += BatchSize)
                    Graphics.RenderMeshInstanced(param, meshes[i], 0, batches[i],
                        Mathf.Min(BatchSize, counts[i] - offset), offset);
        }

        public void Dispose()
        {
            foreach (Mesh mesh in meshes) AsteroidGeneration.DestroyTemporary(mesh);
            meshes.Clear();
            fragments.Clear();
            if (material != null) AsteroidGeneration.DestroyTemporary(material);
            material = null;
            batches = null;
            counts = null;
            VisibleCount = 0;
            visibilityGrid = null;
            visibleIndices.Clear();
        }
    }
}
