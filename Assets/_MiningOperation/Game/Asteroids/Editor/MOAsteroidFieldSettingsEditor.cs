using UnityEditor;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>Field layout vs streaming vs appearance; serialized API intentionally unchanged.</summary>
    [CustomEditor(typeof(AsteroidFieldSettings))]
    public sealed class MOAsteroidFieldSettingsEditor : UnityEditor.Editor
    {
        private bool sources = true, layout = true, streaming, debris, legacy;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("MINING OPERATION / FIELD SETTINGS", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("A Field asset controls layout and rendering budgets. Shape/resources belong to Generator Settings; Albedo/Normal/Mask belong to the Material Inspector.", MessageType.None);
            Fold(ref sources, "GENERATOR & MATERIAL REFERENCES");
            if (sources)
            {
                P("asteroidSettings", "Rock Generator Settings");
                P("iceGeneratorSettings", "Ice Generator Settings");
                P("unifiedAsteroidMaterial", "Default Rock / Ice Material");
                P("iceMaterial", "Legacy ice material fallback");
                P("gasParticleMaterial", "Gas particle material");
                P("gasVolumeMaterial", "Gas volume material");
                if (GUILayout.Button("Install textured material library"))
                    MOTexturedMaterialLibrary.InstallAll();
            }
            Fold(ref layout, "DEFAULT FORMATION / POPULATION");
            if (layout)
            {
                P("formation", "Formation"); P("asteroidCount", "Asteroid count");
                P("smallAsteroidBias", "Small body bias"); P("radiusMeters", "Radius, m");
                P("thicknessMeters", "Thickness, m"); P("arcDegrees", "Arc, degrees");
                P("cloudVerticalScale", "Vertical density"); P("clusterCount", "Clusters");
                P("clusterRadiusFraction", "Cluster radius fraction");
                P("minimumSurfaceGapMeters", "Min surface gap, m");
                P("placementAttempts", "Placement attempts");
                P("useComposedLayout", "Composed layout");
                P("clusterStrength", "Cluster strength");
                P("densityPockets", "Density pockets");
                P("pocketWidthFraction", "Pocket width fraction");
                EditorGUILayout.HelpBox("Individual Spawn Point overrides take precedence over these shared defaults.", MessageType.None);
            }
            Fold(ref streaming, "STREAMING / LOD / MOTION");
            if (streaming)
            {
                P("useGpuFarField", "GPU far field"); P("exactFarShape", "Exact far silhouette");
                P("exactFarSubdivisions", "Exact far subdivisions");
                P("exactFarBuildsPerFrame", "Far builds/frame");
                P("farBuildBudgetMs", "Far build budget, ms");
                P("maxFarRenderDraws", "Max far draws");
                P("fastStreamedAsteroids", "Fast stream meshes");
                P("streamedMeshSubdivisions", "Stream subdivisions");
                P("maximumStreamedSubdivisions", "Maximum stream subdivisions");
                P("cachedStreamedAsteroids", "Cached asteroids");
                P("maxDetailedAsteroids", "Max detailed");
                P("detailedDistanceMeters", "Detailed range, m");
                P("spawnDistanceMeters", "Spawn distance, m");
                P("despawnDistanceMeters", "Despawn distance, m");
                P("distantDrawDistanceMeters", "Far draw distance, m");
                P("maxActiveAsteroids", "Max active");
                P("generatePerFrame", "Generation/frame");
                P("instantiatePerFrame", "Instances/frame");
                P("streamingTickSeconds", "Streaming tick, s");
                P("enableAsteroidRotation", "Asteroid rotation");
                P("rotationSpeedMultiplier", "Rotation multiplier");
                P("mineableRotationMultiplier", "Mineable rotation multiplier");
                P("decorativeRotationMultiplier", "Debris rotation multiplier");
                P("meshColliders", "Mesh collider"); P("hideDepletedAsteroids", "Hide depleted");
            }
            Fold(ref debris, "DECORATIVE DEBRIS");
            if (debris)
            {
                P("enableDecorativeDebris", "Enable debris");
                P("decorativeRockCount", "Decorative rocks");
                P("microFragmentCount", "Micro fragments");
                P("debrisSpreadMeters", "Spread, m");
                P("fragmentMinDiameter", "Min fragment diameter");
                P("fragmentMaxDiameter", "Max fragment diameter");
                P("debrisDrawDistance", "Debris draw distance");
                P("debrisMeshVariants", "Mesh variants");
                P("distantMeshVariants", "Legacy far variants");
                P("distantSubdivisions", "Legacy far subdivisions");
            }
            Fold(ref legacy, "LEGACY COMPATIBILITY (DO NOT DELETE)");
            if (legacy)
            {
                P("asteroidMaterial", "Legacy near material");
                P("distantMaterial", "Legacy far material");
                P("rockAlbedoTexture", "Legacy rock albedo");
                P("rockNormalTexture", "Legacy rock normal");
                P("rockNormalStrength", "Legacy normal strength");
                P("allowExperimentalRockShaders", "Legacy custom shader switch");
                EditorGUILayout.HelpBox("Kept serialized for old projects. New materials own texture references; none of these fields is removed by this update.", MessageType.None);
            }
            serializedObject.ApplyModifiedProperties();
        }
        private void P(string id, string label)
        {
            var prop = serializedObject.FindProperty(id);
            if (prop != null) EditorGUILayout.PropertyField(prop, new GUIContent(label));
        }
        private static void Fold(ref bool value, string name)
        {
            EditorGUILayout.Space(5);
            value = EditorGUILayout.Foldout(value, name, true);
        }
    }
}
