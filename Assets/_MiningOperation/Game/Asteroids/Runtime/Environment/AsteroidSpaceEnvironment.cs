using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Sets scene-level appearance without editing persisted asteroid geometry or resources.
    /// Add once to the test scene. Editor tooling can create lights, planet, camera and profile.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AsteroidSpaceEnvironment : MonoBehaviour
    {
        public AsteroidEnvironmentProfile profile;
        public Light keyLight;
        public Light fillLight;
        public Renderer distantPlanet;
        [SerializeField] private bool applyInEditMode = true;

        private Material skyInstance;
        private Material previousSky;
        private bool initialized;

        private void OnEnable() { if (Application.isPlaying || applyInEditMode) Apply(); }
        private void OnValidate() { if (isActiveAndEnabled && (Application.isPlaying || applyInEditMode)) Apply(); }
        private void Update()
        {
            // Reapply shader globals in play mode when camera/scene reloads; no allocations.
            if (Application.isPlaying && profile != null) ApplyHaze();
        }

        [ContextMenu("Apply Space Environment")]
        public void Apply()
        {
            if (profile == null) return;
            if (!initialized) { previousSky = RenderSettings.skybox; initialized = true; }
            if (profile.gradientSkyMaterial != null && profile.gradientSkyMaterial.shader != null && profile.gradientSkyMaterial.shader.isSupported)
            {
                if (skyInstance == null || skyInstance.shader != profile.gradientSkyMaterial.shader)
                {
                    ReleaseSky();
                    skyInstance = new Material(profile.gradientSkyMaterial)
                    {
                        name = "MO Gradient Sky (temporary)", hideFlags = HideFlags.DontSave
                    };
                }
                skyInstance.SetColor("_SkyTop", profile.upperColor);
                skyInstance.SetColor("_SkyMiddle", profile.horizonColor);
                skyInstance.SetColor("_SkyBottom", profile.lowerColor);
                skyInstance.SetFloat("_NebulaIntensity", profile.nebulaStrength);
                skyInstance.SetFloat("_StarIntensity", profile.starStrength);
                RenderSettings.skybox = skyInstance;
            }
            else
            {
                // Never leave a missing/unsupported shader as magenta background.
                RenderSettings.skybox = null;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = profile.ambientColor;
            if (keyLight != null)
            {
                keyLight.type = LightType.Directional;
                keyLight.color = profile.keyColor;
                keyLight.intensity = Mathf.Clamp(profile.keyIntensity, 0f, 3f);
                keyLight.transform.rotation = Quaternion.Euler(profile.keyDirectionEuler);
                keyLight.shadows = LightShadows.Soft;
            }
            if (fillLight != null)
            {
                fillLight.type = LightType.Directional;
                fillLight.color = profile.fillColor;
                fillLight.intensity = Mathf.Clamp(profile.fillIntensity, 0f, 1f);
                fillLight.shadows = LightShadows.None;
            }
            if (distantPlanet != null)
            {
                distantPlanet.gameObject.SetActive(profile.showPlanet);
                distantPlanet.transform.position = profile.planetPosition;
                distantPlanet.transform.localScale = Vector3.one * profile.planetDiameter;
                if (profile.planetMaterial != null) distantPlanet.sharedMaterial = profile.planetMaterial;
            }
            // Affect only the main gameplay camera, never UI/preview/secondary cameras.
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                camera.backgroundColor = profile.lowerColor;
            }
            ApplyHaze();
        }

        private void ApplyHaze()
        {
            Shader.SetGlobalColor("_MO_HazeTint", profile.hazeColor);
            Shader.SetGlobalFloat("_MO_HazeStart", Mathf.Max(0f, profile.hazeStartMeters));
            Shader.SetGlobalFloat("_MO_HazeEnd", Mathf.Max(profile.hazeStartMeters + 1f, profile.hazeEndMeters));
            Shader.SetGlobalFloat("_MO_HazeStrength", profile.hazeStrength);
        }

        private void OnDisable()
        {
            if (initialized && RenderSettings.skybox == skyInstance) RenderSettings.skybox = previousSky;
            ReleaseSky();
            initialized = false;
            Shader.SetGlobalFloat("_MO_HazeStrength", 0f);
        }
        private void OnDestroy() { ReleaseSky(); }
        private void ReleaseSky()
        {
            if (skyInstance == null) return;
            AsteroidGeneration.DestroyTemporary(skyInstance);
            skyInstance = null;
        }
    }
}
