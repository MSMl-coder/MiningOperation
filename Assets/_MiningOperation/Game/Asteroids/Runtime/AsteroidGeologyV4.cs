using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Additional large readable craters and chipped rock for *new* generation version 4.
    /// Uses only direction, seed and immutable shape profile, so vertices shared by
    /// different icosphere subdivisions have identical displacement.
    /// Geometry for versions 1, 2 and 3 is not modified.
    /// </summary>
    internal static class AsteroidGeologyV4
    {
        private struct Crater
        {
            public Vector3 center;
            public float cosRadius;
            public float depth;
            public float rim;
        }

        public static void Apply(Mesh mesh, int seed, float diameter, AsteroidGeometryProfile profile)
        {
            if (mesh == null || profile == null) return;
            var rand = new AsteroidRandom(unchecked((uint)seed) ^ 0x4D7E3C81u);
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(3, 13,
                Mathf.Clamp01(profile.craterIntensity))) + rand.Range(-1, 3), 2, 18);
            var craters = new Crater[count];
            for (int i = 0; i < count; ++i)
            {
                float angle = i < 3 ? rand.Range(0.28f, 0.58f) : rand.Range(0.09f, 0.31f);
                craters[i] = new Crater
                {
                    center = rand.UnitVector(),
                    cosRadius = Mathf.Cos(angle),
                    depth = rand.Range(0.05f, 0.17f) * Mathf.Clamp01(profile.craterIntensity),
                    rim = rand.Range(0.18f, 0.36f)
                };
            }
            Vector3[] vertices = mesh.vertices;
            var fractureAxis = rand.UnitVector();
            float fracturePosition = rand.Range(-0.35f, 0.35f);
            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 point = vertices[i];
                float length = point.magnitude;
                if (length <= 0.001f) continue;
                Vector3 dir = point / length;
                float relativeRadius = 1f;
                for (int j = 0; j < count; ++j)
                {
                    Crater c = craters[j];
                    float dot = Vector3.Dot(dir, c.center);
                    if (dot < c.cosRadius - 0.075f) continue;
                    float t = Mathf.Clamp01((1f - dot) / Mathf.Max(0.0001f, (1f - c.cosRadius)));
                    // Bowl floor, raised uneven rim, distinct at all LODs.
                    float bowl = -(1f - t) * (1f - t) * (1f + 2f * t);
                    float edge = Mathf.Exp(-Mathf.Pow((t - 0.86f) * 9f, 2f));
                    relativeRadius += c.depth * (bowl + c.rim * edge);
                }
                // Subtle broad tectonic groove, no high frequency silhouette jitter.
                float cut = Mathf.Abs(Vector3.Dot(dir, fractureAxis) - fracturePosition);
                relativeRadius -= 0.018f * Mathf.Clamp01(profile.shapeIrregularity) *
                    Mathf.Exp(-cut * cut * 330f);
                vertices[i] = point * Mathf.Clamp(relativeRadius, 0.70f, 1.13f);
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
