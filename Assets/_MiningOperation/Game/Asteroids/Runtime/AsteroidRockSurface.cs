using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// URP/Lit rock surface without run-time noise texture generation. Imported
    /// 1K albedo and GL tangent-space normal maps are reused by all rocks.
    /// Mesh UVs cover the atlas once, so there is no repeated texture pattern.
    /// </summary>
    internal static class AsteroidRockSurface
    {
        public static void Apply(Material material, Texture2D albedo, Texture2D normals,
            Color tint, float strength)
        {
            if (material == null || !material.HasProperty("_BaseMap")) return;
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BaseMap", albedo != null ? albedo : Texture2D.whiteTexture);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            if (material.HasProperty("_BumpMap"))
            {
                if (normals != null && strength > 0f)
                {
                    material.SetTexture("_BumpMap", normals);
                    material.SetTextureScale("_BumpMap", Vector2.one);
                    material.SetTextureOffset("_BumpMap", Vector2.zero);
                    material.SetFloat("_BumpScale", Mathf.Clamp(strength, 0f, 0.5f));
                    material.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    material.SetTexture("_BumpMap", null);
                    material.DisableKeyword("_NORMALMAP");
                }
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .045f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        }
    }
}
