using System;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    public enum AsteroidFormationType
    {
        CrescentBelt = 0,
        SphericalCloud = 1,
        ClusterGroups = 2,
        BrokenBelt = 3,
        Stream = 4,
        BrokenRing = 5
    }

    [CreateAssetMenu(menuName = "Mining Operation/Asteroids/Field Settings", fileName = "AsteroidFieldSettings")]
    public sealed class AsteroidFieldSettings : ScriptableObject
    {
        [Header("Asteroids")]
        public AsteroidGeneratorSettings asteroidSettings;
        public Material asteroidMaterial;
        public AsteroidFormationType formation = AsteroidFormationType.CrescentBelt;
        [Range(1, 2000)] public int asteroidCount = 120;

        [Header("Formation geometry (meters)")]
        [Min(100f)] public float radiusMeters = 8500f;
        [Min(10f)] public float thicknessMeters = 1800f;
        [Range(45f, 300f)] public float arcDegrees = 180f;
        [Range(0.1f, 1f)] public float cloudVerticalScale = 0.65f;
        [Range(2, 12)] public int clusterCount = 4;
        [Range(0.01f, 1f)] public float clusterRadiusFraction = 0.16f;
        [Min(0f)] public float minimumSurfaceGapMeters = 100f;
        [Range(1, 300)] public int placementAttempts = 75;

        [Header("Runtime streaming (meters)")]
        [Min(100f)] public float spawnDistanceMeters = 13000f;
        [Min(100f)] public float despawnDistanceMeters = 16000f;
        [Range(1, 10)] public int generatePerFrame = 2;
        [Range(1, 10)] public int instantiatePerFrame = 1;
        [Range(1, 300)] public int maxActiveAsteroids = 120;
        public bool meshColliders = false;
        public bool hideDepletedAsteroids = true;

        public void ValidateOrThrow()
        {
            if (asteroidSettings == null || asteroidSettings.catalog == null)
                throw new InvalidOperationException("Field Settings requires Asteroid Generator Settings and Resource Catalog.");
            if (asteroidCount < 1 || asteroidCount > 2000 ||
                !Finite(radiusMeters) || radiusMeters < 100f ||
                !Finite(thicknessMeters) || thicknessMeters < 10f ||
                !Finite(arcDegrees) || arcDegrees < 45f || arcDegrees > 300f ||
                !Finite(cloudVerticalScale) || cloudVerticalScale < 0.1f || cloudVerticalScale > 1f ||
                clusterCount < 2 || clusterCount > 12 ||
                !Finite(clusterRadiusFraction) || clusterRadiusFraction <= 0f || clusterRadiusFraction > 1f ||
                !Finite(minimumSurfaceGapMeters) || minimumSurfaceGapMeters < 0f ||
                placementAttempts < 1 || placementAttempts > 300 ||
                !Finite(spawnDistanceMeters) || spawnDistanceMeters <= 0f ||
                !Finite(despawnDistanceMeters) || despawnDistanceMeters <= spawnDistanceMeters ||
                generatePerFrame < 1 || instantiatePerFrame < 1 || maxActiveAsteroids < 1)
                throw new InvalidOperationException("Asteroid Field Settings have invalid values.");
        }

        private static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
        private void OnValidate()
        {
            asteroidCount = Mathf.Clamp(asteroidCount, 1, 2000);
            radiusMeters = Mathf.Max(100f, radiusMeters);
            thicknessMeters = Mathf.Max(10f, thicknessMeters);
            minimumSurfaceGapMeters = Mathf.Max(0f, minimumSurfaceGapMeters);
            spawnDistanceMeters = Mathf.Max(100f, spawnDistanceMeters);
            despawnDistanceMeters = Mathf.Max(spawnDistanceMeters + 100f, despawnDistanceMeters);
            generatePerFrame = Mathf.Clamp(generatePerFrame, 1, 10);
            instantiatePerFrame = Mathf.Clamp(instantiatePerFrame, 1, 10);
            maxActiveAsteroids = Mathf.Clamp(maxActiveAsteroids, 1, 300);
        }
    }
}
