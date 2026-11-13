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

        [MenuItem("Tools/Mining Operation/Asteroid Generator Studio")]
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
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("MINING OPERATION / ASTEROID STUDIO", EditorStyles.boldLabel);
            settings = (AsteroidGeneratorSettings)EditorGUILayout.ObjectField("Generator Settings", settings, typeof(AsteroidGeneratorSettings), false);
            material = (Material)EditorGUILayout.ObjectField("Surface Material", material, typeof(Material), false);
            seed = EditorGUILayout.IntField("Seed", seed);
            forceClass = EditorGUILayout.Toggle("Override Class", forceClass);
            using (new EditorGUI.DisabledScope(!forceClass))
                asteroidClass = (AsteroidClass)EditorGUILayout.EnumPopup("Class", asteroidClass);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Default Assets")) CreateAssets();
            using (new EditorGUI.DisabledScope(settings == null || settings.catalog == null))
            {
                if (GUILayout.Button("Generate Preview")) GeneratePreview();
                if (GUILayout.Button("Random Seed")) { seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue); GeneratePreview(); }
            }
            EditorGUILayout.EndHorizontal();
            Rect area = GUILayoutUtility.GetRect(100f, 340f, GUILayout.ExpandWidth(true));
            GUI.Box(area, GUIContent.none);
            if (previewMesh != null && Event.current.type == EventType.Repaint) RenderPreview(area);
            if (area.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDrag && Event.current.button == 0)
            {
                rotation += Event.current.delta * 0.6f; Event.current.Use(); Repaint();
            }
            if (area.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom + Event.current.delta.y * 0.12f, 1.5f, 8f); Event.current.Use(); Repaint();
            }
            EditorGUILayout.HelpBox("Drag to rotate; scroll to zoom. Preview is not saved to the scene.", MessageType.None);
            if (lastData != null)
            {
                EditorGUILayout.LabelField("ID", lastData.asteroidId);
                EditorGUILayout.LabelField("Class", lastData.asteroidClass.ToString());
                EditorGUILayout.LabelField("Diameter (nominal)", lastData.diameterMeters.ToString("F2") + " m");
                EditorGUILayout.LabelField("Volume", lastData.volumeM3.ToString("N1") + " m³");
                EditorGUILayout.LabelField("Mass", (lastData.initialMassKg / 1000.0).ToString("N1") + " t");
                EditorGUILayout.LabelField("Bulk density", lastData.densityKgM3.ToString("N0") + " kg/m³");
                EditorGUILayout.LabelField("Porosity", (lastData.porosity01 * 100).ToString("F1") + "%");
                EditorGUILayout.LabelField("Geometry version", lastData.generatorVersion.ToString());
                EditorGUILayout.LabelField("LOD0 triangles", (previewMesh != null ? (previewMesh.triangles.Length / 3).ToString("N0") : "-"));
                EditorGUILayout.LabelField("Composition", EditorStyles.boldLabel);
                foreach (var part in lastData.composition)
                    EditorGUILayout.LabelField(part.resourceId, (part.fraction * 100).ToString("F2") + "%");
                EditorGUILayout.Space(8);
                if (GUILayout.Button("Create Asteroid In Scene")) CreateSceneAsteroid();
                if (GUILayout.Button("Export Mesh Asset")) ExportMesh();
            }
            EditorGUILayout.EndScrollView();
        }

        private void GeneratePreview()
        {
            try
            {
                lastData = AsteroidGeneration.Create(seed, settings, forceClass ? asteroidClass : (AsteroidClass?)null);
                if (previewMesh != null) DestroyImmediate(previewMesh);
                previewMesh = AsteroidGeometryGenerator.Generate(lastData, settings, settings.lod0Subdivisions);
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
            const string folder = "Assets/MiningOperation/AsteroidData";
            EnsureFolder(folder);
            string catalogPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/AsteroidResourceCatalog.asset");
            var catalog = AsteroidResourceCatalog.CreateDefaultRuntime();
            catalog.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(catalog, catalogPath);
            var newSettings = CreateInstance<AsteroidGeneratorSettings>();
            newSettings.catalog = catalog;
            string settingsPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/AsteroidGeneratorSettings.asset");
            AssetDatabase.CreateAsset(newSettings, settingsPath);
            string materialPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/AsteroidSurface.mat");
            Shader shader = FindAvailableShader();
            if (shader != null)
            {
                var newMaterial = new Material(shader);
                if (newMaterial.HasProperty("_BaseColor"))
                    newMaterial.SetColor("_BaseColor", new Color(0.43f, 0.40f, 0.35f));
                if (newMaterial.HasProperty("_Color"))
                    newMaterial.SetColor("_Color", new Color(0.43f, 0.40f, 0.35f));
                AssetDatabase.CreateAsset(newMaterial, materialPath);
                material = newMaterial;
            }
            AssetDatabase.SaveAssets();
            settings = newSettings;
            Selection.activeObject = newSettings;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string current = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(current)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = current;
            }
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
