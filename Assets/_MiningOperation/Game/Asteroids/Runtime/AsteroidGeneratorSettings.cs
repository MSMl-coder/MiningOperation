using System;
using UnityEngine;


namespace MiningOperation.Asteroids
{
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
}
