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
        private struct Candidate { public Vector3 position; public float radius; }

        public static List<AsteroidFieldPlacement> Plan(AsteroidFieldSettings settings,
            long fieldSeed, string fieldId, AsteroidFieldLayoutOptions options)
        {
            if (string.IsNullOrWhiteSpace(fieldId) || fieldId.Length > 80)
                throw new ArgumentException("Field ID must be 1..80 characters.", nameof(fieldId));
            options.Validate();
            ulong identity = Mix(unchecked((ulong)fieldSeed) ^ HashText(fieldId));
            var rng = new AsteroidRandom(unchecked((uint)(identity ^ (identity >> 32))));
            var result = new List<AsteroidFieldPlacement>(options.asteroidCount);
            float[] pockets = CreatePockets(ref rng, options);
            float cellSide = Mathf.Max(1f, options.maximumDiameter * 2f + options.minimumSurfaceGapMeters);
            var occupied = new Dictionary<Vector3Int, List<Candidate>>();
            for (int i = 0; i < options.asteroidCount; i++)
            {
                int asteroidSeed = AsteroidIdUtility.DeriveSeed(unchecked((long)identity), i);
                var rock = new AsteroidRandom(unchecked((uint)asteroidSeed) ^ 0xD3465B3Fu);
                float diameter = Diameter(ref rock, options);
                float radius = diameter * 0.95f; // Conservative irregular-shape envelope.
                Vector3 pos = Vector3.zero;
                bool accepted = false;
                for (int attempt = 0; attempt < options.placementAttempts; attempt++)
                {
                    pos = NextPosition(ref rng, options, pockets);
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
                            float separation = radius + candidate.radius + options.minimumSurfaceGapMeters;
                            if ((pos - candidate.position).sqrMagnitude < separation * separation)
                            {
                                overlap = true;
                                break;
                            }
                        }
                    }
                    if (!overlap) { accepted = true; break; }
                }
                if (!accepted) continue; // Prevent intersecting major asteroids; show count in Studio.
                var placement = new AsteroidFieldPlacement
                {
                    index = i,
                    asteroidSeed = asteroidSeed,
                    asteroidId = "AST-" + identity.ToString("X16") + "-" + i.ToString("X8"),
                    localPosition = pos,
                    localRotation = Quaternion.Euler(rock.Range(0f,360f), rock.Range(0f,360f), rock.Range(0f,360f)),
                    nominalDiameter = diameter
                };
                result.Add(placement);
                var gridKey = Cell(pos, cellSide);
                if (!occupied.TryGetValue(gridKey, out var list))
                {
                    list = new List<Candidate>();
                    occupied.Add(gridKey, list);
                }
                list.Add(new Candidate { position = pos, radius = radius });
            }
            return result;
        }

        private static float[] CreatePockets(ref AsteroidRandom rng, AsteroidFieldLayoutOptions o)
        {
            int n = Mathf.Max(2, o.densityPockets);
            var pockets = new float[n];
            for (int i = 0; i < n; ++i)
                pockets[i] = Mathf.Clamp01((i + rng.Range(0.15f, 0.85f)) / n);
            return pockets;
        }

        private static float Diameter(ref AsteroidRandom rng, AsteroidFieldLayoutOptions o)
        {
            float bias = Mathf.Clamp01(o.smallAsteroidBias);
            float smallChance = Mathf.Lerp(0.35f, 0.83f, bias);
            float giantChance = Mathf.Lerp(0.17f, 0.025f, bias);
            float pick = rng.Value();
            float t = pick < smallChance ? Mathf.Lerp(0.0f, 0.36f, rng.Value()) :
                pick > 1f - giantChance ? Mathf.Lerp(0.72f, 1f, rng.Value()) :
                Mathf.Lerp(0.36f, 0.72f, rng.Value());
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(o.minimumDiameter), Mathf.Log(o.maximumDiameter), t));
        }

        private static float Gaussian(ref AsteroidRandom rng)
        {
            float a = Mathf.Max(0.0001f, rng.Value());
            return Mathf.Sqrt(-2f * Mathf.Log(a)) * Mathf.Cos(rng.Value() * Mathf.PI * 2f);
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
                    // The offset moves the arc closer to its scene Spawn Point.
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
                case AsteroidFormationType.SphericalCloud:
                {
                    Vector3 dir = rng.UnitVector();
                    float r = o.radiusMeters * Mathf.Pow(Mathf.Max(0.0001f, rng.Value()), 0.333333f);
                    return new Vector3(dir.x * r, dir.y * r * o.cloudVerticalScale, dir.z * r);
                }
                default: throw new ArgumentOutOfRangeException(nameof(o.formation));
            }
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
