using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// One idempotent scene setup/repair flow. Does not rebuild any asteroid or touch mining state.
    /// Legacy auto-generated visual objects are removed only by exact name after an explicit click.
    /// </summary>
    public static class AsteroidV2SceneSetup
    {
        private const string Folder = "Assets/_MiningOperation/AsteroidVisualV2";

        // Kept for existing integrations; not exposed as a separate top-level menu.
        public static void InstallEnvironment()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Edit Mode required", "Leave Play Mode before repairing the scene.", "OK");
                return;
            }
            EnsureFolder();
            AsteroidEnvironmentProfile profile = LoadOrCreate<AsteroidEnvironmentProfile>(Folder + "/MO_SpaceEnvironment.asset");
            Undo.RecordObject(profile, "Repair Mining Operation space profile");
            profile.showPlanet = false;
            // Repair old .mat as well, because a previously created material may still use
            // an obsolete/unavailable shader after overwriting the shader source.
            var gradientShader = Shader.Find("Mining Operation/Space Gradient Sky");
            Material gradientMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/MO_SpaceGradient.mat");
            if (gradientShader != null && gradientShader.isSupported)
            {
                if (gradientMaterial == null)
                {
                    gradientMaterial = new Material(gradientShader);
                    AssetDatabase.CreateAsset(gradientMaterial, Folder + "/MO_SpaceGradient.mat");
                }
                else
                {
                    Undo.RecordObject(gradientMaterial, "Repair gradient shader");
                    gradientMaterial.shader = gradientShader;
                }
                profile.gradientSkyMaterial = gradientMaterial;
                EditorUtility.SetDirty(gradientMaterial);
            }
            else
            {
                profile.gradientSkyMaterial = null; // Environment uses safe solid-blue fallback.
                Debug.LogWarning("[MO] Space gradient shader unavailable. Game camera will use a solid blue background instead of magenta.");
            }
            EditorUtility.SetDirty(profile);

            var env = UnityEngine.Object.FindFirstObjectByType<AsteroidSpaceEnvironment>(FindObjectsInactive.Include);
            if (env == null)
            {
                var go = new GameObject("MO Space Environment V2");
                Undo.RegisterCreatedObjectUndo(go, "Add Mining environment");
                env = Undo.AddComponent<AsteroidSpaceEnvironment>(go);
            }
            if (!env.gameObject.activeSelf)
            {
                Undo.RecordObject(env.gameObject, "Activate Mining environment");
                env.gameObject.SetActive(true);
            }
            Undo.RecordObject(env, "Repair Mining environment");
            env.profile = profile;
            env.keyLight = GetOrCreateLight("MO V2 Sun (Key)");
            env.fillLight = GetOrCreateLight("MO V2 Ambient Fill");
            env.distantPlanet = null;

            // Old visual components only. NEVER clean asteroid records, authoring anchors,
            // cameras, authored lights, or any object with an unknown name.
            RemoveOldVisuals(env.gameObject);
            env.Apply();

            Camera camera = Camera.main;
            if (camera == null) camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                Undo.RecordObject(camera, "Repair Mining camera");
                camera.clearFlags = profile.gradientSkyMaterial != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                camera.backgroundColor = profile.lowerColor;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 40000f);
                var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                // Don't force the addition of URP Additional Camera Data or multiple Volumes.
                if (cameraData != null) cameraData.renderPostProcessing = false;
                EditorUtility.SetDirty(camera);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
            Debug.Log("[MO] Repaired sky, one key + one fill light, planet disabled, legacy auto visuals cleaned. No asteroid snapshot changed.");
        }

        /// <summary>Only named obsolete automatically generated visuals are removed.
        /// Active gameplay/spawn point roots are never matched.</summary>
        private static void RemoveOldVisuals(GameObject environmentToKeep)
        {
            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            string[] deleted = { "MO Distant Planet (background)" };
            string[] disabled = { "MO_PrototypeLighting", "MO_KeyLight", "MO_KeyLight (1)",
                "MO_GlobalVolume", "MO V2 Color Grading", "MO V2 Color Grading (1)" };
            foreach (Transform t in all)
            {
                if (t == null || t.gameObject == environmentToKeep) continue;
                if (Array.IndexOf(deleted, t.gameObject.name) >= 0)
                {
                    Undo.DestroyObjectImmediate(t.gameObject);
                    continue;
                }
                if (Array.IndexOf(disabled, t.gameObject.name) >= 0 && t.gameObject.activeSelf)
                {
                    Undo.RecordObject(t.gameObject, "Disable obsolete Mining visuals");
                    t.gameObject.SetActive(false);
                }
                // Also disable extra generated environment controllers, not user-created objects.
                if (t.gameObject.name == "MO Space Environment V2" && t.gameObject != environmentToKeep && t.gameObject.activeSelf)
                {
                    Undo.RecordObject(t.gameObject, "Disable duplicate Mining environment");
                    t.gameObject.SetActive(false);
                }
            }
        }

        private static Light GetOrCreateLight(string name)
        {
            GameObject go = GameObject.Find(name);
            Light light = go != null ? go.GetComponent<Light>() : null;
            if (light != null) return light;
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Mining light");
            return Undo.AddComponent<Light>(go);
        }

        public static void ApplyBalancedPreset(AsteroidFieldSpawnPoint anchor)
        {
            if (anchor == null || anchor.settings == null) return;
            if (!EditorUtility.DisplayDialog("Balanced belt performance preset",
                "Update the SHARED Field Settings asset? Other Spawn Points using this asset will also change. " +
                "Existing generated asteroid records and mining state remain untouched.", "Apply", "Cancel")) return;
            AsteroidFieldSettings s = anchor.settings;
            Undo.RecordObject(s, "Balanced Mining field visuals");
            s.useGpuFarField = true;
            s.enableDecorativeDebris = true;
            s.decorativeRockCount = 1600;
            s.microFragmentCount = 0; // costly and visually low value at present
            s.debrisMeshVariants = 8;
            s.debrisDrawDistance = 9000f;
            s.distantMeshVariants = 12;
            s.distantSubdivisions = 1;
            s.maxDetailedAsteroids = 24;
            s.maxActiveAsteroids = Mathf.Max(s.maxActiveAsteroids, 24);
            s.detailedDistanceMeters = 2200f;
            s.instantiatePerFrame = 2;
            s.streamingTickSeconds = 0.25f;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Debug.Log("[MO] Balanced GPU/debris preset applied. No field regeneration. Re-enter Play Mode to recreate visual buffers.");
        }

        // Kept callable by other tools. Frames an actual asteroid (not a guessed point on an arc).
        public static void FrameBelt()
        {
            var anchor = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<AsteroidFieldSpawnPoint>() : null;
            if (anchor == null) anchor = UnityEngine.Object.FindFirstObjectByType<AsteroidFieldSpawnPoint>();
            if (anchor == null) return;
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam == null) { Debug.LogWarning("[MO] No game camera to frame."); return; }
            var snapshot = anchor.Controller != null ? anchor.Controller.Snapshot : null;
            if (snapshot == null || snapshot.asteroids == null || snapshot.asteroids.Count == 0)
            {
                Debug.LogWarning("[MO] Build a field first, then frame a real asteroid.");
                return;
            }
            var entry = snapshot.asteroids[snapshot.asteroids.Count / 2];
            if (entry == null || entry.asteroid == null) return;
            Vector3 world = anchor.transform.TransformPoint(entry.localPosition);
            float distance = Mathf.Max(140f, entry.asteroid.diameterMeters * 1.6f);
            Undo.RecordObject(cam.transform, "Frame actual asteroid");
            cam.transform.position = world + anchor.transform.TransformDirection(new Vector3(0.45f, 0.28f, -1f)).normalized * distance;
            cam.transform.LookAt(world);
            EditorUtility.SetDirty(cam.transform);
            if (anchor.Controller != null)
            {
                Undo.RecordObject(anchor.Controller, "Use test camera for asteroid streaming");
                anchor.Controller.Viewer = cam.transform;
                EditorUtility.SetDirty(anchor.Controller);
            }
            EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        }

        public static void UpgradeRockMaterials() => AsteroidRockTextureGenerator.UpgradeAll();

        public static void RemoveObsoleteEditorTools()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorUtility.DisplayDialog("Remove obsolete tools?",
                "Remove only old test-scene and global LOD preset scripts. Their functions now live in Field Studio. " +
                "Scene objects, snapshots, resources and active authoring scripts will NOT be deleted.",
                "Remove 2 scripts", "Cancel")) return;
            string baseFolder = "Assets/_MiningOperation/Game/Asteroids/Editor/";
            string[] names = { "AsteroidTestEnvironmentSetup.cs", "AsteroidGraphicsPreset.cs" };
            int removed = 0;
            foreach (var name in names)
                if (AssetDatabase.LoadAssetAtPath<MonoScript>(baseFolder + name) != null &&
                    AssetDatabase.DeleteAsset(baseFolder + name)) removed++;
            AssetDatabase.Refresh();
            Debug.Log("[MO] Removed " + removed + " obsolete editor scripts. Active authoring workflows kept.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_MiningOperation"))
                AssetDatabase.CreateFolder("Assets", "_MiningOperation");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_MiningOperation", "AsteroidVisualV2");
        }
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
