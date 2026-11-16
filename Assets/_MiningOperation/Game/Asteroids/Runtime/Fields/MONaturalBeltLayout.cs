using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// V7: finite, art-directable, non-periodic asteroid-belt layout.
    /// The large-scale crescent remains recognizable, but its centerline, width,
    /// dense pockets and gaps are seeded and independent of rock index/size.
    /// No geometry/resources are created here.
    /// </summary>
    public static class MONaturalBeltLayout
    {
        private struct Body
        {
            public int index;
            public int seed;
            public float diameter;
            public float exclusionRadius;
            public float satelliteRoll;
        }

        private struct Placed
        {
            public Vector3 position;
            public float radius;
            public float diameter;
        }

        private struct Pocket
        {
            public float arcT;
            public float width;
            public float weight;
            public float radialBias;
            public float spreadScale;
        }

        private struct Gap
        {
            public float center;
            public float halfWidth;
        }

        public static List<AsteroidFieldPlacement> Plan(long seed, string fieldId,
            AsteroidFieldLayoutOptions options)
        {
            if (string.IsNullOrWhiteSpace(fieldId) || fieldId.Length > 80)
                throw new ArgumentException("Field ID must contain 1..80 non-whitespace characters.", nameof(fieldId));
            options.Validate();
            ulong identity = Mix(unchecked((ulong)seed) ^ HashId(fieldId));
            var rng = new AsteroidRandom(unchecked((uint)(identity ^ identity >> 32)) ^ 0xD68A8B1Du);
            int noiseSeed = unchecked((int)(identity ^ (identity >> 32)));
            var pockets = MakePockets(ref rng, options);
            var gaps = MakeGaps(ref rng, options);
            var plan = MakeBodyPlan(identity, options);
            var result = new List<AsteroidFieldPlacement>(options.asteroidCount);
            var anchors = new List<Placed>();
            var occupied = new Dictionary<Vector3Int, List<Placed>>();
            float cellSize = Mathf.Max(1f, options.maximumDiameter * 1.91f + options.minimumSurfaceGapMeters);

            for (int i = 0; i < plan.Count; i++)
            {
                Body body = plan[i];
                bool smallBody = body.diameter < Mathf.Exp(Mathf.Lerp(
                    Mathf.Log(options.minimumDiameter), Mathf.Log(options.maximumDiameter), .41f));
                bool satellite = smallBody && anchors.Count > 0 &&
                    body.satelliteRoll < options.naturalSatelliteChance;
                Vector3 pos = Vector3.zero;
                bool accepted = false;
                int attempts = Mathf.Clamp(options.placementAttempts, 1, 300);
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    bool nearAnchor = satellite && attempt < Mathf.Max(2, attempts / 5);
                    pos = nearAnchor
                        ? SampleSatellite(ref rng, anchors, options, body.exclusionRadius)
                        : SampleField(ref rng, options, pockets, gaps, noiseSeed,
                            attempt >= Mathf.Max(6, attempts * 2 / 3));
                    pos = ConstrainHeight(ref rng, options, pos);
                    if (!IsSeparated(pos, body.exclusionRadius, cellSize, options.minimumSurfaceGapMeters, occupied))
                        continue;
                    accepted = true;
                    break;
                }
                if (!accepted) continue;
                var rock = new AsteroidRandom(unchecked((uint)body.seed) ^ 0xD3465B3Fu);
                var rotation = Quaternion.Euler(rock.Range(0f, 360f), rock.Range(0f, 360f), rock.Range(0f, 360f));
                if (options.flattenRotationToPlane && options.depthMode != MOFieldDepthMode.Free3D)
                {
                    Vector3 e = rotation.eulerAngles;
                    rotation = Quaternion.Euler(rock.Range(-9f, 9f), e.y, rock.Range(-9f, 9f));
                }
                result.Add(new AsteroidFieldPlacement
                {
                    index = body.index,
                    asteroidSeed = body.seed,
                    asteroidId = "AST-" + identity.ToString("X16") + "-" + body.index.ToString("X8"),
                    localPosition = pos,
                    localRotation = rotation,
                    nominalDiameter = body.diameter
                });
                var placed = new Placed { position = pos, radius = body.exclusionRadius, diameter = body.diameter };
                Vector3Int cell = Cell(pos, cellSize);
                if (!occupied.TryGetValue(cell, out var list))
                {
                    list = new List<Placed>(4);
                    occupied.Add(cell, list);
                }
                list.Add(placed);
                float major = Mathf.Exp(Mathf.Lerp(Mathf.Log(options.minimumDiameter),
                    Mathf.Log(options.maximumDiameter), .68f));
                if (body.diameter >= major) anchors.Add(placed);
            }
            result.Sort((a, b) => a.index.CompareTo(b.index));
            return result;
        }

        private static List<Body> MakeBodyPlan(ulong identity, AsteroidFieldLayoutOptions options)
        {
            var bodies = new List<Body>(options.asteroidCount);
            for (int i = 0; i < options.asteroidCount; i++)
            {
                int seed = AsteroidIdUtility.DeriveSeed(unchecked((long)identity), i);
                var rock = new AsteroidRandom(unchecked((uint)seed) ^ 0xD3465B3Fu);
                float d = SampleDiameter(ref rock, options);
                bodies.Add(new Body
                {
                    index = i,
                    seed = seed,
                    diameter = d,
                    exclusionRadius = d * .95f,
                    satelliteRoll = rock.Value()
                });
            }
            // Large rocks are given placement priority; IDs/seeds still depend ONLY on original index.
            bodies.Sort((a, b) => b.diameter.CompareTo(a.diameter));
            return bodies;
        }

        // Matches the v2.6.3 diameter distribution; this patch changes *where*
        // rocks are placed, not the diameter/resource distribution of each ID.
        private static float SampleDiameter(ref AsteroidRandom rng, AsteroidFieldLayoutOptions o)
        {
            float bias = Mathf.Clamp01(o.smallAsteroidBias);
            float smallBand = Mathf.Lerp(.58f, .84f, bias);
            float heroBand = Mathf.Lerp(.09f, .03f, bias);
            float pick = rng.Value();
            float t;
            if (pick < smallBand)
            {
                float u = 1f - Mathf.Pow(1f - rng.Value(), Mathf.Lerp(3.6f, 5.8f, bias));
                t = Mathf.Lerp(0f, .34f, u);
            }
            else if (pick > 1f - heroBand)
                t = Mathf.Lerp(.78f, 1f, Mathf.Pow(rng.Value(), .55f));
            else
                t = Mathf.Lerp(.34f, .78f, Mathf.Pow(rng.Value(), .82f));
            if (rng.Value() > .965f)
                t = Mathf.Lerp(t, 1f, rng.Range(.35f, .85f));
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(o.minimumDiameter),
                Mathf.Log(o.maximumDiameter), Mathf.Clamp01(t)));
        }

        private static Pocket[] MakePockets(ref AsteroidRandom rng, AsteroidFieldLayoutOptions o)
        {
            // No evenly spaced anchor cells: both positions AND widths are independent.
            int count = Mathf.Clamp(o.densityPockets, 2, 24);
            var pockets = new Pocket[count];
            for (int i = 0; i < count; i++)
            {
                pockets[i] = new Pocket
                {
                    arcT = rng.Range(.02f, .98f),
                    width = rng.Range(.035f, .14f) * Mathf.Lerp(1.3f, .65f, o.naturalDensityContrast),
                    weight = rng.Range(.25f, 1.9f),
                    radialBias = rng.Range(-.85f, .85f),
                    spreadScale = rng.Range(.55f, 1.5f)
                };
            }
            return pockets;
        }

        private static Gap[] MakeGaps(ref AsteroidRandom rng, AsteroidFieldLayoutOptions o)
        {
            if (o.formation != AsteroidFormationType.BrokenBelt) return Array.Empty<Gap>();
            var gaps = new Gap[3];
            for (int i = 0; i < gaps.Length; i++)
            {
                gaps[i] = new Gap
                {
                    center = rng.Range(.10f, .90f),
                    halfWidth = rng.Range(.018f, .055f)
                };
            }
            return gaps;
        }

        private static Vector3 SampleField(ref AsteroidRandom rng, AsteroidFieldLayoutOptions o,
            Pocket[] pockets, Gap[] gaps, int seed, bool diffuseOnly)
        {
            float t = .5f;
            float radialBias = 0f;
            float spreadScale = 1f;
            for (int trial = 0; trial < 14; trial++)
            {
                bool fromPocket = !diffuseOnly &&
                    rng.Value() < Mathf.Lerp(.08f, .83f, o.naturalDensityContrast);
                if (fromPocket)
                {
                    Pocket p = PickPocket(ref rng, pockets);
                    t = Mathf.Clamp01(p.arcT + Gaussian(ref rng) * p.width);
                    radialBias = p.radialBias;
                    spreadScale = p.spreadScale;
                }
                else
                {
                    t = rng.Value();
                    radialBias = 0f;
                    spreadScale = rng.Range(.7f, 1.5f);
                }
                bool gap = false;
                for (int g = 0; g < gaps.Length; g++)
                {
                    float warpedGap = gaps[g].center +
                        .025f * SmoothNoise(t * 5.8f + 4.8f, seed + g * 23);
                    if (Mathf.Abs(t - warpedGap) < gaps[g].halfWidth)
                    {
                        gap = true;
                        break;
                    }
                }
                if (!gap) break;
                t = rng.Value();
                radialBias = 0f;
            }

            float chaos = o.naturalBeltIrregularity;
            // Vary both centerline and angular coordinate continuously; no clean concentric bands.
            float arcWarp = chaos * .063f * SmoothNoise(t * 3.9f + .7f, seed + 13);
            float warpedT = Mathf.Clamp01(t + arcWarp);
            float angle = (warpedT - .5f) * o.arcDegrees * Mathf.Deg2Rad;
            float centerWarp = (SmoothNoise(t * 2.9f + 7.1f, seed + 31) * .15f +
                                SmoothNoise(t * 7.8f + 2.9f, seed + 67) * .055f) * chaos;
            float radius = o.radiusMeters * (1f + centerWarp);
            float localWidth = o.thicknessMeters * Mathf.Lerp(.52f, 1.48f,
                .5f + .5f * SmoothNoise(t * 4.5f + 1.3f, seed + 113));
            float radialOffset = Mathf.Clamp(Gaussian(ref rng), -2.8f, 2.8f) *
                                 localWidth * spreadScale * .73f +
                                 radialBias * localWidth * o.naturalDensityContrast * .43f;
            float tangentOffset = Gaussian(ref rng) * localWidth * .34f * chaos;
            Vector3 radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 tangent = new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
            Vector3 p3 = radial * (radius + radialOffset) + tangent * tangentOffset;
            p3.z -= o.radiusMeters * (1f + Mathf.Cos(o.arcDegrees * Mathf.Deg2Rad * .5f)) * .5f;
            p3.y = Mathf.Clamp(Gaussian(ref rng), -2.8f, 2.8f) * o.thicknessMeters *
                   o.cloudVerticalScale * .32f;
            return p3;
        }

        private static Pocket PickPocket(ref AsteroidRandom rng, Pocket[] pockets)
        {
            float sum = 0f;
            for (int i = 0; i < pockets.Length; i++) sum += pockets[i].weight;
            float roll = rng.Value() * sum;
            for (int i = 0; i < pockets.Length; i++)
            {
                roll -= pockets[i].weight;
                if (roll <= 0f) return pockets[i];
            }
            return pockets[pockets.Length - 1];
        }

        private static Vector3 SampleSatellite(ref AsteroidRandom rng, List<Placed> anchors,
            AsteroidFieldLayoutOptions o, float bodyRadius)
        {
            int index = rng.Range(0, anchors.Count);
            var anchor = anchors[index];
            float a = rng.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            // Asymmetrical satellites, not a second continuous arc.
            float r = anchor.radius + bodyRadius + o.minimumSurfaceGapMeters +
                      rng.Range(anchor.radius * .25f, anchor.radius * 1.65f);
            return anchor.position + direction * r + new Vector3(0f,
                rng.Range(-o.thicknessMeters, o.thicknessMeters) * .18f, 0f);
        }

        private static Vector3 ConstrainHeight(ref AsteroidRandom rng,
            AsteroidFieldLayoutOptions o, Vector3 pos)
        {
            switch (o.depthMode)
            {
                case MOFieldDepthMode.PlanarSlab:
                    pos.y = o.depthCenterY + Mathf.Clamp(pos.y, -o.depthHalfRange, o.depthHalfRange);
                    break;
                case MOFieldDepthMode.Strict2DPlane:
                    pos.y = o.depthCenterY + (o.depthJitter <= 0f ? 0f : rng.Range(-o.depthJitter, o.depthJitter));
                    break;
            }
            return pos;
        }

        private static bool IsSeparated(Vector3 point, float radius, float cellSize,
            float gap, Dictionary<Vector3Int, List<Placed>> occupied)
        {
            Vector3Int cell = Cell(point, cellSize);
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                var key = new Vector3Int(cell.x + x, cell.y + y, cell.z + z);
                if (!occupied.TryGetValue(key, out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    float minDistance = radius + list[i].radius + gap;
                    if ((point - list[i].position).sqrMagnitude < minDistance * minDistance)
                        return false;
                }
            }
            return true;
        }

        private static Vector3Int Cell(Vector3 point, float side)
            => new Vector3Int(Mathf.FloorToInt(point.x / side), Mathf.FloorToInt(point.y / side),
                Mathf.FloorToInt(point.z / side));

        private static float Gaussian(ref AsteroidRandom rng)
        {
            float a = Mathf.Max(.0001f, rng.Value());
            return Mathf.Sqrt(-2f * Mathf.Log(a)) * Mathf.Cos(rng.Value() * Mathf.PI * 2f);
        }

        private static float SmoothNoise(float x, int seed)
        {
            int cell = Mathf.FloorToInt(x);
            float t = x - cell;
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(HashNoise(cell, seed), HashNoise(cell + 1, seed), t);
        }

        private static float HashNoise(int x, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x9E3779B9u ^ (uint)seed;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return ((h & 0xFFFFFFu) / 8388607.5f) - 1f;
            }
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
