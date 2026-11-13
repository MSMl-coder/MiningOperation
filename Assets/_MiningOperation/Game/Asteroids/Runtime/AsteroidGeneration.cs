using System;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    public static class AsteroidGeneration
    {
        public const int GeneratorVersion = 2;

        public static AsteroidData Create(int seed, AsteroidGeneratorSettings settings,
            AsteroidClass? forcedClass = null, string explicitId = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            AsteroidCompositionGenerator.ValidateCatalog(settings.catalog);
            if (forcedClass.HasValue && (forcedClass.Value < AsteroidClass.I || forcedClass.Value > AsteroidClass.III))
                throw new ArgumentOutOfRangeException(nameof(forcedClass));
            float minDiameter = settings.minimumDiameterMeters;
            float maxDiameter = settings.maximumDiameterMeters;
            if (!AsteroidCompositionGenerator.IsFinite(minDiameter) || !AsteroidCompositionGenerator.IsFinite(maxDiameter) ||
                minDiameter < 1f || maxDiameter < minDiameter)
                throw new InvalidOperationException("Generator diameter settings are invalid.");
            var rng = new AsteroidRandom(unchecked((uint)seed) ^ 0xA3C59AC3u);
            // Log-uniform: many modest asteroids, few extremely large ones.
            float diameter = Mathf.Exp(Mathf.Lerp(Mathf.Log(minDiameter), Mathf.Log(maxDiameter), rng.Value()));
            float roll = rng.Value();
            float p3 = Mathf.Clamp01(settings.classIIIProbability);
            float p2 = Mathf.Min(Mathf.Clamp01(settings.classIIProbability), 1f - p3);
            AsteroidClass astClass = forcedClass ?? (roll < p3 ? AsteroidClass.III :
                roll < p3 + p2 ? AsteroidClass.II : AsteroidClass.I);
            var profile = AsteroidGeometryProfile.FromSettings(settings);
            var composition = AsteroidCompositionGenerator.Generate(ref rng, settings.catalog, astClass, settings);
            double minPorosity = Mathf.Clamp(settings.minimumPorosity, 0f, 0.7f);
            double maxPorosity = Math.Max(minPorosity, Mathf.Clamp(settings.maximumPorosity, 0f, 0.7f));
            double porosity = minPorosity + (maxPorosity - minPorosity) * rng.Value();
            string id = string.IsNullOrWhiteSpace(explicitId) ? "AST-" + unchecked((uint)seed).ToString("X8") : explicitId;
            var data = new AsteroidData
            {
                asteroidId = id,
                seed = seed,
                generatorVersion = GeneratorVersion,
                asteroidClass = astClass,
                diameterMeters = diameter,
                composition = composition,
                porosity01 = porosity,
                geometryProfile = profile
            };
            // This resolution is fixed by separate physics settings; LOD changes cannot affect mass.
            Mesh mesh = AsteroidGeometryGenerator.Generate(data, settings, Mathf.Clamp(settings.volumeSubdivisions, 2, 5));
            try { data.volumeM3 = CalculateVolume(mesh); }
            finally { DestroyTemporary(mesh); }
            if (!AsteroidCompositionGenerator.IsFinite(data.volumeM3) || data.volumeM3 <= 0d)
                throw new InvalidOperationException("Generated asteroid has invalid volume.");
            data.densityKgM3 = AsteroidCompositionGenerator.CalculateBulkDensity(composition, settings.catalog, porosity);
            data.initialMassKg = data.volumeM3 * data.densityKgM3;
            if (!AsteroidCompositionGenerator.IsFinite(data.initialMassKg) || data.initialMassKg <= 0d)
                throw new InvalidOperationException("Generated asteroid has invalid mass.");
            data.remainingMassKg = data.initialMassKg;
            return data;
        }

        public static double CalculateVolume(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            Vector3[] v = mesh.vertices;
            int[] tri = mesh.triangles;
            if (v.Length < 4 || tri.Length < 12 || tri.Length % 3 != 0)
                throw new ArgumentException("Volume requires a closed triangular mesh.", nameof(mesh));
            double sum = 0d;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 a = v[tri[i]], b = v[tri[i + 1]], c = v[tri[i + 2]];
                sum += (double)a.x * ((double)b.y * c.z - (double)b.z * c.y)
                    + (double)a.y * ((double)b.z * c.x - (double)b.x * c.z)
                    + (double)a.z * ((double)b.x * c.y - (double)b.y * c.x);
            }
            return Math.Abs(sum) / 6d;
        }

        internal static void DestroyTemporary(UnityEngine.Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
    }

    // Use for fields: duplicated visual seeds need not mean duplicated persistent IDs.
    public static class AsteroidIdUtility
    {
        public static int DeriveSeed(long fieldSeed, int asteroidIndex)
        {
            unchecked
            {
                ulong x = (ulong)fieldSeed + (ulong)(uint)asteroidIndex * 0x9E3779B97F4A7C15UL;
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return (int)(x ^ (x >> 31));
            }
        }
        public static string CreateFieldId(long fieldSeed, int asteroidIndex)
        {
            if (asteroidIndex < 0) throw new ArgumentOutOfRangeException(nameof(asteroidIndex));
            return "AST-" + unchecked((ulong)fieldSeed).ToString("X16") + "-" + asteroidIndex.ToString("X8");
        }
        public static AsteroidData CreateFromField(long fieldSeed, int asteroidIndex, AsteroidGeneratorSettings settings,
            AsteroidClass? forcedClass = null)
        {
            return AsteroidGeneration.Create(DeriveSeed(fieldSeed, asteroidIndex), settings,
                forcedClass, CreateFieldId(fieldSeed, asteroidIndex));
        }
    }
}
