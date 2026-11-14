using System;
using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>Focused Inspector for scene anchors, with separate preview and destructive build.</summary>
    [CustomEditor(typeof(AsteroidFieldSpawnPoint))]
    public sealed class AsteroidFieldSpawnPointEditor : UnityEditor.Editor
    {
        private bool showPlacement;
        private bool showSizes = true;
        private bool showAdvanced;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var spawn = (AsteroidFieldSpawnPoint)target;
            EditorGUILayout.LabelField("MINING OPERATION  /  SPAWN POINT", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Move and rotate this empty object to place the entire formation. " +
                "Preview before rebuilding saved asteroid records.", MessageType.Info);

            Header("1  Identity & Dependencies");
            Prop("settings", "Field Settings");
            Prop("fieldId", "Field ID");
            Prop("fieldSeed", "Seed");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Seed"))
                {
                    serializedObject.FindProperty("fieldSeed").longValue = DateTime.UtcNow.Ticks;
                }
                if (GUILayout.Button("New Unique ID"))
                {
                    serializedObject.FindProperty("fieldId").stringValue =
                        "BELT-" + Guid.NewGuid().ToString("N").Substring(0, 16);
                }
            }
            Prop("useOverrides", "Independent Layout for This Point");
            bool ownLayout = serializedObject.FindProperty("useOverrides").boolValue;
            if (!ownLayout)
                EditorGUILayout.HelpBox("Using shared Field Settings. To configure this point independently, " +
                    "enable Independent Layout.", MessageType.None);

            Header("2  Formation & Count");
            using (new EditorGUI.DisabledScope(!ownLayout))
            {
                Prop("formation", "Formation");
                Prop("countMode", "Count Mode");
                var mode = (AsteroidCountMode)serializedObject.FindProperty("countMode").enumValueIndex;
                if (mode == AsteroidCountMode.Fixed) Prop("fixedCount", "Asteroid Count");
                if (mode == AsteroidCountMode.SeededRange)
                {
                    Prop("randomCountMin", "Minimum Count");
                    Prop("randomCountMax", "Maximum Count");
                }
                if (mode == AsteroidCountMode.DensityPerSquareKm)
                    Prop("asteroidsPerSquareKm", "Asteroids per km²");
                Prop("radiusMeters", "Radius (m)");
                if (spawn.settings != null && spawn.settings.useComposedLayout)
                {
                    Prop("overrideComposition", "Customize Belt Pockets");
                    if (serializedObject.FindProperty("overrideComposition").boolValue)
                    {
                        Prop("clusterStrength", "Pocket Strength");
                        Prop("densityPockets", "Number of Pockets");
                        Prop("pocketWidthFraction", "Pocket Width");
                    }
                    EditorGUILayout.HelpBox("V2: Spawn Point is near the belt center. Build applies composition changes; Preview alone is non-destructive.", MessageType.None);
                }
                Prop("thicknessMeters", "Thickness (m)");
                if ((AsteroidFormationType)serializedObject.FindProperty("formation").enumValueIndex ==
                    AsteroidFormationType.CrescentBelt ||
                    (AsteroidFormationType)serializedObject.FindProperty("formation").enumValueIndex ==
                    AsteroidFormationType.BrokenBelt)
                    Prop("arcDegrees", "Arc Angle");
                if ((AsteroidFormationType)serializedObject.FindProperty("formation").enumValueIndex ==
                    AsteroidFormationType.ClusterGroups)
                {
                    Prop("clusterCount", "Groups");
                    Prop("clusterRadiusFraction", "Group Size");
                }
                showSizes = EditorGUILayout.Foldout(showSizes, "Asteroid sizes", true);
                if (showSizes)
                {
                    Prop("minimumDiameterMeters", "Min Diameter (m)");
                    Prop("maximumDiameterMeters", "Max Diameter (m)");
                    Prop("smallAsteroidBias", "Small Asteroid Bias");
                }
                showPlacement = EditorGUILayout.Foldout(showPlacement, "Placement & spacing", true);
                if (showPlacement)
                {
                    Prop("surfaceGapMeters", "Min Surface Gap (m)");
                    Prop("placementAttempts", "Placement Attempts");
                    Prop("cloudVerticalScale", "Cloud Vertical Spread");
                }
            }
            serializedObject.ApplyModifiedProperties();

            Header("3  Live Field Status");
            int count = spawn.ResolveCount();
            EditorGUILayout.LabelField("Requested", count + " asteroids");
            var controller = spawn.Controller;
            var snapshot = controller != null ? controller.Snapshot : null;
            if (snapshot == null)
                EditorGUILayout.HelpBox("NOT BUILT — place and preview the field, then Build.", MessageType.Info);
            else
            {
                EditorGUILayout.LabelField("Stored", snapshot.asteroids == null ? "Invalid snapshot" :
                    snapshot.asteroids.Count + " asteroids");
                if (AsteroidAuthoringWorkflow.IsOutdated(spawn))
                    EditorGUILayout.HelpBox("OUTDATED or legacy-untracked build. The scene still contains " +
                        "its old data. Preview and explicitly rebuild to apply your changes.", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("UP TO DATE — saved field matches the current " +
                        "generation parameters.", MessageType.Info);
            }
            string invalid = AsteroidAuthoringWorkflow.Validate(spawn);
            if (invalid != null) EditorGUILayout.HelpBox(invalid, MessageType.Error);
            else if (spawn.settings != null)
            {
                var layout = spawn.BuildOptions(spawn.settings);
                if (layout.maximumDiameter > layout.thicknessMeters * 0.7f)
                    EditorGUILayout.HelpBox("Large asteroids compared to field thickness. " +
                        "Consider increasing thickness for clearer depth.", MessageType.Info);
                if (layout.asteroidCount >= 800)
                    EditorGUILayout.HelpBox("Large belt: preview and build may require more CPU time. " +
                        "The Studio limits automatic previews to 350.", MessageType.Info);
            }

            Header("4  Actions");
            if (GUILayout.Button("Open Layout Preview", GUILayout.Height(30)))
                AsteroidFieldStudio.OpenFor(spawn);
            if (AsteroidAuthoringWorkflow.IsBuilding)
            {
                if (AsteroidAuthoringWorkflow.BuildingController == controller)
                    EditorGUILayout.LabelField("Build Progress", (AsteroidAuthoringWorkflow.Progress * 100f).ToString("F0") + "%");
                if (GUILayout.Button("Cancel Build")) AsteroidAuthoringWorkflow.CancelBuild();
            }
            else
            {
                using (new EditorGUI.DisabledScope(invalid != null || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(snapshot == null ? "Build Field in Scene" :
                        "Rebuild Saved Field…", GUILayout.Height(32)))
                        AsteroidAuthoringWorkflow.BeginBuild(spawn);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frame Formation"))
                {
                    Selection.activeGameObject = spawn.gameObject;
                    if (SceneView.lastActiveSceneView != null)
                    {
                        float radius = spawn.useOverrides ? spawn.radiusMeters :
                            (spawn.settings != null ? spawn.settings.radiusMeters : 1000f);
                        SceneView.lastActiveSceneView.Frame(new Bounds(spawn.transform.position,
                            Vector3.one * Mathf.Max(500f, radius * 2f)), false);
                    }
                }
                if (GUILayout.Button("Select Settings") && spawn.settings != null)
                    Selection.activeObject = spawn.settings;
            }
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced / destructive actions", true);
            if (showAdvanced)
            {
                if (GUILayout.Button("Copy Shared Layout Into Overrides"))
                {
                    AsteroidAuthoringWorkflow.CopySharedLayout(spawn);
                    serializedObject.Update();
                }
                using (new EditorGUI.DisabledScope(controller == null || snapshot == null ||
                    EditorApplication.isPlayingOrWillChangePlaymode || AsteroidAuthoringWorkflow.IsBuilding))
                {
                    if (GUILayout.Button("Clear Saved Field…") &&
                        EditorUtility.DisplayDialog("Delete saved asteroid field?",
                            "This removes every asteroid record and mined state for '" + spawn.name +
                            "'. This operation cannot be reversed by a normal preview refresh.",
                            "Clear Records", "Cancel"))
                    {
                        Undo.RecordObject(controller, "Clear asteroid field");
                        Undo.RecordObject(spawn, "Clear asteroid field signature");
                        controller.ClearField();
                        spawn.LastBuildSignature = string.Empty;
                        EditorUtility.SetDirty(controller);
                        EditorUtility.SetDirty(spawn);
                    }
                }
            }
        }

        private void Header(string title)
        {
            GUILayout.Space(10f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
        private void Prop(string property, string label)
        {
            SerializedProperty item = serializedObject.FindProperty(property);
            if (item != null) EditorGUILayout.PropertyField(item, new GUIContent(label), true);
        }

        private void OnSceneGUI()
        {
            var spawn = (AsteroidFieldSpawnPoint)target;
            if (spawn == null) return;
            float radius = spawn.useOverrides ? spawn.radiusMeters :
                (spawn.settings != null ? spawn.settings.radiusMeters : 1000f);
            Handles.color = new Color(0.2f, 0.85f, 1f, 0.80f);
            Handles.Label(spawn.transform.position + Vector3.up * Mathf.Max(20f, radius * 0.08f),
                spawn.gameObject.name + "  |  " + spawn.ResolveCount() + " asteroids");
            if (!spawn.useOverrides || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorGUI.BeginChangeCheck();
            float changed = Handles.RadiusHandle(Quaternion.identity, spawn.transform.position, radius);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spawn, "Resize asteroid field");
                spawn.radiusMeters = Mathf.Max(100f, changed);
                EditorUtility.SetDirty(spawn);
            }
        }

        public static void CreateSpawnPoint()
        {
            AsteroidFieldSettings chosen = null;
            if (Selection.activeGameObject != null)
            {
                var current = Selection.activeGameObject.GetComponent<AsteroidFieldSpawnPoint>();
                if (current != null) chosen = current.settings;
            }
            if (chosen == null)
            {
                string[] ids = AssetDatabase.FindAssets("t:AsteroidFieldSettings");
                if (ids.Length == 1) chosen = AssetDatabase.LoadAssetAtPath<AsteroidFieldSettings>(
                    AssetDatabase.GUIDToAssetPath(ids[0]));
            }
            var anchor = AsteroidAuthoringWorkflow.CreateSpawnPoint(chosen);
            AsteroidFieldStudio.OpenFor(anchor);
        }
    }
}
