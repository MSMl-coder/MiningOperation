using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Stable, code-defined recipes for the existing unified rock/ice shader.
    /// No transient ScriptableObjects, texture files or changing .asset GUIDs.
    /// A preset is copied into a MATERIAL, not bound live to a shared profile.
    /// After applying, every shader parameter can be independently edited.
    /// </summary>
    public static class MOAsteroidMaterialPresets
    {
        public const string MaterialTag = "MO_SurfacePreset";
        public const string RequiredShader = "Mining Operation/Asteroid Unified Surface";

        public sealed class Preset
        {
            public readonly string id;
            public readonly string category;
            public readonly string title;
            public readonly string description;
            public readonly Dictionary<string, float> floats;
            public readonly Dictionary<string, Color> colors;

            internal Preset(string id, string category, string title, string description,
                Dictionary<string, float> floats, Dictionary<string, Color> colors)
            {
                this.id = id;
                this.category = category;
                this.title = title;
                this.description = description;
                this.floats = floats;
                this.colors = colors;
            }
        }

        private sealed class Builder
        {
            internal Dictionary<string, float> numbers = new Dictionary<string, float>();
            internal Dictionary<string, Color> colors = new Dictionary<string, Color>();
            internal Builder F(string name, float value) { numbers[name] = value; return this; }
            internal Builder C(string name, string hex)
            {
                if (!ColorUtility.TryParseHtmlString(hex, out Color parsed))
                    throw new ArgumentException("Invalid material palette color " + hex);
                colors[name] = parsed;
                return this;
            }
            internal Builder Copy()
            {
                return new Builder
                {
                    numbers = new Dictionary<string, float>(numbers),
                    colors = new Dictionary<string, Color>(colors)
                };
            }
            internal Preset Finish(string id, string category, string title, string note)
            {
                return new Preset(id, category, title, note,
                    new Dictionary<string, float>(numbers), new Dictionary<string, Color>(colors));
            }
        }

        // Fixed definitions: each preset starts from complete material values, so switching
        // between stone/ice cannot leave old crater, roughness or illumination settings behind.
        private static Builder Base()
        {
            return new Builder()
                .C("_RockBase", "#77746E").C("_RockSecondary", "#4D5155")
                .C("_IceBase", "#B7D0DA").C("_IceSecondary", "#628899")
                .C("_CavityColor", "#34363A").C("_RimTint", "#A3AAB0")
                .F("_IceBlend", 0f)
                .F("_NoiseMode", 0f).F("_MacroFrequency", 1.25f).F("_MacroOctaves", 3f)
                .F("_MacroWarp", .10f).F("_MacroContrast", 1.10f)
                .F("_MacroStrength", .21f).F("_MacroNormalStrength", .26f)
                .F("_CraterMode", 2f).F("_CraterFrequency", 1.35f).F("_CraterDensity", .40f)
                .F("_CraterRadius", .90f).F("_CraterDepth", .38f)
                .F("_CraterRimHeight", .12f).F("_CraterRimSharpness", 4.5f)
                .F("_CraterSecondary", .10f).F("_CraterColorStrength", .40f)
                .F("_CraterNormalStrength", .40f).F("_CraterRoughnessStrength", .17f)
                .F("_MicroNoiseType", 0f).F("_DetailFrequency", 10f)
                .F("_DetailStrength", .10f).F("_DetailNormalStrength", .14f)
                .F("_MicroRoughnessStrength", .18f)
                .F("_StrataFrequency", 3f).F("_StrataStrength", .08f)
                .F("_IceCrackStrength", 0f)
                .F("_RockStructureStrength", 0f).F("_RockFractureFrequency", 3.0f)
                .F("_RockFractureWidth", .075f).F("_RockFractureWarp", .18f)
                .F("_RockCrackDarkness", .70f).F("_RockFacetContrast", .40f)
                .F("_RockHeightStrength", .80f).F("_RockExposure", 1.0f)
                .F("_CavityStrength", .38f).F("_CavityContrast", 1.25f)
                .F("_CavityRoughnessStrength", .20f)
                .F("_Smoothness", .055f).F("_Metallic", 0f)
                .F("_AmbientFill", .38f).F("_LightWrap", .33f)
                .F("_DarkSideFill", .22f).F("_RimStrength", .005f).F("_RimPower", 2.5f);
        }

        public static readonly string[] Categories =
            { "Silicate / igneous", "Carbonaceous", "Metal-rich", "Ice" };

        public static readonly Preset[] All = Build();

        private static Preset[] Build()
        {
            Builder b = Base();
            return new[]
            {
                // Cinematic fractured stone: strongly separated rock planes and
                // dark seams, without the bright circular crater edges of v2.6.11.
                b.Copy()
                    .C("_RockBase", "#68737C").C("_RockSecondary", "#37424C")
                    .C("_CavityColor", "#222A33")
                    .F("_RockStructureStrength", .96f)
                    .F("_RockFractureFrequency", 2.55f)
                    .F("_RockFractureWidth", .087f)
                    .F("_RockFractureWarp", .28f)
                    .F("_RockCrackDarkness", .79f)
                    .F("_RockFacetContrast", .73f)
                    .F("_RockHeightStrength", 1.32f)
                    .F("_RockExposure", 1.12f)
                    .F("_NoiseMode", 1f).F("_MacroFrequency", 1.40f)
                    .F("_MacroContrast", 1.35f).F("_MacroStrength", .20f)
                    .F("_MacroNormalStrength", .48f)
                    .F("_CraterMode", 3f).F("_CraterFrequency", 1.0f)
                    .F("_CraterDensity", .15f).F("_CraterRimHeight", .015f)
                    .F("_CraterNormalStrength", .27f)
                    .F("_DetailFrequency", 10f).F("_DetailStrength", .10f)
                    .F("_DetailNormalStrength", .20f)
                    .F("_CavityStrength", .44f)
                    .F("_AmbientFill", .34f).F("_LightWrap", .19f)
                    .F("_DarkSideFill", .18f).F("_RimStrength", 0f)
                    .Finish("rock.cinematic_fractured", Categories[0],
                        "Cinematic fractured rock", "Large angular stone faces, irregular dark cracks and visible scale; best with Fractured geometry."),
                b.Copy()
                    .C("_RockBase", "#595D63").C("_RockSecondary", "#383C42")
                    .C("_CavityColor", "#272A31")
                    .F("_NoiseMode", 1f).F("_MacroFrequency", 1.00f)
                    .F("_MacroNormalStrength", .42f).F("_DetailNormalStrength", .22f)
                                        .F("_CraterDensity", .24f).F("_StrataStrength", .11f)
                    .F("_RockStructureStrength", .72f).F("_RockFractureFrequency", 3.15f)
                    .F("_RockFractureWidth", .07f).F("_RockCrackDarkness", .74f)
                    .F("_RockFacetContrast", .62f).F("_RockHeightStrength", 1.05f)
                    .Finish("rock.basalt", Categories[0], "Basalt / fractured dark", "Dark volcanic silicate, large facets and moderate surface cracks"),
                b.Copy()
                    .C("_RockBase", "#8D8376").C("_RockSecondary", "#5C5958")
                    .F("_NoiseMode", 0f).F("_MacroFrequency", 1.55f)
                    .F("_MacroStrength", .28f).F("_DetailFrequency", 12f)
                    .F("_DetailStrength", .13f).F("_CraterDensity", .28f)
                    .Finish("rock.granite", Categories[0], "Granite / coarse silicate", "Neutral coarse mineral rock with moderate contrast"),
                b.Copy()
                    .C("_RockBase", "#847967").C("_RockSecondary", "#504D47")
                    .F("_MacroFrequency", 1.15f).F("_NoiseMode", 2f)
                    .F("_CraterFrequency", .95f).F("_CraterDepth", .48f)
                    .F("_CraterDensity", .48f).F("_CraterRimHeight", .08f)
                    .Finish("rock.silicate", Categories[0], "Silicate / cratered", "Large readable bowls and warm-grey mineral coloration"),
                b.Copy()
                    .C("_RockBase", "#494847").C("_RockSecondary", "#292F33")
                    .C("_CavityColor", "#202326")
                    .F("_NoiseMode", 3f).F("_MacroStrength", .19f)
                    .F("_CraterDensity", .25f).F("_DetailFrequency", 8f)
                    .F("_DetailNormalStrength", .11f).F("_CavityStrength", .32f)
                    .Finish("carbon.dark", Categories[1], "Carbonaceous / dark", "Dark porous carbon-rich surface; restrained fine detail"),
                b.Copy()
                    .C("_RockBase", "#67625A").C("_RockSecondary", "#383B3B")
                    .F("_MicroNoiseType", 2f).F("_MacroFrequency", 1.4f)
                    .F("_DetailNormalStrength", .23f).F("_CraterMode", 3f)
                                        .F("_CraterDensity", .14f).F("_CavityStrength", .53f)
                    .F("_RockStructureStrength", .82f).F("_RockFractureFrequency", 3.6f)
                    .F("_RockFractureWarp", .26f).F("_RockFractureWidth", .06f)
                    .F("_RockCrackDarkness", .82f).F("_RockHeightStrength", 1.12f)
                    .F("_CraterRimHeight", .02f)
                    .Finish("carbon.breccia", Categories[1], "Breccia / rubble", "Broken aggregate and dark pockets; erosion rather than bright rims"),
                b.Copy()
                    .C("_RockBase", "#77736B").C("_RockSecondary", "#50545A")
                    .F("_Metallic", .28f).F("_Smoothness", .22f)
                    .F("_MacroStrength", .17f).F("_CraterDensity", .20f)
                    .F("_CraterDepth", .30f).F("_MicroRoughnessStrength", .25f)
                    .Finish("metal.iron_nickel", Categories[2], "Iron–nickel / weathered", "Muted metal-rich body with low, varied sheen"),
                b.Copy()
                    .C("_RockBase", "#806553").C("_RockSecondary", "#464A52")
                    .F("_Metallic", .12f).F("_Smoothness", .12f)
                    .F("_MacroStrength", .31f).F("_MacroFrequency", 1.0f)
                    .F("_CraterColorStrength", .50f)
                    .Finish("metal.oxidized", Categories[2], "Oxidized mineral surface", "Warm iron-stained mineral rock with bluish dark hollows"),
                b.Copy()
                    .C("_IceBase", "#C2D5DE").C("_IceSecondary", "#779AAA")
                    .C("_CavityColor", "#526C79")
                    .F("_IceBlend", 1f).F("_CraterMode", 4f)
                    .F("_CraterDensity", .20f).F("_CraterDepth", .27f)
                    .F("_CraterRimHeight", .045f).F("_CraterNormalStrength", .25f)
                    .F("_Smoothness", .24f).F("_IceCrackStrength", .22f)
                    .Finish("ice.clean", Categories[3], "Clean water ice", "Smooth pale ice with large, shallow basins and fine cracks"),
                b.Copy()
                    .C("_RockBase", "#676A6A").C("_RockSecondary", "#3F5059")
                    .C("_IceBase", "#9FB6C1").C("_IceSecondary", "#5F8190")
                    .F("_IceBlend", .62f).F("_CraterMode", 4f)
                    .F("_CraterDensity", .38f).F("_CraterRimHeight", .06f)
                    .F("_IceCrackStrength", .40f).F("_Smoothness", .17f)
                    .Finish("ice.dirty", Categories[3], "Dirty ice / regolith", "Rock-dust inclusions, mottled icy material and dark fissures"),
                b.Copy()
                    .C("_IceBase", "#B1CAD6").C("_IceSecondary", "#4B788B")
                    .C("_CavityColor", "#3F5766")
                    .F("_IceBlend", 1f).F("_NoiseMode", 1f)
                    .F("_CraterMode", 4f).F("_CraterDensity", .08f)
                    .F("_CraterRimHeight", .02f).F("_IceCrackStrength", .68f)
                    .F("_MacroNormalStrength", .42f).F("_Smoothness", .14f)
                    .Finish("ice.fractured", Categories[3], "Fractured ice", "Irregular coarse ice with cracks and almost no circular impact rims")
            };
        }

        public static Preset Find(string id)
        {
            foreach (Preset p in All)
                if (string.Equals(p.id, id, StringComparison.Ordinal)) return p;
            return null;
        }

        public static Preset[] ForCategory(string category)
        {
            var list = new List<Preset>();
            foreach (Preset p in All)
                if (p.category == category) list.Add(p);
            return list.ToArray();
        }

        public static bool Apply(Material material, Preset preset)
        {
            if (material == null || preset == null || material.shader == null ||
                material.shader.name != RequiredShader) return false;
            Undo.RecordObject(material, "Apply asteroid material preset: " + preset.title);
            foreach (var entry in preset.floats)
                if (material.HasProperty(entry.Key)) material.SetFloat(entry.Key, entry.Value);
            foreach (var entry in preset.colors)
                if (material.HasProperty(entry.Key)) material.SetColor(entry.Key, entry.Value);
            material.SetOverrideTag(MaterialTag, preset.id);
            EditorUtility.SetDirty(material);
            return true;
        }
    }
}
