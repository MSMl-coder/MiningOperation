using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    public static class AsteroidCompositionGenerator
    {
        public static List<MaterialFraction> Generate(ref AsteroidRandom rng, AsteroidResourceCatalog catalog,
            AsteroidClass asteroidClass, AsteroidGeneratorSettings settings)
        {
            ValidateCatalog(catalog);
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (asteroidClass < AsteroidClass.I || asteroidClass > AsteroidClass.III)
                throw new ArgumentOutOfRangeException(nameof(asteroidClass));

            var basePool = new List<ResourceDefinition>();
            var rarePool = new List<ResourceDefinition>();
            var exoticPool = new List<ResourceDefinition>();
            foreach (var resource in catalog.resources)
            {
                switch (resource.rarity)
                {
                    case ResourceRarity.Common:
                    case ResourceRarity.Industrial: basePool.Add(resource); break;
                    case ResourceRarity.Rare: rarePool.Add(resource); break;
                    case ResourceRarity.Exotic: exoticPool.Add(resource); break;
                }
            }
            if (basePool.Count == 0)
                throw new InvalidOperationException("Resource Catalog requires at least one Common/Industrial resource.");

            bool allowRare = asteroidClass != AsteroidClass.I && rarePool.Count > 0 &&
                (asteroidClass == AsteroidClass.II ? settings.maximumRareFractionClassII : settings.maximumRareFractionClassIII) > 0f &&
                rng.Value() < Mathf.Clamp01(asteroidClass == AsteroidClass.II ? settings.rareChanceClassII : settings.rareChanceClassIII);
            bool allowExotic = asteroidClass == AsteroidClass.III && exoticPool.Count > 0 &&
                settings.maximumExoticFractionClassIII > 0f &&
                rng.Value() < Mathf.Clamp01(settings.exoticChanceClassIII);

            var chosen = new List<ResourceDefinition>();
            // At least one ordinary material is always present; rare/exotic occur only when their gate succeeds.
            int maxCount = Mathf.Min(Mathf.Clamp(settings.maximumResources, 1, 12),
                basePool.Count + (allowRare ? 1 : 0) + (allowExotic ? 1 : 0));
            int minCount = Mathf.Clamp(settings.minimumResources, 1, maxCount);
            int targetCount = rng.Range(minCount, maxCount + 1);
            int specialCount = (allowRare ? 1 : 0) + (allowExotic ? 1 : 0);
            targetCount = Mathf.Max(targetCount, Math.Min(maxCount, specialCount + 1));
            // When configured for only 1-2 resources, the ordinary resource takes precedence.
            if (allowRare && targetCount > chosen.Count + 1 + (allowExotic ? 1 : 0))
                chosen.Add(TakeWeighted(ref rng, rarePool));
            else if (allowRare && !allowExotic && targetCount >= 2)
                chosen.Add(TakeWeighted(ref rng, rarePool));

            if (allowExotic && targetCount > chosen.Count + 1)
                chosen.Add(TakeWeighted(ref rng, exoticPool));
            // If only two slots are available and both gates succeed, exotic wins; the normal material remains.
            if (allowRare && chosen.Count == 0 && targetCount > 1 && !allowExotic)
                chosen.Add(TakeWeighted(ref rng, rarePool));

            while (chosen.Count < targetCount && basePool.Count > 0)
                chosen.Add(TakeWeighted(ref rng, basePool));

            // Special shares have explicit caps, unlike V1's uncontrolled normalization.
            double rareShare = 0d, exoticShare = 0d;
            foreach (var resource in chosen)
            {
                if (resource.rarity == ResourceRarity.Rare)
                    rareShare = SampleShare(ref rng, asteroidClass == AsteroidClass.III ?
                        settings.maximumRareFractionClassIII : settings.maximumRareFractionClassII);
                else if (resource.rarity == ResourceRarity.Exotic)
                    exoticShare = SampleShare(ref rng, settings.maximumExoticFractionClassIII);
            }
            double baseShare = 1.0 - rareShare - exoticShare;
            if (baseShare <= 0) throw new InvalidOperationException("Rare/exotic fraction caps exceed total mass.");

            var result = new List<MaterialFraction>();
            var ordinaryWeights = new List<double>();
            double weightSum = 0d;
            foreach (var resource in chosen)
            {
                if (resource.rarity == ResourceRarity.Rare)
                    result.Add(new MaterialFraction(resource.id, rareShare));
                else if (resource.rarity == ResourceRarity.Exotic)
                    result.Add(new MaterialFraction(resource.id, exoticShare));
                else
                {
                    double weight = (0.25 + rng.Value()) * Math.Max(0.01, resource.selectionWeight);
                    ordinaryWeights.Add(weight);
                    weightSum += weight;
                    result.Add(new MaterialFraction(resource.id, -1d));
                }
            }
            int ordinary = 0;
            double ordinaryAssigned = 0d;
            int ordinaryCount = ordinaryWeights.Count;
            foreach (var part in result)
            {
                if (part.fraction >= 0) continue;
                part.fraction = ordinary == ordinaryCount - 1 ? Math.Max(0, baseShare - ordinaryAssigned) :
                    baseShare * ordinaryWeights[ordinary] / weightSum;
                ordinaryAssigned += part.fraction;
                ordinary++;
            }
            return result;
        }

        private static double SampleShare(ref AsteroidRandom rng, float cap)
        {
            double maximum = Math.Max(0d, Math.Min(0.5d, cap));
            if (maximum == 0d) return 0d;
            return maximum * (0.2d + 0.8d * rng.Value());
        }

        private static ResourceDefinition TakeWeighted(ref AsteroidRandom rng, List<ResourceDefinition> pool)
        {
            double sum = 0;
            foreach (var p in pool) sum += p.selectionWeight;
            double roll = rng.Value() * sum;
            int index = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= pool[i].selectionWeight;
                if (roll < 0) { index = i; break; }
            }
            ResourceDefinition chosen = pool[index];
            pool.RemoveAt(index);
            return chosen;
        }

        public static void ValidateCatalog(AsteroidResourceCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (catalog.resources == null || catalog.resources.Count == 0)
                throw new InvalidOperationException("Resource Catalog is empty.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ResourceDefinition resource in catalog.resources)
            {
                if (resource == null || string.IsNullOrWhiteSpace(resource.id) || !ids.Add(resource.id))
                    throw new InvalidOperationException("Catalog contains null, missing or duplicate resource IDs.");
                if (!IsFinite(resource.selectionWeight) || resource.selectionWeight <= 0f ||
                    !IsFinite(resource.densityKgM3) || resource.densityKgM3 <= 0f ||
                    resource.rarity < ResourceRarity.Common || resource.rarity > ResourceRarity.Exotic)
                    throw new InvalidOperationException("Catalog contains invalid weight, density or rarity: " + resource.id);
            }
        }

        public static double CalculateBulkDensity(List<MaterialFraction> composition,
            AsteroidResourceCatalog catalog, double porosity01)
        {
            ValidateCatalog(catalog);
            if (!IsFinite(porosity01) || porosity01 < 0d || porosity01 >= 1d)
                throw new ArgumentOutOfRangeException(nameof(porosity01));
            if (composition == null || composition.Count == 0)
                throw new ArgumentException("Composition must not be empty.", nameof(composition));
            var densities = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var resource in catalog.resources) densities.Add(resource.id, resource.densityKgM3);
            double reciprocal = 0d, sum = 0d;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in composition)
            {
                if (part == null || string.IsNullOrEmpty(part.resourceId) || !ids.Add(part.resourceId) ||
                    !densities.TryGetValue(part.resourceId, out double density) ||
                    !IsFinite(part.fraction) || part.fraction < 0d)
                    throw new ArgumentException("Composition has invalid IDs or fractions.", nameof(composition));
                reciprocal += part.fraction / density;
                sum += part.fraction;
            }
            if (Math.Abs(sum - 1d) > 1e-7 || reciprocal <= 0d)
                throw new ArgumentException("Composition fractions must sum to 1.", nameof(composition));
            // Mass fractions -> harmonic mean grain density, multiplied by (1 - porosity).
            return (1d - porosity01) / reciprocal;
        }

        internal static bool IsFinite(double d) { return !double.IsNaN(d) && !double.IsInfinity(d); }
    }
}
