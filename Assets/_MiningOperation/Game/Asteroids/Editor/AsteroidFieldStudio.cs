using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    // Editor-only tool: layouts are cheap to preview, materialization of rock data is
    // time-sliced on EditorApplication.update to keep the inspector responsive.
    public sealed class AsteroidFieldStudio : EditorWindow
    {
        private AsteroidFieldSettings settings;
        private AsteroidFieldController field;
        private long seed = 80521L;
        private List<AsteroidFieldPlacement> preview;
        private Vector2 scroll;
        private AsteroidFieldController buildingField;

        [MenuItem("Tools/Mining Operation/Asteroid Field Studio")]
        public static void Open() => GetWindow<AsteroidFieldStudio>("Field Studio");

        private void OnDisable()
        {
            EditorApplication.update -= ContinueBuild;
            EditorUtility.ClearProgressBar();
            if (buildingField != null) buildingField.CancelBuild();
            buildingField = null;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("MINING OPERATION  /  ASTEROID FIELD STUDIO", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Generate belts, groups and clouds of mineable asteroids. " +
                "The scene stores all asteroid records; actual 3D meshes are streamed around the viewer.", MessageType.Info);
            settings = (AsteroidFieldSettings)EditorGUILayout.ObjectField("Field Settings", settings,
                typeof(AsteroidFieldSettings), false);
            field = (AsteroidFieldController)EditorGUILayout.ObjectField("Field Root", field,
                typeof(AsteroidFieldController), true);
            seed = EditorGUILayout.LongField("Seed", seed);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Field Settings Asset")) CreateSettingsAsset();
            if (GUILayout.Button("Use Selected Field")) UseSelectedField();
            if (GUILayout.Button("Open Asteroid Studio")) AsteroidGeneratorStudio.Open();
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(settings == null || settings.asteroidSettings == null))
            {
                if (GUILayout.Button("Preview Formation", GUILayout.Height(30))) PlanPreview();
            }
            Rect chart = GUILayoutUtility.GetRect(100f, 350f, GUILayout.ExpandWidth(true));
            DrawPreview(chart);
            if (preview != null)
            {
                EditorGUILayout.LabelField("Layout", preview.Count + " / " + settings.asteroidCount + " asteroids");
                int a = 0, b = 0, c = 0;
                foreach (AsteroidFieldPlacement placement in preview)
                {
                    AsteroidClass cls = PredictClass(placement.asteroidSeed, settings.asteroidSettings);
                    if (cls == AsteroidClass.I) a++;
                    else if (cls == AsteroidClass.II) b++;
                    else c++;
                }
                EditorGUILayout.LabelField("Class I / II / III", $"{a} / {b} / {c}");
                if (preview.Count < settings.asteroidCount)
                    EditorGUILayout.HelpBox("Could not fit all asteroids. Increase formation size, " +
                        "reduce the exclusion gap, or increase placement attempts.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(settings == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Create Field Root in Scene", GUILayout.Height(30))) CreateSceneField();
                using (new EditorGUI.DisabledScope(field == null || buildingField != null))
                {
                    if (GUILayout.Button("Generate Persistent Field", GUILayout.Height(35))) BeginGenerate();
                }
                if (buildingField != null)
                {
                    EditorGUILayout.LabelField("Building", $"{buildingField.BuildProgress} / {buildingField.BuildTotal}");
                    if (GUILayout.Button("Cancel Generation")) StopBuild();
                }
                using (new EditorGUI.DisabledScope(field == null || field.Snapshot == null || buildingField != null))
                {
                    if (GUILayout.Button("Clear Temporary Spawned Asteroids"))
                    {
                        field.ClearSpawned();
                        EditorUtility.SetDirty(field);
                    }
                    if (GUILayout.Button("Select Field and Frame Scene")) FocusScene();
                }
            }
            EditorGUILayout.HelpBox("In Play Mode assign a Viewer transform (ship or camera), or tag " +
                "your camera MainCamera. The scene root shows full 3D gizmo layout when selected. " +
                "Regenerating replaces the previous mined state of that field.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void PlanPreview()
        {
            try
            {
                settings.ValidateOrThrow();
                string id = field != null ? field.FieldId : "field-preview";
                if (string.IsNullOrWhiteSpace(id)) id = "field-preview";
                preview = AsteroidFieldLayout.Plan(settings, seed, id);
                Repaint();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Field Preview Error", e.Message, "OK");
            }
        }

        private static AsteroidClass PredictClass(int asteroidSeed, AsteroidGeneratorSettings rockSettings)
        {
            var rng = new AsteroidRandom(unchecked((uint)asteroidSeed) ^ 0xA3C59AC3u);
            rng.Value(); // diameter roll precedes class roll
            float roll = rng.Value();
            float p3 = Mathf.Clamp01(rockSettings.classIIIProbability);
            float p2 = Mathf.Min(Mathf.Clamp01(rockSettings.classIIProbability), 1f - p3);
            return roll < p3 ? AsteroidClass.III :
                   roll < p3 + p2 ? AsteroidClass.II : AsteroidClass.I;
        }

        private void DrawPreview(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.075f, 0.09f, 0.12f));
            if (preview == null || Event.current.type != EventType.Repaint) return;
            float extent = Mathf.Max(10f, settings.radiusMeters + settings.thicknessMeters);
            foreach (var p in preview)
                extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(p.localPosition.x), Mathf.Abs(p.localPosition.z)) * 1.08f);
            float scale = Mathf.Max(1f, Mathf.Min(rect.width - 24f, rect.height - 24f)) / (extent * 2f);
            EditorGUI.DrawRect(new Rect(rect.center.x - 0.5f, rect.y + 8, 1f, rect.height - 16f),
                new Color(0.3f, 0.36f, 0.42f));
            EditorGUI.DrawRect(new Rect(rect.x + 8, rect.center.y - 0.5f, rect.width - 16f, 1f),
                new Color(0.3f, 0.36f, 0.42f));
            foreach (var p in preview)
            {
                AsteroidClass cls = PredictClass(p.asteroidSeed, settings.asteroidSettings);
                Color color = cls == AsteroidClass.III ? new Color(1f, 0.61f, 0.27f) :
                    cls == AsteroidClass.II ? new Color(0.94f, 0.82f, 0.38f) :
                    new Color(0.48f, 0.77f, 0.9f);
                float x = rect.center.x + p.localPosition.x * scale;
                float y = rect.center.y - p.localPosition.z * scale;
                float d = Mathf.Clamp(p.nominalDiameter * scale * 1.7f, 2f, 11f);
                EditorGUI.DrawRect(new Rect(x - d * 0.5f, y - d * 0.5f, d, d), color);
            }
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, 165f, 20f), "TOP / LOCAL X-Z", EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(rect.x + 12f, rect.yMax - 25f, 260f, 18f),
                "I cyan    II yellow    III orange", EditorStyles.whiteMiniLabel);
        }

        private void CreateSceneField()
        {
            var go = new GameObject("AsteroidField");
            Undo.RegisterCreatedObjectUndo(go, "Create Asteroid Field");
            var instance = go.AddComponent<AsteroidFieldController>();
            instance.Settings = settings;
            instance.FieldSeed = seed;
            instance.FieldId = "FIELD-" + Guid.NewGuid().ToString("N").Substring(0, 16);
            field = instance;
            Selection.activeGameObject = go;
            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(go.scene);
            if (settings.asteroidSettings != null) PlanPreview();
        }

        private void UseSelectedField()
        {
            if (Selection.activeGameObject == null) return;
            var selection = Selection.activeGameObject.GetComponent<AsteroidFieldController>();
            if (selection == null) return;
            field = selection;
            settings = selection.Settings;
            seed = selection.FieldSeed;
            if (settings != null && settings.asteroidSettings != null) PlanPreview();
        }

        private void BeginGenerate()
        {
            if (field == null) return;
            if (field.Snapshot != null && !EditorUtility.DisplayDialog("Replace field data?",
                "This overwrites the previous field and its mining progress. Continue?",
                "Regenerate", "Cancel")) return;
            try
            {
                field.Settings = settings;
                field.FieldSeed = seed;
                if (string.IsNullOrWhiteSpace(field.FieldId))
                    field.FieldId = "FIELD-" + Guid.NewGuid().ToString("N").Substring(0, 16);
                field.BeginBuild();
                buildingField = field;
                EditorApplication.update -= ContinueBuild;
                EditorApplication.update += ContinueBuild;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Field Generation Error", e.Message, "OK");
            }
        }

        private void ContinueBuild()
        {
            if (buildingField == null) { StopBuild(); return; }
            try
            {
                bool finished = buildingField.BuildStep(2);
                bool cancel = EditorUtility.DisplayCancelableProgressBar("Mining Operation: Field Generation",
                    $"Generating {buildingField.BuildProgress} / {buildingField.BuildTotal} asteroid records",
                    buildingField.BuildTotal == 0 ? 0f :
                        buildingField.BuildProgress / (float)buildingField.BuildTotal);
                if (cancel) { StopBuild(); return; }
                if (finished)
                {
                    var done = buildingField;
                    StopBuild(false);
                    EditorUtility.SetDirty(done);
                    EditorSceneManager.MarkSceneDirty(done.gameObject.scene);
                    Repaint();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                StopBuild();
                EditorUtility.DisplayDialog("Field Generation Error", e.Message, "OK");
            }
        }

        private void StopBuild(bool cancel = true)
        {
            EditorApplication.update -= ContinueBuild;
            EditorUtility.ClearProgressBar();
            if (buildingField != null && cancel) buildingField.CancelBuild();
            buildingField = null;
        }

        private void FocusScene()
        {
            if (field == null) return;
            Selection.activeGameObject = field.gameObject;
            if (SceneView.lastActiveSceneView == null || settings == null) return;
            float radius = settings.radiusMeters + settings.thicknessMeters;
            SceneView.lastActiveSceneView.Frame(new Bounds(field.transform.position,
                Vector3.one * Mathf.Max(1000f, radius * 2f)), false);
        }

        private void CreateSettingsAsset()
        {
            const string folder = "Assets/MiningOperation/AsteroidData";
            string current = "Assets";
            string[] parts = folder.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            var created = CreateInstance<AsteroidFieldSettings>();
            AssetDatabase.CreateAsset(created,
                AssetDatabase.GenerateUniqueAssetPath(folder + "/AsteroidFieldSettings.asset"));
            AssetDatabase.SaveAssets();
            settings = created;
            Selection.activeObject = created;
        }
    }
}
