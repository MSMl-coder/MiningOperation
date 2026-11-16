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
        public MOFieldDepthMode depthMode;
        public float depthCenterY, depthHalfRange, depthJitter;
        public bool flattenRotationToPlane;
        // Opt-in by serialized default for all belt Spawn Points; other formation modes ignore it.
        public bool useNaturalBeltDistribution = true;
        public float naturalBeltIrregularity = .82f;
        public float naturalDensityContrast = .75f;
        public float naturalSatelliteChance = .10f;

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
                pocketWidthFraction = s.pocketWidthFraction,
                depthMode = MOFieldDepthMode.Free3D,
                depthCenterY = 0f,
                depthHalfRange = 0f,
                depthJitter = 0f,
                flattenRotationToPlane = false,
                useNaturalBeltDistribution = true,
                naturalBeltIrregularity = .82f,
                naturalDensityContrast = .75f,
                naturalSatelliteChance = .10f
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
                !Enum.IsDefined(typeof(MOFieldDepthMode), depthMode) ||
                !IsFinite(clusterStrength) || clusterStrength < 0f || clusterStrength > 1f ||
                densityPockets < 0 || densityPockets > 32 ||
                !IsFinite(depthCenterY) || !IsFinite(depthHalfRange) || depthHalfRange < 0f ||
                !IsFinite(depthJitter) || depthJitter < 0f ||
                !IsFinite(pocketWidthFraction) || pocketWidthFraction < 0f || pocketWidthFraction > 0.4f ||
                !IsFinite(naturalBeltIrregularity) || naturalBeltIrregularity < 0f || naturalBeltIrregularity > 1f ||
                !IsFinite(naturalDensityContrast) || naturalDensityContrast < 0f || naturalDensityContrast > 1f ||
                !IsFinite(naturalSatelliteChance) || naturalSatelliteChance < 0f || naturalSatelliteChance > .35f)
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
        public const int LayoutVersion = 7;
        private struct PlannedRock { public int index; public int asteroidSeed; public float diameter; public float radius; }
        private struct AnchorRock { public Vector3 position; public float diameter; public float radius; }

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
            if (options.useNaturalBeltDistribution &&
                (options.formation == AsteroidFormationType.CrescentBelt ||
                 options.formation == AsteroidFormationType.BrokenBelt))
                return MONaturalBeltLayout.Plan(fieldSeed, fieldId, options);
            if (options.useComposedLayout)
                return AsteroidFieldComposer.Plan(settings, fieldSeed, fieldId, options);
            if (string.IsNullOrWhiteSpace(fieldId) || fieldId.Length > 80)
                throw new ArgumentException("Field ID must contain 1..80 non-whitespace characters.", nameof(fieldId));

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
            float cellSize = Mathf.Max(1f, maxD * 2f + gap);
            var grid = new Dictionary<Vector3Int, List<AsteroidFieldPlacement>>();
            var anchors = new List<AnchorRock>();
            List<PlannedRock> plan = BuildPlan(mixed, options);

            foreach (var rockInfo in plan)
            {
                bool accepted = false;
                Vector3 location = Vector3.zero;
                bool preferSatellite = ShouldPreferSatellite(rockInfo.diameter, options, anchors.Count);
                for (int pass = 0; pass < attempts; pass++)
                {
                    location = preferSatellite && pass < Mathf.Max(2, attempts / 3)
                        ? SampleNearAnchor(ref rng, options, anchors, rockInfo.radius)
                        : SamplePosition(ref rng, options, rockInfo.index, pass, groupCenters);
                    location = ApplyDepthConstraint(location, ref rng, options);
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
                            float distance = rockInfo.radius + previous.nominalDiameter * 0.95f +
                                (options.formation == AsteroidFormationType.IceShelfContinents
                                    ? -Mathf.Min(rockInfo.radius, previous.nominalDiameter * 0.95f) * 0.24f
                                    : gap);
                            if ((location - previous.localPosition).sqrMagnitude < distance * distance)
                            {
                                intersects = true;
                                break;
                            }
                        }
                    }
                    if (!intersects) { accepted = true; break; }
                }
                if (!accepted) continue;
                var localRng = new AsteroidRandom(unchecked((uint)rockInfo.asteroidSeed) ^ 0xA3C59AC3u);
                Quaternion rotation = (options.formation == AsteroidFormationType.IceShelfFragments ||
                    options.formation == AsteroidFormationType.IceShelfContinents)
                    ? Quaternion.Euler(localRng.Range(-4f, 4f), localRng.Range(0f, 360f), localRng.Range(-4f, 4f))
                    : Quaternion.Euler(localRng.Range(0f, 360f), localRng.Range(0f, 360f), localRng.Range(0f, 360f));
                rotation = ApplyRotationConstraint(rotation, ref localRng, options);
                var acceptedPlacement = new AsteroidFieldPlacement
                {
                    index = rockInfo.index,
                    asteroidSeed = rockInfo.asteroidSeed,
                    asteroidId = "AST-" + mixed.ToString("X16") + "-" + rockInfo.index.ToString("X8"),
                    localPosition = location,
                    localRotation = rotation,
                    nominalDiameter = rockInfo.diameter
                };
                placements.Add(acceptedPlacement);
                Vector3Int placementCell = GridCell(location, cellSize);
                if (!grid.TryGetValue(placementCell, out var occupants))
                {
                    occupants = new List<AsteroidFieldPlacement>();
                    grid.Add(placementCell, occupants);
                }
                occupants.Add(acceptedPlacement);
                if (rockInfo.diameter >= MajorAnchorDiameter(options))
                    anchors.Add(new AnchorRock { position = location, diameter = rockInfo.diameter, radius = rockInfo.radius });
            }
            placements.Sort((a, b) => a.index.CompareTo(b.index));
            return placements;
        }

        private static List<PlannedRock> BuildPlan(ulong mixed, AsteroidFieldLayoutOptions options)
        {
            var plan = new List<PlannedRock>(options.asteroidCount);
            for (int i = 0; i < options.asteroidCount; i++)
            {
                int asteroidSeed = AsteroidIdUtility.DeriveSeed(unchecked((long)mixed), i);
                var rockRng = new AsteroidRandom(unchecked((uint)asteroidSeed) ^ 0xA3C59AC3u);
                float diameter = Diameter(ref rockRng, options);
                plan.Add(new PlannedRock
                {
                    index = i,
                    asteroidSeed = asteroidSeed,
                    diameter = diameter,
                    radius = diameter * 0.95f
                });
            }
            plan.Sort((a, b) => b.diameter.CompareTo(a.diameter));
            return plan;
        }

        private static float Diameter(ref AsteroidRandom rng, AsteroidFieldLayoutOptions o)
        {
            if (o.formation == AsteroidFormationType.IceShelfFragments)
            {
                float u = Mathf.Pow(rng.Value(), .82f);
                return Mathf.Exp(Mathf.Lerp(Mathf.Log(o.minimumDiameter), Mathf.Log(o.maximumDiameter), u));
            }
            if (o.formation == AsteroidFormationType.IceShelfContinents)
            {
                float u = Mathf.Pow(rng.Value(), .38f);
                return Mathf.Exp(Mathf.Lerp(Mathf.Log(o.minimumDiameter), Mathf.Log(o.maximumDiameter), u));
            }
            float bias = Mathf.Clamp01(o.smallAsteroidBias);
            float smallBand = Mathf.Lerp(.58f, .84f, bias);
            float heroBand = Mathf.Lerp(.09f, .03f, bias);
            float pick = rng.Value();
            float t;
            if (pick < smallBand)
            {
                float u = 1f - Mathf.Pow(1f - rng.Value(), Mathf.Lerp(3.6f, 5.8f, bias));
                t = Mathf.Lerp(0.00f, 0.34f, u);
            }
            else if (pick > 1f - heroBand)
            {
                float u = Mathf.Pow(rng.Value(), .55f);
                t = Mathf.Lerp(0.78f, 1.00f, u);
            }
            else
            {
                float u = Mathf.Pow(rng.Value(), .82f);
                t = Mathf.Lerp(0.34f, 0.78f, u);
            }
            if (rng.Value() > 0.965f)
                t = Mathf.Lerp(t, 1f, rng.Range(.35f, .85f));
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(o.minimumDiameter), Mathf.Log(o.maximumDiameter), Mathf.Clamp01(t)));
        }

        private static bool ShouldPreferSatellite(float diameter, AsteroidFieldLayoutOptions o, int anchorCount)
        {
            if (anchorCount <= 0 || o.formation == AsteroidFormationType.IceShelfFragments || o.formation == AsteroidFormationType.IceShelfContinents)
                return false;
            return diameter <= SatelliteDiameter(o);
        }

        private static float SatelliteDiameter(AsteroidFieldLayoutOptions o)
            => Mathf.Exp(Mathf.Lerp(Mathf.Log(o.minimumDiameter), Mathf.Log(o.maximumDiameter), 0.42f));

        private static float MajorAnchorDiameter(AsteroidFieldLayoutOptions o)
            => Mathf.Exp(Mathf.Lerp(Mathf.Log(o.minimumDiameter), Mathf.Log(o.maximumDiameter), 0.66f));

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
                float a = Mathf.Lerp(-Mathf.PI * 0.75f, Mathf.PI * 0.75f, t);
                float r = s.radiusMeters * rng.Range(0.4f, 0.9f);
                centers[i] = ApplyDepthConstraint(new Vector3(
                    Mathf.Sin(a) * r,
                    rng.Range(-s.thicknessMeters, s.thicknessMeters),
                    Mathf.Cos(a) * r), ref rng, s);
            }
            return centers;
        }

        private static Vector3 SampleNearAnchor(ref AsteroidRandom rng, AsteroidFieldLayoutOptions s, List<AnchorRock> anchors, float radius)
        {
            AnchorRock host = PickAnchor(ref rng, anchors);
            Vector3 u = rng.UnitVector();
            if (s.depthMode != MOFieldDepthMode.Free3D) u.y *= .15f;
            if (u.sqrMagnitude < .0001f) u = Vector3.right;
            u.Normalize();
            float r = host.radius + radius + rng.Range(host.radius * .18f, host.radius * .95f);
            Vector3 jitter = new Vector3(rng.Range(-s.thicknessMeters, s.thicknessMeters), rng.Range(-s.thicknessMeters, s.thicknessMeters), rng.Range(-s.thicknessMeters, s.thicknessMeters)) * .08f;
            if (s.depthMode != MOFieldDepthMode.Free3D) jitter.y *= .3f;
            return host.position + u * r + jitter;
        }

        private static AnchorRock PickAnchor(ref AsteroidRandom rng, List<AnchorRock> anchors)
        {
            float total = 0f;
            for (int i = 0; i < anchors.Count; i++) total += anchors[i].diameter * anchors[i].diameter;
            float roll = rng.Value() * Mathf.Max(.0001f, total);
            for (int i = 0; i < anchors.Count; i++)
            {
                roll -= anchors[i].diameter * anchors[i].diameter;
                if (roll <= 0f) return anchors[i];
            }
            return anchors[anchors.Count - 1];
        }

        private static Vector3 SamplePosition(ref AsteroidRandom rng, AsteroidFieldLayoutOptions s, int index,
            int attempt, Vector3[] groupCenters)
        {
            switch (s.formation)
            {
                case AsteroidFormationType.CrescentBelt:
                {
                    float t = Mathf.Clamp01((index + 0.5f + rng.Range(-0.48f, 0.48f)) / s.asteroidCount);
                    float theta = (t - 0.5f) * s.arcDegrees * Mathf.Deg2Rad;
                    float radius = s.radiusMeters + rng.Range(-s.thicknessMeters, s.thicknessMeters);
                    return new Vector3(Mathf.Sin(theta) * radius,
                        rng.Range(-s.thicknessMeters * 0.3f, s.thicknessMeters * 0.3f),
                        Mathf.Cos(theta) * radius);
                }
                case AsteroidFormationType.IceShelfFragments:
                {
                    float x = rng.Range(-s.radiusMeters, s.radiusMeters);
                    float z = rng.Range(-s.radiusMeters * .6f, s.radiusMeters * .6f);
                    float y = rng.Range(-s.thicknessMeters * .12f, s.thicknessMeters * .12f);
                    return new Vector3(x, y, z);
                }
                case AsteroidFormationType.IceShelfContinents:
                {
                    float seam = ((index % Mathf.Max(2, s.clusterCount)) - (Mathf.Max(2, s.clusterCount) - 1) * 0.5f) *
                        (s.radiusMeters * 0.18f / Mathf.Max(1, s.clusterCount - 1));
                    float side = ((index / Mathf.Max(2, s.clusterCount)) & 1) == 0 ? -1f : 1f;
                    float x = Mathf.Lerp(-s.radiusMeters, s.radiusMeters, Mathf.Clamp01((index + 0.5f) / Mathf.Max(1f, s.asteroidCount)));
                    x += rng.Range(-s.radiusMeters * .12f, s.radiusMeters * .12f);
                    float z = seam + side * s.radiusMeters * rng.Range(.06f, .16f) + rng.Range(-s.radiusMeters * .05f, s.radiusMeters * .05f);
                    float y = rng.Range(-s.thicknessMeters * .06f, s.thicknessMeters * .06f);
                    return new Vector3(x, y, z);
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

        private static Vector3 ApplyDepthConstraint(Vector3 location, ref AsteroidRandom rng, AsteroidFieldLayoutOptions options)
        {
            switch (options.depthMode)
            {
                case MOFieldDepthMode.PlanarSlab:
                    location.y = options.depthCenterY + Mathf.Clamp(location.y, -options.depthHalfRange, options.depthHalfRange);
                    break;
                case MOFieldDepthMode.Strict2DPlane:
                    location.y = options.depthCenterY + (options.depthJitter > 0f ? rng.Range(-options.depthJitter, options.depthJitter) : 0f);
                    break;
            }
            return location;
        }

        private static Quaternion ApplyRotationConstraint(Quaternion rotation, ref AsteroidRandom rng, AsteroidFieldLayoutOptions options)
        {
            if (!options.flattenRotationToPlane || options.depthMode == MOFieldDepthMode.Free3D)
                return rotation;
            Vector3 e = rotation.eulerAngles;
            float pitch = options.depthMode == MOFieldDepthMode.Strict2DPlane ? rng.Range(-3f, 3f) : Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -12f, 12f);
            float roll = options.depthMode == MOFieldDepthMode.Strict2DPlane ? rng.Range(-3f, 3f) : Mathf.Clamp(Mathf.DeltaAngle(0f, e.z), -12f, 12f);
            return Quaternion.Euler(pitch, e.y, roll);
        }

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
