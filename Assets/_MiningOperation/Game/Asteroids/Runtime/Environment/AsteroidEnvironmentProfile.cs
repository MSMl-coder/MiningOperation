using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>Visual-only space environment. Not part of asteroid saves or resource data.</summary>
    [CreateAssetMenu(menuName = "Mining Operation/Asteroids/Space Environment", fileName = "MO_SpaceEnvironment")]
    public sealed class AsteroidEnvironmentProfile : ScriptableObject
    {
        [Header("Background / sky")]
        public Material gradientSkyMaterial;
        public Color upperColor = new Color(0.024f, 0.067f, 0.14f);
        public Color horizonColor = new Color(0.24f, 0.39f, 0.44f);
        public Color lowerColor = new Color(0.055f, 0.14f, 0.24f);
        [Range(0f, 1f)] public float nebulaStrength = 0.24f;
        [Range(0f, 1f)] public float starStrength = 0.12f;

        [Header("Lighting")]
        public Color keyColor = new Color(1f, 0.94f, 0.81f);
        [Range(0f, 4f)] public float keyIntensity = 1.3f;
        public Color fillColor = new Color(0.15f, 0.27f, 0.37f);
        [Range(0f, 4f)] public float fillIntensity = 0.35f;
        public Color ambientColor = new Color(0.17f, 0.22f, 0.27f);
        public Vector3 keyDirectionEuler = new Vector3(35f, -40f, 0f);

        [Header("Distance haze: depth grading, NOT physical atmospheric fog")]
        public Color hazeColor = new Color(0.24f, 0.39f, 0.45f);
        [Min(0f)] public float hazeStartMeters = 3000f;
        [Min(10f)] public float hazeEndMeters = 16500f;
        [Range(0f, 1f)] public float hazeStrength = 0.50f;

        [Header("Optional distant planet")]
        public Material planetMaterial;
        public bool showPlanet = false;
        public Vector3 planetPosition = new Vector3(-32000f, 3000f, 46000f);
        [Min(100f)] public float planetDiameter = 24000f;
    }
}
