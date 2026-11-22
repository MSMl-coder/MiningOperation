using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Typed source-of-truth for new texture-backed surfaces. No Generator/Field .asset
    /// is created or modified. All texture references persist in actual .mat assets.
    /// </summary>
    public static class MOTexturedMaterialLibrary
    {
        public const string ShaderName = "Mining Operation/Asteroid Textured Surface";
        public const string TextureRoot = "Assets/_MiningOperation/Game/Asteroids/Textures";
        public const string MaterialRoot = "Assets/_MiningOperation/AsteroidAuthoring/Materials/Textured";
        public const string Tag = "MO_SurfacePreset";

        public sealed class Recipe
        {
            public readonly string id, category, displayName, fileStem;
            public readonly float frequency, normal, smoothness, metallic, ao;
            public Recipe(string id, string category, string displayName, string fileStem,
                float frequency, float normal, float smoothness, float metallic, float ao)
            {
                this.id = id;
                this.category = category;
                this.displayName = displayName;
                this.fileStem = fileStem;
                this.frequency = frequency;
                this.normal = normal;
                this.smoothness = smoothness;
                this.metallic = metallic;
                this.ao = ao;
            }
        }

        public static readonly Recipe[] Recipes =
        {
            new Recipe("rock.cinematic_fractured", "Silicate / igneous", "Cinematic fractured rock", "CinematicFracturedRock", 2.1f, .95f, .83f, 1f, .76f),
            new Recipe("rock.basalt", "Silicate / igneous", "Dark basalt", "Basalt", 2.8f, .92f, .95f, 1f, .72f),
            new Recipe("rock.granite", "Silicate / igneous", "Granite", "Granite", 3.1f, .80f, .92f, 1f, .70f),
            new Recipe("rock.silicate", "Silicate / igneous", "Pale silicate (reference)", "Silicate", 2.3f, .82f, .95f, 1f, .73f),
            new Recipe("carbon.dark", "Carbonaceous", "Dark carbonaceous", "DarkCarbonaceous", 2.8f, .71f, .98f, 1f, .77f),
            new Recipe("carbon.breccia", "Carbonaceous", "Breccia / rubble", "Breccia", 2.5f, .98f, .90f, 1f, .83f),
            new Recipe("metal.iron_nickel", "Metal-rich", "Iron–nickel", "IronNickel", 2.4f, .76f, 1.06f, 1f, .64f),
            new Recipe("metal.oxidized", "Metal-rich", "Oxidized minerals", "OxidizedMinerals", 2.7f, .79f, 1f, 1f, .78f),
            new Recipe("ice.clean", "Ice", "Clean water ice", "CleanWaterIce", 2.0f, .55f, .90f, 1f, .56f),
            new Recipe("ice.dirty", "Ice", "Dirty ice / regolith", "DirtyIce", 2.5f, .62f, .96f, 1f, .74f),
            new Recipe("ice.fractured", "Ice", "Fractured ice", "FracturedIce", 2.8f, .72f, .91f, 1f, .73f)
        };

        public static Recipe Find(string id)
        {
            foreach (Recipe recipe in Recipes)
                if (string.Equals(recipe.id, id, StringComparison.Ordinal)) return recipe;
            return null;
        }

        public static string[] Categories => new[] { "Silicate / igneous", "Carbonaceous", "Metal-rich", "Ice" };

        public static Recipe[] ForCategory(string category)
        {
            var results = new List<Recipe>();
            foreach (Recipe r in Recipes) if (r.category == category) results.Add(r);
            return results.ToArray();
        }

        private static Texture2D ReadMap(string name, string kind)
        {
            string path = TextureRoot + "/" + kind + "/MO_" + name + "_" + kind + ".png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                throw new InvalidOperationException("Texture not imported: " + path +
                    ". Install the entire patch including Textures, then Refresh/Reimport.");
            return tex;
        }

        public static void Apply(Material target, Recipe recipe)
        {
            if (target == null || recipe == null) throw new ArgumentNullException("target/recipe");
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Textured shader unavailable/unsupported: " + ShaderName +
                    ". Check the first shader error in Unity Console before installing materials.");
            // Read all references before modifying target (atomic enough for Editor undo).
            Texture2D albedo = ReadMap(recipe.fileStem, "Albedo");
            Texture2D normal = ReadMap(recipe.fileStem, "Normal");
            Texture2D mask = ReadMap(recipe.fileStem, "Mask");
            Undo.RecordObject(target, "Apply asteroid texture preset");
            if (target.shader != shader) target.shader = shader;
            target.SetTexture("_BaseMap", albedo);
            target.SetTexture("_NormalMap", normal);
            target.SetTexture("_MaskMap", mask);
            target.SetColor("_BaseColor", Color.white);
            target.SetFloat("_TextureFrequency", recipe.frequency);
            target.SetFloat("_TriplanarSharpness", 4f);
            target.SetFloat("_NormalStrength", recipe.normal);
            target.SetFloat("_SmoothnessMultiplier", recipe.smoothness);
            target.SetFloat("_MetallicMultiplier", recipe.metallic);
            target.SetFloat("_AOInfluence", recipe.ao);
            target.SetFloat("_AmbientFloor", .085f);
            target.SetFloat("_Exposure", 1.0f);
            target.SetFloat("_ColorVariation", .075f);
            target.SetFloat("_DetailFadeStart", .022f);
            target.SetOverrideTag(Tag, recipe.id);
            target.enableInstancing = true;
            EditorUtility.SetDirty(target);
        }

        public static Material CreateOrReuse(Recipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            EnsureFolder("Assets/_MiningOperation/AsteroidAuthoring");
            EnsureFolder("Assets/_MiningOperation/AsteroidAuthoring/Materials");
            EnsureFolder(MaterialRoot);
            string path = MaterialRoot + "/MO_" + recipe.fileStem + "_Textured.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            // Critical safety: don't overwrite a broken material asset whose MonoImporter
            // or shader reference cannot currently resolve.
            string abs = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", path);
            if (File.Exists(abs))
                throw new InvalidOperationException("An existing material file cannot load. Refusing to overwrite: " + path);
            Shader shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Textured asteroid shader is missing/unsupported.");
            var material = new Material(shader) { name = "MO_" + recipe.fileStem + "_Textured", enableInstancing = true };
            try
            {
                Apply(material, recipe);
                AssetDatabase.CreateAsset(material, path);
                return material;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(material);
                throw;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }

        [MenuItem("Tools/Mining Operation/Materials/Install All Textured Asteroid Materials")]
        public static void InstallAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Edit Mode required", "Stop Play Mode to install materials.", "OK");
                return;
            }
            try
            {
                int created = 0;
                foreach (Recipe recipe in Recipes)
                {
                    string path = MaterialRoot + "/MO_" + recipe.fileStem + "_Textured.mat";
                    bool exists = AssetDatabase.LoadAssetAtPath<Material>(path) != null;
                    CreateOrReuse(recipe);
                    if (!exists) created++;
                }
                AssetDatabase.SaveAssets();
                Debug.Log("[MO] Textured materials ready: " + Recipes.Length + "; new .mat assets: " + created +
                    ". No existing Field/Generator Settings or old materials were overwritten.");
            }
            catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Material installation failed", e.Message, "OK"); }
        }

        [MenuItem("Tools/Mining Operation/Materials/Validate & Fix Texture Importers")]
        public static void ConfigureImporters()
        {
            int changed = 0;
            foreach (Recipe recipe in Recipes)
            {
                foreach (string kind in new[] { "Albedo", "Normal", "Mask" })
                {
                    string path = TextureRoot + "/" + kind + "/MO_" + recipe.fileStem + "_" + kind + ".png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) { Debug.LogWarning("[MO] Missing texture: " + path); continue; }
                    bool isNormal = kind == "Normal";
                    bool linear = kind != "Albedo";
                    TextureImporterType type = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    bool needs = importer.textureType != type || importer.sRGBTexture == linear ||
                        !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
                        importer.filterMode != FilterMode.Trilinear;
                    if (!needs) continue;
                    importer.textureType = type;
                    if (!isNormal) importer.sRGBTexture = !linear;
                    importer.mipmapEnabled = true;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 4;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            Debug.Log("[MO] Texture import settings validated. Reimported: " + changed + ".");
        }
    }
}
