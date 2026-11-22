using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    public static class AsteroidGeometryGenerator
    {
        private enum ShapeFamily { Rounded, Elongated, Faceted, ContactBinary, Cratered, Blocky }
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
            public ShapeFamily family;
            public Vector3 axes;
            public Quaternion rotation;
            public Vector3 offset;
            public Vector3 lobeAxis;
            public float lobeStrength;
            public float irregularity, roughness, craterIntensity;
            public float macroWeight, mediumWeight, smallWeight, ridgeStrength, pinchStrength;
            public readonly List<Crater> craters = new List<Crater>();
            public readonly List<FracturePlane> fractures = new List<FracturePlane>();
        }

        // Public convenience overload generates current-version geometry from live settings.
        public static Mesh Generate(int seed, float diameterMeters, AsteroidGeneratorSettings settings, int subdivisions)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return Generate(new AsteroidData
            {
                seed = seed, diameterMeters = diameterMeters,
                generatorVersion = AsteroidGeneration.GeneratorVersion,
                geometryProfile = AsteroidGeometryProfile.FromSettings(settings)
            }, settings, subdivisions, true);
        }

        // Prefer THIS overload for existing asteroids; it uses the saved immutable profile.
        public static Mesh Generate(AsteroidData data, AsteroidGeneratorSettings legacySettings, int subdivisions)
            => Generate(data, legacySettings, subdivisions, true);

        internal static Mesh Generate(AsteroidData data, AsteroidGeneratorSettings legacySettings,
            int subdivisions, bool visualColors)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.generatorVersion == 1)
            {
                if (legacySettings == null)
                    throw new InvalidOperationException("V1 geometry requires the original Generator Settings asset.");
                Mesh legacyMesh = AsteroidGeometryLegacyV1.Generate(data.seed, data.diameterMeters,
                    legacySettings, subdivisions);
                if (visualColors)
                {
                    var legacyColors = new Color32[legacyMesh.vertexCount];
                    for (int i = 0; i < legacyColors.Length; i++)
                        legacyColors[i] = new Color32(255, 255, 255, 255);
                    legacyMesh.colors32 = legacyColors;
                    AsteroidSurfaceUV.Apply(legacyMesh, data.seed);
                }
                return legacyMesh;
            }
            if ((data.generatorVersion == AsteroidGeneration.IceShelfGeneratorVersion ||
                 data.generatorVersion == AsteroidGeneration.IceShelfContinentalGeneratorVersion) &&
                data.geometryProfile != null && data.geometryProfile.iceShelf)
                return AsteroidIceShelfGeometry.Generate(data.seed, data.diameterMeters,
                    data.geometryProfile, subdivisions, visualColors);
            if ((data.generatorVersion != 2 && data.generatorVersion != 3 && data.generatorVersion != 4 && data.generatorVersion != 5 && data.generatorVersion != 8 && data.generatorVersion != 9) || data.geometryProfile == null)
                throw new InvalidOperationException("Unsupported asteroid geometry version or missing shape profile.");
            // V8: more distinct silhouette families with a cleaner silhouette target.
            // Versions 1..5 follow their exact legacy recipes for save compatibility.
            AsteroidGeometryProfile working = data.geometryProfile;
            if (data.generatorVersion == 5)
            {
                working = data.geometryProfile.Copy();
                working.surfaceRoughness *= .22f;
                working.craterIntensity *= .62f;
                working.minCraters = Mathf.Min(working.minCraters, 3);
                working.maxCraters = Mathf.Min(working.maxCraters, 7);
            }
            else if (data.generatorVersion == 8 || data.generatorVersion == 9)
            {
                working = data.geometryProfile.Copy();
                working.surfaceRoughness *= .16f;
                working.craterIntensity *= .78f;
                working.minCraters = Mathf.Min(working.minCraters, 5);
                working.maxCraters = Mathf.Min(working.maxCraters, 10);
            }
            Mesh mesh = GenerateV2(data.seed, data.diameterMeters, working, subdivisions, visualColors, data.generatorVersion);
            if (data.generatorVersion == 5 || data.generatorVersion == 8 || data.generatorVersion == 9)
            {
                AsteroidGeologyV5.Apply(mesh, data.seed, data.geometryProfile);
                if (data.generatorVersion == 9)
                    AsteroidGeologyV9.Apply(mesh, data.seed, data.geometryProfile);
            }
            else
            {
                if (data.generatorVersion >= 3)
                    AsteroidGeologyV3.Apply(mesh, data.seed, data.diameterMeters, data.geometryProfile);
                if (data.generatorVersion >= 4)
                    AsteroidGeologyV4.Apply(mesh, data.seed, data.diameterMeters, data.geometryProfile);
            }
            if (visualColors) AsteroidSurfaceUV.Apply(mesh, data.seed);
            return mesh;
        }

        private static Mesh GenerateV2(int seed, float diameterMeters, AsteroidGeometryProfile profile,
            int subdivisions, bool visualColors, int geometryVersion)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!AsteroidCompositionGenerator.IsFinite(diameterMeters) || diameterMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(diameterMeters));
            var shape = geometryVersion == 8 || geometryVersion == 9 ? BuildShapeV8(seed, profile) : BuildShapeLegacy(seed, profile);
            IcoTemplate sphere = GetTemplate(Mathf.Clamp(subdivisions, 0, 6));
            var vertices = new List<Vector3>(sphere.directions);
            float scale = diameterMeters * 0.5f;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 direction = vertices[i].normalized;
                float large = Fbm(direction * 1.85f + shape.offset, 4, 0.54f);
                float medium = Fbm(direction * 4.6f + shape.offset * 1.37f, 3, 0.56f);
                float small = Fbm(direction * 15.5f + shape.offset * 0.41f, 2, 0.51f);
                float ridge = 1f - Mathf.Abs(Fbm(direction * 3.15f - shape.offset * 0.22f, 3, 0.57f));
                ridge = ridge * ridge;
                float radius = 1f + shape.irregularity * (shape.macroWeight * large + shape.mediumWeight * medium)
                                  + shape.roughness * (shape.smallWeight * small)
                                  + shape.irregularity * shape.ridgeStrength * (ridge - .38f);
                if (shape.lobeStrength > 0f)
                {
                    float dot = Vector3.Dot(direction, shape.lobeAxis);
                    float waist = 1f - dot * dot;
                    radius -= shape.lobeStrength * waist * waist * waist;
                    radius += shape.pinchStrength * Mathf.Abs(dot) * 0.065f;
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
                radius = Mathf.Max(0.27f, radius);
                vertices[i] = shape.rotation * (Vector3.Scale(direction, shape.axes) * (scale * radius));
            }
            var mesh = new Mesh { name = $"MO_Asteroid_V2_{seed}_S{subdivisions}" };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(sphere.triangles, 0);
            if (visualColors)
            {
                var colors = new Color32[vertices.Count];
                float tint = Mathf.Clamp01(0.5f + 0.25f * Noise(shape.offset * 0.12f));
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector3 p = vertices[i] / scale;
                    float grain = Fbm(p * 3.2f + shape.offset * 0.17f, 2, 0.53f);
                    float strata = Mathf.Abs(Fbm(p * 1.35f - shape.offset * 0.22f, 2, 0.55f));
                    float hero = 1f - Mathf.Abs(Fbm(p * 0.92f + shape.offset * 0.09f, 2, 0.5f));
                    float value = Mathf.Clamp01(0.80f + grain * 0.18f - strata * 0.08f + hero * 0.04f);
                    colors[i] = new Color(value * Mathf.Lerp(0.94f, 1.05f, tint),
                        value * Mathf.Lerp(0.97f, 1.01f, tint),
                        value * Mathf.Lerp(1.04f, 0.91f, tint), 1f);
                }
                mesh.colors32 = colors;
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Shape BuildShapeLegacy(int seed, AsteroidGeometryProfile profile)
        {
            var rng = new AsteroidRandom(unchecked((uint)seed) ^ 0xB5297A4Du);
            float minAxis = Mathf.Clamp(profile.minimumAxisScale, 0.1f, 1f);
            float mode = rng.Value();
            var shape = new Shape
            {
                family = ShapeFamily.Rounded,
                axes = new Vector3(rng.Range(minAxis, 1f), rng.Range(minAxis, 1f), rng.Range(minAxis, 1f)),
                rotation = Quaternion.Euler(rng.Range(0f, 360f), rng.Range(0f, 360f), rng.Range(0f, 360f)),
                offset = new Vector3(rng.Range(-99f, 99f), rng.Range(-99f, 99f), rng.Range(-99f, 99f)),
                lobeAxis = rng.UnitVector(),
                lobeStrength = mode < 0.2f ? rng.Range(0.18f, 0.43f) * profile.shapeIrregularity : 0f,
                irregularity = Mathf.Clamp01(profile.shapeIrregularity),
                roughness = Mathf.Clamp01(profile.surfaceRoughness),
                craterIntensity = Mathf.Clamp01(profile.craterIntensity),
                macroWeight = .34f,
                mediumWeight = .105f,
                smallWeight = .054f,
                ridgeStrength = 0f,
                pinchStrength = 0f
            };
            if (mode >= 0.2f && mode < 0.42f) shape.axes.x = minAxis;
            if (mode >= 0.42f && mode < 0.63f) shape.axes.y = minAxis;
            int minCount = Mathf.Clamp(profile.minCraters, 0, 60);
            int maxCount = Mathf.Clamp(profile.maxCraters, minCount, 60);
            int count = rng.Range(minCount, maxCount + 1);
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

        private static Shape BuildShapeV8(int seed, AsteroidGeometryProfile profile)
        {
            var rng = new AsteroidRandom(unchecked((uint)seed) ^ 0xC8F1172Du);
            float minAxis = Mathf.Clamp(profile.minimumAxisScale, 0.1f, 1f);
            float familyRoll = rng.Value();
            ShapeFamily family = familyRoll < 0.20f ? ShapeFamily.Rounded :
                familyRoll < 0.38f ? ShapeFamily.Elongated :
                familyRoll < 0.58f ? ShapeFamily.Faceted :
                familyRoll < 0.73f ? ShapeFamily.ContactBinary :
                familyRoll < 0.88f ? ShapeFamily.Cratered : ShapeFamily.Blocky;
            // Explicit art-directed morphology. We still consume the same random roll,
            // so Mixed shapes generated with old seeds remain bit-for-bit consistent.
            switch (profile.shapePreset)
            {
                case MOAsteroidShapePreset.Rounded: family = ShapeFamily.Rounded; break;
                case MOAsteroidShapePreset.Fractured: family = ShapeFamily.Faceted; break;
                case MOAsteroidShapePreset.Elongated: family = ShapeFamily.Elongated; break;
                case MOAsteroidShapePreset.ContactBinary: family = ShapeFamily.ContactBinary; break;
                case MOAsteroidShapePreset.Cratered: family = ShapeFamily.Cratered; break;
                case MOAsteroidShapePreset.Blocky: family = ShapeFamily.Blocky; break;
            }
            var shape = new Shape
            {
                family = family,
                axes = Vector3.one,
                rotation = Quaternion.Euler(rng.Range(0f, 360f), rng.Range(0f, 360f), rng.Range(0f, 360f)),
                offset = new Vector3(rng.Range(-129f, 129f), rng.Range(-129f, 129f), rng.Range(-129f, 129f)),
                lobeAxis = rng.UnitVector(),
                irregularity = Mathf.Clamp01(profile.shapeIrregularity),
                roughness = Mathf.Clamp01(profile.surfaceRoughness),
                craterIntensity = Mathf.Clamp01(profile.craterIntensity),
                macroWeight = .28f,
                mediumWeight = .075f,
                smallWeight = .020f,
                ridgeStrength = .035f,
                pinchStrength = 0f
            };

            switch (family)
            {
                case ShapeFamily.Rounded:
                    shape.axes = new Vector3(rng.Range(.86f, 1.06f), rng.Range(.84f, 1.05f), rng.Range(.86f, 1.06f));
                    shape.lobeStrength = 0f;
                    shape.macroWeight = .20f;
                    shape.mediumWeight = .06f;
                    shape.smallWeight = .015f;
                    shape.ridgeStrength = .01f;
                    AddFractures(shape, ref rng, 0, 1, .79f, .92f);
                    AddCraters(shape, ref rng, profile, 1, 5, .12f, .34f, .05f, .17f);
                    break;
                case ShapeFamily.Elongated:
                    shape.axes = new Vector3(rng.Range(.50f, .72f), rng.Range(.72f, 1.02f), rng.Range(1.08f, 1.42f));
                    if (rng.Value() < .5f) shape.axes = new Vector3(shape.axes.z, shape.axes.y, shape.axes.x);
                    shape.lobeStrength = rng.Range(.06f, .14f) * profile.shapeIrregularity;
                    shape.macroWeight = .30f;
                    shape.mediumWeight = .08f;
                    shape.smallWeight = .017f;
                    shape.ridgeStrength = .04f;
                    AddFractures(shape, ref rng, 1, 3, .71f, .88f);
                    AddCraters(shape, ref rng, profile, 0, 4, .10f, .28f, .05f, .14f);
                    break;
                case ShapeFamily.Faceted:
                    shape.axes = new Vector3(rng.Range(minAxis, .82f), rng.Range(minAxis, .92f), rng.Range(.90f, 1.16f));
                    shape.lobeStrength = 0f;
                    shape.macroWeight = .34f;
                    shape.mediumWeight = .09f;
                    shape.smallWeight = .010f;
                    shape.ridgeStrength = .08f;
                    AddFractures(shape, ref rng, 3, 6, .63f, .86f);
                    AddCraters(shape, ref rng, profile, 0, 4, .08f, .22f, .04f, .12f);
                    break;
                case ShapeFamily.ContactBinary:
                    shape.axes = new Vector3(rng.Range(.76f, .96f), rng.Range(.66f, .90f), rng.Range(.88f, 1.12f));
                    shape.lobeStrength = rng.Range(.18f, .34f) * Mathf.Lerp(.72f, 1f, profile.shapeIrregularity);
                    shape.pinchStrength = rng.Range(.25f, .58f);
                    shape.macroWeight = .24f;
                    shape.mediumWeight = .06f;
                    shape.smallWeight = .014f;
                    shape.ridgeStrength = .028f;
                    AddFractures(shape, ref rng, 1, 2, .76f, .90f);
                    AddCraters(shape, ref rng, profile, 0, 4, .10f, .26f, .04f, .13f);
                    break;
                case ShapeFamily.Cratered:
                    shape.axes = new Vector3(rng.Range(.78f, 1.03f), rng.Range(.74f, .98f), rng.Range(.78f, 1.04f));
                    shape.lobeStrength = rng.Value() < .25f ? rng.Range(.04f, .12f) * profile.shapeIrregularity : 0f;
                    shape.macroWeight = .22f;
                    shape.mediumWeight = .05f;
                    shape.smallWeight = .012f;
                    shape.ridgeStrength = .012f;
                    AddFractures(shape, ref rng, 0, 2, .78f, .91f);
                    AddHeroCrater(shape, ref rng, .22f, .46f, .14f, .24f);
                    AddCraters(shape, ref rng, profile, 4, 10, .06f, .20f, .04f, .14f);
                    break;
                default: // Blocky
                    shape.axes = new Vector3(rng.Range(minAxis, .78f), rng.Range(minAxis, .86f), rng.Range(.82f, 1.06f));
                    shape.lobeStrength = 0f;
                    shape.macroWeight = .32f;
                    shape.mediumWeight = .07f;
                    shape.smallWeight = .008f;
                    shape.ridgeStrength = .11f;
                    AddFractures(shape, ref rng, 4, 7, .58f, .84f);
                    AddCraters(shape, ref rng, profile, 0, 3, .08f, .20f, .03f, .10f);
                    break;
            }
            return shape;
        }

        private static void AddFractures(Shape shape, ref AsteroidRandom rng, int minCount, int maxCount, float minDistance, float maxDistance)
        {
            int cuts = rng.Range(minCount, maxCount + 1);
            for (int i = 0; i < cuts; i++)
                shape.fractures.Add(new FracturePlane
                {
                    normal = rng.UnitVector(),
                    distance = rng.Range(minDistance, maxDistance)
                });
        }

        private static void AddHeroCrater(Shape shape, ref AsteroidRandom rng, float minRadius, float maxRadius, float minDepth, float maxDepth)
        {
            shape.craters.Add(new Crater
            {
                center = rng.UnitVector(),
                radius = rng.Range(minRadius, maxRadius),
                depth = rng.Range(minDepth, maxDepth)
            });
        }

        private static void AddCraters(Shape shape, ref AsteroidRandom rng, AsteroidGeometryProfile profile,
            int minExtra, int maxExtra, float minRadius, float maxRadius, float minDepth, float maxDepth)
        {
            int profileMin = Mathf.Clamp(profile.minCraters, 0, 60);
            int profileMax = Mathf.Clamp(profile.maxCraters, profileMin, 60);
            int baseCount = profileMax > profileMin ? rng.Range(profileMin, profileMax + 1) : profileMin;
            int total = Mathf.Clamp(baseCount + rng.Range(minExtra, maxExtra + 1), 0, 32);
            for (int i = 0; i < total; i++)
            {
                shape.craters.Add(new Crater
                {
                    center = rng.UnitVector(),
                    radius = rng.Range(minRadius, maxRadius),
                    depth = rng.Range(minDepth, maxDepth)
                });
            }
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
                    int ab = Midpoint(a, b, vertices, cache);
                    int bc = Midpoint(b, c, vertices, cache);
                    int ca = Midpoint(c, a, vertices, cache);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles.Clear();
                triangles.AddRange(next);
            }
        }
        private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<ulong, int> cache)
        {
            ulong key = a < b ? ((ulong)(uint)a << 32) | (uint)b : ((ulong)(uint)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            Vector3 mid = (vertices[a] + vertices[b]).normalized;
            index = vertices.Count;
            vertices.Add(mid);
            cache.Add(key, index);
            return index;
        }
    }
}
