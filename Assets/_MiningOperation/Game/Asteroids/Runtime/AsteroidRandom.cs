using UnityEngine;

namespace MiningOperation.Asteroids
{
    // Explicit PRNG, independent of UnityEngine.Random global state.
    public struct AsteroidRandom
    {
        private uint state;
        public AsteroidRandom(uint seed) { state = seed == 0 ? 0x6D2B79F5u : seed; }
        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            state = x;
            return x;
        }
        public float Value() { return (NextUInt() >> 8) * (1f / 16777216f); }
        public float Range(float min, float max) { return min + (max - min) * Value(); }
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }
        public Vector3 UnitVector()
        {
            float z = Range(-1f, 1f);
            float a = Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z);
        }
    }
}
