using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Shared procedural rocky albedo/normal textures for safe URP/Lit rendering.
    /// Avoids compiling a new shader variant at runtime or allocating textures per rock.
    /// Real geometry makes macro-craters; normal map supplies micro-surface variation.
    /// </summary>
    internal static class AsteroidRockSurface
    {
        private static Texture2D albedo;
        private static Texture2D normals;
        private const int Size = 256;

        public static void Apply(Material material)
        {
            if (material == null || !material.HasProperty("_BaseMap")) return;
            EnsureTextures();
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normals);
                material.SetFloat("_BumpScale", 0.72f);
                material.EnableKeyword("_NORMALMAP");
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.06f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        }

        private static void EnsureTextures()
        {
            if (albedo != null && normals != null) return;
            albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
            {
                name = "MO Procedural Rock Albedo", wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave,
                anisoLevel = 2
            };
            normals = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true)
            {
                name = "MO Procedural Rock Normal", wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave,
                anisoLevel = 2
            };
            var colorData = new Color32[Size * Size];
            var normalData = new Color32[Size * Size];
            // Periodic trigonometric components help avoid strong tiling discontinuities.
            float[,] height = new float[Size, Size];
            for (int y = 0; y < Size; ++y)
                for (int x = 0; x < Size; ++x)
                {
                    float u = x / (float)Size, v = y / (float)Size;
                    float broad = Mathf.PerlinNoise(u * 5.1f + 11.8f, v * 5.1f + 3.9f);
                    float mid = Mathf.PerlinNoise(u * 21f + 8.6f, v * 21f + 30.7f);
                    float fine = Mathf.PerlinNoise(u * 83f + 47f, v * 83f + 13f);
                    float h = broad * 0.45f + mid * 0.35f + fine * 0.20f;
                    height[x, y] = h;
                    float cavity = Mathf.Clamp01((h - 0.25f) * 1.8f);
                    float tint = Mathf.Lerp(0.30f, 0.72f, cavity);
                    colorData[y * Size + x] = new Color(tint * 0.98f,
                        tint * 0.95f, tint * 0.88f, 1f);
                }
            for (int y = 0; y < Size; ++y)
                for (int x = 0; x < Size; ++x)
                {
                    float dx = height[(x + 1) % Size, y] - height[(x + Size - 1) % Size, y];
                    float dy = height[x, (y + 1) % Size] - height[x, (y + Size - 1) % Size];
                    Vector3 n = new Vector3(-dx * 13f, -dy * 13f, 1f).normalized;
                    byte nx = (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f);
                    byte ny = (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f);
                    byte nz = (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f);
                    // Both red and alpha store X for URP normal unpack paths.
                    normalData[y * Size + x] = new Color32(nx, ny, nz, nx);
                }
            albedo.SetPixels32(colorData);
            normals.SetPixels32(normalData);
            albedo.Apply(true, true);
            normals.Apply(true, true);
        }
    }
}
