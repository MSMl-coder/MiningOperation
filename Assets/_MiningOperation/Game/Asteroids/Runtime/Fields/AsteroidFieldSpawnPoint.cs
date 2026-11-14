using System;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    public enum AsteroidCountMode { Fixed, SeededRange, DensityPerSquareKm }

    /// <summary>
    /// Attach to an EMPTY scene object. Every anchor owns an independent field ID, seed,
    /// formation and count, while the controller retains its authoritative save snapshot.
    /// The settings asset is NEVER mutated by overrides.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AsteroidFieldController))]
    public sealed class AsteroidFieldSpawnPoint : MonoBehaviour
    {
        [Header("Identity / shared assets")]
        public AsteroidFieldSettings settings;
        public string fieldId;
        public long fieldSeed = 80521L;
        public bool useOverrides = true;

        [Header("Formation for this spawn point")]
        public AsteroidFormationType formation = AsteroidFormationType.CrescentBelt;
        public AsteroidCountMode countMode = AsteroidCountMode.Fixed;
        [Range(1, 2000)] public int fixedCount = 120;
        [Range(1, 2000)] public int randomCountMin = 60;
        [Range(1, 2000)] public int randomCountMax = 180;
        [Min(0.001f)] public float asteroidsPerSquareKm = 0.55f;
        [Min(100f)] public float radiusMeters = 8500f;
        [Min(10f)] public float thicknessMeters = 1800f;
        [Range(45f, 300f)] public float arcDegrees = 180f;
        [Range(0.1f, 1f)] public float cloudVerticalScale = 0.65f;
        [Range(2, 12)] public int clusterCount = 4;
        [Range(0.01f, 1f)] public float clusterRadiusFraction = 0.16f;
        [Min(0f)] public float surfaceGapMeters = 100f;
        [Range(1, 300)] public int placementAttempts = 75;

        [Header("V2 Composition (local override)")]
        [Tooltip("If disabled, these three values come from shared Field Settings.")]
        public bool overrideComposition;
        [Range(0f, 1f)] public float clusterStrength = 0.78f;
        [Range(2, 32)] public int densityPockets = 10;
        [Range(0.01f, 0.4f)] public float pocketWidthFraction = 0.08f;

        [Header("Asteroid sizes (independent of resource class)")]
        [Min(1f)] public float minimumDiameterMeters = 8f;
        [Min(1f)] public float maximumDiameterMeters = 500f;
        [Range(0f, 1f)] public float smallAsteroidBias = 0.72f;

        // Editor-only authoring metadata. Does not enter asteroid field JSON saves.
        [SerializeField, HideInInspector] private string lastBuildSignature;
        public string LastBuildSignature
        {
            get => lastBuildSignature;
            set => lastBuildSignature = value;
        }

        public AsteroidFieldController Controller => GetComponent<AsteroidFieldController>();

        public int ResolveCount()
        {
            switch (countMode)
            {
                case AsteroidCountMode.SeededRange:
                    int min = Mathf.Clamp(randomCountMin, 1, 2000);
                    int max = Mathf.Clamp(randomCountMax, min, 2000);
                    var rng = new AsteroidRandom(unchecked((uint)AsteroidIdUtility.DeriveSeed(fieldSeed, 9017)));
                    return rng.Range(min, max + 1);
                case AsteroidCountMode.DensityPerSquareKm:
                    double squareKm = Math.PI * (double)radiusMeters * radiusMeters / 1000000d;
                    return Mathf.Clamp((int)Math.Round(squareKm * Math.Max(0.001f, asteroidsPerSquareKm)), 1, 2000);
                default: return Mathf.Clamp(fixedCount, 1, 2000);
            }
        }

        public AsteroidFieldLayoutOptions BuildOptions(AsteroidFieldSettings common)
        {
            if (common == null) throw new ArgumentNullException(nameof(common));
            if (!useOverrides) return AsteroidFieldLayoutOptions.FromSettings(common);
            return new AsteroidFieldLayoutOptions
            {
                formation = formation, asteroidCount = ResolveCount(),
                radiusMeters = radiusMeters, thicknessMeters = thicknessMeters,
                arcDegrees = arcDegrees, cloudVerticalScale = cloudVerticalScale,
                clusterCount = clusterCount, clusterRadiusFraction = clusterRadiusFraction,
                minimumSurfaceGapMeters = surfaceGapMeters,
                placementAttempts = placementAttempts,
                minimumDiameter = minimumDiameterMeters,
                maximumDiameter = maximumDiameterMeters,
                smallAsteroidBias = smallAsteroidBias,
                useComposedLayout = common.useComposedLayout,
                clusterStrength = overrideComposition ? clusterStrength : common.clusterStrength,
                densityPockets = overrideComposition ? densityPockets : common.densityPockets,
                pocketWidthFraction = overrideComposition ? pocketWidthFraction : common.pocketWidthFraction
            };
        }

        public void SyncToController()
        {
            var controller = Controller;
            if (controller == null) return;
            if (settings != null) controller.Settings = settings;
            if (!string.IsNullOrWhiteSpace(fieldId)) controller.FieldId = fieldId;
            controller.FieldSeed = fieldSeed;
            controller.GenerateOnStart = true;
        }

        private void Awake() { SyncToController(); }
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(fieldId)) fieldId = "BELT-" + Guid.NewGuid().ToString("N").Substring(0, 16);
            fixedCount = Mathf.Clamp(fixedCount, 1, 2000);
            randomCountMin = Mathf.Clamp(randomCountMin, 1, 2000);
            randomCountMax = Mathf.Clamp(randomCountMax, randomCountMin, 2000);
            radiusMeters = Mathf.Max(100f, radiusMeters);
            thicknessMeters = Mathf.Max(10f, thicknessMeters);
            maximumDiameterMeters = Mathf.Max(minimumDiameterMeters, maximumDiameterMeters);
            SyncToController();
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.95f, 0.55f);
            Gizmos.DrawWireSphere(transform.position, useOverrides ? radiusMeters :
                (settings != null ? settings.radiusMeters : 1000f));
        }
    }
}
