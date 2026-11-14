using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Deterministic UV fallback for URP/Lit, avoiding dependency on experimental shaders.
    /// Each asteroid receives an offset from its seed. Spherical seams remain a limitation;
    /// a validated triplanar shader can replace this later without touching geometry/saves.
    /// </summary>
    internal static class AsteroidSurfaceUV
    {
        public static void Apply(Mesh mesh, int seed)
        {
            if (mesh == null) return;
            Vector3[] v = mesh.vertices;
            var uv = new Vector2[v.Length];
            float offsetU = (unchecked((uint)seed) & 65535u) / 65535f;
            float offsetV = ((unchecked((uint)seed) >> 16) & 65535u) / 65535f;
            for (int i = 0; i < v.Length; ++i)
            {
                Vector3 n = v[i].normalized;
                uv[i] = new Vector2((Mathf.Atan2(n.z, n.x) / (2f * Mathf.PI) + 0.5f) * 3.5f + offsetU,
                    (Mathf.Asin(Mathf.Clamp(n.y, -1f, 1f)) / Mathf.PI + 0.5f) * 3.5f + offsetV);
            }
            mesh.uv = uv;
            mesh.RecalculateTangents();
        }
    }
}
