using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
                    if (System.IO.File.Exists(Folder + "/MO_SpaceGradient.mat"))
                        throw new InvalidOperationException("Existing sky material could not be loaded. Repair it; do not overwrite it.");
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

            ApplyCameraSettings(profile);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
            Debug.Log("[MO] Repaired sky, one key + one fill light, planet disabled, legacy auto visuals cleaned. No asteroid snapshot changed.");
        }

        private static void ApplyCameraSettings(AsteroidEnvironmentProfile profile)
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                var mainData = camera.GetComponent<UniversalAdditionalCameraData>();
                if (mainData != null && mainData.renderType == CameraRenderType.Overlay)
                    camera = null;
            }
            if (camera == null)
            {
                Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    var candidate = cameras[i];
                    if (candidate == null || !candidate.enabled || candidate.targetTexture != null) continue;
                    var urp = candidate.GetComponent<UniversalAdditionalCameraData>();
                    if (urp != null && urp.renderType == CameraRenderType.Overlay) continue;
                    if (camera == null || candidate.depth > camera.depth) camera = candidate;
                }
            }
            if (camera == null)
            {
                Debug.LogWarning("[MO] No active Base gameplay camera. The sky profile is installed, but a Game camera must clear to Skybox to display it.");
                return;
            }
            Undo.RecordObject(camera, "Apply space camera clear");
            camera.clearFlags = profile.gradientSkyMaterial != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = profile.lowerColor;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 40000f);
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                if (cameraData.renderType == CameraRenderType.Overlay)
                    Debug.LogWarning("[MO] The MainCamera is an URP Overlay camera. Background skybox is drawn by the Base camera; change that Base camera's clear mode.");
                // Do not override post-processing; the owning game camera controls it.
            }
            EditorUtility.SetDirty(camera);
        }

        /// <summary>Only named obsolete automatically generated visuals are removed.
        /// Active gameplay/spawn point roots are never matched.</summary>
        private static void RemoveOldVisuals(GameObject environmentToKeep)
        {
            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            string[] disabled = { "MO Distant Planet (background)", "MO_PrototypeLighting", "MO_KeyLight", "MO_KeyLight (1)",
                "MO_GlobalVolume", "MO V2 Color Grading", "MO V2 Color Grading (1)" };
            foreach (Transform t in all)
            {
                if (t == null || t.gameObject == environmentToKeep) continue;
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
            if (System.IO.File.Exists(path))
                throw new InvalidOperationException("Cannot load existing environment asset " + path +
                    ". Repair its script instead of creating a replacement.");
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
