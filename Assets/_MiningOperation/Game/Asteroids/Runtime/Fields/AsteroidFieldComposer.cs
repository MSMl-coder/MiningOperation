using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// V2 composition. Primary bodies only: no visual debris is added to authoritative mineable state.
    /// Positions/diameters/identities are determined once and are stable for equal inputs.
    /// Anchor is near the visual center of a crescent instead of the center of the orbital arc.
    /// </summary>
    public static class AsteroidFieldComposer
    {
        private struct Candidate { public Vector3 position; public float radius; public float diameter; }
        private struct PlannedRock { public int index; public int asteroidSeed; public float diameter; public float radius; }

        public static List<AsteroidFieldPlacement> Plan(AsteroidFieldSettings settings,
            long fieldSeed, string fieldId, AsteroidFieldLayoutOptions options)
        {
            if (string.IsNullOrWhiteSpace(fieldId) || fieldId.Length > 80)
                throw new ArgumentException("Field ID must be 1..80 characters.", nameof(fieldId));
            options.Validate();
            ulong identity = Mix(unchecked((ulong)fieldSeed) ^ HashText(fieldId));
            var rng = new AsteroidRandom(unchecked((uint)(identity ^ (identity >> 32))));
            float[] pockets = CreatePockets(ref rng, options);
            float cellSide = Mathf.Max(1f, options.maximumDiameter * 2f + options.minimumSurfaceGapMeters);
            var occupied = new Dictionary<Vector3Int, List<Candidate>>();
            var anchors = new List<Candidate>();
            List<PlannedRock> plan = BuildPlan(identity, options);
            var result = new List<AsteroidFieldPlacement>(options.asteroidCount);

            foreach (var item in plan)
            {
                Vector3 pos = Vector3.zero;
                bool accepted = false;
                bool preferSatellite = ShouldPreferSatellite(item.diameter, options, anchors.Count);
                for (int attempt = 0; attempt < options.placementAttempts; attempt++)
                {
                    pos = preferSatellite && attempt < Mathf.Max(2, options.placementAttempts / 3)
                        ? SampleNearAnchor(ref rng, options, anchors, item.radius)
                        : NextPosition(ref rng, options, pockets);
                    pos = ApplyDepthConstraint(pos, ref rng, options);
                    var cell = Cell(pos, cellSide);
                    bool overlap = false;
                    for (int x = -1; x <= 1 && !overlap; ++x)
                    for (int y = -1; y <= 1 && !overlap; ++y)
                    for (int z = -1; z <= 1 && !overlap; ++z)
                    {
                        var key = new Vector3Int(cell.x + x, cell.y + y, cell.z + z);
                        if (!occupied.TryGetValue(key, out var other)) continue;
                        foreach (var candidate in other)
                        {
                            float separation = item.radius + candidate.radius +
                                (options.formation == AsteroidFormationType.IceShelfContinents
                                    ? -Mathf.Min(item.radius, candidate.radius) * 0.24f
                                    : options.minimumSurfaceGapMeters);
                            if ((pos - candidate.position).sqrMagnitude < separation * separation)
                            {
                                overlap = true;
                                break;
                            }
                        }
                    }
                    if (!overlap) { accepted = true; break; }
                }
                if (!accepted) continue;
                var rock = new AsteroidRandom(unchecked((uint)item.asteroidSeed) ^ 0xD3465B3Fu);
                var placement = new AsteroidFieldPlacement
                {
                    index = item.index,
                    asteroidSeed = item.asteroidSeed,
                    asteroidId = "AST-" + identity.ToString("X16") + "-" + item.index.ToString("X8"),
                    localPosition = pos,
                    localRotation = ApplyRotationConstraint((options.formation == AsteroidFormationType.IceShelfFragments ||
                        options.formation == AsteroidFormationType.IceShelfContinents)
                        ? Quaternion.Euler(rock.Range(-4f, 4f), rock.Range(0f, 360f), rock.Range(-4f, 4f))
                        : Quaternion.Euler(rock.Range(0f,360f), rock.Range(0f,360f), rock.Range(0f,360f)), ref rock, options),
                    nominalDiameter = item.diameter
                };
                result.Add(placement);
                var gridKey = Cell(pos, cellSide);
                if (!occupied.TryGetValue(gridKey, out var list))
                {
                    list = new List<Candidate>();
                    occupied.Add(gridKey, list);
                }
                var placed = new Candidate { position = pos, radius = item.radius, diameter = item.diameter };
                list.Add(placed);
                if (item.diameter >= MajorAnchorDiameter(options)) anchors.Add(placed);
            }
            result.Sort((a, b) => a.index.CompareTo(b.index));
            return result;
        }

        private static List<PlannedRock> BuildPlan(ulong identity, AsteroidFieldLayoutOptions options)
        {
            var plan = new List<PlannedRock>(options.asteroidCount);
            for (int i = 0; i < options.asteroidCount; i++)
            {
                int asteroidSeed = AsteroidIdUtility.DeriveSeed(unchecked((long)identity), i);
                var rock = new AsteroidRandom(unchecked((uint)asteroidSeed) ^ 0xD3465B3Fu);
                float diameter = Diameter(ref rock, options);
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

        private static float[] CreatePockets(ref AsteroidRandom rng, AsteroidFieldLayoutOptions o)
        {
            int n = Mathf.Max(2, o.densityPockets + 1);
            var pockets = new float[n];
            for (int i = 0; i < n; ++i)
                pockets[i] = Mathf.Clamp01((i + rng.Range(0.12f, 0.88f)) / n);
            return pockets;
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
            float mediumBand = 1f - smallBand - heroBand;
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
            // Rare outliers make fields less uniform.
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

        private static float Gaussian(ref AsteroidRandom rng)
        {
            float a = Mathf.Max(0.0001f, rng.Value());
            return Mathf.Sqrt(-2f * Mathf.Log(a)) * Mathf.Cos(rng.Value() * Mathf.PI * 2f);
        }

        private static Vector3 SampleNearAnchor(ref AsteroidRandom rng, AsteroidFieldLayoutOptions o, List<Candidate> anchors, float radius)
        {
            Candidate host = PickAnchor(ref rng, anchors);
            Vector3 dir = rng.UnitVector();
            if (o.depthMode != MOFieldDepthMode.Free3D) dir.y *= .15f;
            if (dir.sqrMagnitude < .0001f) dir = Vector3.right;
            dir.Normalize();
            float distance = host.radius + radius + rng.Range(host.radius * .18f, host.radius * .95f);
            Vector3 jitter = new Vector3(rng.Range(-o.thicknessMeters, o.thicknessMeters), rng.Range(-o.thicknessMeters, o.thicknessMeters), rng.Range(-o.thicknessMeters, o.thicknessMeters)) * .08f;
            if (o.depthMode != MOFieldDepthMode.Free3D) jitter.y *= .3f;
            return host.position + dir * distance + jitter;
        }

        private static Candidate PickAnchor(ref AsteroidRandom rng, List<Candidate> anchors)
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

        private static Vector3 NextPosition(ref AsteroidRandom rng, AsteroidFieldLayoutOptions o, float[] pockets)
        {
            float t = rng.Value();
            if (rng.Value() < o.clusterStrength)
            {
                float center = pockets[rng.Range(0, pockets.Length)];
                t = Mathf.Clamp01(center + Gaussian(ref rng) * o.pocketWidthFraction);
            }
            float spread = o.thicknessMeters;
            float u = Mathf.Clamp(Gaussian(ref rng) * 0.32f, -1f, 1f);
            float v = Mathf.Clamp(Gaussian(ref rng) * 0.28f, -1f, 1f);
            switch (o.formation)
            {
                case AsteroidFormationType.CrescentBelt:
                case AsteroidFormationType.BrokenBelt:
                {
                    if (o.formation == AsteroidFormationType.BrokenBelt)
                    {
                        int section = Mathf.Min(2, Mathf.FloorToInt(t * 3f));
                        t = (section + Mathf.Lerp(0.04f, 0.81f, t * 3f - section)) / 3f;
                    }
                    float half = o.arcDegrees * Mathf.Deg2Rad * 0.5f;
                    float angle = (t * 2f - 1f) * half;
                    float radial = o.radiusMeters + u * spread;
                    float z = radial * Mathf.Cos(angle) - o.radiusMeters * (1f + Mathf.Cos(half)) * 0.5f;
                    return new Vector3(radial * Mathf.Sin(angle), v * spread * o.cloudVerticalScale, z);
                }
                case AsteroidFormationType.BrokenRing:
                {
                    int section = Mathf.Min(3, Mathf.FloorToInt(t * 4f));
                    float local = t * 4f - section;
                    float angle = (section + Mathf.Lerp(0.07f, 0.82f, local)) * Mathf.PI * 0.5f;
                    return new Vector3(Mathf.Sin(angle) * (o.radiusMeters + u * spread),
                        v * spread * o.cloudVerticalScale,
                        Mathf.Cos(angle) * (o.radiusMeters + u * spread));
                }
                case AsteroidFormationType.ClusterGroups:
                {
                    float theta = (t * 2f - 1f) * Mathf.PI * 0.8f;
                    int group = rng.Range(0, Mathf.Max(2, o.clusterCount));
                    float a = (group + 0.5f) / Mathf.Max(2, o.clusterCount) * Mathf.PI * 1.6f - Mathf.PI * 0.8f;
                    float centerR = o.radiusMeters * 0.66f;
                    float rx = Gaussian(ref rng) * o.radiusMeters * o.clusterRadiusFraction * 0.36f;
                    float rz = Gaussian(ref rng) * o.radiusMeters * o.clusterRadiusFraction * 0.36f;
                    return new Vector3(Mathf.Sin(a) * centerR + rx,
                        Gaussian(ref rng) * spread * 0.23f,
                        Mathf.Cos(a) * centerR + rz + Mathf.Sin(theta) * spread * 0.08f);
                }
                case AsteroidFormationType.Stream:
                {
                    float x = (t * 2f - 1f) * o.radiusMeters;
                    float bend = Mathf.Sin(t * Mathf.PI * 3.2f) * spread * 0.55f;
                    return new Vector3(x, v * spread * o.cloudVerticalScale * 0.65f, bend + u * spread);
                }
                case AsteroidFormationType.IceShelfFragments:
                {
                    float x = (t * 2f - 1f) * o.radiusMeters;
                    float z = Mathf.Clamp(Gaussian(ref rng) * .28f, -.8f, .8f) * o.radiusMeters;
                    float y = v * o.thicknessMeters * .12f;
                    return new Vector3(x, y, z);
                }
                case AsteroidFormationType.IceShelfContinents:
                {
                    int belts = Mathf.Max(2, Mathf.Min(5, o.clusterCount));
                    int band = rng.Range(0, belts);
                    float lane = belts == 1 ? 0f : Mathf.Lerp(-.34f, .34f, band / (float)(belts - 1));
                    float side = rng.Value() < 0.5f ? -1f : 1f;
                    float x = (t * 2f - 1f) * o.radiusMeters + Gaussian(ref rng) * o.radiusMeters * .09f;
                    float z = (lane + side * rng.Range(.06f, .16f)) * o.radiusMeters + Gaussian(ref rng) * o.radiusMeters * .05f;
                    float y = v * o.thicknessMeters * .06f;
                    return new Vector3(x, y, z);
                }
                case AsteroidFormationType.SphericalCloud:
                {
                    Vector3 dir = rng.UnitVector();
                    float r = o.radiusMeters * Mathf.Pow(Mathf.Max(0.0001f, rng.Value()), 0.333333f);
                    return new Vector3(dir.x * r, dir.y * r * o.cloudVerticalScale, dir.z * r);
                }
                default: throw new ArgumentOutOfRangeException(nameof(o.formation));
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
        private static Vector3Int Cell(Vector3 p, float size) => new Vector3Int(
            Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.y / size), Mathf.FloorToInt(p.z / size));
        private static ulong HashText(string s)
        {
            unchecked
            {
                ulong x = 14695981039346656037UL;
                foreach (char c in s) { x ^= c; x *= 1099511628211UL; }
                return x;
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
