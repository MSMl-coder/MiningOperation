using System;
using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    public sealed class AsteroidGeneratorStudio : EditorWindow
    {
        private AsteroidGeneratorSettings settings;
        private Material material;
        private int seed = 12345;
        private bool forceClass;
        private AsteroidClass asteroidClass = AsteroidClass.I;
        private AsteroidData lastData;
        private Vector2 scroll;
        private PreviewRenderUtility preview;
        private Mesh previewMesh;
        private Material previewFallbackMaterial;
        private Vector2 rotation = new Vector2(25f, -25f);
        private float zoom = 2.8f;
        private bool showTuning = true;
        private string lastGeneratedSignature;
        private string lastGuiError;
        private string CurrentSignature => settings == null ? string.Empty :
            seed + "|" + forceClass + "|" + asteroidClass + "|" +
            JsonUtility.ToJson(settings) + "|" +
            (settings.catalog == null ? "" : JsonUtility.ToJson(settings.catalog));

        public static void Open() { GetWindow<AsteroidGeneratorStudio>("Asteroid Studio"); }

        private void OnDisable() { CleanupPreview(); }
        private void CleanupPreview()
        {
            if (previewMesh != null) DestroyImmediate(previewMesh);
            previewMesh = null;
            if (previewFallbackMaterial != null) DestroyImmediate(previewFallbackMaterial);
            previewFallbackMaterial = null;
            if (preview != null) preview.Cleanup();
            preview = null;
        }

        private void OnGUI()
        {
            try
            {
                using (var scope = new EditorGUILayout.ScrollViewScope(scroll))
                {
                    scroll = scope.scrollPosition;
                    DrawAuthoringUI();
                }
                lastGuiError = null;
            }
            catch (ExitGUIException) { throw; }
            catch (Exception e)
            {
                if (lastGuiError != e.Message) Debug.LogException(e, this);
                lastGuiError = e.Message;
            }
        }

        private void DrawAuthoringUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("MINING OPERATION  /  ASTEROID AUTHORING", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Adjust generator settings, then preview a single asteroid.", EditorStyles.miniLabel);
            EditorGUILayout.Space(7);
            EditorGUILayout.LabelField("1  Inputs", EditorStyles.boldLabel);
            settings = (AsteroidGeneratorSettings)EditorGUILayout.ObjectField(
                "Generator Settings", settings, typeof(AsteroidGeneratorSettings), false);
            material = (Material)EditorGUILayout.ObjectField("Surface Material", material,
                typeof(Material), false);
            seed = EditorGUILayout.IntField("Asteroid Seed", seed);
            forceClass = EditorGUILayout.Toggle("Override Resource Class", forceClass);
            using (new EditorGUI.DisabledScope(!forceClass))
                asteroidClass = (AsteroidClass)EditorGUILayout.EnumPopup("Resource Class", asteroidClass);
            if (settings == null || settings.catalog == null)
                EditorGUILayout.HelpBox("Generator Settings and Resource Catalog are required. " +
                    "Use Create Full Starter Assets to make a connected set.", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Full Starter Assets")) CreateAssets();
                using (new EditorGUI.DisabledScope(settings == null))
                {
                    if (GUILayout.Button("Select Generator Settings")) Selection.activeObject = settings;
                }
            }

            if (settings != null)
            {
                EditorGUILayout.Space(8);
                showTuning = EditorGUILayout.Foldout(showTuning, "2  Quick Tuning", true);
                if (showTuning)
                {
                    var options = new SerializedObject(settings);
                    options.Update();
                    DrawProperty(options, "minimumDiameterMeters", "Min Diameter (m)");
                    DrawProperty(options, "maximumDiameterMeters", "Max Diameter (m)");
                    DrawProperty(options, "shapeIrregularity", "Shape Irregularity");
                    DrawProperty(options, "craterIntensity", "Crater Depth / Strength");
                    DrawProperty(options, "surfaceRoughness", "Surface Roughness");
                    DrawProperty(options, "classIIProbability", "Class II Frequency");
                    DrawProperty(options, "classIIIProbability", "Class III Frequency");
                    DrawProperty(options, "lod0Subdivisions", "Preview Detail (LOD0)");
                    options.ApplyModifiedProperties();
                    EditorGUILayout.HelpBox("Shape and material are independent of resource class. " +
                        "For advanced geology and resource tuning, select Generator Settings.", MessageType.None);
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("3  Interactive 3D Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(settings == null || settings.catalog == null))
                {
                    if (GUILayout.Button("Generate Preview", GUILayout.Height(28))) GeneratePreview();
                    if (GUILayout.Button("New Random Seed", GUILayout.Height(28)))
                    {
                        seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                        GeneratePreview();
                    }
                }
                if (GUILayout.Button("Reset View", GUILayout.Height(28), GUILayout.Width(90)))
                {
                    rotation = new Vector2(25f, -25f);
                    zoom = 2.8f;
                    Repaint();
                }
            }
            if (lastData != null && lastGeneratedSignature != CurrentSignature)
                EditorGUILayout.HelpBox("Preview is OUTDATED. Generate Preview to apply the new seed/settings. " +
                    "The previously generated object remains unchanged.", MessageType.Warning);
            Rect area = GUILayoutUtility.GetRect(100f, 350f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(area, new Color(0.085f, 0.11f, 0.155f));
            if (previewMesh != null && Event.current.type == EventType.Repaint) RenderPreview(area);
            if (area.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDrag &&
                Event.current.button == 0)
            {
                rotation += Event.current.delta * 0.6f;
                Event.current.Use();
                Repaint();
            }
            if (area.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom + Event.current.delta.y * 0.12f, 1.5f, 8f);
                Event.current.Use();
                Repaint();
            }
            EditorGUILayout.LabelField("Left drag: rotate    •    Mouse wheel: zoom", EditorStyles.miniLabel);
            if (lastData == null)
            {
                EditorGUILayout.HelpBox("No asteroid preview yet.", MessageType.None);
                return;
            }

            EditorGUILayout.Space(7);
            EditorGUILayout.LabelField("4  Generated Characteristics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID", lastData.asteroidId);
            EditorGUILayout.LabelField("Class", lastData.asteroidClass.ToString());
            EditorGUILayout.LabelField("Diameter", lastData.diameterMeters.ToString("F2") + " m");
            EditorGUILayout.LabelField("Volume", lastData.volumeM3.ToString("N1") + " m³");
            EditorGUILayout.LabelField("Mass", (lastData.initialMassKg / 1000.0).ToString("N1") + " t");
            EditorGUILayout.LabelField("Bulk Density", lastData.densityKgM3.ToString("N0") + " kg/m³");
            EditorGUILayout.LabelField("Porosity", (lastData.porosity01 * 100).ToString("F1") + "%");
            EditorGUILayout.LabelField("Mesh Triangles", previewMesh == null ? "–" :
                (previewMesh.triangles.Length / 3).ToString("N0"));
            if (lastData.composition != null)
                foreach (var part in lastData.composition)
                {
                    EditorGUILayout.LabelField(part.resourceId, (part.fraction * 100).ToString("F2") + "%");
                    Rect track = GUILayoutUtility.GetRect(80f, 4f, GUILayout.ExpandWidth(true));
                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(track, new Color(0.12f, 0.16f, 0.19f));
                        EditorGUI.DrawRect(new Rect(track.x, track.y,
                            track.width * Mathf.Clamp01((float)part.fraction), track.height),
                            new Color(0.34f, 0.73f, 0.84f));
                    }
                }
            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(settings == null || lastGeneratedSignature != CurrentSignature))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Asteroid in Scene", GUILayout.Height(30))) CreateSceneAsteroid();
                if (GUILayout.Button("Export Mesh Asset", GUILayout.Height(30))) ExportMesh();
            }
        }

        private static void DrawProperty(SerializedObject obj, string key, string label)
        {
            SerializedProperty property = obj.FindProperty(key);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private void GeneratePreview()
        {
            try
            {
                lastData = AsteroidGeneration.Create(seed, settings, forceClass ? asteroidClass : (AsteroidClass?)null);
                if (previewMesh != null) DestroyImmediate(previewMesh);
                previewMesh = AsteroidGeometryGenerator.Generate(lastData, settings, settings.lod0Subdivisions);
                lastGeneratedSignature = CurrentSignature;
                Repaint();
            }
            catch (Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("Asteroid Generation Error", ex.Message, "OK"); }
        }

        private void RenderPreview(Rect area)
        {
            if (preview == null)
            {
                preview = new PreviewRenderUtility();
                preview.cameraFieldOfView = 35f;
                preview.lights[0].intensity = 1.5f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, 30, 0);
                preview.lights[1].intensity = 0.7f;
            }
            preview.BeginPreview(area, GUIStyle.none);
            var camera = preview.camera;
            float radius = previewMesh.bounds.extents.magnitude;
            camera.transform.position = new Vector3(0, 0, -Mathf.Max(1f, radius) * zoom);
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = Mathf.Max(0.01f, radius * 0.001f);
            camera.farClipPlane = Mathf.Max(100f, radius * 20f);
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.09f, 0.11f, 0.14f);
            Material drawMaterial = material != null ? material : GetPreviewFallbackMaterial();
            preview.DrawMesh(previewMesh, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(rotation.x, rotation.y, 0), Vector3.one), drawMaterial, 0);
            preview.Render();
            GUI.DrawTexture(area, preview.EndPreview(), ScaleMode.StretchToFill, false);
        }

        private Material GetPreviewFallbackMaterial()
        {
            if (previewFallbackMaterial != null) return previewFallbackMaterial;
            Shader shader = FindAvailableShader();
            if (shader == null)
                throw new InvalidOperationException("No available shader for asteroid preview.");
            previewFallbackMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (previewFallbackMaterial.HasProperty("_BaseColor"))
                previewFallbackMaterial.SetColor("_BaseColor", new Color(0.47f, 0.43f, 0.36f));
            if (previewFallbackMaterial.HasProperty("_Color"))
                previewFallbackMaterial.SetColor("_Color", new Color(0.47f, 0.43f, 0.36f));
            return previewFallbackMaterial;
        }

        private static Shader FindAvailableShader()
        {
            // Do not use ?? with UnityEngine.Object types: Unity can return "fake null".
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            return shader;
        }

        private void CreateAssets()
        {
            if (settings != null && !EditorUtility.DisplayDialog("Create another asset set?",
                "Create a new connected Resource Catalog, Generator Settings and Field Settings? " +
                "Existing assets will be preserved.", "Create New Set", "Cancel")) return;
            AsteroidFieldSettings field = AsteroidAuthoringWorkflow.CreateReadyAssets();
            settings = field.asteroidSettings;
            material = field.asteroidMaterial;
            lastGeneratedSignature = null;
            Selection.activeObject = settings;
            Repaint();
        }

        private void CreateSceneAsteroid()
        {
            // Multiple manual placements of one preview must not collide in persistent saves.
            string id = lastData.asteroidId;
            var used = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (AsteroidInstance existing in UnityEngine.Object.FindObjectsByType<AsteroidInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing.Data != null) used.Add(existing.Data.asteroidId);
                else if (!string.IsNullOrEmpty(existing.PersistentId)) used.Add(existing.PersistentId);
            }
            for (int suffix = 1; used.Contains(id); suffix++)
                id = lastData.asteroidId + "-" + suffix.ToString("D4");
            var placedData = JsonUtility.FromJson<AsteroidData>(JsonUtility.ToJson(lastData));
            placedData.asteroidId = id;
            var go = new GameObject("Asteroid_" + id);
            Undo.RegisterCreatedObjectUndo(go, "Create Asteroid");
            var instance = go.AddComponent<AsteroidInstance>();
            instance.Settings = settings;
            instance.Seed = lastData.seed;
            instance.ForceClass = forceClass;
            instance.ForcedClass = asteroidClass;
            instance.SurfaceMaterial = material;
            instance.Apply(placedData);
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private void ExportMesh()
        {
            string path = EditorUtility.SaveFilePanelInProject("Export Asteroid Mesh", "Asteroid_" + seed,
                "asset", "Choose mesh asset destination");
            if (string.IsNullOrEmpty(path)) return;
            Mesh exported = AsteroidGeometryGenerator.Generate(lastData, settings, settings.lod0Subdivisions);
            AssetDatabase.CreateAsset(exported, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = exported;
        }
    }
}
