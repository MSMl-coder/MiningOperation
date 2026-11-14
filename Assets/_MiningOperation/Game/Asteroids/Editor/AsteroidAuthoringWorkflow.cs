using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Single Editor-only workflow for assets, anchors, signatures and non-blocking builds.
    /// Does not change asteroid IDs, generator math, snapshots or runtime save formats.
    /// </summary>
    [InitializeOnLoad]
    public static class AsteroidAuthoringWorkflow
    {
        private const string AssetFolder = "Assets/_MiningOperation/AsteroidAuthoring";
        private sealed class BuildJob
        {
            public AsteroidFieldSpawnPoint anchor;
            public AsteroidFieldController controller;
            public string signature;
        }

        private static BuildJob job;
        public static bool IsBuilding => job != null;
        public static AsteroidFieldController BuildingController => job == null ? null : job.controller;
        public static float Progress => job == null || job.controller == null || job.controller.BuildTotal == 0
            ? 0f : job.controller.BuildProgress / (float)job.controller.BuildTotal;
        public static event Action StateChanged;

        static AsteroidAuthoringWorkflow()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CancelBuild;
            EditorApplication.quitting += CancelBuild;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) CancelBuild();
            };
        }

        public static bool IsReady(AsteroidFieldSettings field) =>
            field != null && field.asteroidSettings != null && field.asteroidSettings.catalog != null;

        public static string Validate(AsteroidFieldSpawnPoint spawn)
        {
            if (spawn == null) return "Select or create a Spawn Point.";
            if (!IsReady(spawn.settings)) return "Missing Field Settings, Generator Settings or Resource Catalog.";
            if (string.IsNullOrWhiteSpace(spawn.fieldId)) return "Field ID is empty.";
            if (DuplicateId(spawn)) return "Another Spawn Point in the scene uses the same Field ID.";
            try
            {
                spawn.settings.ValidateOrThrow();
                AsteroidCompositionGenerator.ValidateCatalog(spawn.settings.asteroidSettings.catalog);
                spawn.BuildOptions(spawn.settings).Validate();
            }
            catch (Exception e) { return e.Message; }
            return null;
        }

        public static bool DuplicateId(AsteroidFieldSpawnPoint candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.fieldId)) return false;
            var anchors = UnityEngine.Object.FindObjectsByType<AsteroidFieldSpawnPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var other in anchors)
                if (other != null && other != candidate && other.gameObject.scene == candidate.gameObject.scene &&
                    string.Equals(other.fieldId, candidate.fieldId, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// Only generation-relevant inputs are hashed: display materials, streaming radius,
        /// transform position and camera changes deliberately do not stale a field.
        /// </summary>
        public static string Signature(AsteroidFieldSpawnPoint spawn)
        {
            if (spawn == null || !IsReady(spawn.settings)) return string.Empty;
            AsteroidFieldLayoutOptions layout = spawn.BuildOptions(spawn.settings);
            var signature = new StringBuilder(1024);
            signature.Append("v2|layout=").Append(AsteroidFieldLayout.LayoutVersion)
                .Append("|generator=").Append(AsteroidGeneration.GeneratorVersion)
                .Append("|id=").Append(spawn.fieldId)
                .Append("|seed=").Append(spawn.fieldSeed)
                .Append("|options=").Append(JsonUtility.ToJson(layout));
            AsteroidGeneratorSettings g = spawn.settings.asteroidSettings;
            // Explicit value serialization avoids Unity Object instance-ID changes across reloads.
            Append(signature, g.minimumDiameterMeters, g.maximumDiameterMeters, g.shapeIrregularity,
                g.craterIntensity, g.surfaceRoughness, g.minimumAxisScale, g.minimumPorosity,
                g.maximumPorosity, g.classIIProbability, g.classIIIProbability,
                g.rareChanceClassII, g.rareChanceClassIII, g.exoticChanceClassIII,
                g.maximumRareFractionClassII, g.maximumRareFractionClassIII,
                g.maximumExoticFractionClassIII);
            signature.Append("|craters=").Append(g.minCraters).Append(',').Append(g.maxCraters)
                .Append("|resources=").Append(g.minimumResources).Append(',').Append(g.maximumResources)
                .Append("|volumeDetail=").Append(g.volumeSubdivisions);
            foreach (ResourceDefinition r in g.catalog.resources)
            {
                if (r == null) { signature.Append("|null-resource"); continue; }
                signature.Append("|resource=").Append(r.id).Append(',').Append(r.rarity).Append(',');
                Append(signature, r.selectionWeight, r.densityKgM3);
            }
            string source = signature.ToString();
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 1099511628211UL;
                }
                return hash.ToString("X16");
            }
        }

        private static void Append(StringBuilder builder, params float[] values)
        {
            for (int i = 0; i < values.Length; i++)
                builder.Append('|').Append(values[i].ToString("R", CultureInfo.InvariantCulture));
        }

        public static bool IsOutdated(AsteroidFieldSpawnPoint spawn)
        {
            if (spawn == null || spawn.Controller == null) return true;
            AsteroidFieldSnapshot data = spawn.Controller.Snapshot;
            if (data == null || data.asteroids == null) return true;
            if (data.fieldId != spawn.fieldId || data.fieldSeed != spawn.fieldSeed) return true;
            if (spawn.settings == null) return true;
            AsteroidFieldLayoutOptions options = spawn.BuildOptions(spawn.settings);
            if (data.formation != options.formation || data.asteroids.Count > options.asteroidCount) return true;
            // A dense formation can legitimately fit fewer rocks than requested.
            // The stored signature is the authoritative comparison for new builds.
            return string.IsNullOrEmpty(spawn.LastBuildSignature) ||
                !string.Equals(spawn.LastBuildSignature, Signature(spawn), StringComparison.Ordinal);
        }

        public static AsteroidFieldSettings CreateReadyAssets()
        {
            EnsureFolder();
            var catalog = ScriptableObject.CreateInstance<AsteroidResourceCatalog>();
            var defaults = AsteroidResourceCatalog.CreateDefaultRuntime();
            catalog.resources = defaults.resources;
            UnityEngine.Object.DestroyImmediate(defaults);
            AssetDatabase.CreateAsset(catalog,
                AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/ResourceCatalog.asset"));

            var generator = ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
            generator.catalog = catalog;
            AssetDatabase.CreateAsset(generator,
                AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/AsteroidGenerator.asset"));

            var field = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            field.asteroidSettings = generator;
            // New V2 fields start as coherent, visually dense belts. Old field assets
            // are never touched by this constructor.
            field.useComposedLayout = true;
            field.asteroidCount = 320;
            field.radiusMeters = 3800f;
            field.thicknessMeters = 850f;
            field.arcDegrees = 155f;
            field.minimumSurfaceGapMeters = 16f;
            field.clusterStrength = 0.78f;
            field.densityPockets = 10;
            field.pocketWidthFraction = 0.07f;
            field.enableDecorativeDebris = true;
            field.decorativeRockCount = 1600;
            field.microFragmentCount = 0;
            field.debrisSpreadMeters = 280f;
            field.debrisDrawDistance = 9000f;
            field.distantDrawDistanceMeters = 23000f;
            field.asteroidMaterial = CreateOptionalMaterial("AsteroidRock.mat", "Universal Render Pipeline/Lit");
            field.distantMaterial = CreateOptionalMaterial("AsteroidFar.mat", "Universal Render Pipeline/Lit");
            AssetDatabase.CreateAsset(field,
                AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/AsteroidField.asset"));
            AsteroidRockTextureGenerator.Install(field);
            AssetDatabase.SaveAssets();
            return field;
        }

        private static Material CreateOptionalMaterial(string name, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            var material = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(material,
                AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/" + name));
            return material;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_MiningOperation"))
                AssetDatabase.CreateFolder("Assets", "_MiningOperation");
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets/_MiningOperation", "AsteroidAuthoring");
        }

        public static AsteroidFieldSpawnPoint CreateSpawnPoint(AsteroidFieldSettings fieldSettings)
        {
            var go = new GameObject("Belt Spawn Point");
            Undo.RegisterCreatedObjectUndo(go, "Create asteroid spawn point");
            if (SceneView.lastActiveSceneView != null)
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            var anchor = go.AddComponent<AsteroidFieldSpawnPoint>();
            anchor.fieldId = "BELT-" + Guid.NewGuid().ToString("N").Substring(0, 16);
            anchor.fieldSeed = DateTime.UtcNow.Ticks;
            anchor.settings = fieldSettings;
            CopySharedLayout(anchor);
            anchor.SyncToController();
            Selection.activeGameObject = go;
            EditorUtility.SetDirty(anchor);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return anchor;
        }

        /// <summary>Attach authoring UX to a pre-v1.4 scene field without rebuilding its snapshot.</summary>
        public static AsteroidFieldSpawnPoint ConvertLegacyField(AsteroidFieldController existing)
        {
            if (existing == null) return null;
            var current = existing.GetComponent<AsteroidFieldSpawnPoint>();
            if (current != null) return current;
            string originalId = existing.FieldId;
            long originalSeed = existing.FieldSeed;
            AsteroidFieldSettings originalSettings = existing.Settings;
            var added = Undo.AddComponent<AsteroidFieldSpawnPoint>(existing.gameObject);
            added.settings = originalSettings;
            added.fieldId = originalId;
            added.fieldSeed = originalSeed;
            CopySharedLayout(added);
            added.LastBuildSignature = string.Empty; // Older snapshots have no authoring signature.
            added.SyncToController();
            EditorUtility.SetDirty(added);
            EditorUtility.SetDirty(existing);
            EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
            return added;
        }

        public static void CopySharedLayout(AsteroidFieldSpawnPoint spawn)
        {
            if (spawn == null || !IsReady(spawn.settings)) return;
            AsteroidFieldSettings s = spawn.settings;
            Undo.RecordObject(spawn, "Copy shared field layout");
            spawn.formation = s.formation;
            spawn.fixedCount = s.asteroidCount;
            spawn.radiusMeters = s.radiusMeters;
            spawn.thicknessMeters = s.thicknessMeters;
            spawn.arcDegrees = s.arcDegrees;
            spawn.cloudVerticalScale = s.cloudVerticalScale;
            spawn.clusterCount = s.clusterCount;
            spawn.clusterRadiusFraction = s.clusterRadiusFraction;
            spawn.surfaceGapMeters = s.minimumSurfaceGapMeters;
            spawn.placementAttempts = s.placementAttempts;
            spawn.minimumDiameterMeters = s.asteroidSettings.minimumDiameterMeters;
            spawn.maximumDiameterMeters = s.asteroidSettings.maximumDiameterMeters;
            spawn.smallAsteroidBias = s.smallAsteroidBias;
            EditorUtility.SetDirty(spawn);
        }

        public static bool BeginBuild(AsteroidFieldSpawnPoint anchor)
        {
            string invalid = Validate(anchor);
            if (invalid != null) { EditorUtility.DisplayDialog("Asteroid field needs attention", invalid, "OK"); return false; }
            if (IsBuilding) { EditorUtility.DisplayDialog("Generator busy", "Finish or cancel the current build first.", "OK"); return false; }
            AsteroidFieldController controller = anchor.Controller;
            if (controller == null) return false;
            if (controller.Snapshot != null && !EditorUtility.DisplayDialog("Rebuild asteroid field?",
                "This REPLACES the saved asteroid records for '" + anchor.name + "', " +
                "including its mined resource state. Make a scene backup first if needed.",
                "Replace Field", "Cancel")) return false;
            string signature = Signature(anchor);
            try
            {
                Undo.RecordObject(controller, "Build asteroid field");
                Undo.RecordObject(anchor, "Record asteroid field build");
                anchor.SyncToController();
                controller.BeginBuild();
                job = new BuildJob { anchor = anchor, controller = controller, signature = signature };
                EditorApplication.update += ContinueBuild;
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                controller.CancelBuild();
                Debug.LogException(e, controller);
                EditorUtility.DisplayDialog("Asteroid generation failed", e.Message, "OK");
                return false;
            }
        }

        private static void ContinueBuild()
        {
            if (job == null || job.controller == null || job.anchor == null ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            { CancelBuild(); return; }
            try
            {
                bool finished = job.controller.BuildStep(3);
                if (EditorUtility.DisplayCancelableProgressBar("Mining Operation / Building Field",
                    job.anchor.name + "  |  " + job.controller.BuildProgress + " / " + job.controller.BuildTotal,
                    Progress)) { CancelBuild(); return; }
                if (!finished) { StateChanged?.Invoke(); return; }
                AsteroidFieldController built = job.controller;
                AsteroidFieldSpawnPoint anchor = job.anchor;
                anchor.LastBuildSignature = job.signature;
                EditorUtility.SetDirty(built);
                EditorUtility.SetDirty(anchor);
                EditorSceneManager.MarkSceneDirty(built.gameObject.scene);
                FinishBuild(false);
                StateChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e, job.controller);
                CancelBuild();
                EditorUtility.DisplayDialog("Asteroid generation failed", e.Message, "OK");
            }
        }

        public static void CancelBuild()
        {
            if (job != null && job.controller != null) job.controller.CancelBuild();
            FinishBuild(true);
        }

        private static void FinishBuild(bool announce)
        {
            EditorApplication.update -= ContinueBuild;
            EditorUtility.ClearProgressBar();
            job = null;
            if (announce) StateChanged?.Invoke();
        }
    }
}
