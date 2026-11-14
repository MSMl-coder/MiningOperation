using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Single-wrap spherical UVs for the validated URP/Lit path. Unlike the old
    /// 3.5x UV multiplier, the rock atlas appears ONCE around each asteroid.
    /// Triangle-local seam/pole vertices prevent long interpolation streaks.
    /// No change to vertex positions, resource data, mesh silhouette or volume.
    /// </summary>
    internal static class AsteroidSurfaceUV
    {
        public static void Apply(Mesh mesh, int seed)
        {
            if (mesh == null || mesh.vertexCount == 0) return;
            Vector3[] original = mesh.vertices;
            Vector3[] originalNormals = mesh.normals;
            Color32[] originalColors = mesh.colors32;
            int[] triangles = mesh.triangles;
            int baseCount = original.Length;
            bool colored = originalColors != null && originalColors.Length == baseCount;
            var vertices = new List<Vector3>(original);
            var normals = new List<Vector3>(originalNormals);
            var colors = colored ? new List<Color32>(originalColors) : null;
            var uv = new List<Vector2>(baseCount + baseCount / 12);
            // Rotate only the UV frame. Seed changes the mapped portion, but never
            // changes the original shape, normals or mass calculations.
            float yaw = (unchecked((uint)seed) * (1f / 4294967296f)) * 360f;
            Quaternion uvRotation = Quaternion.AngleAxis(yaw, Vector3.up);
            for (int i = 0; i < baseCount; ++i)
            {
                Vector3 d = uvRotation * original[i].normalized;
                float u = Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI) + 0.5f;
                float v = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) / Mathf.PI + 0.5f;
                uv.Add(new Vector2(u, Mathf.Clamp(v, 0.001f, 0.999f)));
            }
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t+1], c = triangles[t+2];
                float ua = uv[a].x, ub = uv[b].x, uc = uv[c].x;
                bool seam = Mathf.Max(ua, Mathf.Max(ub, uc)) - Mathf.Min(ua, Mathf.Min(ub, uc)) > 0.5f;
                // Copy only the affected triangle corners. This preserves the
                // original indices/positions for other LOD comparisons.
                if (seam)
                {
                    if (ua < .5f) a = Duplicate(a, 1f, vertices, normals, colors, uv);
                    if (ub < .5f) b = Duplicate(b, 1f, vertices, normals, colors, uv);
                    if (uc < .5f) c = Duplicate(c, 1f, vertices, normals, colors, uv);
                }
                // A single UV shared by triangles around a pole causes fan-like
                // stretching. Give pole triangles their own longitude.
                a = FixPole(a,b,c,vertices,normals,colors,uv,uvRotation);
                b = FixPole(b,c,a,vertices,normals,colors,uv,uvRotation);
                c = FixPole(c,a,b,vertices,normals,colors,uv,uvRotation);
                triangles[t] = a; triangles[t+1] = b; triangles[t+2] = c;
            }
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            if (colors != null) mesh.SetColors(colors);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateTangents();
        }

        private static int FixPole(int id, int otherA, int otherB,
            List<Vector3> vertices, List<Vector3> normals, List<Color32> colors,
            List<Vector2> uv, Quaternion rotation)
        {
            Vector3 p = (rotation * vertices[id].normalized);
            if (Mathf.Abs(p.y) < 0.998f) return id;
            float desired = (uv[otherA].x + uv[otherB].x) * .5f;
            int next = Duplicate(id, 0f, vertices, normals, colors, uv);
            Vector2 originalUV = uv[next];
            originalUV.x = desired;
            uv[next] = originalUV;
            return next;
        }

        private static int Duplicate(int index, float uShift,
            List<Vector3> vertices, List<Vector3> normals, List<Color32> colors, List<Vector2> uv)
        {
            int next = vertices.Count;
            vertices.Add(vertices[index]);
            normals.Add(normals[index]);
            if (colors != null) colors.Add(colors[index]);
            Vector2 value = uv[index]; value.x += uShift;
            uv.Add(value);
            return next;
        }
    }
}
