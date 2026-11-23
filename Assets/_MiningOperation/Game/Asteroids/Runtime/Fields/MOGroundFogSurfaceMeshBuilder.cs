using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// A SINGLE top-facing cloud surface per gas location. Builds a heightfield and
    /// alpha coverage from the existing immutable gas cloud records. No raymarching,
    /// per-cloud transparent shells, or extra GameObjects at runtime.
    /// This is a fast top-down approximation, not a true volume.
    /// </summary>
    public static class MOGroundFogSurfaceMeshBuilder
    {
        public const string GeneratedMeshName = "MO Generated Ground Fog Fast Surface";

        public static Mesh Build(
            MOGasFieldSnapshot snapshot, Transform owner, float floorY, float height,
            float radiusMultiplier, int resolution = 96, float breakup = 0.85f,
            float cutouts = 0.55f, float heightVariation = 0.8f)
        {
            MOGasFieldLayout.Validate(snapshot);
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            resolution = Mathf.Clamp(resolution, 24, 160);
            height = Mathf.Max(5f, height);
            radiusMultiplier = Mathf.Clamp(radiusMultiplier, .5f, 3f);
            breakup = Mathf.Clamp01(breakup);
            cutouts = Mathf.Clamp01(cutouts);
            heightVariation = Mathf.Clamp01(heightVariation);

            int count = snapshot.clouds.Count;
            var centers = new Vector2[count];
            var radii = new float[count];
            var density = new float[count];
            float minX = float.PositiveInfinity, minZ = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxZ = float.NegativeInfinity;
            float typicalRadius = 0f;

            for (int i = 0; i < count; i++)
            {
                MOGasCloudRecord cloud = snapshot.clouds[i];
                // World X/Z placement follows existing deterministic gas records,
                // regardless of Spawn Point world Y.
                Vector3 world = owner.TransformPoint(cloud.localCenter);
                float radius = Mathf.Max(10f, cloud.radiusMeters * radiusMultiplier);
                centers[i] = new Vector2(world.x, world.z);
                radii[i] = radius;
                density[i] = Mathf.Clamp01(cloud.visualDensity01);
                typicalRadius += radius;
                minX = Mathf.Min(minX, world.x - radius * 1.1f);
                maxX = Mathf.Max(maxX, world.x + radius * 1.1f);
                minZ = Mathf.Min(minZ, world.z - radius * 1.1f);
                maxZ = Mathf.Max(maxZ, world.z + radius * 1.1f);
            }

            typicalRadius = Mathf.Max(100f, typicalRadius / count);
            float minSpan = Mathf.Max(100f, typicalRadius * .3f);
            if (maxX - minX < minSpan) maxX = minX + minSpan;
            if (maxZ - minZ < minSpan) maxZ = minZ + minSpan;
            int stride = resolution + 1;
            int vertexCount = stride * stride;
            var vertices = new List<Vector3>(vertexCount);
            var uv = new List<Vector2>(vertexCount);
            var colors = new List<Color>(vertexCount);
            var triangles = new List<int>(resolution * resolution * 6);
            Matrix4x4 toLocal = owner.worldToLocalMatrix;

            // Surface shape is calculated only when a spawn point is authored/rebuilt.
            // The shader later reads just one low-cost noise texture sample per pixel.
            float macroScale = Mathf.Max(30f, typicalRadius * .24f);
            float fineScale = Mathf.Max(15f, typicalRadius * .095f);
            float seedOffset = (unchecked((uint)snapshot.fieldSeed) & 65535u) * .00019f;
            for (int row = 0; row <= resolution; row++)
            for (int col = 0; col <= resolution; col++)
            {
                float x = Mathf.Lerp(minX, maxX, col / (float)resolution);
                float z = Mathf.Lerp(minZ, maxZ, row / (float)resolution);

                float cover = 0f;
                for (int c = 0; c < count; c++)
                {
                    float dx = (x - centers[c].x) / radii[c];
                    float dz = (z - centers[c].y) / radii[c];
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    float influence = 1f - Mathf.SmoothStep(.28f, 1f, dist);
                    cover = Mathf.Max(cover, influence * density[c]);
                }

                float macro = Mathf.PerlinNoise(x / macroScale + 13.7f + seedOffset,
                                                 z / macroScale + 24.1f);
                float middle = Mathf.PerlinNoise(x / fineScale - 31.5f,
                                                  z / fineScale + 5.3f + seedOffset);
                float structure = macro * .67f + middle * .33f;
                float shape = Mathf.Lerp(.68f, .25f + structure * 1.3f, breakup);
                float opacity = Mathf.Clamp01(cover * Mathf.Clamp01(shape));
                float hole = Mathf.SmoothStep(
                    Mathf.Lerp(.12f, .51f, cutouts),
                    Mathf.Lerp(.28f, .72f, cutouts),
                    structure);
                opacity *= Mathf.Lerp(1f, hole, cutouts);

                // Individual hills and valleys create irregular cloud tops.
                float dome = Mathf.Lerp(.42f, Mathf.Lerp(.12f, 1.2f, structure),
                    heightVariation);
                float finalHeight = floorY + height * Mathf.Clamp01(cover) * dome;
                vertices.Add(toLocal.MultiplyPoint3x4(new Vector3(x, finalHeight, z)));
                // World-scaled UVs, independent of how large the generated field is.
                uv.Add(new Vector2(x / typicalRadius, z / typicalRadius));
                colors.Add(new Color(structure, cover, 0f, opacity));
            }

            for (int row = 0; row < resolution; row++)
            for (int col = 0; col < resolution; col++)
            {
                int a = row * stride + col;
                int b = a + stride;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
            }

            var mesh = new Mesh { name = GeneratedMeshName };
            if (vertexCount > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
