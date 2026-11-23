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
            Shader gas = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (gas == null || !gas.isSupported)
            {
                EditorUtility.DisplayDialog("Unity URP needed",
                    "Missing URP/Particles/Unlit. Check that an active URP Pipeline Asset is assigned.", "OK");
                return false;
            }
            EnsureFolder();
            // Never overwrite a serialized asset that Unity cannot currently load.
            // This typically means a broken MonoScript GUID; the user's data may be intact.
            EnsureReadableAsset<Material>(Root + "/MO_GasSoftVolume_v233.mat");
            EnsureReadableAsset<Material>(Root + "/MO_GasVolumeURP_v24.mat");
            string gasPath = Root + "/MO_GasSoftVolume_v233.png";
            if (!File.Exists(gasPath))
            {
                WriteTexture(gasPath, GenerateGasSprite(512));
                AssetDatabase.Refresh();
            }
            Import(gasPath, false, true);
            Texture2D gasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(gasPath);
            if (gasTexture == null) throw new InvalidOperationException("Failed to import gas texture.");

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

            // Cheap, one-surface ground fog. Preserve existing gas and raymarched fog assets.
            Shader fastShader = Shader.Find("MiningOperation/GroundFogFastURP");
            if (fastShader != null && fastShader.isSupported)
            {
                const string fastPath = Root + "/MO_GroundFogFastURP_v03.mat";
                const string noisePath = Root + "/MO_GroundFogFastNoise_v03.png";
                EnsureReadableAsset<Material>(fastPath);
                if (!File.Exists(noisePath))
                {
                    WriteTexture(noisePath, GenerateGroundFogNoise(256));
                    AssetDatabase.Refresh();
                }
                Import(noisePath, true, false);
                var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(noisePath);
                var fastMat = AssetDatabase.LoadAssetAtPath<Material>(fastPath);
                if (fastMat == null)
                {
                    fastMat = new Material(fastShader);
                    fastMat.SetFloat("_GasOpacity", .2f);
                    fastMat.SetFloat("_GasNoiseScale", 1f);
                    fastMat.SetFloat("_FogBreakup", .8f);
                    fastMat.SetFloat("_FogCutouts", .55f);
                    fastMat.SetColor("_GasColorA", new Color(.16f, .25f, .28f));
                    fastMat.SetColor("_GasColorB", new Color(.24f, .33f, .36f));
                    if (noise != null) fastMat.SetTexture("_FogNoiseTex", noise);
                    AssetDatabase.CreateAsset(fastMat, fastPath);
                }
                if (settings.groundFogFastMaterial == null ||
                    settings.groundFogFastMaterial.shader == null ||
                    !settings.groundFogFastMaterial.shader.isSupported)
                {
                    Undo.RecordObject(settings, "Assign fast ground fog material");
                    settings.groundFogFastMaterial = fastMat;
                    EditorUtility.SetDirty(settings);
                }
            }

            // Independent ground-fog asset: do not modify the working volumetric gas material.
            Shader groundShader = Shader.Find("MiningOperation/GroundFogURP");
            if (groundShader != null && groundShader.isSupported)
            {
                const string fogPath = Root + "/MO_GroundFogURP_v01.mat";
                EnsureReadableAsset<Material>(fogPath);
                Material fogMat = AssetDatabase.LoadAssetAtPath<Material>(fogPath);
                if (fogMat == null)
                {
                    fogMat = new Material(groundShader);
                    fogMat.SetFloat("_GasOpacity", .28f);
                    fogMat.SetFloat("_GasSteps", 16f);
                    fogMat.SetFloat("_GasContrast", .62f);
                    fogMat.SetColor("_GasColorA", new Color(.18f, .27f, .30f));
                    fogMat.SetColor("_GasColorB", new Color(.25f, .35f, .38f));
                    AssetDatabase.CreateAsset(fogMat, fogPath);
                }
                if (settings.groundFogMaterial == null || settings.groundFogMaterial.shader == null ||
                    !settings.groundFogMaterial.shader.isSupported)
                {
                    Undo.RecordObject(settings, "Assign optional ground fog material");
                    settings.groundFogMaterial = fogMat;
                    EditorUtility.SetDirty(settings);
                }
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

            Undo.RecordObject(settings, "Install gas visuals");
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


        public static bool InstallIceResources(AsteroidFieldSettings settings)
        {
            if (settings == null) return false;
            EnsureFolder();
            EnsureReadableAsset<AsteroidResourceCatalog>(Root + "/MO_IceResourceCatalog.asset");
            EnsureReadableAsset<AsteroidGeneratorSettings>(Root + "/MO_IceGenerator.asset");
            EnsureIceResources(settings);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static void EnsureReadableAsset<T>(string path) where T : UnityEngine.Object
        {
            // AssetDatabase.LoadAssetAtPath may return null for a file that exists on disk
            // when m_Script/MonoScript GUIDs are broken. Recreating it destroys saved data.
            if (File.Exists(path) && AssetDatabase.LoadAssetAtPath<T>(path) == null)
                throw new InvalidOperationException("Existing asset cannot be loaded: " + path +
                    ". Do NOT regenerate it. Fix its script reference with the Settings Asset Repair tool first.");
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
            if (generator != null && generator.catalog == null)
            {
                // Only repair the built-in generator's known catalog relationship;
                // never guess a resource catalog for a custom generator asset.
                if (AssetDatabase.GetAssetPath(generator) != generatorPath)
                    throw new InvalidOperationException("Custom ice generator has no Resource Catalog. Assign the intended catalog manually.");
                Undo.RecordObject(generator, "Restore default ice resource catalog reference");
                generator.catalog = catalog;
                EditorUtility.SetDirty(generator);
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

        private static Texture2D GenerateGroundFogNoise(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Periodicity is approximated by sampling a tiled toroidal domain.
                // Pixel shader requires only ONE texture sample, not costly trig noise.
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float a = Mathf.PerlinNoise(u * 5f + 9.7f, v * 5f + 6.4f);
                float b = Mathf.PerlinNoise(u * 13f + 21.8f, v * 13f + 43.2f);
                float n = Mathf.Clamp01(a * .65f + b * .35f);
                byte c = (byte)Mathf.RoundToInt(n * 255f);
                pixels[y * size + x] = new Color32(c, c, c, 255);
            }
            t.SetPixels32(pixels);
            t.Apply(false, false);
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
