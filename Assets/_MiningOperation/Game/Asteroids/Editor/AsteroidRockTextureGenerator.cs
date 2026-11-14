using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>Generates a tileable (edge blended) 512x512 rock detail texture as an asset.
    /// No third-party texture assets or shader packages required.</summary>
    public static class AsteroidRockTextureGenerator
    {
        private const string Root = "Assets/_MiningOperation/AsteroidTestAssets";

        public static void UpgradeAll()
        {
            EnsureFolder();
            var ids = AssetDatabase.FindAssets("t:AsteroidFieldSettings");
            int changed = 0;
            foreach (string id in ids)
            {
                var field = AssetDatabase.LoadAssetAtPath<AsteroidFieldSettings>(AssetDatabase.GUIDToAssetPath(id));
                if (field == null) continue;
                if (field.asteroidMaterial == null)
                {
                    var shader = Shader.Find("Mining Operation/Asteroid Rock URP");
                    if (shader == null) { Debug.LogError("Import AsteroidRockURP.shader before upgrading materials."); return; }
                    Material newMat = new Material(shader) { name = "AsteroidRock_HQ" };
                    AssetDatabase.CreateAsset(newMat,
                        AssetDatabase.GenerateUniqueAssetPath(Root + "/AsteroidRock_HQ.mat"));
                    Undo.RecordObject(field, "Assign asteroid material");
                    field.asteroidMaterial = newMat;
                    EditorUtility.SetDirty(field);
                }
                UpgradeMaterial(field.asteroidMaterial);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[MO] Upgraded near rock material on " + changed + " field settings. Far material unchanged.");
        }

        public static void UpgradeMaterial(Material material)
        {
            if (material == null) return;
            Shader shader = Shader.Find("Mining Operation/Asteroid Rock URP");
            if (shader == null)
            {
                Debug.LogWarning("[MO] Asteroid Rock shader unavailable. Keeping existing material.");
                return;
            }
            Undo.RecordObject(material, "Improve asteroid rock material");
            material.shader = shader;
            var texture = GetOrCreateTexture();
            if (material.HasProperty("_TriplanarRock")) material.SetTexture("_TriplanarRock", texture);
            if (material.HasProperty("_TriplanarScale")) material.SetFloat("_TriplanarScale", 0.035f);
            if (material.HasProperty("_TriplanarBlend")) material.SetFloat("_TriplanarBlend", 0.68f);
            if (material.HasProperty("_DetailStrength")) material.SetFloat("_DetailStrength", 0.84f);
            if (material.HasProperty("_ReliefStrength")) material.SetFloat("_ReliefStrength", 0.25f);
            if (material.HasProperty("_BaseRockColor")) material.SetColor("_BaseRockColor", new Color(0.39f, 0.37f, 0.35f));
            if (material.HasProperty("_DarkRockColor")) material.SetColor("_DarkRockColor", new Color(0.16f, 0.15f, 0.15f));
            if (material.HasProperty("_BrightRockColor")) material.SetColor("_BrightRockColor", new Color(0.56f, 0.53f, 0.49f));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        private static Texture2D GetOrCreateTexture()
        {
            EnsureFolder();
            const string path = Root + "/AsteroidRockDetail512.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int n = 512;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true)
            { name = "AsteroidRockDetail512", wrapMode = TextureWrapMode.Repeat,
              filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var colors = new Color[n * n];
            // Four-corner blending makes fractal samples tileable while preserving detail.
            for (int y = 0; y < n; ++y)
            for (int x = 0; x < n; ++x)
            {
                float u = x / (float)(n - 1);
                float v = y / (float)(n - 1);
                float n0 = TileNoise(u, v, 3f, 13f, 24f);
                float n1 = TileNoise(u, v, 12f, 22f, 6f);
                float n2 = TileNoise(u, v, 48f, 36f, 29f);
                float vein = Mathf.Pow(1f - Mathf.Abs(2f * n1 - 1f), 7f);
                float intensity = Mathf.Clamp01(0.36f + n0 * 0.46f + n1 * 0.28f + n2 * 0.09f - vein * 0.10f);
                float warm = Mathf.Clamp01(n0 * 0.3f + n1 * 0.2f);
                colors[y * n + x] = new Color(intensity * Mathf.Lerp(0.95f, 1.05f, warm),
                    intensity * 0.97f, intensity * Mathf.Lerp(1.03f, 0.96f, warm), 1f);
            }
            tex.SetPixels(colors); tex.Apply(true, false);
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }
        private static float TileNoise(float u, float v, float scale, float ox, float oy)
        {
            float a = Mathf.PerlinNoise(u * scale + ox, v * scale + oy);
            float b = Mathf.PerlinNoise((u - 1f) * scale + ox, v * scale + oy);
            float c = Mathf.PerlinNoise(u * scale + ox, (v - 1f) * scale + oy);
            float d = Mathf.PerlinNoise((u - 1f) * scale + ox, (v - 1f) * scale + oy);
            return Mathf.Lerp(Mathf.Lerp(a,b,u), Mathf.Lerp(c,d,u), v);
        }
        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_MiningOperation")) AssetDatabase.CreateFolder("Assets", "_MiningOperation");
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/_MiningOperation", "AsteroidTestAssets");
        }
    }
}
