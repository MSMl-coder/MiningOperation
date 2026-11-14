using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    [Serializable]
    public sealed class AsteroidFieldPlacement
    {
        public int index;
        public int asteroidSeed;
        public string asteroidId;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public float nominalDiameter;
    }

    /// <summary>Serializable per-anchor generation overrides. Existing settings assets remain shared/read-only.</summary>
    [Serializable]
    public sealed class AsteroidFieldLayoutOptions
    {
        public AsteroidFormationType formation;
        public int asteroidCount;
        public float radiusMeters, thicknessMeters, arcDegrees, cloudVerticalScale;
        public int clusterCount;
        public float clusterRadiusFraction, minimumSurfaceGapMeters;
        public int placementAttempts;
        public float minimumDiameter, maximumDiameter, smallAsteroidBias;
        public bool useComposedLayout;
        public float clusterStrength, pocketWidthFraction;
        public int densityPockets;

        public static AsteroidFieldLayoutOptions FromSettings(AsteroidFieldSettings s)
        {
            return new AsteroidFieldLayoutOptions
            {
                formation = s.formation, asteroidCount = s.asteroidCount,
                radiusMeters = s.radiusMeters, thicknessMeters = s.thicknessMeters,
                arcDegrees = s.arcDegrees, cloudVerticalScale = s.cloudVerticalScale,
                clusterCount = s.clusterCount, clusterRadiusFraction = s.clusterRadiusFraction,
                minimumSurfaceGapMeters = s.minimumSurfaceGapMeters,
                placementAttempts = s.placementAttempts,
                minimumDiameter = s.asteroidSettings.minimumDiameterMeters,
                maximumDiameter = s.asteroidSettings.maximumDiameterMeters,
                smallAsteroidBias = s.smallAsteroidBias,
                useComposedLayout = s.useComposedLayout,
                clusterStrength = s.clusterStrength,
                densityPockets = s.densityPockets,
                pocketWidthFraction = s.pocketWidthFraction
            };
        }
        public void Validate()
        {
            if (asteroidCount < 1 || asteroidCount > 2000 ||
                radiusMeters < 100f || thicknessMeters < 10f ||
                minimumDiameter < 1f || maximumDiameter < minimumDiameter ||
                !IsFinite(minimumDiameter) || !IsFinite(maximumDiameter) ||
                !IsFinite(radiusMeters) || !IsFinite(thicknessMeters) ||
                !IsFinite(smallAsteroidBias) || smallAsteroidBias < 0f || smallAsteroidBias > 1f ||
                minimumSurfaceGapMeters < 0f || placementAttempts < 1 ||
                !Enum.IsDefined(typeof(AsteroidFormationType), formation) ||
                !IsFinite(clusterStrength) || clusterStrength < 0f || clusterStrength > 1f ||
                densityPockets < 0 || densityPockets > 32 ||
                !IsFinite(pocketWidthFraction) || pocketWidthFraction < 0f || pocketWidthFraction > 0.4f)
                throw new InvalidOperationException("Invalid field layout options.");
        }
        private static bool IsFinite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
    }

    /// <summary>
    /// Pure deterministic layout stage. Computes position, orientation and IDs without
    /// generating Unity meshes. Geometry/class/resources are delegated to v1.1 generator.
    /// </summary>
    public static class AsteroidFieldLayout
    {
        public const int LayoutVersion = 3;

        public static List<AsteroidFieldPlacement> Plan(AsteroidFieldSettings settings, long fieldSeed, string fieldId)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.ValidateOrThrow();
            return Plan(settings, fieldSeed, fieldId, AsteroidFieldLayoutOptions.FromSettings(settings));
        }

        public static List<AsteroidFieldPlacement> Plan(AsteroidFieldSettings settings, long fieldSeed,
            string fieldId, AsteroidFieldLayoutOptions options)
        {
            if (settings == null || options == null) throw new ArgumentNullException("settings/options");
            settings.ValidateOrThrow();
            options.Validate();
            if (options.useComposedLayout)
                return AsteroidFieldComposer.Plan(settings, fieldSeed, fieldId, options);
            if (string.IsNullOrWhiteSpace(fieldId) || fieldId.Length > 80)
                throw new ArgumentException("Field ID must contain 1..80 non-whitespace characters.", nameof(fieldId));

            // Stable field identity means different fields with the same numeric seed do not share IDs.
            ulong mixed = Mix(unchecked((ulong)fieldSeed) ^ HashId(fieldId));
            var rng = new AsteroidRandom((uint)(mixed ^ (mixed >> 32)));
            var placements = new List<AsteroidFieldPlacement>(options.asteroidCount);
            Vector3[] groupCenters = MakeClusterCenters(ref rng, options);
            float gap = options.minimumSurfaceGapMeters;
            int attempts = options.placementAttempts;
            float minD = options.minimumDiameter;
            float maxD = options.maximumDiameter;
            if (float.IsNaN(minD) || float.IsInfinity(minD) || minD < 1f ||
                float.IsNaN(maxD) || float.IsInfinity(maxD) || maxD < minD)
                throw new InvalidOperationException("Invalid asteroid diameter range.");
            // Broadphase grid: avoid O(N² * placementAttempts) when building large belts.
            // Any two exclusion spheres fit within one of the adjacent grid cells.
            float cellSize = Mathf.Max(1f, maxD * 2f + gap);
            var grid = new Dictionary<Vector3Int, List<AsteroidFieldPlacement>>();

            for (int i = 0; i < options.asteroidCount; i++)
            {
                // Index-specific seed: count/placement changes do not alter a rock's identity.
                long rockFieldSeed = unchecked((long)mixed);
                int asteroidSeed = AsteroidIdUtility.DeriveSeed(rockFieldSeed, i);
                var rockRng = new AsteroidRandom(unchecked((uint)asteroidSeed) ^ 0xA3C59AC3u);
                // Skew toward small rocks, with a small but nonzero tail of giants.
                float u = rockRng.Value();
                float exponent = Mathf.Lerp(1f, 5f, options.smallAsteroidBias);
                float quantile = Mathf.Pow(u, exponent);
                float diameter = Mathf.Exp(Mathf.Lerp(Mathf.Log(minD), Mathf.Log(maxD), quantile));
                float exclusionRadius = diameter * 0.95f; // conservative envelope around irregular shape
                bool accepted = false;
                Vector3 location = Vector3.zero;
                for (int pass = 0; pass < attempts; pass++)
                {
                    location = SamplePosition(ref rng, options, i, pass, groupCenters);
                    Vector3Int cell = GridCell(location, cellSize);
                    bool intersects = false;
                    for (int dx = -1; dx <= 1 && !intersects; dx++)
                    for (int dy = -1; dy <= 1 && !intersects; dy++)
                    for (int dz = -1; dz <= 1 && !intersects; dz++)
                    {
                        var neighbour = new Vector3Int(cell.x + dx, cell.y + dy, cell.z + dz);
                        if (!grid.TryGetValue(neighbour, out var nearby)) continue;
                        foreach (var previous in nearby)
                        {
                            float distance = exclusionRadius + previous.nominalDiameter * 0.95f + gap;
                            if ((location - previous.localPosition).sqrMagnitude < distance * distance)
                            {
                                intersects = true;
                                break;
                            }
                        }
                    }
                    if (!intersects) { accepted = true; break; }
                }
                // If extremely dense, skip rather than secretly intersect objects.
                if (!accepted) continue;
                Quaternion rotation = Quaternion.Euler(rng.Range(0f, 360f), rng.Range(0f, 360f), rng.Range(0f, 360f));
                var acceptedPlacement = new AsteroidFieldPlacement
                {
                    index = i,
                    asteroidSeed = asteroidSeed,
                    asteroidId = "AST-" + mixed.ToString("X16") + "-" + i.ToString("X8"),
                    localPosition = location,
                    localRotation = rotation,
                    nominalDiameter = diameter
                };
                placements.Add(acceptedPlacement);
                Vector3Int placementCell = GridCell(location, cellSize);
                if (!grid.TryGetValue(placementCell, out var occupants))
                {
                    occupants = new List<AsteroidFieldPlacement>();
                    grid.Add(placementCell, occupants);
                }
                occupants.Add(acceptedPlacement);
            }
            return placements;
        }

        private static Vector3Int GridCell(Vector3 location, float cellSize)
        {
            return new Vector3Int(Mathf.FloorToInt(location.x / cellSize),
                Mathf.FloorToInt(location.y / cellSize), Mathf.FloorToInt(location.z / cellSize));
        }

        private static Vector3[] MakeClusterCenters(ref AsteroidRandom rng, AsteroidFieldLayoutOptions s)
        {
            if (s.formation != AsteroidFormationType.ClusterGroups) return null;
            var centers = new Vector3[s.clusterCount];
            for (int i = 0; i < centers.Length; i++)
            {
                float t = (i + 0.5f) / centers.Length;
                // Spread cluster centers apart along an arc to create recognizable pockets.
                float a = Mathf.Lerp(-Mathf.PI * 0.75f, Mathf.PI * 0.75f, t);
                float r = s.radiusMeters * rng.Range(0.4f, 0.9f);
                centers[i] = new Vector3(Mathf.Sin(a) * r, rng.Range(-s.thicknessMeters, s.thicknessMeters), Mathf.Cos(a) * r);
            }
            return centers;
        }

        private static Vector3 SamplePosition(ref AsteroidRandom rng, AsteroidFieldLayoutOptions s, int index,
            int attempt, Vector3[] groupCenters)
        {
            switch (s.formation)
            {
                case AsteroidFormationType.CrescentBelt:
                {
                    // Stratified along the arc, deliberately broken by random radial/vertical offsets.
                    float t = Mathf.Clamp01((index + 0.5f + rng.Range(-0.48f, 0.48f)) / s.asteroidCount);
                    float theta = (t - 0.5f) * s.arcDegrees * Mathf.Deg2Rad;
                    float radius = s.radiusMeters + rng.Range(-s.thicknessMeters, s.thicknessMeters);
                    return new Vector3(Mathf.Sin(theta) * radius,
                        rng.Range(-s.thicknessMeters * 0.3f, s.thicknessMeters * 0.3f),
                        Mathf.Cos(theta) * radius);
                }
                case AsteroidFormationType.SphericalCloud:
                {
                    Vector3 u = rng.UnitVector();
                    float r = s.radiusMeters * Mathf.Pow(Mathf.Max(0.00001f, rng.Value()), 1f / 3f);
                    return new Vector3(u.x * r, u.y * r * s.cloudVerticalScale, u.z * r);
                }
                case AsteroidFormationType.ClusterGroups:
                {
                    int cluster = rng.Range(0, groupCenters.Length);
                    Vector3 u = rng.UnitVector();
                    float r = s.radiusMeters * s.clusterRadiusFraction * Mathf.Pow(Mathf.Max(0.00001f, rng.Value()), 1f / 3f);
                    return groupCenters[cluster] + new Vector3(u.x * r, u.y * r * s.cloudVerticalScale, u.z * r);
                }
                case AsteroidFormationType.BrokenBelt:
                {
                    // Three populated arc sections separated by deliberately empty gaps.
                    const int sections = 3;
                    int section = index % sections;
                    float t = (index / sections + rng.Value()) / Mathf.CeilToInt(s.asteroidCount / (float)sections);
                    float arcT = (section + Mathf.Lerp(0.08f, 0.78f, Mathf.Clamp01(t))) / sections;
                    float theta = (arcT - 0.5f) * s.arcDegrees * Mathf.Deg2Rad;
                    float radius = s.radiusMeters + rng.Range(-s.thicknessMeters, s.thicknessMeters);
                    return new Vector3(Mathf.Sin(theta) * radius,
                        rng.Range(-s.thicknessMeters * 0.4f, s.thicknessMeters * 0.4f),
                        Mathf.Cos(theta) * radius);
                }
                case AsteroidFormationType.Stream:
                {
                    // Elongated fragmented trail with an uneven spine and side branches.
                    float t = rng.Range(-1f, 1f);
                    float branch = rng.Value() < 0.24f ? 1f : -0.25f;
                    float x = t * s.radiusMeters;
                    float z = Mathf.Sin(t * 7.8f) * s.thicknessMeters * 0.8f +
                        rng.Range(-s.thicknessMeters, s.thicknessMeters) * 0.62f +
                        branch * s.thicknessMeters * 0.5f;
                    float y = rng.Range(-s.thicknessMeters * 0.35f, s.thicknessMeters * 0.35f);
                    return new Vector3(x, y, z);
                }
                case AsteroidFormationType.BrokenRing:
                {
                    // Four partial arcs: 360-degree ring with broad gaps between segments.
                    const int sections = 4;
                    int section = index % sections;
                    float t = (index / sections + rng.Value()) /
                        Mathf.CeilToInt(s.asteroidCount / (float)sections);
                    float ringT = (section + Mathf.Lerp(0.10f, 0.79f, Mathf.Clamp01(t))) / sections;
                    float theta = ringT * Mathf.PI * 2f;
                    float radius = s.radiusMeters + rng.Range(-s.thicknessMeters, s.thicknessMeters);
                    return new Vector3(Mathf.Sin(theta) * radius,
                        rng.Range(-s.thicknessMeters * 0.3f, s.thicknessMeters * 0.3f),
                        Mathf.Cos(theta) * radius);
                }
                default: throw new ArgumentOutOfRangeException(nameof(s.formation));
            }
        }

        // FNV-1a over UTF-16 code units: deterministic, no string.GetHashCode randomization.
        private static ulong HashId(string value)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                foreach (char c in value) { h ^= c; h *= 1099511628211UL; }
                return h;
            }
        }
        private static ulong Mix(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }
    }
}
