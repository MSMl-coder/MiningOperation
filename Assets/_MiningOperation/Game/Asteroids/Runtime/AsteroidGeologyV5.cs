using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// New rocks only: sparse large impact bowls with irregular raised rims.
    /// Removes the old repeated, wrinkled micro-relief from the geometric stage.
    /// The effect is a continuous direction-based function, consistent across LODs.
    /// </summary>
    internal static class AsteroidGeologyV5
    {
        private struct Crater
        {
            public Vector3 direction;
            public float cosRadius, depth, rim, phase;
        }

        public static void Apply(Mesh mesh, int seed, AsteroidGeometryProfile profile)
        {
            if (mesh == null || profile == null) return;
            var random = new AsteroidRandom(unchecked((uint)seed) ^ 0x53B71ACDu);
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(3f, 10f,
                Mathf.Clamp01(profile.craterIntensity))), 2, 12);
            var impacts = new Crater[count];
            for (int c = 0; c < count; c++)
            {
                float radius = c < 2 ? random.Range(.28f,.53f) : random.Range(.10f,.34f);
                impacts[c] = new Crater
                {
                    direction = random.UnitVector(),
                    cosRadius = Mathf.Cos(radius),
                    depth = random.Range(.035f,.105f) * Mathf.Clamp01(profile.craterIntensity),
                    rim = random.Range(.12f,.29f),
                    phase = random.Range(0f, Mathf.PI * 2f)
                };
            }
            Vector3[] points = mesh.vertices;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 position = points[i];
                float r = position.magnitude;
                if (r < 0.00001f) continue;
                Vector3 dir = position / r;
                float delta = 0f;
                for (int j = 0; j < impacts.Length; j++)
                {
                    Crater impact = impacts[j];
                    float cosine = Vector3.Dot(dir, impact.direction);
                    // 25% padding outside a crater's physical rim.
                    float q = (1f - cosine) / Mathf.Max(.00001f, 1f - impact.cosRadius);
                    if (q > 1.45f) continue;
                    float t = Mathf.Sqrt(Mathf.Max(q, 0f));
                    float bowl = t < 1f ? -Mathf.Pow(1f - t * t, 2f) : 0f;
                    float edge = Mathf.Exp(-Mathf.Pow((t - .92f) * 11f, 2f));
                    // Small asymmetry, not high-frequency noisy concentric waves.
                    Vector3 tangent = Vector3.Cross(impact.direction, Vector3.up);
                    if (tangent.sqrMagnitude < .00001f) tangent = Vector3.Cross(impact.direction, Vector3.right);
                    tangent.Normalize();
                    Vector3 bitangent = Vector3.Cross(impact.direction, tangent);
                    float angle = Mathf.Atan2(Vector3.Dot(dir, bitangent), Vector3.Dot(dir, tangent));
                    float uneven = 1f + .065f * Mathf.Sin(angle * 5f + impact.phase);
                    delta += impact.depth * (bowl + edge * impact.rim * uneven);
                }
                points[i] = position * Mathf.Clamp(1f + delta, .80f, 1.09f);
            }
            mesh.vertices = points;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
