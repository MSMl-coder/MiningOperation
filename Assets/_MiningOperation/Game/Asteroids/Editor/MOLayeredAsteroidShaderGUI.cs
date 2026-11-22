using System;
using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Material-only inspector. No extra ScriptableObject assets or texture generation.
    /// Works inside Environment Generator's MaterialEditor and Unity's material inspector.
    /// </summary>
    public sealed class MOLayeredAsteroidShaderGUI : ShaderGUI
    {
        private bool colors = true;
        private bool macro = true;
        private bool rockFracture = true;
        private bool crater = false;
        private bool micro = false;
        private bool cavity = false;
        private bool lighting = false;
        private bool advanced = false;
        private Material lastMaterial;
        private int chosenCategory;
        private string chosenPresetId;
        private bool anyMaterialPropertyChanged;

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            DrawMaterialPresets(editor);
            anyMaterialPropertyChanged = false;
            EditorGUILayout.HelpBox(
                "Layered rock/ice surface. ROCK FRACTURE creates broken stone faces and dark seams; Macro controls broad color variation, Crater is optional, Micro is grain. Near/Far share the same material.",
                MessageType.None);
            if (Fold(ref colors, "01  COLOR / COMPOSITION"))
            {
                Field(editor, properties, "_RockBase", "Rock base");
                Field(editor, properties, "_RockSecondary", "Rock variation");
                Field(editor, properties, "_IceBase", "Ice base");
                Field(editor, properties, "_IceSecondary", "Ice variation");
                Field(editor, properties, "_IceBlend", "Ice blend (0 rock / 1 ice)");
            }
            if (Fold(ref macro, "02  MACRO MASK — GEOLOGICAL MASSES"))
            {
                Field(editor, properties, "_NoiseMode", "Noise type");
                Field(editor, properties, "_MacroFrequency", "Frequency / scale");
                Field(editor, properties, "_MacroOctaves", "Octaves");
                Field(editor, properties, "_MacroWarp", "Domain warp");
                Field(editor, properties, "_MacroContrast", "Mask contrast");
                Field(editor, properties, "_MacroStrength", "Color variation");
                Field(editor, properties, "_MacroNormalStrength", "Normal strength");
            }
            if (Fold(ref rockFracture, "03  ROCK FRACTURE — SHARP STONE FACES"))
            {
                Field(editor, properties, "_RockStructureStrength", "Fracture amount (0 = original)");
                Field(editor, properties, "_RockFractureFrequency", "Fracture scale (lower = larger)");
                Field(editor, properties, "_RockFractureWidth", "Crevice width");
                Field(editor, properties, "_RockFractureWarp", "Fracture irregularity");
                Field(editor, properties, "_RockCrackDarkness", "Crevice darkness");
                Field(editor, properties, "_RockFacetContrast", "Rock-face variation");
                Field(editor, properties, "_RockHeightStrength", "Fractured normal relief");
                Field(editor, properties, "_RockExposure", "Rock exposure");
                EditorGUILayout.HelpBox("Use the 'Cinematic fractured rock' preset, then select the Fractured shape for newly generated rocks. The shader adds normals/color; only new V9 geometry adds actual mesh fractures.", MessageType.None);
            }
            if (Fold(ref crater, "04  CRATER MASK — BOWLS AND RIMS"))
            {
                Field(editor, properties, "_CraterMode", "Crater shape");
                var mode = FindProperty("_CraterMode", properties, false);
                if (mode != null && mode.floatValue > .5f)
                {
                    Field(editor, properties, "_CraterFrequency", "Frequency (lower = larger)");
                    Field(editor, properties, "_CraterDensity", "Crater probability");
                    Field(editor, properties, "_CraterRadius", "Radius variation / scale");
                    Field(editor, properties, "_CraterDepth", "Bowl depth");
                    Field(editor, properties, "_CraterRimHeight", "Rim height");
                    Field(editor, properties, "_CraterRimSharpness", "Rim sharpness");
                    Field(editor, properties, "_CraterSecondary", "Secondary smaller craters");
                    Field(editor, properties, "_CraterColorStrength", "Color contribution");
                    Field(editor, properties, "_CraterNormalStrength", "Normal contribution");
                    Field(editor, properties, "_CraterRoughnessStrength", "Roughness contribution");
                    EditorGUILayout.HelpBox(
                        "Crater normals and color are simulated in the shader. Silhouette/true depth still comes from the procedural mesh. Low Frequency (about 1.0–1.8) produces larger visible craters.",
                        MessageType.None);
                }
            }
            if (Fold(ref micro, "05  MICRO MASK — SURFACE GRAIN"))
            {
                Field(editor, properties, "_MicroNoiseType", "Noise type");
                Field(editor, properties, "_DetailFrequency", "Frequency");
                Field(editor, properties, "_DetailStrength", "Color contribution");
                Field(editor, properties, "_DetailNormalStrength", "Normal contribution");
                Field(editor, properties, "_MicroRoughnessStrength", "Roughness contribution");
                Field(editor, properties, "_StrataFrequency", "Layer frequency");
                Field(editor, properties, "_StrataStrength", "Layer strength");
                Field(editor, properties, "_IceCrackStrength", "Ice cracks");
            }
            if (Fold(ref cavity, "06  CAVITY MASK — DEPRESSIONS"))
            {
                Field(editor, properties, "_CavityColor", "Cavity tint");
                Field(editor, properties, "_CavityStrength", "Darkening strength");
                Field(editor, properties, "_CavityContrast", "Mask contrast");
                Field(editor, properties, "_CavityRoughnessStrength", "Roughness contribution");
            }
            if (Fold(ref lighting, "07  LIGHTING / MATERIAL RESPONSE"))
            {
                Field(editor, properties, "_Smoothness", "Smoothness");
                Field(editor, properties, "_Metallic", "Metallic");
                Field(editor, properties, "_AmbientFill", "Ambient fill");
                Field(editor, properties, "_LightWrap", "Terminator softness");
                Field(editor, properties, "_DarkSideFill", "Dark-side brightness");
                Field(editor, properties, "_RimTint", "Edge light color");
                Field(editor, properties, "_RimStrength", "Edge light strength");
                Field(editor, properties, "_RimPower", "Edge light width");
            }
            if (Fold(ref advanced, "ADVANCED RENDERING"))
            {
                editor.RenderQueueField();
                editor.EnableInstancingField();
                editor.DoubleSidedGIField();
                EditorGUILayout.HelpBox("Object radius, seed and LOD visibility are managed by the renderer and should not be edited on the material.", MessageType.None);
            }
            if (anyMaterialPropertyChanged)
            {
                // Manual property edits are independent of library defaults.
                // Foldout clicks never mark a material as customized.
                foreach (UnityEngine.Object obj in editor.targets)
                {
                    var material = obj as Material;
                    if (material == null) continue;
                    Undo.RecordObject(material, "Customize asteroid material preset");
                    material.SetOverrideTag(MOAsteroidMaterialPresets.MaterialTag, "custom");
                    EditorUtility.SetDirty(material);
                }
            }
        }

        private void DrawMaterialPresets(MaterialEditor editor)
        {
            Material material = editor.target as Material;
            if (material == null) return;
            if (lastMaterial != material)
            {
                lastMaterial = material;
                string currentId = material.GetTag(MOAsteroidMaterialPresets.MaterialTag, false, "");
                var activePreset = MOAsteroidMaterialPresets.Find(currentId);
                chosenCategory = 0;
                if (activePreset != null)
                {
                    for (int i = 0; i < MOAsteroidMaterialPresets.Categories.Length; i++)
                        if (MOAsteroidMaterialPresets.Categories[i] == activePreset.category) chosenCategory = i;
                }
                chosenPresetId = currentId;
            }
            EditorGUILayout.LabelField("MATERIAL PRESET LIBRARY", EditorStyles.boldLabel);
            string active = material.GetTag(MOAsteroidMaterialPresets.MaterialTag, false, "");
            var assigned = MOAsteroidMaterialPresets.Find(active);
            EditorGUILayout.LabelField("Currently applied", assigned != null ? assigned.title : "Custom / Unassigned");
            chosenCategory = EditorGUILayout.Popup("Rock / ice category", chosenCategory,
                MOAsteroidMaterialPresets.Categories);
            var available = MOAsteroidMaterialPresets.ForCategory(
                MOAsteroidMaterialPresets.Categories[chosenCategory]);
            int index = 0;
            for (int i = 0; i < available.Length; i++)
                if (available[i].id == chosenPresetId) { index = i; break; }
            if (available.Length == 0) return;
            index = EditorGUILayout.Popup("Material type", index, Array.ConvertAll(available, p => p.title));
            chosenPresetId = available[index].id;
            EditorGUILayout.HelpBox(available[index].description, MessageType.None);
            if (GUILayout.Button("Apply selected preset", GUILayout.Height(24f)))
            {
                int applied = 0;
                foreach (UnityEngine.Object obj in editor.targets)
                    if (MOAsteroidMaterialPresets.Apply(obj as Material, available[index])) applied++;
                if (applied > 0)
                {
                    lastMaterial = null; // reload selection when inspector is redrawn
                    editor.Repaint();
                }
            }
            EditorGUILayout.HelpBox("Applying a preset replaces all exposed surface values on the selected .mat. Subsequent manual changes become Custom. Make a unique material first if different fields must look different.", MessageType.None);
            EditorGUILayout.Space(8f);
        }

        private static bool Fold(ref bool open, string title)
        {
            open = EditorGUILayout.Foldout(open, title, true);
            return open;
        }

        private void Field(MaterialEditor editor, MaterialProperty[] props, string name, string label)
        {
            MaterialProperty property = FindProperty(name, props, false);
            if (property == null) return;
            EditorGUI.BeginChangeCheck();
            editor.ShaderProperty(property, label);
            if (EditorGUI.EndChangeCheck()) anyMaterialPropertyChanged = true;
        }
    }
}
