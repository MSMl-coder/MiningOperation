using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MiningOperation.Asteroids
{
    // Restricts vertical Y placement. Enum numbers are intentionally unchanged for scene compatibility.
    public enum MOFieldDepthMode
    {
        Free3D = 0,
        PlanarSlab = 1,
        Strict2DPlane = 2
    }

    [Serializable]
    public sealed class MOSpawnDepthConstraints
    {
        // Limits LOCAL Y (height) while retaining XZ distribution for planar gameplay.
        public MOFieldDepthMode depthMode = MOFieldDepthMode.Free3D;
        [FormerlySerializedAs("zCenter")] public float yCenter;
        [FormerlySerializedAs("zHalfRange"), Min(0f)] public float yHalfRange = 250f;
        [FormerlySerializedAs("zJitter"), Min(0f)] public float yJitter = 18f;
        public bool flattenRotationToPlane;

        public void ClampValues()
        {
            yHalfRange = Mathf.Max(0f, yHalfRange);
            yJitter = Mathf.Max(0f, yJitter);
            if (!Finite(yCenter)) yCenter = 0f;
        }

        public bool IsConstrained => depthMode != MOFieldDepthMode.Free3D;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Per-spawn geometry recipe. Only shape parameters are copied;
    /// economics, material rarity and resource catalogs remain shared.</summary>
    [Serializable]
    public sealed class MOSpawnGeology
    {
        public MOAsteroidShapePreset shapePreset = MOAsteroidShapePreset.Mixed;
        [Range(0f, 1f)] public float shapeIrregularity = .55f;
        [Range(0f, 1f)] public float craterIntensity = .55f;
        [Range(0f, 1f)] public float surfaceRoughness = .45f;
        [Range(0, 20)] public int minCraters = 2;
        [Range(0, 60)] public int maxCraters = 18;
        [Range(.1f, 1f)] public float minimumAxisScale = .5f;

        public MOSpawnGeology Clone()
        {
            return new MOSpawnGeology
            {
                shapeIrregularity = shapeIrregularity,
                shapePreset = shapePreset,
                craterIntensity = craterIntensity,
                surfaceRoughness = surfaceRoughness,
                minCraters = minCraters,
                maxCraters = maxCraters,
                minimumAxisScale = minimumAxisScale
            };
        }

        public void CopyFrom(AsteroidGeneratorSettings source)
        {
            if (source == null) return;
            shapeIrregularity = source.shapeIrregularity;
            shapePreset = source.shapePreset;
            craterIntensity = source.craterIntensity;
            surfaceRoughness = source.surfaceRoughness;
            minCraters = source.minCraters;
            maxCraters = source.maxCraters;
            minimumAxisScale = source.minimumAxisScale;
        }
        public void ApplyTo(AsteroidGeneratorSettings target)
        {
            if (target == null) return;
            target.shapeIrregularity = Mathf.Clamp01(shapeIrregularity);
            target.shapePreset = shapePreset;
            target.craterIntensity = Mathf.Clamp01(craterIntensity);
            target.surfaceRoughness = Mathf.Clamp01(surfaceRoughness);
            target.minCraters = Mathf.Clamp(minCraters, 0, 20);
            target.maxCraters = Mathf.Clamp(maxCraters, target.minCraters, 60);
            target.minimumAxisScale = Mathf.Clamp(minimumAxisScale, .1f, 1f);
        }
        public void ApplyTo(AsteroidGeometryProfile target)
        {
            if (target == null) return;
            target.shapeIrregularity = Mathf.Clamp01(shapeIrregularity);
            target.shapePreset = shapePreset;
            target.craterIntensity = Mathf.Clamp01(craterIntensity);
            target.surfaceRoughness = Mathf.Clamp01(surfaceRoughness);
            target.minCraters = Mathf.Clamp(minCraters, 0, 20);
            target.maxCraters = Mathf.Clamp(maxCraters, target.minCraters, 60);
            target.minimumAxisScale = Mathf.Clamp(minimumAxisScale, .1f, 1f);
        }
        public bool Matches(AsteroidGeneratorSettings source)
        {
            if (source == null) return true;
            return shapePreset == source.shapePreset &&
                Mathf.Approximately(shapeIrregularity, source.shapeIrregularity) &&
                Mathf.Approximately(craterIntensity, source.craterIntensity) &&
                Mathf.Approximately(surfaceRoughness, source.surfaceRoughness) &&
                minCraters == source.minCraters && maxCraters == source.maxCraters &&
                Mathf.Approximately(minimumAxisScale, source.minimumAxisScale);
        }
    }

    /// <summary>All streaming/LOD budgets belong to the owning Spawn Point,
    /// never to an unrelated belt that happens to use the same Field asset.</summary>
    [Serializable]
    public sealed class MOSpawnQuality
    {
        public bool useGpuFarField = true;
        public bool exactFarShape = true;
        [Range(1, 3)] public int exactFarSubdivisions = 2;
        [Range(1, 16)] public int exactFarBuildsPerFrame = 2;
        [Range(.5f, 10f)] public float farBuildBudgetMs = 2f;
        [Range(32, 1000)] public int maxFarRenderDraws = 300;
        public bool fastStreamedAsteroids = true;
        [Range(0, 4)] public int streamedMeshSubdivisions = 3;
        [Range(2, 5)] public int maximumStreamedSubdivisions = 4;
        [Range(0, 32)] public int cachedStreamedAsteroids = 12;
        [Range(1, 128)] public int maxDetailedAsteroids = 28;
        [Min(50f)] public float detailedDistanceMeters = 1800f;
        [Min(100f)] public float spawnDistanceMeters = 13000f;
        [Min(100f)] public float despawnDistanceMeters = 16000f;
        [Min(100f)] public float distantDrawDistanceMeters = 24000f;
        [Min(100f)] public float debrisDrawDistance = 9000f;
        [Range(1, 300)] public int maxActiveAsteroids = 120;
        [Range(1, 10)] public int generatePerFrame = 2;
        [Range(1, 10)] public int instantiatePerFrame = 1;
        [Range(.1f, 2f)] public float streamingTickSeconds = .25f;
        public bool meshColliders;
        public bool hideDepletedAsteroids = true;

        public void CopyFrom(AsteroidFieldSettings s)
        {
            if (s == null) return;
            useGpuFarField = s.useGpuFarField;
            exactFarShape = s.exactFarShape;
            exactFarSubdivisions = s.exactFarSubdivisions;
            exactFarBuildsPerFrame = s.exactFarBuildsPerFrame;
            farBuildBudgetMs = s.farBuildBudgetMs;
            maxFarRenderDraws = s.maxFarRenderDraws;
            fastStreamedAsteroids = s.fastStreamedAsteroids;
            streamedMeshSubdivisions = s.streamedMeshSubdivisions;
            maximumStreamedSubdivisions = s.maximumStreamedSubdivisions;
            cachedStreamedAsteroids = s.cachedStreamedAsteroids;
            maxDetailedAsteroids = s.maxDetailedAsteroids;
            detailedDistanceMeters = s.detailedDistanceMeters;
            spawnDistanceMeters = s.spawnDistanceMeters;
            despawnDistanceMeters = s.despawnDistanceMeters;
            distantDrawDistanceMeters = s.distantDrawDistanceMeters;
            debrisDrawDistance = s.debrisDrawDistance;
            maxActiveAsteroids = s.maxActiveAsteroids;
            generatePerFrame = s.generatePerFrame;
            instantiatePerFrame = s.instantiatePerFrame;
            streamingTickSeconds = s.streamingTickSeconds;
            meshColliders = s.meshColliders;
            hideDepletedAsteroids = s.hideDepletedAsteroids;
        }
        public void ApplyTo(AsteroidFieldSettings s)
        {
            if (s == null) return;
            s.useGpuFarField = useGpuFarField;
            s.exactFarShape = exactFarShape;
            s.exactFarSubdivisions = Mathf.Clamp(exactFarSubdivisions, 1, 3);
            s.exactFarBuildsPerFrame = Mathf.Clamp(exactFarBuildsPerFrame, 1, 16);
            s.farBuildBudgetMs = Mathf.Clamp(farBuildBudgetMs, .5f, 10f);
            s.maxFarRenderDraws = Mathf.Clamp(maxFarRenderDraws, 32, 1000);
            s.fastStreamedAsteroids = fastStreamedAsteroids;
            s.streamedMeshSubdivisions = Mathf.Clamp(streamedMeshSubdivisions, 0, 4);
            s.maximumStreamedSubdivisions = Mathf.Clamp(maximumStreamedSubdivisions, 2, 5);
            s.cachedStreamedAsteroids = Mathf.Clamp(cachedStreamedAsteroids, 0, 32);
            s.maxDetailedAsteroids = Mathf.Clamp(maxDetailedAsteroids, 1, 128);
            s.detailedDistanceMeters = Mathf.Max(50f, detailedDistanceMeters);
            s.spawnDistanceMeters = Mathf.Max(100f, spawnDistanceMeters);
            s.despawnDistanceMeters = Mathf.Max(s.spawnDistanceMeters + 100f, despawnDistanceMeters);
            s.distantDrawDistanceMeters = Mathf.Max(s.detailedDistanceMeters + 100f, distantDrawDistanceMeters);
            s.debrisDrawDistance = Mathf.Max(100f, debrisDrawDistance);
            s.maxActiveAsteroids = Mathf.Clamp(maxActiveAsteroids, 1, 300);
            s.generatePerFrame = Mathf.Clamp(generatePerFrame, 1, 10);
            s.instantiatePerFrame = Mathf.Clamp(instantiatePerFrame, 1, 10);
            s.streamingTickSeconds = Mathf.Clamp(streamingTickSeconds, .1f, 2f);
            s.meshColliders = meshColliders;
            s.hideDepletedAsteroids = hideDepletedAsteroids;
        }
    }

    /// <summary>Per-location appearance and animation. Material fields are optional
    /// references: use a distinct material asset if the shader/material itself needs editing.</summary>
    [Serializable]
    public sealed class MOSpawnVisuals
    {
        [Tooltip("One material for near, far, and debris of this Spawn Point. Overrides the shared unified material.")]
        public Material unifiedAsteroidMaterialOverride;
        public Material rockMaterialOverride;
        public Material iceMaterialOverride;
        public Material distantMaterialOverride;
        public Material gasParticleMaterialOverride;
        public Material gasVolumeMaterialOverride;
        public Material groundFogMaterialOverride;
        public bool allowExperimentalRockShaders;
        [Range(0f, .5f)] public float rockNormalStrength = .18f;
        public bool enableDecorativeDebris = true;
        [Range(0, 16000)] public int decorativeRockCount = 1600;
        [Range(0, 30000)] public int microFragmentCount;
        [Min(1f)] public float debrisSpreadMeters = 280f;
        [Min(.1f)] public float fragmentMinDiameter = .6f;
        [Min(.1f)] public float fragmentMaxDiameter = 13f;
        [Range(1, 24)] public int debrisMeshVariants = 8;
        public bool enableAsteroidRotation = true;
        [Range(0f, 4f)] public float rotationSpeedMultiplier = 1f;
        [Range(0f, 4f)] public float mineableRotationMultiplier = 1f;
        [Range(0f, 4f)] public float decorativeRotationMultiplier = 1f;

        public void CopyFrom(AsteroidFieldSettings s)
        {
            if (s == null) return;
            // Material overrides remain null: the installed shared materials are defaults,
            // and users may assign a distinct asset on any individual Spawn Point.
            allowExperimentalRockShaders = s.allowExperimentalRockShaders;
            rockNormalStrength = s.rockNormalStrength;
            enableDecorativeDebris = s.enableDecorativeDebris;
            decorativeRockCount = s.decorativeRockCount;
            microFragmentCount = s.microFragmentCount;
            debrisSpreadMeters = s.debrisSpreadMeters;
            fragmentMinDiameter = s.fragmentMinDiameter;
            fragmentMaxDiameter = s.fragmentMaxDiameter;
            debrisMeshVariants = s.debrisMeshVariants;
            enableAsteroidRotation = s.enableAsteroidRotation;
            rotationSpeedMultiplier = s.rotationSpeedMultiplier;
            mineableRotationMultiplier = s.mineableRotationMultiplier;
            decorativeRotationMultiplier = s.decorativeRotationMultiplier;
        }
        public void ApplyTo(AsteroidFieldSettings s)
        {
            if (s == null) return;
            if (unifiedAsteroidMaterialOverride != null) s.unifiedAsteroidMaterial = unifiedAsteroidMaterialOverride;
            if (rockMaterialOverride != null) s.asteroidMaterial = rockMaterialOverride;
            if (iceMaterialOverride != null) s.iceMaterial = iceMaterialOverride;
            if (distantMaterialOverride != null) s.distantMaterial = distantMaterialOverride;
            if (gasParticleMaterialOverride != null) s.gasParticleMaterial = gasParticleMaterialOverride;
            if (gasVolumeMaterialOverride != null) s.gasVolumeMaterial = gasVolumeMaterialOverride;
            if (groundFogMaterialOverride != null) s.groundFogMaterial = groundFogMaterialOverride;
            s.allowExperimentalRockShaders = allowExperimentalRockShaders;
            s.rockNormalStrength = Mathf.Clamp(rockNormalStrength, 0f, .5f);
            s.enableDecorativeDebris = enableDecorativeDebris;
            s.decorativeRockCount = Mathf.Clamp(decorativeRockCount, 0, 16000);
            s.microFragmentCount = Mathf.Clamp(microFragmentCount, 0, 30000);
            s.debrisSpreadMeters = Mathf.Max(1f, debrisSpreadMeters);
            s.fragmentMinDiameter = Mathf.Max(.1f, fragmentMinDiameter);
            s.fragmentMaxDiameter = Mathf.Max(s.fragmentMinDiameter, fragmentMaxDiameter);
            s.debrisMeshVariants = Mathf.Clamp(debrisMeshVariants, 1, 24);
            s.enableAsteroidRotation = enableAsteroidRotation;
            s.rotationSpeedMultiplier = Mathf.Clamp(rotationSpeedMultiplier, 0f, 4f);
            s.mineableRotationMultiplier = Mathf.Clamp(mineableRotationMultiplier, 0f, 4f);
            s.decorativeRotationMultiplier = Mathf.Clamp(decorativeRotationMultiplier, 0f, 4f);
        }

    }
}
