using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>Generates reusable assets ONCE in Editor. No procedural texture allocations during Play.</summary>
    public static class MOVisualAssetInstaller
    {
        private const string Root = "Assets/_MiningOperation/AsteroidAuthoring/Visuals";

        public static bool Install(AsteroidFieldSettings settings)
        {
            if (settings == null) return false;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader gas = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (lit == null || !lit.isSupported || gas == null || !gas.isSupported)
            {
                EditorUtility.DisplayDialog("Unity URP needed",
                    "Missing URP/Lit or URP/Particles/Unlit. Check that an active URP Pipeline Asset is assigned.", "OK");
                return false;
            }
            EnsureFolder();
            string icePath = Root + "/MO_IceSurface_Seamless.png";
            string gasPath = Root + "/MO_GasSoftVolume_v233.png";
            bool createdTextures = false;
            if (!File.Exists(icePath))
            {
                WriteTexture(icePath, GenerateIceTexture(512));
                createdTextures = true;
            }
            if (!File.Exists(gasPath))
            {
                WriteTexture(gasPath, GenerateGasSprite(512));
                createdTextures = true;
            }
            if (createdTextures) AssetDatabase.Refresh();
            Import(icePath, true, false);
            Import(gasPath, false, true);
            Texture2D iceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(icePath);
            Texture2D gasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(gasPath);
            if (iceTexture == null || gasTexture == null)
                throw new InvalidOperationException("Failed to import ice/gas textures.");

            string iceMaterialPath = Root + "/MO_Ice_URP.mat";
            var iceMat = AssetDatabase.LoadAssetAtPath<Material>(iceMaterialPath);
            bool createIceMaterial = iceMat == null;
            if (createIceMaterial)
            {
                iceMat = new Material(lit);
                AssetDatabase.CreateAsset(iceMat, iceMaterialPath);
            }
            if (createIceMaterial || iceMat.shader == null || !iceMat.shader.isSupported)
            {
                iceMat.shader = lit;
                iceMat.enableInstancing = true;
                iceMat.SetTexture("_BaseMap", iceTexture);
                iceMat.SetColor("_BaseColor", new Color(.81f, .94f, 1f, 1f));
                iceMat.SetFloat("_Metallic", 0f);
                iceMat.SetFloat("_Smoothness", .31f);
                EditorUtility.SetDirty(iceMat);
            }

            string gasMaterialPath = Root + "/MO_GasSoftVolume_v233.mat";
            var gasMat = AssetDatabase.LoadAssetAtPath<Material>(gasMaterialPath);
            bool createGasMaterial = gasMat == null;
            if (createGasMaterial)
            {
                gasMat = new Material(gas);
                AssetDatabase.CreateAsset(gasMat, gasMaterialPath);
            }
            if (createGasMaterial || gasMat.shader == null || !gasMat.shader.isSupported)
            {
                gasMat.shader = gas;
                gasMat.enableInstancing = true;
                gasMat.renderQueue = (int)RenderQueue.Transparent;
                gasMat.SetOverrideTag("RenderType", "Transparent");
                if (gasMat.HasProperty("_Surface")) gasMat.SetFloat("_Surface", 1f);
                if (gasMat.HasProperty("_Blend")) gasMat.SetFloat("_Blend", 0f);
                if (gasMat.HasProperty("_SrcBlend")) gasMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                if (gasMat.HasProperty("_DstBlend")) gasMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                if (gasMat.HasProperty("_ZWrite")) gasMat.SetInt("_ZWrite", 0);
                if (gasMat.HasProperty("_Cull")) gasMat.SetInt("_Cull", 0);
                gasMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (gasMat.HasProperty("_BaseMap")) gasMat.SetTexture("_BaseMap", gasTexture);
                if (gasMat.HasProperty("_BaseColor")) gasMat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(gasMat);
            }

            // V2.4: optional volumetric material. Never make it mandatory for the stable
            // particle fallback. Do not overwrite existing user-authored materials.
            Shader gasVolumeShader = Shader.Find("MiningOperation/GasVolumeURP");
            if (gasVolumeShader != null && gasVolumeShader.isSupported)
            {
                const string volumePath = Root + "/MO_GasVolumeURP_v24.mat";
                var volumeMat = AssetDatabase.LoadAssetAtPath<Material>(volumePath);
                if (volumeMat == null)
                {
                    volumeMat = new Material(gasVolumeShader);
                    volumeMat.SetColor("_GasColorA", new Color(.20f, .48f, .64f));
                    volumeMat.SetColor("_GasColorB", new Color(.52f, .73f, .81f));
                    volumeMat.SetFloat("_GasOpacity", .18f);
                    volumeMat.SetFloat("_GasSteps", 10f);
                    AssetDatabase.CreateAsset(volumeMat, volumePath);
                }
                if (settings.gasVolumeMaterial == null ||
                    settings.gasVolumeMaterial.shader == null ||
                    !settings.gasVolumeMaterial.shader.isSupported)
                {
                    Undo.RecordObject(settings, "Assign optional gas volume material");
                    settings.gasVolumeMaterial = volumeMat;
                    EditorUtility.SetDirty(settings);
                }
            }

            EnsureIceResources(settings);
            Undo.RecordObject(settings, "Install ice and gas visuals");
            // Preserve intentionally assigned custom URP-compatible materials.
            if (settings.iceMaterial == null || settings.iceMaterial.shader == null ||
                !settings.iceMaterial.shader.isSupported) settings.iceMaterial = iceMat;
            // Upgrade only our previous generated default. Never replace user-authored materials.
            bool stockLegacy = settings.gasParticleMaterial != null &&
                AssetDatabase.GetAssetPath(settings.gasParticleMaterial) == Root + "/MO_Gas_URP.mat";
            if (stockLegacy || settings.gasParticleMaterial == null ||
                settings.gasParticleMaterial.shader == null ||
                !settings.gasParticleMaterial.shader.isSupported)
                settings.gasParticleMaterial = gasMat;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return true;
        }


        private static void EnsureIceResources(AsteroidFieldSettings field)
        {
            if (field.iceGeneratorSettings != null && field.iceGeneratorSettings.catalog != null) return;
            const string catalogPath = Root + "/MO_IceResourceCatalog.asset";
            const string generatorPath = Root + "/MO_IceGenerator.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<AsteroidResourceCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AsteroidResourceCatalog>();
                catalog.resources = new System.Collections.Generic.List<ResourceDefinition>
                {
                    new ResourceDefinition { id="water_ice", displayName="Water Ice", rarity=ResourceRarity.Common, densityKgM3=930f, selectionWeight=7f },
                    new ResourceDefinition { id="hydrated_silicates", displayName="Hydrated Silicates", rarity=ResourceRarity.Common, densityKgM3=2600f, selectionWeight=2f },
                    new ResourceDefinition { id="ammonia_ice", displayName="Ammonia Ice", rarity=ResourceRarity.Industrial, densityKgM3=820f, selectionWeight=3f },
                    new ResourceDefinition { id="methane_ice", displayName="Methane Ice", rarity=ResourceRarity.Industrial, densityKgM3=520f, selectionWeight=2f },
                    new ResourceDefinition { id="noble_cryosalts", displayName="Noble Cryosalts", rarity=ResourceRarity.Rare, densityKgM3=1700f, selectionWeight=1f },
                    new ResourceDefinition { id="exotic_cryo_crystals", displayName="Exotic Cryocrystals", rarity=ResourceRarity.Exotic, densityKgM3=1500f, selectionWeight=.35f }
                };
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            var generator = AssetDatabase.LoadAssetAtPath<AsteroidGeneratorSettings>(generatorPath);
            if (generator == null)
            {
                generator = field.asteroidSettings != null ?
                    UnityEngine.Object.Instantiate(field.asteroidSettings) :
                    ScriptableObject.CreateInstance<AsteroidGeneratorSettings>();
                generator.catalog = catalog;
                generator.shapeIrregularity = .73f;
                generator.surfaceRoughness = .20f;
                generator.craterIntensity = .38f;
                generator.minCraters = 0;
                generator.maxCraters = 9;
                generator.minimumAxisScale = .34f;
                generator.minimumPorosity = .05f;
                generator.maximumPorosity = .19f;
                AssetDatabase.CreateAsset(generator, generatorPath);
            }
            field.iceGeneratorSettings = generator;
            EditorUtility.SetDirty(field);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_MiningOperation")) AssetDatabase.CreateFolder("Assets", "_MiningOperation");
            if (!AssetDatabase.IsValidFolder("Assets/_MiningOperation/AsteroidAuthoring"))
                AssetDatabase.CreateFolder("Assets/_MiningOperation", "AsteroidAuthoring");
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/_MiningOperation/AsteroidAuthoring", "Visuals");
        }

        private static void Import(string path, bool repeating, bool alpha)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            bool changed = false;
            if (imp.textureType != TextureImporterType.Default)
                { imp.textureType = TextureImporterType.Default; changed = true; }
            if (!imp.sRGBTexture) { imp.sRGBTexture = true; changed = true; }
            if (imp.alphaIsTransparency != alpha)
                { imp.alphaIsTransparency = alpha; changed = true; }
            var wrap = repeating ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (imp.wrapMode != wrap) { imp.wrapMode = wrap; changed = true; }
            if (!imp.mipmapEnabled) { imp.mipmapEnabled = true; changed = true; }
            if (imp.filterMode != FilterMode.Trilinear)
                { imp.filterMode = FilterMode.Trilinear; changed = true; }
            if (imp.maxTextureSize < 1024) { imp.maxTextureSize = 1024; changed = true; }
            if (changed) imp.SaveAndReimport();
        }

        private static void WriteTexture(string path, Texture2D t)
        {
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }

        // Horizontal periodicity: texture U=0 and U=1 are identical, preventing UV seam.
        private static float PeriodicNoise(float u, float v, float scale, float ox, float oy)
        {
            float a = Mathf.PerlinNoise(ox + u * scale, oy + v * scale);
            float b = Mathf.PerlinNoise(ox + (u - 1f) * scale, oy + v * scale);
            return Mathf.Lerp(a, b, u);
        }

        private static Texture2D GenerateIceTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            const int faultCount = 24;
            var faults = new Vector2[faultCount];
            var seeded = new System.Random(34523);
            for (int i = 0; i < faultCount; i++) faults[i] = new Vector2((float)seeded.NextDouble(), (float)seeded.NextDouble());
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / (size - 1), v = (float)y / (size - 1);
                float baseTone = .68f * PeriodicNoise(u, v, 3.7f, 12f, 5f) +
                    .25f * PeriodicNoise(u, v, 12f, 22f, 13f) +
                    .07f * PeriodicNoise(u, v, 30f, 48f, 25f);
                float first = 10f, second = 10f;
                for (int i = 0; i < faultCount; i++)
                {
                    float dx = Mathf.Abs(u - faults[i].x);
                    dx = Mathf.Min(dx, 1f - dx);
                    float dy = (v - faults[i].y) * 0.82f;
                    float d = dx * dx + dy * dy;
                    if (d < first) { second = first; first = d; }
                    else if (d < second) second = d;
                }
                float fracture = 1f - Mathf.SmoothStep(.0018f, .0065f, second - first);
                float tone = Mathf.Clamp01(.63f + (baseTone - .5f) * .43f - fracture * .18f);
                float blue = Mathf.Clamp01(tone * 1.05f + .03f);
                pixels[y * size + x] = new Color(tone * .81f, tone * .95f, blue, 1f);
            }
            t.SetPixels32(pixels);
            t.Apply();
            return t;
        }

        private static Texture2D GenerateGasSprite(int size)
        {
            // Non-repeating soft-edged sprite. Each particle is tinted independently;
            // no hard circles, seams or tiled world-space noise textures.
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size, v = (y + .5f) / size;
                float macro = Mathf.PerlinNoise(u * 3.4f + 11.3f, v * 3.4f + 8.2f);
                float middle = Mathf.PerlinNoise(u * 8.1f + 3.7f, v * 8.1f + 24.9f);
                float fine = Mathf.PerlinNoise(u * 17.4f + 47f, v * 17.4f + 5.7f);
                float dx = (u - .5f) * 2f + (macro - .5f) * .16f;
                float dy = (v - .5f) * 2f + (middle - .5f) * .12f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = 1f - Mathf.SmoothStep(.34f, .99f, radius);
                float structure = .58f + .30f * macro + .15f * middle + .08f * fine;
                float alpha = Mathf.Clamp01(edge * edge * structure);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            t.SetPixels32(pixels);
            t.Apply(false, false);
            return t;
        }
    }
}
