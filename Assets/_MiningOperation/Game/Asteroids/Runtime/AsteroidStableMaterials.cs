using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Default path is the shipping URP/Lit shader (no custom shader variants,
    /// no magenta rocks). Shared imported textures, NOT new textures per rock.
    /// </summary>
    public static class AsteroidStableMaterials
    {
        public static bool IsUnified(Material material) => material != null && material.shader != null &&
            material.shader.name == "Mining Operation/Asteroid Unified Surface";

        public static Material Create(Material requested, bool allowExperimental, Color color, string name,
            Texture2D rockAlbedo = null, Texture2D rockNormal = null, float normalStrength = 0.18f)
        {
            Shader standard = Shader.Find("Universal Render Pipeline/Lit");
            bool usableCustom = requested != null && requested.shader != null && requested.shader.isSupported &&
                requested.shader.name != "Hidden/InternalErrorShader";
            Material result;
            bool unified = usableCustom && requested.shader.name == "Mining Operation/Asteroid Unified Surface";
            if (unified || (allowExperimental && usableCustom))
                result = new Material(requested);
            else if (standard != null && standard.isSupported)
            {
                bool trustedLit = requested != null && requested.shader == standard &&
                    requested.shader.isSupported;
                result = trustedLit ? new Material(requested) : new Material(standard);
                // Ice uses an authored URP/Lit material; do not overwrite it with rock maps.
                // Rock surfaces retain their explicit single-wrap albedo/normal textures.
                if (rockAlbedo != null || !trustedLit)
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
            if (unified)
            {
                // Shared authored settings are never modified; only the controller-owned clone.
                result.SetFloat("_MO_LODVisibility", 1f);
                result.SetFloat("_MO_ObjectRadius", 1f);
            }
            result.name = name;
            result.hideFlags = HideFlags.DontSave;
            result.enableInstancing = true;
            return result;
        }
    }
}
