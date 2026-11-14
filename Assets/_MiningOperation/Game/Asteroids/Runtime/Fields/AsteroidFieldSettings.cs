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
        [Tooltip("Optional lightweight material for GPU-instanced distant rocks.")]
        public Material distantMaterial;
        [Header("V2.1.1 - safe materials and streaming")]
        [Tooltip("OFF = use stable URP/Lit for near, far and debris. Enable only after validating custom shaders in Game view.")]
        public bool allowExperimentalRockShaders = false;
        [Tooltip("Create one render mesh per nearby asteroid, instead of five LOD meshes in the same frame.")]
        public bool fastStreamedAsteroids = true;
        [Range(0, 4)] public int streamedMeshSubdivisions = 3;
        [Range(0, 32)] public int cachedStreamedAsteroids = 12;
        [Header("V2.2 - continuous shapes / deterministic spin")]
        [Tooltip("Far asteroids use their own persisted seed and shape, not another rock's preset.")]
        public bool exactFarShape = true;
        [Range(1, 3)] public int exactFarSubdivisions = 2;
        [Range(1, 16)] public int exactFarBuildsPerFrame = 2;
        [Range(32, 1000)] public int maxFarRenderDraws = 300;
        [Range(0.5f, 10f)] public float farBuildBudgetMs = 2f;
        public bool enableAsteroidRotation = true;
        [Range(0f, 4f)] public float rotationSpeedMultiplier = 1f; // existing master multiplier
        [Header("V2.2.1 - non-repeating rocky surface")]
        [Tooltip("Imported 1K albedo; applied only ONCE around each rock. Use Field Studio > Install Rock Surface.")]
        public Texture2D rockAlbedoTexture;
        [Tooltip("Imported GL normal map; subtle microdetail, not macro-craters.")]
        public Texture2D rockNormalTexture;
        [Range(0f, .5f)] public float rockNormalStrength = .18f;
        [Header("V2.2.1 - motion")]
        [Range(0f, 4f)] public float mineableRotationMultiplier = 1f;
        [Range(0f, 4f)] public float decorativeRotationMultiplier = 1f;
        [Range(2, 5)] public int maximumStreamedSubdivisions = 4;
        public AsteroidFormationType formation = AsteroidFormationType.CrescentBelt;
        [Range(1, 2000)] public int asteroidCount = 120;
        [Range(0f, 1f)] public float smallAsteroidBias = 0.68f;

        [Header("Formation geometry (meters)")]
        [Min(100f)] public float radiusMeters = 8500f;
        [Min(10f)] public float thicknessMeters = 1800f;
        [Range(45f, 300f)] public float arcDegrees = 180f;
        [Range(0.1f, 1f)] public float cloudVerticalScale = 0.65f;
        [Range(2, 12)] public int clusterCount = 4;
        [Range(0.01f, 1f)] public float clusterRadiusFraction = 0.16f;
        [Min(0f)] public float minimumSurfaceGapMeters = 100f;
        [Range(1, 300)] public int placementAttempts = 75;

        [Header("V2 - composed belts (new builds only; saved fields remain unchanged)")]
        [Tooltip("Arrange geological bodies in pockets along a centerline instead of uniformly on an enormous arc.")]
        public bool useComposedLayout = true;
        [Range(0f, 1f)] public float clusterStrength = 0.78f;
        [Range(2, 32)] public int densityPockets = 10;
        [Range(0.01f, 0.4f)] public float pocketWidthFraction = 0.08f;

        [Header("V2 - decorative debris (never mineable; no GameObjects)")]
        public bool enableDecorativeDebris = true;
        [Range(0, 16000)] public int decorativeRockCount = 1600;
        [Range(0, 30000)] public int microFragmentCount = 0;
        [Min(1f)] public float debrisSpreadMeters = 280f;
        [Min(0.1f)] public float fragmentMinDiameter = 0.6f;
        [Min(0.1f)] public float fragmentMaxDiameter = 13f;
        [Min(100f)] public float debrisDrawDistance = 9000f;
        [Range(1, 24)] public int debrisMeshVariants = 8;

        [Header("Runtime streaming (meters)")]
        [Min(100f)] public float spawnDistanceMeters = 13000f;
        [Min(100f)] public float despawnDistanceMeters = 16000f;
        [Range(1, 10)] public int generatePerFrame = 2;
        [Range(1, 10)] public int instantiatePerFrame = 1;
        [Range(1, 300)] public int maxActiveAsteroids = 120;
        public bool meshColliders = false;
        public bool hideDepletedAsteroids = true;

        [Header("Optimized rendering v1.3 (additive, preserves old assets)")]
        [Tooltip("Distant asteroids render as GPU instances, without GameObjects. Near rocks remain unique.")]
        public bool useGpuFarField = true;
        [Min(50f)] public float detailedDistanceMeters = 1800f;
        [Range(1, 128)] public int maxDetailedAsteroids = 28;
        [Min(100f)] public float distantDrawDistanceMeters = 24000f;
        [Range(1, 24)] public int distantMeshVariants = 12;
        [Range(0, 3)] public int distantSubdivisions = 1;
        [Range(0.1f, 2f)] public float streamingTickSeconds = 0.25f;

        public void ValidateOrThrow()
        {
            if (asteroidSettings == null || asteroidSettings.catalog == null)
                throw new InvalidOperationException("Field Settings requires Asteroid Generator Settings and Resource Catalog.");
            if (asteroidCount < 1 || asteroidCount > 2000 ||
                !Finite(smallAsteroidBias) || smallAsteroidBias < 0f || smallAsteroidBias > 1f ||
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
                generatePerFrame < 1 || instantiatePerFrame < 1 || maxActiveAsteroids < 1 ||
                streamedMeshSubdivisions < 0 || streamedMeshSubdivisions > 4 ||
                exactFarSubdivisions < 1 || exactFarSubdivisions > 3 ||
                exactFarBuildsPerFrame < 1 || exactFarBuildsPerFrame > 16 ||
                maxFarRenderDraws < 32 || maxFarRenderDraws > 1000 ||
                !Finite(farBuildBudgetMs) || farBuildBudgetMs < 0.5f ||
                !Finite(rotationSpeedMultiplier) || rotationSpeedMultiplier < 0f ||
                !Finite(rockNormalStrength) || rockNormalStrength < 0f || rockNormalStrength > .5f ||
                !Finite(mineableRotationMultiplier) || mineableRotationMultiplier < 0f || mineableRotationMultiplier > 4f ||
                !Finite(decorativeRotationMultiplier) || decorativeRotationMultiplier < 0f || decorativeRotationMultiplier > 4f ||
                maximumStreamedSubdivisions < 2 || maximumStreamedSubdivisions > 5 ||
                cachedStreamedAsteroids < 0 || cachedStreamedAsteroids > 32 ||
                !Finite(detailedDistanceMeters) || detailedDistanceMeters < 50f ||
                !Finite(distantDrawDistanceMeters) || distantDrawDistanceMeters < detailedDistanceMeters ||
                maxDetailedAsteroids < 1 || distantMeshVariants < 1 ||
                distantSubdivisions < 0 || distantSubdivisions > 3 ||
                !Finite(streamingTickSeconds) || streamingTickSeconds < 0.1f ||
                !Finite(clusterStrength) || clusterStrength < 0f || clusterStrength > 1f ||
                densityPockets < 2 || densityPockets > 32 ||
                !Finite(pocketWidthFraction) || pocketWidthFraction < 0.01f || pocketWidthFraction > 0.4f ||
                decorativeRockCount < 0 || decorativeRockCount > 16000 ||
                microFragmentCount < 0 || microFragmentCount > 30000 ||
                !Finite(debrisSpreadMeters) || debrisSpreadMeters < 1f ||
                !Finite(fragmentMinDiameter) || fragmentMinDiameter < 0.1f ||
                !Finite(fragmentMaxDiameter) || fragmentMaxDiameter < fragmentMinDiameter ||
                !Finite(debrisDrawDistance) || debrisDrawDistance < 100f ||
                debrisMeshVariants < 1 || debrisMeshVariants > 24)
                throw new InvalidOperationException("Asteroid Field Settings have invalid values.");
        }

        private static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
        private void OnValidate()
        {
            asteroidCount = Mathf.Clamp(asteroidCount, 1, 2000);
            smallAsteroidBias = Mathf.Clamp01(smallAsteroidBias);
            radiusMeters = Mathf.Max(100f, radiusMeters);
            thicknessMeters = Mathf.Max(10f, thicknessMeters);
            minimumSurfaceGapMeters = Mathf.Max(0f, minimumSurfaceGapMeters);
            spawnDistanceMeters = Mathf.Max(100f, spawnDistanceMeters);
            despawnDistanceMeters = Mathf.Max(spawnDistanceMeters + 100f, despawnDistanceMeters);
            generatePerFrame = Mathf.Clamp(generatePerFrame, 1, 10);
            streamedMeshSubdivisions = Mathf.Clamp(streamedMeshSubdivisions, 0, 4);
            exactFarSubdivisions = Mathf.Clamp(exactFarSubdivisions, 1, 3);
            exactFarBuildsPerFrame = Mathf.Clamp(exactFarBuildsPerFrame, 1, 16);
            maxFarRenderDraws = Mathf.Clamp(maxFarRenderDraws, 32, 1000);
            farBuildBudgetMs = Mathf.Clamp(farBuildBudgetMs, 0.5f, 10f);
            rotationSpeedMultiplier = Mathf.Clamp(rotationSpeedMultiplier, 0f, 4f);
            rockNormalStrength = Mathf.Clamp(rockNormalStrength, 0f, .5f);
            mineableRotationMultiplier = Mathf.Clamp(mineableRotationMultiplier, 0f, 4f);
            decorativeRotationMultiplier = Mathf.Clamp(decorativeRotationMultiplier, 0f, 4f);
            maximumStreamedSubdivisions = Mathf.Clamp(maximumStreamedSubdivisions, 2, 5);
            cachedStreamedAsteroids = Mathf.Clamp(cachedStreamedAsteroids, 0, 32);
            instantiatePerFrame = Mathf.Clamp(instantiatePerFrame, 1, 10);
            maxActiveAsteroids = Mathf.Clamp(maxActiveAsteroids, 1, 300);
            detailedDistanceMeters = Mathf.Max(50f, detailedDistanceMeters);
            maxDetailedAsteroids = Mathf.Clamp(maxDetailedAsteroids, 1, 128);
            distantDrawDistanceMeters = Mathf.Max(detailedDistanceMeters + 100f, distantDrawDistanceMeters);
            distantMeshVariants = Mathf.Clamp(distantMeshVariants, 1, 24);
            distantSubdivisions = Mathf.Clamp(distantSubdivisions, 0, 3);
            streamingTickSeconds = Mathf.Clamp(streamingTickSeconds, 0.1f, 2f);
            clusterStrength = Mathf.Clamp01(clusterStrength);
            densityPockets = Mathf.Clamp(densityPockets, 2, 32);
            pocketWidthFraction = Mathf.Clamp(pocketWidthFraction, 0.01f, 0.4f);
            decorativeRockCount = Mathf.Clamp(decorativeRockCount, 0, 16000);
            microFragmentCount = Mathf.Clamp(microFragmentCount, 0, 30000);
            debrisSpreadMeters = Mathf.Max(1f, debrisSpreadMeters);
            fragmentMinDiameter = Mathf.Max(0.1f, fragmentMinDiameter);
            fragmentMaxDiameter = Mathf.Max(fragmentMinDiameter, fragmentMaxDiameter);
            debrisDrawDistance = Mathf.Max(100f, debrisDrawDistance);
            debrisMeshVariants = Mathf.Clamp(debrisMeshVariants, 1, 24);
        }
    }
}
