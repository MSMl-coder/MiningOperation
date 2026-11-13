using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    public static class AsteroidGeometryGenerator
    {
        private struct Crater { public Vector3 center; public float radius; public float depth; }
        private struct FracturePlane { public Vector3 normal; public float distance; }
        private sealed class IcoTemplate
        {
            public Vector3[] directions;
            public int[] triangles;
        }
        // Immutable icosphere directions/indices are shared between all generated asteroids.
        private static readonly Dictionary<int, IcoTemplate> Templates = new Dictionary<int, IcoTemplate>();
        private sealed class Shape
        {
            public Vector3 axes;
            public Quaternion rotation;
            public Vector3 offset;
            public Vector3 lobeAxis;
            public float lobeStrength;
            public float irregularity, roughness, craterIntensity;
            public readonly List<Crater> craters = new List<Crater>();
            public readonly List<FracturePlane> fractures = new List<FracturePlane>();
        }

        // Public convenience overload generates current-version geometry from live settings.
        public static Mesh Generate(int seed, float diameterMeters, AsteroidGeneratorSettings settings, int subdivisions)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return GenerateV2(seed, diameterMeters, AsteroidGeometryProfile.FromSettings(settings), subdivisions);
        }

        // Prefer THIS overload for existing asteroids; it uses the saved immutable profile.
        public static Mesh Generate(AsteroidData data, AsteroidGeneratorSettings legacySettings, int subdivisions)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.generatorVersion == 1)
            {
                if (legacySettings == null)
                    throw new InvalidOperationException("V1 geometry requires the original Generator Settings asset.");
                return AsteroidGeometryLegacyV1.Generate(data.seed, data.diameterMeters, legacySettings, subdivisions);
            }
            if (data.generatorVersion != AsteroidGeneration.GeneratorVersion || data.geometryProfile == null)
                throw new InvalidOperationException("Unsupported asteroid geometry version or missing V2 shape profile.");
            return GenerateV2(data.seed, data.diameterMeters, data.geometryProfile, subdivisions);
        }

        private static Mesh GenerateV2(int seed, float diameterMeters, AsteroidGeometryProfile profile, int subdivisions)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!AsteroidCompositionGenerator.IsFinite(diameterMeters) || diameterMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(diameterMeters));
            var shape = BuildShape(seed, profile);
            IcoTemplate sphere = GetTemplate(Mathf.Clamp(subdivisions, 0, 6));
            var vertices = new List<Vector3>(sphere.directions);
            float scale = diameterMeters * 0.5f;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 direction = vertices[i].normalized;
                float large = Fbm(direction * 2.1f + shape.offset, 4, 0.54f);
                float medium = Fbm(direction * 6.5f + shape.offset * 1.71f, 3, 0.55f);
                float small = Fbm(direction * 22f + shape.offset * 0.47f, 3, 0.51f);
                float radius = 1f + shape.irregularity * (0.34f * large + 0.105f * medium)
                                  + shape.roughness * (0.054f * small);
                if (shape.lobeStrength > 0f)
                {
                    float dot = Vector3.Dot(direction, shape.lobeAxis);
                    float waist = 1f - dot * dot;
                    // Smoothly pinched equator produces contact-binary silhouettes without topology breaks.
                    radius -= shape.lobeStrength * waist * waist * waist;
                }
                foreach (Crater crater in shape.craters)
                {
                    float u = Vector3.Distance(direction, crater.center) / crater.radius;
                    if (u > 1.28f) continue;
                    float bowl = u < 1f ? -(1f - u * u) * (1f - u * u) : 0f;
                    float rim = Mathf.Exp(-Mathf.Pow((u - 1f) * 7.3f, 2f)) * 0.145f;
                    radius += shape.craterIntensity * crater.depth * (bowl + rim);
                }
                foreach (FracturePlane fracture in shape.fractures)
                {
                    float facing = Vector3.Dot(direction, fracture.normal);
                    if (facing > 0.0001f)
                        radius = Mathf.Min(radius, fracture.distance / facing);
                }
                radius = Mathf.Max(0.31f, radius);
                vertices[i] = shape.rotation * (Vector3.Scale(direction, shape.axes) * (scale * radius));
            }
            var mesh = new Mesh { name = $"MO_Asteroid_V2_{seed}_S{subdivisions}" };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(sphere.triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Shape BuildShape(int seed, AsteroidGeometryProfile profile)
        {
            var rng = new AsteroidRandom(unchecked((uint)seed) ^ 0xB5297A4Du);
            float minAxis = Mathf.Clamp(profile.minimumAxisScale, 0.1f, 1f);
            float mode = rng.Value();
            var shape = new Shape
            {
                axes = new Vector3(rng.Range(minAxis, 1f), rng.Range(minAxis, 1f), rng.Range(minAxis, 1f)),
                rotation = Quaternion.Euler(rng.Range(0f, 360f), rng.Range(0f, 360f), rng.Range(0f, 360f)),
                offset = new Vector3(rng.Range(-99f, 99f), rng.Range(-99f, 99f), rng.Range(-99f, 99f)),
                lobeAxis = rng.UnitVector(),
                lobeStrength = mode < 0.2f ? rng.Range(0.18f, 0.43f) * profile.shapeIrregularity : 0f,
                irregularity = Mathf.Clamp01(profile.shapeIrregularity),
                roughness = Mathf.Clamp01(profile.surfaceRoughness),
                craterIntensity = Mathf.Clamp01(profile.craterIntensity)
            };
            // Distinct families: rounded boulders, slabs, elongated fragments and contact binaries.
            if (mode >= 0.2f && mode < 0.42f) shape.axes.x = minAxis;
            if (mode >= 0.42f && mode < 0.63f) shape.axes.y = minAxis;
            int minCount = Mathf.Clamp(profile.minCraters, 0, 60);
            int maxCount = Mathf.Clamp(profile.maxCraters, minCount, 60);
            int count = rng.Range(minCount, maxCount + 1);
            // Truncated planar faces mimic exposed fracture surfaces on a subset of rocks.
            int cutCount = mode > 0.2f && mode < 0.7f ? rng.Range(1, 4) : rng.Range(0, 2);
            for (int i = 0; i < cutCount; i++)
                shape.fractures.Add(new FracturePlane
                {
                    normal = rng.UnitVector(),
                    distance = rng.Range(0.69f, 0.91f)
                });
            for (int i = 0; i < count; i++)
            {
                shape.craters.Add(new Crater
                {
                    center = rng.UnitVector(),
                    radius = rng.Range(0.065f, 0.46f),
                    depth = rng.Range(0.035f, 0.22f)
                });
            }
            return shape;
        }

        private static float Fbm(Vector3 p, int octaves, float persistence)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(p) * amp;
                norm += amp;
                amp *= persistence;
                p = p * 2.03f + new Vector3(13.7f, 7.3f, 19.1f);
            }
            return sum / norm;
        }
        private static uint Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)z * 2246822519u;
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                return h;
            }
        }
        private static float Corner(int x, int y, int z)
        {
            return ((Hash(x, y, z) & 0xFFFFFFu) / 8388607.5f) - 1f;
        }
        private static float Smooth(float t) { return t * t * (3f - 2f * t); }
        private static float Noise(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = Smooth(p.x - x), fy = Smooth(p.y - y), fz = Smooth(p.z - z);
            float a = Mathf.Lerp(Corner(x,y,z), Corner(x+1,y,z), fx);
            float b = Mathf.Lerp(Corner(x,y+1,z), Corner(x+1,y+1,z), fx);
            float c = Mathf.Lerp(Corner(x,y,z+1), Corner(x+1,y,z+1), fx);
            float d = Mathf.Lerp(Corner(x,y+1,z+1), Corner(x+1,y+1,z+1), fx);
            return Mathf.Lerp(Mathf.Lerp(a,b,fy), Mathf.Lerp(c,d,fy), fz);
        }

        private static IcoTemplate GetTemplate(int subdivisions)
        {
            if (Templates.TryGetValue(subdivisions, out var template)) return template;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            BuildIcosphere(subdivisions, vertices, triangles);
            template = new IcoTemplate { directions = vertices.ToArray(), triangles = triangles.ToArray() };
            Templates.Add(subdivisions, template);
            return template;
        }

        private static void BuildIcosphere(int subdivisions, List<Vector3> vertices, List<int> triangles)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] initial =
            {
                new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),
                new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),
                new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)
            };
            foreach (Vector3 v in initial) vertices.Add(v.normalized);
            int[] faces =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            triangles.AddRange(faces);
            for (int level = 0; level < subdivisions; level++)
            {
                var cache = new Dictionary<ulong, int>();
                var next = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Mid(a, b, vertices, cache), bc = Mid(b, c, vertices, cache), ca = Mid(c, a, vertices, cache);
                    next.Add(a); next.Add(ab); next.Add(ca);
                    next.Add(b); next.Add(bc); next.Add(ab);
                    next.Add(c); next.Add(ca); next.Add(bc);
                    next.Add(ab); next.Add(bc); next.Add(ca);
                }
                triangles.Clear(); triangles.AddRange(next);
            }
        }
        private static int Mid(int a, int b, List<Vector3> v, Dictionary<ulong, int> cache)
        {
            uint lo = (uint)Math.Min(a,b), hi = (uint)Math.Max(a,b);
            ulong key = ((ulong)lo << 32) | hi;
            if (cache.TryGetValue(key, out int found)) return found;
            int index = v.Count;
            v.Add((v[a] + v[b]).normalized);
            cache.Add(key, index);
            return index;
        }
    }
}
