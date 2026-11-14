using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Default path is the shipping URP/Lit shader (no custom shader variants,
    /// no magenta rocks). Shared imported textures, NOT new textures per rock.
    /// </summary>
    public static class AsteroidStableMaterials
    {
        public static Material Create(Material requested, bool allowExperimental, Color color, string name,
            Texture2D rockAlbedo = null, Texture2D rockNormal = null, float normalStrength = 0.18f)
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
                AsteroidRockSurface.Apply(result, rockAlbedo, rockNormal, color, normalStrength);
                // Ambient light from the scene is preferred to an emissive glow.
                if (result.HasProperty("_EmissionColor")) result.SetColor("_EmissionColor", new Color(.018f, .023f, .026f));
                result.EnableKeyword("_EMISSION");
            }
            else if (usableCustom)
            {
                Debug.LogWarning("[MO] URP/Lit unavailable. Check the active Render Pipeline asset.");
                result = new Material(requested);
            }
            else
            {
                Debug.LogError("[MO] Missing compatible asteroid shader (URP pipeline not assigned?).");
                return null;
            }
            result.name = name;
            result.hideFlags = HideFlags.DontSave;
            result.enableInstancing = true;
            return result;
        }
    }
}
