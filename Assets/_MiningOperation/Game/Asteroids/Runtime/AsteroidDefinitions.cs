using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    public enum AsteroidClass { I = 1, II = 2, III = 3 }
    public enum ResourceRarity { Common = 0, Industrial = 1, Rare = 2, Exotic = 3 }

    [Serializable]
    public sealed class ResourceDefinition
    {
        public string id = "iron";
        public string displayName = "Iron";
        public ResourceRarity rarity = ResourceRarity.Common;
        [Min(0.01f)] public float selectionWeight = 1f;
        [Min(100f)] public float densityKgM3 = 5000f;
    }

    [CreateAssetMenu(menuName = "Mining Operation/Asteroids/Resource Catalog", fileName = "AsteroidResourceCatalog")]
    public sealed class AsteroidResourceCatalog : ScriptableObject
    {
        public List<ResourceDefinition> resources = new List<ResourceDefinition>();

        public static AsteroidResourceCatalog CreateDefaultRuntime()
        {
            var catalog = CreateInstance<AsteroidResourceCatalog>();
            catalog.hideFlags = HideFlags.DontSave;
            catalog.resources = new List<ResourceDefinition>
            {
                new ResourceDefinition { id="silicates", displayName="Silicates", rarity=ResourceRarity.Common, selectionWeight=5f, densityKgM3=2900f },
                new ResourceDefinition { id="iron", displayName="Iron", rarity=ResourceRarity.Common, selectionWeight=4f, densityKgM3=7870f },
                new ResourceDefinition { id="carbon", displayName="Carbonaceous Matter", rarity=ResourceRarity.Common, selectionWeight=3f, densityKgM3=2100f },
                new ResourceDefinition { id="nickel", displayName="Nickel", rarity=ResourceRarity.Industrial, selectionWeight=3f, densityKgM3=8900f },
                new ResourceDefinition { id="cobalt", displayName="Cobalt", rarity=ResourceRarity.Industrial, selectionWeight=2f, densityKgM3=8900f },
                new ResourceDefinition { id="platinum_group", displayName="Platinum Group", rarity=ResourceRarity.Rare, selectionWeight=1f, densityKgM3=21000f },
                new ResourceDefinition { id="iridium", displayName="Iridium", rarity=ResourceRarity.Exotic, selectionWeight=0.6f, densityKgM3=22560f }
            };
            return catalog;
        }
    }

    // A COPY of the shape parameters is stored with each V2 asteroid. Changes to the
    // ScriptableObject do not silently change an already generated asteroid's mesh.
    [Serializable]
    public sealed class AsteroidGeometryProfile
    {
        public float shapeIrregularity;
        public float craterIntensity;
        public float surfaceRoughness;
        public int minCraters;
        public int maxCraters;
        public float minimumAxisScale;

        public AsteroidGeometryProfile Copy()
        {
            return (AsteroidGeometryProfile)MemberwiseClone();
        }

        public static AsteroidGeometryProfile FromSettings(AsteroidGeneratorSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new AsteroidGeometryProfile
            {
                shapeIrregularity = Mathf.Clamp01(settings.shapeIrregularity),
                craterIntensity = Mathf.Clamp01(settings.craterIntensity),
                surfaceRoughness = Mathf.Clamp01(settings.surfaceRoughness),
                minCraters = Mathf.Clamp(settings.minCraters, 0, 60),
                maxCraters = Mathf.Clamp(settings.maxCraters, Mathf.Clamp(settings.minCraters, 0, 60), 60),
                minimumAxisScale = Mathf.Clamp(settings.minimumAxisScale, 0.1f, 1f)
            };
        }
    }

    [CreateAssetMenu(menuName = "Mining Operation/Asteroids/Generator Settings", fileName = "AsteroidGeneratorSettings")]
    public sealed class AsteroidGeneratorSettings : ScriptableObject
    {
        public AsteroidResourceCatalog catalog;
        [Min(1f)] public float minimumDiameterMeters = 12f;
        [Min(1f)] public float maximumDiameterMeters = 240f;
        [Range(0f, 1f)] public float shapeIrregularity = 0.55f;
        [Range(0f, 1f)] public float craterIntensity = 0.55f;
        [Range(0f, 1f)] public float surfaceRoughness = 0.45f;
        [Range(0, 20)] public int minCraters = 2;
        [Range(0, 60)] public int maxCraters = 18;
        [Range(1, 8)] public int minimumResources = 2;
        [Range(1, 12)] public int maximumResources = 5;
        [Range(0f, 1f)] public float classIIProbability = 0.25f;
        [Range(0f, 1f)] public float classIIIProbability = 0.07f;
        // V2: actual per-asteroid Bernoulli probabilities, NOT selection weights.
        [Range(0f, 1f)] public float rareChanceClassII = 0.25f;
        [Range(0f, 1f)] public float rareChanceClassIII = 0.65f;
        [Range(0f, 1f)] public float exoticChanceClassIII = 0.22f;
        [Range(0.1f, 1f)] public float minimumAxisScale = 0.48f;
        [Range(0f, 0.7f)] public float minimumPorosity = 0.07f;
        [Range(0f, 0.7f)] public float maximumPorosity = 0.27f;
        [Range(0f, 0.5f)] public float maximumRareFractionClassII = 0.08f;
        [Range(0f, 0.5f)] public float maximumRareFractionClassIII = 0.16f;
        [Range(0f, 0.25f)] public float maximumExoticFractionClassIII = 0.025f;
        [Range(0, 6)] public int lod0Subdivisions = 4;
        [Range(0, 6)] public int lod1Subdivisions = 3;
        [Range(0, 6)] public int lod2Subdivisions = 3;
        [Range(0, 6)] public int lod3Subdivisions = 2;
        [Range(0, 6)] public int lod4Subdivisions = 1;
        // Fixed physics source: unaffected by render LOD configuration.
        [Range(2, 5)] public int volumeSubdivisions = 4;

        private void OnValidate()
        {
            minimumDiameterMeters = Mathf.Max(1f, minimumDiameterMeters);
            maximumDiameterMeters = Mathf.Max(minimumDiameterMeters, maximumDiameterMeters);
            maxCraters = Mathf.Max(minCraters, maxCraters);
            maximumResources = Mathf.Max(minimumResources, maximumResources);
            classIIIProbability = Mathf.Min(classIIIProbability, 1f - classIIProbability);
            maximumPorosity = Mathf.Max(minimumPorosity, maximumPorosity);
            lod0Subdivisions = Mathf.Clamp(lod0Subdivisions, 0, 6);
            lod1Subdivisions = Mathf.Clamp(lod1Subdivisions, 0, lod0Subdivisions);
            lod2Subdivisions = Mathf.Clamp(lod2Subdivisions, 0, lod1Subdivisions);
            lod3Subdivisions = Mathf.Clamp(lod3Subdivisions, 0, lod2Subdivisions);
            lod4Subdivisions = Mathf.Clamp(lod4Subdivisions, 0, lod3Subdivisions);
            volumeSubdivisions = Mathf.Clamp(volumeSubdivisions, 2, 5);
        }
    }

    [Serializable]
    public sealed class MaterialFraction
    {
        public string resourceId;
        public double fraction;
        public MaterialFraction(string id, double value) { resourceId = id; fraction = value; }
    }

    [Serializable]
    public sealed class AsteroidData
    {
        public string asteroidId;
        public int seed;
        public int generatorVersion;
        public AsteroidClass asteroidClass;
        public float diameterMeters;
        public double volumeM3;
        public double densityKgM3;
        public double initialMassKg;
        public double remainingMassKg;
        public List<MaterialFraction> composition = new List<MaterialFraction>();
        // V2 additions. Null for V1 scenes/saves.
        public AsteroidGeometryProfile geometryProfile;
        public double porosity01;
    }

    [Serializable]
    public sealed class AsteroidSaveRecord
    {
        public string asteroidId;
        public int seed;
        public int generatorVersion;
        public int asteroidClass;
        public float diameterMeters;
        public double volumeM3;
        public double densityKgM3;
        public double initialMassKg;
        public double remainingMassKg;
        public List<MaterialFraction> composition = new List<MaterialFraction>();
        // V2 additions. Added at the END for compatibility with V1 JSON.
        public AsteroidGeometryProfile geometryProfile;
        public double porosity01;
    }
}
