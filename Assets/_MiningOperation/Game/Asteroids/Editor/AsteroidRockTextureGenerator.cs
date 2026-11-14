using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// v2.2.1: import the packaged non-repeating rock maps and assign them to
    /// Field Settings. Never switch materials to an experimental shader.
    /// Existing JSON fields, scene controllers and mineable state are untouched.
    /// </summary>
    public static class AsteroidRockTextureGenerator
    {
        private const string Root = "Assets/_MiningOperation/Game/Asteroids/Textures/";
        private const string AlbedoPath = Root + "MO_RockAlbedo_NonRepeating.png";
        private const string NormalPath = Root + "MO_RockNormal_Soft.png";

        public static bool Install(AsteroidFieldSettings field)
        {
            if (field == null) return false;
            ConfigureImport(AlbedoPath, false);
            ConfigureImport(NormalPath, true);
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            if (albedo == null || normal == null)
            {
                Debug.LogError("[MO] Missing v2.2.1 rock PNG assets. Import the PATCH into Assets/_MiningOperation/Game/Asteroids/. Expected: " + Root);
                return false;
            }
            Undo.RecordObject(field, "Install non-tiling asteroid rock surface");
            field.rockAlbedoTexture = albedo;
            field.rockNormalTexture = normal;
            field.rockNormalStrength = .18f;
            field.allowExperimentalRockShaders = false;
            EditorUtility.SetDirty(field);
            AssetDatabase.SaveAssets();
            Debug.Log("[MO] Non-tiling rock maps installed on " + field.name + ". One UV revolution; 0.18 normal strength; standard URP/Lit.");
            return true;
        }

        public static void UpgradeAll()
        {
            var ids = AssetDatabase.FindAssets("t:AsteroidFieldSettings");
            int count = 0;
            foreach (string id in ids)
            {
                var field = AssetDatabase.LoadAssetAtPath<AsteroidFieldSettings>(AssetDatabase.GUIDToAssetPath(id));
                if (Install(field)) count++;
            }
            Debug.Log("[MO] Installed v2.2.1 safe rock maps on " + count + " field settings.");
        }

        // Backwards compatible call from old editor workflow: intentionally no shader swap.
        public static void UpgradeMaterial(Material material)
        {
            if (material == null) return;
            if (material.shader == null || !material.shader.isSupported)
                Debug.LogWarning("[MO] Material has an unsupported shader; v2.2.1 uses safe URP/Lit at runtime. Install the rock maps on Field Settings.");
        }

        private static void ConfigureImport(string path, bool normal)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool dirty = false;
            TextureImporterType desired = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != desired) { importer.textureType = desired; dirty = true; }
            if (normal && importer.convertToNormalmap) { importer.convertToNormalmap = false; dirty = true; }
            if (importer.sRGBTexture != !normal) { importer.sRGBTexture = !normal; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; dirty = true; }
            if (importer.filterMode != FilterMode.Trilinear) { importer.filterMode = FilterMode.Trilinear; dirty = true; }
            if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; dirty = true; }
            if (importer.anisoLevel != 4) { importer.anisoLevel = 4; dirty = true; }
            if (importer.maxTextureSize < 1024) { importer.maxTextureSize = 1024; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }
    }
}
