using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>Deterministic rotation at a given simulation time. No per-asteroid Update().</summary>
    public static class AsteroidSpin
    {
        public static Quaternion Evaluate(Quaternion initial, int seed, float diameterMeters,
            double simulationSeconds, float speedMultiplier = 1f)
        {
            var rng = new AsteroidRandom(unchecked((uint)seed) ^ 0x612A3B49u);
            Vector3 axis = rng.UnitVector();
            if (axis.sqrMagnitude < 0.00001f) axis = Vector3.up;
            float sizeFactor = 1f - Mathf.InverseLerp(30f, 500f, Mathf.Max(1f, diameterMeters));
            // 0.12..0.38 deg/s for large, 0.40..1.05 deg/s for small.
            // Variation remains deterministic for every seed.
            float min = Mathf.Lerp(.12f, .40f, sizeFactor);
            float max = Mathf.Lerp(.38f, 1.05f, sizeFactor);
            double speed = Mathf.Lerp(min, max, rng.Value()) * Mathf.Max(0f, speedMultiplier);
            // Double precision prevents loss of rotation precision after long sessions.
            float degrees = (float)((simulationSeconds * speed) % 360.0);
            return initial * Quaternion.AngleAxis(degrees, axis);
        }
    }
}
