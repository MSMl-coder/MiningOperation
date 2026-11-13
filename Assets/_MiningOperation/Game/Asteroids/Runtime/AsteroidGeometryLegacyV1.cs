using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    internal static class AsteroidGeometryLegacyV1
    {
        private struct Crater { public Vector3 center; public float radius; public float depth; }
        private sealed class Shape
        {
            public Vector3 axes;
            public Vector3 offset;
            public float irregularity, roughness, craterIntensity;
            public readonly List<Crater> craters = new List<Crater>();
        }

        // LODs sample the SAME analytic displacement function. No random state is consumed by mesh subdivision.
        internal static Mesh Generate(int seed, float diameterMeters, AsteroidGeneratorSettings settings, int subdivisions)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var shape = BuildShape(seed, settings);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            BuildIcosphere(Mathf.Clamp(subdivisions, 0, 6), vertices, triangles);
            float scale = Mathf.Max(0.1f, diameterMeters) * 0.5f;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 direction = vertices[i].normalized;
                float n1 = Fbm(direction * 2.4f + shape.offset, 4, 0.52f);
                float n2 = Fbm(direction * 7.8f + shape.offset * 1.73f, 3, 0.55f);
                float radius = 1f + shape.irregularity * (0.24f * n1 + 0.095f * n2)
                    + shape.roughness * 0.042f * Fbm(direction * 26f + shape.offset * 0.7f, 3, 0.5f);
                foreach (var crater in shape.craters)
                {
                    float chord = Vector3.Distance(direction, crater.center);
                    float u = chord / crater.radius;
                    if (u >= 1.3f) continue;
                    float bowl = u < 1f ? -(1f - u * u) * (1f - u * u) : 0f;
                    float rim = Mathf.Exp(-Mathf.Pow((u - 1f) * 7f, 2f)) * 0.17f;
                    radius += shape.craterIntensity * crater.depth * (bowl + rim);
                }
                radius = Mathf.Max(0.35f, radius);
                vertices[i] = Vector3.Scale(direction, shape.axes) * (scale * radius);
            }
            var mesh = new Mesh { name = $"Asteroid_{seed}_LOD{subdivisions}" };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Shape BuildShape(int seed, AsteroidGeneratorSettings settings)
        {
            var rng = new AsteroidRandom(unchecked((uint)seed) ^ 0xB5297A4Du);
            float minAxis = Mathf.Clamp(settings.minimumAxisScale, 0.1f, 1f);
            var s = new Shape
            {
                axes = new Vector3(1f, rng.Range(minAxis, 1f), rng.Range(minAxis, 1f)),
                offset = new Vector3(rng.Range(-100f, 100f), rng.Range(-100f, 100f), rng.Range(-100f, 100f)),
                irregularity = settings.shapeIrregularity,
                roughness = settings.surfaceRoughness,
                craterIntensity = settings.craterIntensity
            };
            int count = rng.Range(Mathf.Max(0, settings.minCraters), Mathf.Max(settings.minCraters, settings.maxCraters) + 1);
            for (int i = 0; i < count; i++)
                s.craters.Add(new Crater { center = rng.UnitVector(), radius = rng.Range(0.10f, 0.48f), depth = rng.Range(0.045f, 0.22f) });
            return s;
        }

        private static float Fbm(Vector3 p, int octaves, float persistence)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(p) * amp;
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
        private static float Corner(int x, int y, int z) { return ((Hash(x, y, z) & 0xFFFFFFu) / 8388607.5f) - 1f; }
        private static float Smooth(float t) { return t * t * (3f - 2f * t); }
        private static float ValueNoise(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = Smooth(p.x - x), fy = Smooth(p.y - y), fz = Smooth(p.z - z);
            float a = Mathf.Lerp(Corner(x,y,z), Corner(x+1,y,z), fx);
            float b = Mathf.Lerp(Corner(x,y+1,z), Corner(x+1,y+1,z), fx);
            float c = Mathf.Lerp(Corner(x,y,z+1), Corner(x+1,y,z+1), fx);
            float d = Mathf.Lerp(Corner(x,y+1,z+1), Corner(x+1,y+1,z+1), fx);
            return Mathf.Lerp(Mathf.Lerp(a,b,fy), Mathf.Lerp(c,d,fy), fz);
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
            foreach (var v in initial) vertices.Add(v.normalized);
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
                var cache = new Dictionary<ulong,int>();
                var next = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i+1], c = triangles[i+2];
                    int ab = Mid(a,b,vertices,cache), bc = Mid(b,c,vertices,cache), ca = Mid(c,a,vertices,cache);
                    next.AddRange(new [] { a,ab,ca, b,bc,ab, c,ca,bc, ab,bc,ca });
                }
                triangles.Clear(); triangles.AddRange(next);
            }
        }
        private static int Mid(int a, int b, List<Vector3> vertices, Dictionary<ulong,int> cache)
        {
            uint low = (uint)Mathf.Min(a,b), high = (uint)Mathf.Max(a,b);
            ulong key = ((ulong)low << 32) | high;
            if (cache.TryGetValue(key, out int existing)) return existing;
            int index = vertices.Count;
            vertices.Add((vertices[a] + vertices[b]).normalized);
            cache[key] = index;
            return index;
        }
    }
}
