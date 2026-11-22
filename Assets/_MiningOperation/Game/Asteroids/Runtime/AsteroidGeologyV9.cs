using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// V9-only seeded, low-frequency fractured silhouette deformation. Large faults
    /// are part of the mesh and therefore visible against the sky, unlike normal-map
    /// detail. No world coordinates or runtime random state are used. Existing V1..V8
    /// asteroids are unchanged. Ice shelves retain their V6/V7 recipes.
    /// </summary>
    internal static class AsteroidGeologyV9
    {
        private struct Fault
        {
            public Vector3 direction;
            public float plane;
            public float width;
            public float amplitude;
        }

        internal static void Apply(Mesh mesh, int seed, AsteroidGeometryProfile profile)
        {
            if (mesh == null || profile == null) return;
            if (mesh.vertexCount == 0) return;

            float strength;
            int faultCount;
            switch (profile.shapePreset)
            {
                case MOAsteroidShapePreset.Rounded:
                    strength = .23f; faultCount = 3; break;
                case MOAsteroidShapePreset.Elongated:
                    strength = .48f; faultCount = 6; break;
                case MOAsteroidShapePreset.ContactBinary:
                    strength = .46f; faultCount = 5; break;
                case MOAsteroidShapePreset.Cratered:
                    strength = .32f; faultCount = 4; break;
                case MOAsteroidShapePreset.Fractured:
                    strength = 1.0f; faultCount = 12; break;
                case MOAsteroidShapePreset.Blocky:
                    strength = .92f; faultCount = 10; break;
                default:
                    strength = .65f; faultCount = 8; break;
            }
            strength *= Mathf.Lerp(.60f, 1.0f, Mathf.Clamp01(profile.shapeIrregularity));
            var rng = new AsteroidRandom(unchecked((uint)seed) ^ 0x7398C2F1u);
            var faults = new Fault[faultCount];
            for (int i = 0; i < faults.Length; ++i)
            {
                faults[i] = new Fault
                {
                    direction = rng.UnitVector(),
                    plane = rng.Range(-.42f, .66f),
                    width = rng.Range(.020f, .084f),
                    amplitude = rng.Range(.012f, .047f) * (rng.Value() > .48f ? 1f : -1f)
                };
            }

            Vector3[] vertices = mesh.vertices;
            float phase = unchecked((uint)seed & 65535u) * .00033f;
            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 p = vertices[i];
                float r = p.magnitude;
                if (r < 1e-6f) continue;
                Vector3 d = p / r;
                // Continuous undulation, small enough that fault lines remain readable.
                float warp = Mathf.Sin(d.x * 6.7f + d.y * 4.6f + phase) * .025f
                    + Mathf.Sin(d.z * 7.2f - d.x * 4.1f - phase) * .017f;
                float displacement = 0f;
                for (int f = 0; f < faults.Length; ++f)
                {
                    Fault fault = faults[f];
                    float along = Vector3.Dot(d, fault.direction) - fault.plane + warp;
                    float t = Mathf.Clamp01((along + fault.width) / (2f * fault.width));
                    float side = t * t * (3f - 2f * t) - .5f;
                    displacement += side * fault.amplitude;
                }
                // Gentle directional cleavage avoids repeating normal/sine stripes.
                float cleavage = Mathf.Sin(d.x * 8.4f + d.z * 2.3f + phase) *
                    Mathf.Sin(d.y * 5.1f - d.z * 3.6f) * .011f;
                displacement = Mathf.Clamp((displacement + cleavage) * strength, -.115f, .115f);
                vertices[i] = p * (1f + displacement);
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
