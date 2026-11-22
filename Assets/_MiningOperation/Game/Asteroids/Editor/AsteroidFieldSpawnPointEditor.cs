using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>Compact Inspector entry point: all actual authoring stays in Field Studio.</summary>
    [CustomEditor(typeof(AsteroidFieldSpawnPoint))]
    public sealed class AsteroidFieldSpawnPointEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var anchor = (AsteroidFieldSpawnPoint)target;
            EditorGUILayout.LabelField("MINING OPERATION / LOCATION", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Type", anchor.contentType.ToString());
            EditorGUILayout.LabelField("Seed", anchor.fieldSeed.ToString());
            EditorGUILayout.LabelField("Settings", anchor.settings != null ? anchor.settings.name : "Missing");
            if (anchor.contentType == MiningFieldContent.Gas)
            {
                var gas = anchor.GetComponent<MOGasFieldRenderer>();
                EditorGUILayout.LabelField("Clouds", gas != null ? gas.CloudCount.ToString() : "Not generated");
            }
            else
            {
                var controller = anchor.Controller;
                EditorGUILayout.LabelField("Asteroids", controller != null && controller.Snapshot != null ?
                    controller.Snapshot.asteroids.Count.ToString() : "Not built");
            }
            if (anchor.contentType == MiningFieldContent.Gas)
            {
                EditorGUILayout.LabelField("GAS MATERIALS", EditorStyles.boldLabel);
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("visuals.gasVolumeMaterialOverride"), new GUIContent("Volume material"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("visuals.gasParticleMaterialOverride"), new GUIContent("Particle material"));
                if (serializedObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(anchor);
                    if (anchor.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                }
            }
            else
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("SURFACE MATERIAL", EditorStyles.boldLabel);
                serializedObject.Update();
                SerializedProperty slot = serializedObject.FindProperty("visuals.unifiedAsteroidMaterialOverride");
                if (slot != null)
                    EditorGUILayout.PropertyField(slot, new GUIContent("Local material override"));
                if (serializedObject.ApplyModifiedProperties())
                {
                    anchor.Controller?.InvalidateLocalConfiguration();
                    EditorUtility.SetDirty(anchor);
                    if (anchor.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                }
                Material active = anchor.visuals != null ? anchor.visuals.unifiedAsteroidMaterialOverride : null;
                if (active == null && anchor.settings != null) active = anchor.settings.unifiedAsteroidMaterial;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Edit Material", GUILayout.Height(24f)))
                    {
                        if (active != null) Selection.activeObject = active;
                        else Debug.LogWarning("[MO] No unified surface material assigned. Assign an existing .mat or install the unified surface via the asset tools.", anchor);
                    }
                    if (GUILayout.Button("Make Unique Material", GUILayout.Height(24f)))
                    {
                        try
                        {
                            Material copy = MOUnifiedAsteroidAssetTools.MakeLocal(anchor);
                            if (anchor.gameObject.scene.IsValid())
                                EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                            Selection.activeObject = copy;
                        }
                        catch (System.Exception ex) { Debug.LogException(ex, anchor); }
                    }
                }
                EditorGUILayout.HelpBox("Rock/ice presets, noise, crater and lighting values are edited only on the .mat asset. A shared material affects all its users.", MessageType.None);
            }
            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Open Environment Generator", GUILayout.Height(32f)))
                AsteroidFieldStudio.OpenFor(anchor);
            EditorGUILayout.HelpBox("Move or rotate this empty object in Scene View. Formation and build actions are in Field Studio; surface presets are in the .mat Inspector.", MessageType.None);
        }
    }
}
