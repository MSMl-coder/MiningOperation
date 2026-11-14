using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// V3 additive shape pass. V1/V2 meshes never pass through this class.
    /// All deformation is deterministic from seed; applied identically at render and physics LOD.
    /// Applies broad angular fractures and scale-separated rocky relief to avoid 'potato' silhouettes.
    /// </summary>
    internal static class AsteroidGeologyV3
    {
        public static void Apply(Mesh mesh, int seed, float diameter, AsteroidGeometryProfile profile)
        {
            if (mesh == null || profile == null) return;
            var r = new AsteroidRandom(unchecked((uint)seed) ^ 0xA24BB167u);
            int family = r.Range(0, 6);
            Vector3 axis = r.UnitVector();
            Vector3 face = r.UnitVector();
            Vector3 cross = Vector3.Cross(face, axis).normalized;
            float cut = r.Range(0.64f, 0.85f);
            float strength = Mathf.Lerp(0.04f, 0.22f, profile.shapeIrregularity);
            Vector3 shift = new Vector3(r.Range(3f,40f), r.Range(10f,50f), r.Range(5f,37f));
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                Vector3 dir = p.normalized;
                float radius = p.magnitude;
                float alignment = Vector3.Dot(dir, axis);
                float relief = Noise(dir * 7.3f + shift) * 0.65f + Noise(dir * 22f + shift * 1.31f) * 0.35f;
                float serration = 1f - Mathf.Abs(2f * Noise(dir * 4.2f + shift * 0.37f) - 1f);
                float multiplier = 1f;
                switch (family)
                {
                    case 0: // angular boulder with strong slab cut
                        multiplier -= strength * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, face) - cut), 0.45f) * 1.7f;
                        multiplier += (relief - 0.48f) * strength * 0.7f;
                        break;
                    case 1: // flake/slab, broad planar notch
                        multiplier -= strength * 0.9f * Mathf.Pow(Mathf.Abs(Vector3.Dot(dir, face)), 4f);
                        multiplier -= strength * 0.6f * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, cross)), 4f);
                        multiplier += (relief - 0.45f) * strength;
                        break;
                    case 2: // long fracture/chiseled shard
                        multiplier += strength * (Mathf.Abs(alignment) - 0.5f) * 0.65f;
                        multiplier -= strength * 0.9f * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, face)), 5f);
                        multiplier += (serration - 0.5f) * strength * 0.8f;
                        break;
                    case 3: // split contact-binary block
                        multiplier -= strength * 1.0f * Mathf.Pow(1f - alignment * alignment, 5f);
                        multiplier += (relief - 0.4f) * strength * 0.7f;
                        break;
                    case 4: // heavily eroded crater remnant
                        multiplier -= strength * 0.75f * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, face)), 10f);
                        multiplier += (serration - 0.52f) * strength * 1.15f;
                        break;
                    default: // broken high-frequency rubble
                        multiplier += (serration - 0.48f) * strength * 1.4f;
                        multiplier -= strength * 0.7f * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, face)), 3f);
                        break;
                }
                multiplier = Mathf.Clamp(multiplier, 0.66f, 1.18f);
                vertices[i] = dir * radius * multiplier;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static float Noise(Vector3 p)
        {
            // Local 3D blending avoids discontinuities from planar-only noise.
            Vector3 n = new Vector3(
                Mathf.PerlinNoise(p.y, p.z),
                Mathf.PerlinNoise(p.z + 31.2f, p.x + 12.4f),
                Mathf.PerlinNoise(p.x + 21.1f, p.y + 6.4f));
            return (n.x + n.y + n.z) * 0.333333f;
        }
    }
}
