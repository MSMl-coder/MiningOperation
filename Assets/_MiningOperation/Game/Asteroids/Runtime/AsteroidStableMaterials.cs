using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Conservative URP/Lit rendering fallback. Temporary until custom shader variants
    /// have been validated on every target graphics API. One material per renderer pool,
    /// never one material per asteroid. Does not modify serialized material assets.
    /// </summary>
    public static class AsteroidStableMaterials
    {
        public static Material Create(Material requested, bool allowExperimental, Color color, string name)
        {
            Shader standard = Shader.Find("Universal Render Pipeline/Lit");
            bool usableCustom = requested != null && requested.shader != null && requested.shader.isSupported &&
                requested.shader.name != "Hidden/InternalErrorShader";
            Material result;
            if (allowExperimental && usableCustom)
                result = new Material(requested);
            else if (standard != null && standard.isSupported)
            {
                result = new Material(standard);
                result.SetColor("_BaseColor", color);
                result.SetFloat("_Metallic", 0f);
                result.SetFloat("_Smoothness", 0.10f);
                AsteroidRockSurface.Apply(result);
                if (result.HasProperty("_EmissionColor"))
                {
                    result.SetColor("_EmissionColor", new Color(0.035f, 0.045f, 0.052f));
                    result.EnableKeyword("_EMISSION");
                }
            }
            else if (usableCustom)
            {
                Debug.LogWarning("[MO] Universal Render Pipeline/Lit is not available; using the configured material. Check the active URP asset.");
                result = new Material(requested);
            }
            else
            {
                Debug.LogError("[MO] No compatible URP asteroid shader found. Check Graphics and Quality render pipeline assets.");
                return null;
            }
            result.name = name;
            result.hideFlags = HideFlags.DontSave;
            result.enableInstancing = true;
            return result;
        }
    }
}
