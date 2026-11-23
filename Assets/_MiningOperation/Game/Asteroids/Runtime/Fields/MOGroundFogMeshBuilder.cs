using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids
{
    /// <summary>Independent visual mesh for low, flattened fog clusters.
    /// Original gas records, field IDs and save schema remain unchanged.
    /// Geometry is anchored to absolute world Y even when the spawn transform is far below zero.</summary>
    public static class MOGroundFogMeshBuilder
    {
        public const string GeneratedMeshName = "MO Generated Ground Fog";
        public static Mesh Build(MOGasFieldSnapshot snapshot, Transform owner, float floorY, float height,
            float radiusMultiplier, int longitudeSegments = 24, int latitudeSegments = 12)
        {
            MOGasFieldLayout.Validate(snapshot);
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            longitudeSegments = Mathf.Clamp(longitudeSegments, 12, 32);
            latitudeSegments = Mathf.Clamp(latitudeSegments, 8, 20);
            height = Mathf.Max(5f, height);
            radiusMultiplier = Mathf.Clamp(radiusMultiplier, .5f, 3f);
            var vertices = new List<Vector3>();
            var ellipsoids = new List<Vector4>();
            var centersY = new List<Vector4>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            // Convert desired world-space flattened ellipsoids into owner-local shell geometry.
            var inv = owner.worldToLocalMatrix;
            foreach (var cloud in snapshot.clouds)
            {
                Vector3 horizontal = owner.TransformPoint(new Vector3(cloud.localCenter.x, 0, cloud.localCenter.z));
                float radius = Mathf.Max(1f, cloud.radiusMeters * radiusMultiplier);
                float halfHeight = height * .5f;
                Vector3 centerWorld = new Vector3(horizontal.x, floorY + halfHeight, horizontal.z);
                Vector3 centerLocal = inv.MultiplyPoint3x4(centerWorld);
                int start = vertices.Count;
                var seedVariation = (unchecked((uint)cloud.seed) & 0x00FFFFFFu) / 16777215f;
                for (int row = 0; row <= latitudeSegments; row++)
                {
                    float theta = Mathf.PI * row / latitudeSegments;
                    for (int column = 0; column <= longitudeSegments; column++)
                    {
                        float longitude = 2f * Mathf.PI * column / longitudeSegments;
                        Vector3 n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(longitude),
                            Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(longitude));
                        Vector3 pWorld = centerWorld + new Vector3(n.x * radius, n.y * halfHeight, n.z * radius);
                        vertices.Add(inv.MultiplyPoint3x4(pWorld));
                        ellipsoids.Add(new Vector4(centerWorld.x, centerWorld.z, radius, halfHeight));
                        centersY.Add(new Vector4(centerWorld.y, 0f, 0f, 0f));
                        colors.Add(new Color(cloud.visualDensity01, seedVariation, 0, 1));
                    }
                }
                int stride = longitudeSegments + 1;
                for (int row = 0; row < latitudeSegments; row++)
                for (int column = 0; column < longitudeSegments; column++)
                {
                    int a = start + row * stride + column;
                    int b = a + stride;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                }
            }
            Mesh mesh = new Mesh { name = GeneratedMeshName };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(1, ellipsoids);
            mesh.SetUVs(2, centersY);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
