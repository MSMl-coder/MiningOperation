using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// One mesh containing all volume shells for one field (not one GameObject per cloud).
    /// The shader uses each shell's local center and radius to sample 3D density.
    /// No mining, colliders, dynamic UV atlas or scene-dependent assets.
    /// </summary>
    public static class MOGasVolumeMeshBuilder
    {
        public const string GeneratedMeshName = "MO Generated Gas Volumes";

        public static Mesh Build(MOGasFieldSnapshot snapshot, int longitudeSegments = 20, int latitudeSegments = 12)
        {
            MOGasFieldLayout.Validate(snapshot);
            longitudeSegments = Mathf.Clamp(longitudeSegments, 12, 32);
            latitudeSegments = Mathf.Clamp(latitudeSegments, 8, 20);

            int verticesPerCloud = (latitudeSegments + 1) * (longitudeSegments + 1);
            var vertices = new List<Vector3>(snapshot.clouds.Count * verticesPerCloud);
            var centersAndRadii = new List<Vector4>(snapshot.clouds.Count * verticesPerCloud);
            var colors = new List<Color>(snapshot.clouds.Count * verticesPerCloud);
            var triangles = new List<int>(snapshot.clouds.Count * latitudeSegments * longitudeSegments * 6);

            foreach (MOGasCloudRecord cloud in snapshot.clouds)
            {
                int start = vertices.Count;
                float radius = cloud.radiusMeters;
                var props = new Vector4(cloud.localCenter.x, cloud.localCenter.y,
                    cloud.localCenter.z, radius);
                // A stable seed-derived 0..1 offset, independent of world position.
                float variation = (unchecked((uint)cloud.seed) & 0x00FFFFFFu) / 16777215f;
                var color = new Color(cloud.visualDensity01, variation, 0f, 1f);

                for (int row = 0; row <= latitudeSegments; ++row)
                {
                    float theta = Mathf.PI * row / latitudeSegments;
                    float sinTheta = Mathf.Sin(theta);
                    float cosTheta = Mathf.Cos(theta);
                    for (int column = 0; column <= longitudeSegments; ++column)
                    {
                        float longitude = 2f * Mathf.PI * column / longitudeSegments;
                        var n = new Vector3(sinTheta * Mathf.Cos(longitude), cosTheta,
                            sinTheta * Mathf.Sin(longitude));
                        vertices.Add(cloud.localCenter + n * radius);
                        centersAndRadii.Add(props);
                        colors.Add(color);
                    }
                }

                int stride = longitudeSegments + 1;
                for (int row = 0; row < latitudeSegments; ++row)
                for (int column = 0; column < longitudeSegments; ++column)
                {
                    int a = start + row * stride + column;
                    int b = a + stride;
                    int c = a + 1;
                    int d = b + 1;
                    // Counterclockwise from outside; the shader distinguishes front/back faces.
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(c); triangles.Add(d); triangles.Add(b);
                }
            }

            var mesh = new Mesh { name = GeneratedMeshName };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(1, centersAndRadii);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
