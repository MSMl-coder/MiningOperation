using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// One mesh containing many independent, softly overlapping fog cloudlets.
    /// Each source gas cloud record can generate multiple deterministic visual lobes.
    /// The saved gas snapshot / IDs / composition are never modified.
    /// The authored fog floor is absolute WORLD Y, independent of Spawn Point Y.
    /// </summary>
    public static class MOGroundFogMeshBuilder
    {
        public const string GeneratedMeshName = "MO Generated Ground Fog";

        // A single lobe by default maintains compatibility with existing Build callers/tests.
        // The GroundFog presentation passes the explicit authoring controls below.
        public static Mesh Build(
            MOGasFieldSnapshot snapshot,
            Transform owner,
            float floorY,
            float height,
            float radiusMultiplier,
            int longitudeSegments = 20,
            int latitudeSegments = 12,
            int cloudletsPerCluster = 1,
            float heightVariation = 0.65f,
            float cloudletSpread = 0.8f)
        {
            MOGasFieldLayout.Validate(snapshot);
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            longitudeSegments = Mathf.Clamp(longitudeSegments, 12, 32);
            latitudeSegments = Mathf.Clamp(latitudeSegments, 8, 20);
            cloudletsPerCluster = Mathf.Clamp(cloudletsPerCluster, 1, 24);
            height = Mathf.Max(5f, height);
            radiusMultiplier = Mathf.Clamp(radiusMultiplier, .5f, 3f);
            heightVariation = Mathf.Clamp01(heightVariation);
            cloudletSpread = Mathf.Clamp01(cloudletSpread);

            int perCloudVertices = (latitudeSegments + 1) * (longitudeSegments + 1);
            int totalLobes = snapshot.clouds.Count * cloudletsPerCluster;
            var vertices = new List<Vector3>(totalLobes * perCloudVertices);
            var ellipsoids = new List<Vector4>(totalLobes * perCloudVertices);
            var centersY = new List<Vector4>(totalLobes * perCloudVertices);
            var colors = new List<Color>(totalLobes * perCloudVertices);
            var triangles = new List<int>(totalLobes * latitudeSegments * longitudeSegments * 6);
            Matrix4x4 inv = owner.worldToLocalMatrix;

            foreach (var cloud in snapshot.clouds)
            {
                Vector3 anchor = owner.TransformPoint(new Vector3(cloud.localCenter.x, 0, cloud.localCenter.z));
                float clusterRadius = Mathf.Max(1f, cloud.radiusMeters * radiusMultiplier);
                var random = new AsteroidRandom(unchecked((uint)cloud.seed));

                for (int lobe = 0; lobe < cloudletsPerCluster; lobe++)
                {
                    bool single = cloudletsPerCluster == 1;
                    bool foundation = lobe == 0 && !single;
                    float angle = random.Range(0f, Mathf.PI * 2f);
                    float distance = Mathf.Sqrt(random.Value()) * cloudletSpread *
                        clusterRadius * 0.68f;
                    float dx = foundation ? 0f : Mathf.Cos(angle) * distance;
                    float dz = foundation ? 0f : Mathf.Sin(angle) * distance;

                    // One low and faint central veil ties the cloudlets together.
                    // Other lobes have independent widths and tops: no uniform fog ceiling.
                    float radius = single ? clusterRadius :
                        foundation ? clusterRadius * .58f :
                        clusterRadius * random.Range(.24f, .49f);
                    float topHeight = single ? height :
                        foundation ? height * .38f :
                        height * Mathf.Lerp(.68f, random.Range(.68f, 1.22f), heightVariation);

                    float halfHeight = topHeight * .5f;
                    Vector3 center = new Vector3(anchor.x + dx, floorY + halfHeight, anchor.z + dz);

                    float relativeDensity = single ? 1f :
                        foundation ? .22f :
                        random.Range(.48f, .85f) /
                        Mathf.Sqrt(Mathf.Max(1f, cloudletsPerCluster / 7f));

                    float variation = random.Value(); // independent noise domain per lobe
                    Color metadata = new Color(
                        Mathf.Clamp01(cloud.visualDensity01 * relativeDensity),
                        variation, 0f, 1f);

                    int start = vertices.Count;
                    Vector4 ellipsoid = new Vector4(center.x, center.z, radius, halfHeight);
                    Vector4 worldY = new Vector4(center.y, 0f, 0f, 0f);

                    for (int row = 0; row <= latitudeSegments; row++)
                    {
                        float theta = Mathf.PI * row / latitudeSegments;
                        float sin = Mathf.Sin(theta);
                        float cos = Mathf.Cos(theta);
                        for (int column = 0; column <= longitudeSegments; column++)
                        {
                            float phi = 2f * Mathf.PI * column / longitudeSegments;
                            Vector3 normal = new Vector3(sin * Mathf.Cos(phi), cos, sin * Mathf.Sin(phi));
                            Vector3 pointWS = center + new Vector3(
                                normal.x * radius, normal.y * halfHeight, normal.z * radius);
                            vertices.Add(inv.MultiplyPoint3x4(pointWS));
                            ellipsoids.Add(ellipsoid);
                            centersY.Add(worldY);
                            colors.Add(metadata);
                        }
                    }

                    int stride = longitudeSegments + 1;
                    for (int row = 0; row < latitudeSegments; row++)
                    for (int col = 0; col < longitudeSegments; col++)
                    {
                        int a = start + row * stride + col;
                        int b = a + stride;
                        triangles.Add(a);
                        triangles.Add(a + 1);
                        triangles.Add(b);
                        triangles.Add(a + 1);
                        triangles.Add(b + 1);
                        triangles.Add(b);
                    }
                }
            }

            var mesh = new Mesh { name = GeneratedMeshName };
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
