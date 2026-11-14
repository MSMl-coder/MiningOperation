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
            // Larger bodies generally tumble more slowly, with stable individuality.
            float slow = Mathf.Lerp(0.46f, 1.5f,
                1f - Mathf.InverseLerp(12f, 480f, Mathf.Max(1f, diameterMeters)));
            double speed = Mathf.Lerp(0.035f, 0.22f, rng.Value()) * slow *
                Mathf.Max(0f, speedMultiplier);
            // Double precision prevents loss of rotation precision after long sessions.
            float degrees = (float)((simulationSeconds * speed) % 360.0);
            return initial * Quaternion.AngleAxis(degrees, axis);
        }
    }
}
