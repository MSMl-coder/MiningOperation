using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    public enum AsteroidClass { I = 1, II = 2, III = 3 }
    public enum ResourceRarity { Common = 0, Industrial = 1, Rare = 2, Exotic = 3 }

    // Preserved in each asteroid's immutable geometry profile. Zero keeps the
    // original seed-dependent mixture for existing serialized snapshots.
    public enum MOAsteroidShapePreset
    {
        Mixed = 0,
        Rounded = 1,
        Fractured = 2,
        Elongated = 3,
        ContactBinary = 4,
        Cratered = 5,
        Blocky = 6
    }


    [Serializable]
    public sealed class ResourceDefinition
    {
        public string id = "iron";
        public string displayName = "Iron";
        public ResourceRarity rarity = ResourceRarity.Common;
        [Min(0.01f)] public float selectionWeight = 1f;
        [Min(100f)] public float densityKgM3 = 5000f;
    }



    // A COPY of the shape parameters is stored with each V2 asteroid. Changes to the
    // ScriptableObject do not silently change an already generated asteroid's mesh.
    [Serializable]
    public sealed class AsteroidGeometryProfile
    {
        public float shapeIrregularity;
        public MOAsteroidShapePreset shapePreset;
        public float craterIntensity;
        public float surfaceRoughness;
        public int minCraters;
        public int maxCraters;
        public float minimumAxisScale;
        // V6-only ice shelf recipe; defaults preserve all pre-V6 save records.
        public bool iceShelf;
        public bool continentalShelf;
        public float shelfThicknessRatio;
        public float shelfFractureStrength;
        public float shelfCollisionStrength;
        public float shelfRiftDepth;

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
                shapePreset = settings.shapePreset,
                craterIntensity = Mathf.Clamp01(settings.craterIntensity),
                surfaceRoughness = Mathf.Clamp01(settings.surfaceRoughness),
                minCraters = Mathf.Clamp(settings.minCraters, 0, 60),
                maxCraters = Mathf.Clamp(settings.maxCraters, Mathf.Clamp(settings.minCraters, 0, 60), 60),
                minimumAxisScale = Mathf.Clamp(settings.minimumAxisScale, 0.1f, 1f)
            };
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
