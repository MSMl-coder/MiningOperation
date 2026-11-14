// Mining Operation: low-cost space lighting prototype for Unity 6 URP.
// Place this file in an Editor folder. Menu: Tools > Mining Operation > Apply Space Lighting.
// The utility modifies ONLY the active scene's environment, camera post-processing,
// and the MO_PrototypeLighting objects it owns. Save a copy of the scene first.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MiningOperation.EditorTools
{
    public static class MiningOperationVisualSetup
    {
        private const string TextureName = "MO_BlueSpace_2K.png";
        private const string LightingRootName = "MO_PrototypeLighting";

        [MenuItem("Tools/Mining Operation/Apply Space Lighting (URP)")]
        public static void ApplySpaceLighting()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Mining Operation", "Exit Play Mode before applying the scene setup.", "OK");
                return;
            }

            Texture2D texture = FindSkyboxTexture();
            if (texture == null)
            {
                EditorUtility.DisplayDialog("Missing texture", "Could not find MO_BlueSpace_2K.png anywhere under Assets. Import the Textures folder first.", "OK");
                return;
            }

            // Generate next to the imported texture. No fixed Assets/MiningOperation path.
            string generated = ResolveGeneratedFolder(texture);
            EnsureFolderExists(generated);
            ConfigureTexture(texture);
            Material skybox = GetOrCreateSkybox(texture, generated);

            // Static sky: no procedural calculations or runtime scripts.
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#426076");
            RenderSettings.ambientEquatorColor = Hex("#294B62");
            RenderSettings.ambientGroundColor = Hex("#172C3D");
            RenderSettings.ambientIntensity = 1f;

            // Stylized distance haze on supported lit materials. No volumetric fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("#0C3E58");
            RenderSettings.fogStartDistance = 220f;
            RenderSettings.fogEndDistance = 1000f;

            GameObject root = GameObject.Find(LightingRootName);
            if (root == null)
            {
                root = new GameObject(LightingRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Mining Operation Lighting");
            }

            Light main = GetOrCreateChildLight(root.transform);
            main.type = LightType.Directional;
            main.color = Hex("#D2E9FF");
            main.intensity = 1.25f;
            main.shadows = LightShadows.Soft;
            main.shadowStrength = 0.60f;
            main.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            RenderSettings.sun = main;

            Volume volume = GetOrCreateVolume(root.transform);
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.sharedProfile = GetOrCreateProfile(generated);

            // Camera: render skybox and apply the scene's Global Volume.
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                Undo.RecordObject(cam, "Mining Operation camera background");
                cam.clearFlags = CameraClearFlags.Skybox;
                var urpData = cam.GetUniversalAdditionalCameraData();
                Undo.RecordObject(urpData, "Mining Operation post-processing");
                urpData.renderPostProcessing = true;
                // Ensure camera Volume Mask includes the Default layer (root is Default).
                urpData.volumeLayerMask |= 1;
                EditorUtility.SetDirty(cam);
                EditorUtility.SetDirty(urpData);
            }
            else
            {
                Debug.LogWarning("[MO Visual] No camera found. Set your camera Background Type = Skybox and Post Processing = On manually.");
            }

            EditorUtility.SetDirty(skybox);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[MO Visual] Space Lighting applied. Check URP Asset: Main Light Cast Shadows + Soft Shadows (Low). Existing lights and volumes were NOT deleted.");
            EditorUtility.DisplayDialog("Mining Operation", "Blue Space Skybox, ambient, key light, linear fog and URP Global Volume are ready.\n\nCheck Main Light shadows and Soft Shadows = Low in your URP Asset.", "OK");
        }

        private static Texture2D FindSkyboxTexture()
        {
            // Try a texture next to this tool first, to avoid selecting an old copy.
            string scriptRoot = null;
            foreach (string guid in AssetDatabase.FindAssets("MiningOperationVisualSetup t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                const string suffix = "/Editor/MiningOperationVisualSetup.cs";
                if (path.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                {
                    scriptRoot = path.Substring(0, path.Length - suffix.Length);
                    break;
                }
            }
            if (!string.IsNullOrEmpty(scriptRoot))
            {
                Texture2D local = AssetDatabase.LoadAssetAtPath<Texture2D>(scriptRoot + "/Textures/" + TextureName);
                if (local != null) return local;
            }

            foreach (string guid in AssetDatabase.FindAssets("MO_BlueSpace_2K t:Texture2D"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/" + TextureName, System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        private static string ResolveGeneratedFolder(Texture2D texture)
        {
            string texturePath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(texturePath) || !texturePath.StartsWith("Assets/", System.StringComparison.Ordinal))
                throw new System.InvalidOperationException("Skybox texture must be an asset under Assets/.");

            string textureFolder = System.IO.Path.GetDirectoryName(texturePath).Replace('\\', '/');
            const string suffix = "/Textures";
            string root = textureFolder.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)
                ? textureFolder.Substring(0, textureFolder.Length - suffix.Length)
                : textureFolder;
            return root + "/Generated";
        }

        private static void EnsureFolderExists(string generatedFolder)
        {
            if (AssetDatabase.IsValidFolder(generatedFolder)) return;
            string parent = System.IO.Path.GetDirectoryName(generatedFolder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
                throw new System.IO.DirectoryNotFoundException("Asset folder is missing: " + parent);
            string guid = AssetDatabase.CreateFolder(parent, "Generated");
            if (string.IsNullOrEmpty(guid) || !AssetDatabase.IsValidFolder(generatedFolder))
                throw new System.IO.IOException("Could not create asset folder: " + generatedFolder);
        }

        private static void ConfigureTexture(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool changed = importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
                           importer.maxTextureSize != 2048 || importer.textureCompression != TextureImporterCompression.CompressedHQ;
            if (!changed) return;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static Material GetOrCreateSkybox(Texture2D texture, string generatedFolder)
        {
            string path = generatedFolder + "/MO_BlueSpace_Skybox.mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                Shader shader = Shader.Find("Skybox/Panoramic");
                if (shader == null) throw new System.InvalidOperationException("Cannot find Skybox/Panoramic shader.");
                result = new Material(shader) { name = "MO_BlueSpace_Skybox" };
                AssetDatabase.CreateAsset(result, path);
            }
            result.SetTexture("_MainTex", texture);
            result.SetFloat("_Mapping", 1f); // Latitude Longitude Layout
            result.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            result.SetFloat("_ImageType", 0f); // 360 degrees
            result.SetFloat("_Layout", 0f); // None
            result.SetFloat("_Exposure", 1.0f);
            result.SetFloat("_Rotation", 0f);
            result.SetColor("_Tint", Color.gray);
            return result;
        }

        private static Light GetOrCreateChildLight(Transform parent)
        {
            Transform existing = parent.Find("MO_KeyLight");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("MO_KeyLight");
                Undo.RegisterCreatedObjectUndo(go, "Create Mining Operation Key Light");
                go.transform.SetParent(parent, false);
            }
            Light light = go.GetComponent<Light>();
            if (light == null) light = Undo.AddComponent<Light>(go);
            return light;
        }

        private static Volume GetOrCreateVolume(Transform parent)
        {
            Transform existing = parent.Find("MO_GlobalVolume");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("MO_GlobalVolume");
                Undo.RegisterCreatedObjectUndo(go, "Create Mining Operation Global Volume");
                go.transform.SetParent(parent, false);
            }
            Volume volume = go.GetComponent<Volume>();
            if (volume == null) volume = Undo.AddComponent<Volume>(go);
            return volume;
        }

        private static VolumeProfile GetOrCreateProfile(string generatedFolder)
        {
            string path = generatedFolder + "/MO_PrototypeSpace_Volume.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.TryGet(out Tonemapping tone)) tone = profile.Add<Tonemapping>(true);
            tone.active = true;
            tone.mode.Override(TonemappingMode.ACES);

            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.12f);
            bloom.scatter.Override(0.55f);

            if (!profile.TryGet(out ColorAdjustments grading)) grading = profile.Add<ColorAdjustments>(true);
            grading.active = true;
            grading.postExposure.Override(0f);
            grading.contrast.Override(5f);
            grading.saturation.Override(-8f);
            grading.colorFilter.Override(Color.white);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Color Hex(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out Color c) ? c : Color.white;
        }
    }
}
