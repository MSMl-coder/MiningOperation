using System;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    public enum AsteroidCountMode { Fixed, SeededRange, DensityPerSquareKm }

    // 0 must remain Rock: pre-v2.3 scenes and field JSON implicitly deserialize to zero.
    public enum MiningFieldContent
    {
        Rock = 0,
        Ice = 1, // ice asteroids
        Gas = 2,
        IceShelf = 3 // giant plateau-like shelf bodies
    }
    // Zero preserves the stable v2.3.x particle presentation for existing scenes.
    public enum MOGasPresentation { SoftParticles = 0, Volumetric = 1 }

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
        public bool useComposedLayout = true;

        [Header("Content type (v2.3)")]
        public MiningFieldContent contentType = MiningFieldContent.Rock;
        [Header("Gas field (when Content Type = Gas)")]
        [Range(1, 20)] public int gasCloudCount = 5;
        [Range(24, 200)] public int gasPuffsPerCloud = 72;
        [Min(50f)] public float gasCloudRadiusMeters = 850f;
        [Range(0.05f, 0.7f)] public float gasOpacity = 0.18f;
        public Color gasColorA = new Color(0.30f, 0.63f, 0.80f, 1f);
        public Color gasColorB = new Color(0.63f, 0.74f, 0.81f, 1f);
        [Range(0f, 1f)] public float gasDriftStrength = 0.32f;
        [Range(0f, 1f)] public float gasLayerContrast = 0.35f;

        [Header("V2.4 Gas rendering (visual only)")]
        [Tooltip("SoftParticles is the safe fallback. Volumetric uses 3D procedural density and a dedicated URP material.")]
        public MOGasPresentation gasPresentation = MOGasPresentation.SoftParticles;
        [Range(6, 24)] public int gasVolumeSteps = 10;
        [Range(0.25f, 3f)] public float gasVolumeNoiseScale = 1f;
        [Range(0.25f, 2f)] public float gasVolumeBrightness = 1f;
        [Min(100f)] public float gasVisibilityDistanceMeters = 14000f;

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

        [Header("Natural belt distribution — Rock / Ice")]
        [Tooltip("Replaces regularly spaced belts with a warped, irregular density-island layout.")]
        public bool useNaturalBeltDistribution = true;
        [Range(0f, 1f)] public float naturalBeltIrregularity = .82f;
        [Range(0f, 1f)] public float naturalDensityContrast = .75f;
        [Range(0f, .35f)] public float naturalSatelliteChance = .10f;

        [Header("2D / Y height constraint (per spawn)")]
        public MOSpawnDepthConstraints depthConstraints = new MOSpawnDepthConstraints();

        [Header("Ice Shelf / Plateau")]
        [Range(.025f, .28f)] public float shelfThicknessRatio = .10f;
        [Range(0f, 1f)] public float shelfFractureStrength = .28f;
        [Range(0f, 1f)] public float shelfCollisionStrength = .58f;
        [Range(0f, 1f)] public float shelfRiftDepth = .48f;

        [Header("Per-Spawn Configuration (v2.6)")]
        [SerializeField, HideInInspector] private bool localSettingsInitialized;
        public MOSpawnGeology rockGeology = new MOSpawnGeology();
        public MOSpawnGeology iceGeology = new MOSpawnGeology();
        public MOSpawnQuality quality = new MOSpawnQuality();
        public MOSpawnVisuals visuals = new MOSpawnVisuals();

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

        /// <summary>One-time upgrade of existing scene Spawn Points. Copies shared numeric
        /// defaults into local scene-serialized values, never edits the shared assets.</summary>
        public bool EnsureLocalSettings()
        {
            if (localSettingsInitialized || settings == null) return false;
            if (rockGeology == null) rockGeology = new MOSpawnGeology();
            if (iceGeology == null) iceGeology = new MOSpawnGeology();
            if (quality == null) quality = new MOSpawnQuality();
            if (visuals == null) visuals = new MOSpawnVisuals();
            if (depthConstraints == null) depthConstraints = new MOSpawnDepthConstraints();
            rockGeology.CopyFrom(settings.asteroidSettings);
            iceGeology.CopyFrom(settings.iceGeneratorSettings ?? settings.asteroidSettings);
            quality.CopyFrom(settings);
            visuals.CopyFrom(settings);
            // Freeze the previous shared composition/layout inputs on each location,
            // preserving its old distribution while breaking unintended coupling.
            useComposedLayout = settings.useComposedLayout;
            clusterStrength = settings.clusterStrength;
            densityPockets = settings.densityPockets;
            pocketWidthFraction = settings.pocketWidthFraction;
            overrideComposition = true;
            localSettingsInitialized = true;
            return true;
        }

        public MOSpawnGeology ActiveGeology
        {
            get
            {
                EnsureLocalSettings();
                MOSpawnGeology source =
                    contentType == MiningFieldContent.Ice || contentType == MiningFieldContent.IceShelf
                        ? iceGeology : rockGeology;
                var resolved = source != null ? source.Clone() : new MOSpawnGeology();
                visuals?.ApplyArtDirection(contentType, resolved);
                return resolved;
            }
        }

        /// <summary>Creates a non-persistent copy for this field's runtime/build.
        /// The caller owns and must destroy all three ScriptableObjects.</summary>
        public AsteroidFieldSettings CreateResolvedSettings(
            out AsteroidGeneratorSettings rockGenerator,
            out AsteroidGeneratorSettings iceGenerator)
        {
            rockGenerator = null;
            iceGenerator = null;
            if (settings == null) return null;
            EnsureLocalSettings();
            AsteroidFieldSettings resolved = null;
            try
            {
                resolved = Instantiate(settings);
                resolved.hideFlags = HideFlags.DontSave;
                if (settings.asteroidSettings != null)
                {
                    rockGenerator = Instantiate(settings.asteroidSettings);
                    rockGenerator.hideFlags = HideFlags.DontSave;
                    var rockShape = rockGeology != null ? rockGeology.Clone() : new MOSpawnGeology();
                    visuals?.ApplyArtDirection(MiningFieldContent.Rock, rockShape);
                    rockShape.ApplyTo(rockGenerator);
                    resolved.asteroidSettings = rockGenerator;
                }
                if (settings.iceGeneratorSettings != null)
                {
                    iceGenerator = Instantiate(settings.iceGeneratorSettings);
                    iceGenerator.hideFlags = HideFlags.DontSave;
                    var iceShape = iceGeology != null ? iceGeology.Clone() : new MOSpawnGeology();
                    visuals?.ApplyArtDirection(contentType == MiningFieldContent.IceShelf ? MiningFieldContent.IceShelf : MiningFieldContent.Ice, iceShape);
                    iceShape.ApplyTo(iceGenerator);
                    resolved.iceGeneratorSettings = iceGenerator;
                }
                quality.ApplyTo(resolved);
                visuals.ApplyTo(resolved);
                return resolved;
            }
            catch
            {
                if (rockGenerator != null) AsteroidGeneration.DestroyTemporary(rockGenerator);
                if (iceGenerator != null) AsteroidGeneration.DestroyTemporary(iceGenerator);
                if (resolved != null) AsteroidGeneration.DestroyTemporary(resolved);
                rockGenerator = null;
                iceGenerator = null;
                throw;
            }
        }

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
            EnsureLocalSettings();
            if (!useOverrides)
            {
                var inherited = AsteroidFieldLayoutOptions.FromSettings(common);
                inherited.depthMode = depthConstraints != null ? depthConstraints.depthMode : MOFieldDepthMode.Free3D;
                inherited.depthCenterY = depthConstraints != null ? depthConstraints.yCenter : 0f;
                inherited.depthHalfRange = depthConstraints != null ? depthConstraints.yHalfRange : 0f;
                inherited.depthJitter = depthConstraints != null ? depthConstraints.yJitter : 0f;
                inherited.flattenRotationToPlane = depthConstraints != null && depthConstraints.flattenRotationToPlane;
                inherited.useNaturalBeltDistribution = useNaturalBeltDistribution;
                inherited.naturalBeltIrregularity = naturalBeltIrregularity;
                inherited.naturalDensityContrast = naturalDensityContrast;
                inherited.naturalSatelliteChance = naturalSatelliteChance;
                return inherited;
            }
            return new AsteroidFieldLayoutOptions
            {
                formation = formation,
                asteroidCount = ResolveCount(),
                radiusMeters = radiusMeters,
                thicknessMeters = thicknessMeters,
                arcDegrees = arcDegrees,
                cloudVerticalScale = cloudVerticalScale,
                clusterCount = clusterCount,
                clusterRadiusFraction = clusterRadiusFraction,
                minimumSurfaceGapMeters = surfaceGapMeters,
                placementAttempts = placementAttempts,
                minimumDiameter = minimumDiameterMeters,
                maximumDiameter = maximumDiameterMeters,
                smallAsteroidBias = smallAsteroidBias,
                useComposedLayout = useComposedLayout,
                clusterStrength = clusterStrength,
                densityPockets = densityPockets,
                pocketWidthFraction = pocketWidthFraction,
                depthMode = depthConstraints != null ? depthConstraints.depthMode : MOFieldDepthMode.Free3D,
                depthCenterY = depthConstraints != null ? depthConstraints.yCenter : 0f,
                depthHalfRange = depthConstraints != null ? depthConstraints.yHalfRange : 0f,
                depthJitter = depthConstraints != null ? depthConstraints.yJitter : 0f,
                flattenRotationToPlane = depthConstraints != null && depthConstraints.flattenRotationToPlane,
                useNaturalBeltDistribution = useNaturalBeltDistribution,
                naturalBeltIrregularity = naturalBeltIrregularity,
                naturalDensityContrast = naturalDensityContrast,
                naturalSatelliteChance = naturalSatelliteChance
            };
        }

        public void SyncToController()
        {
            var controller = Controller;
            if (controller == null) return;
            if (settings != null) controller.Settings = settings;
            if (!string.IsNullOrWhiteSpace(fieldId)) controller.FieldId = fieldId;
            controller.FieldSeed = fieldSeed;
            bool isGas = contentType == MiningFieldContent.Gas;
            controller.GenerateOnStart = !isGas;
            controller.RuntimeStreaming = !isGas;
            // An existing saved asteroid snapshot remains untouched when switching content modes.
            controller.enabled = !isGas;
            var gas = GetComponent<MOGasFieldRenderer>();
            if (gas != null) gas.SyncVisualVisibility();
        }

        private void Awake()
        {
            EnsureLocalSettings();
            SyncToController();
            if (Application.isPlaying && contentType == MiningFieldContent.Gas &&
                GetComponent<MOGasFieldRenderer>() == null)
                gameObject.AddComponent<MOGasFieldRenderer>();
        }
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(fieldId)) fieldId = "BELT-" + Guid.NewGuid().ToString("N").Substring(0, 16);
            fixedCount = Mathf.Clamp(fixedCount, 1, 2000);
            randomCountMin = Mathf.Clamp(randomCountMin, 1, 2000);
            randomCountMax = Mathf.Clamp(randomCountMax, randomCountMin, 2000);
            radiusMeters = Mathf.Max(100f, radiusMeters);
            thicknessMeters = Mathf.Max(10f, thicknessMeters);
            maximumDiameterMeters = Mathf.Max(minimumDiameterMeters, maximumDiameterMeters);
            bool shelfFormation = formation == AsteroidFormationType.IceShelfFragments ||
                formation == AsteroidFormationType.IceShelfContinents;
            if (useOverrides && shelfFormation && contentType != MiningFieldContent.IceShelf)
                contentType = MiningFieldContent.IceShelf;
            naturalBeltIrregularity = Mathf.Clamp01(naturalBeltIrregularity);
            naturalDensityContrast = Mathf.Clamp01(naturalDensityContrast);
            naturalSatelliteChance = Mathf.Clamp(naturalSatelliteChance, 0f, .35f);
            shelfThicknessRatio = Mathf.Clamp(shelfThicknessRatio, .025f, .28f);
            shelfFractureStrength = Mathf.Clamp01(shelfFractureStrength);
            shelfCollisionStrength = Mathf.Clamp01(shelfCollisionStrength);
            shelfRiftDepth = Mathf.Clamp01(shelfRiftDepth);
            EnsureLocalSettings();
            if (depthConstraints == null) depthConstraints = new MOSpawnDepthConstraints();
            depthConstraints.ClampValues();
            if (visuals == null) visuals = new MOSpawnVisuals();
            visuals.retroNoiseSuppression = Mathf.Clamp01(visuals.retroNoiseSuppression);
            visuals.retroCraterReadability = Mathf.Clamp01(visuals.retroCraterReadability);
            visuals.retroSilhouetteStrength = Mathf.Clamp01(visuals.retroSilhouetteStrength);
            gasCloudCount = Mathf.Clamp(gasCloudCount, 1, 20);
            gasPuffsPerCloud = Mathf.Clamp(gasPuffsPerCloud, 24, 200);
            gasCloudRadiusMeters = Mathf.Max(50f, gasCloudRadiusMeters);
            gasOpacity = Mathf.Clamp(gasOpacity, 0.05f, 0.7f);
            gasDriftStrength = Mathf.Clamp01(gasDriftStrength);
            gasLayerContrast = Mathf.Clamp01(gasLayerContrast);
            gasVolumeSteps = Mathf.Clamp(gasVolumeSteps, 6, 24);
            gasVolumeNoiseScale = Mathf.Clamp(gasVolumeNoiseScale, 0.25f, 3f);
            gasVolumeBrightness = Mathf.Clamp(gasVolumeBrightness, 0.25f, 2f);
            gasVisibilityDistanceMeters = Mathf.Max(100f, gasVisibilityDistanceMeters);
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
