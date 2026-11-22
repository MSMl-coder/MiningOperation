using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>Only changes presentation; keeps serialized field names/asset GUIDs intact.</summary>
    [CustomEditor(typeof(AsteroidGeneratorSettings))]
    public sealed class MOAsteroidGeneratorSettingsEditor : UnityEditor.Editor
    {
        private bool resources = true, geometry = true, physics, lod;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("MINING OPERATION / ASTEROID GENERATOR", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This asset defines one asteroid's geology, symbolic resources and geometry quality. Field positioning and material textures belong elsewhere.", MessageType.None);
            Property("catalog", "Resource catalog (required)");
            SpaceFold(ref geometry, "GEOMETRY / SHAPE");
            if (geometry)
            {
                Property("shapePreset", "Shape family");
                Property("shapeIrregularity", "Irregularity");
                Property("surfaceRoughness", "Surface roughness");
                Property("minimumAxisScale", "Minimum axis scale");
                Property("craterIntensity", "Crater depth");
                Property("minCraters", "Min geometric craters");
                Property("maxCraters", "Max geometric craters");
                Property("minimumDiameterMeters", "Legacy min diameter (shared)");
                Property("maximumDiameterMeters", "Legacy max diameter (shared)");
                EditorGUILayout.HelpBox("Each Spawn Point can override its own size range and shape. This asset remains the shared default.", MessageType.None);
            }
            SpaceFold(ref resources, "RESOURCE COMPOSITION (NOT MATERIAL)");
            if (resources)
            {
                Property("minimumResources", "Min resources");
                Property("maximumResources", "Max resources");
                Property("classIIProbability", "Class II probability");
                Property("classIIIProbability", "Class III probability");
                Property("rareChanceClassII", "Rare resource chance II");
                Property("rareChanceClassIII", "Rare resource chance III");
                Property("exoticChanceClassIII", "Exotic resource chance III");
                Property("maximumRareFractionClassII", "Rare mass fraction II");
                Property("maximumRareFractionClassIII", "Rare mass fraction III");
                Property("maximumExoticFractionClassIII", "Exotic mass fraction III");
            }
            SpaceFold(ref physics, "PHYSICS / SAVE DATA");
            if (physics)
            {
                Property("minimumPorosity", "Minimum porosity");
                Property("maximumPorosity", "Maximum porosity");
                Property("volumeSubdivisions", "Volume calculation precision");
            }
            SpaceFold(ref lod, "GEOMETRY LOD DEFAULTS");
            if (lod)
            {
                Property("lod0Subdivisions", "LOD 0 subdivisions");
                Property("lod1Subdivisions", "LOD 1 subdivisions");
                Property("lod2Subdivisions", "LOD 2 subdivisions");
                Property("lod3Subdivisions", "LOD 3 subdivisions");
                Property("lod4Subdivisions", "LOD 4 subdivisions");
            }
            serializedObject.ApplyModifiedProperties();
        }
        private void Property(string path, string label)
        {
            SerializedProperty p = serializedObject.FindProperty(path);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label));
        }
        private static void SpaceFold(ref bool expanded, string title)
        {
            EditorGUILayout.Space(5);
            expanded = EditorGUILayout.Foldout(expanded, title, true);
        }
    }
}
