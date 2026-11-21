using System;
using UnityEngine;
using System.Collections.Generic;

namespace MiningOperation.Asteroids
{
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
}
