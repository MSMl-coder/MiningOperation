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
                    // Baked objects have persistent MeshRenderers. Changing a Spawn Point
                    // material must update those renderers, not just the settings asset.
                    if (anchor.Controller != null && anchor.Controller.BakedSceneRoot != null)
                    {
                        try { MOBakedMaterialSync.Apply(anchor, false); }
                        catch (System.Exception ex) { Debug.LogException(ex, anchor); }
                    }
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
                            anchor.Controller?.InvalidateLocalConfiguration();
                            if (anchor.Controller != null && anchor.Controller.BakedSceneRoot != null)
                                MOBakedMaterialSync.Apply(anchor, false);
                            if (anchor.gameObject.scene.IsValid())
                                EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                            Selection.activeObject = copy;
                        }
                        catch (System.Exception ex) { Debug.LogException(ex, anchor); }
                    }
                }
                if (GUILayout.Button("Assign Textured Rock / Ice Material", GUILayout.Height(24f)))
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                        EditorUtility.DisplayDialog("Edit Mode required", "Stop Play Mode before assigning materials.", "OK");
                    else
                    {
                        try
                        {
                            string id = anchor.contentType == MiningFieldContent.Ice ||
                                anchor.contentType == MiningFieldContent.IceShelf ? "ice.fractured" : "rock.cinematic_fractured";
                            Material textured = MOTexturedMaterialLibrary.CreateOrReuse(MOTexturedMaterialLibrary.Find(id));
                            Undo.RecordObject(anchor, "Assign textured asteroid material");
                            if (anchor.visuals == null) anchor.visuals = new MOSpawnVisuals();
                            anchor.visuals.unifiedAsteroidMaterialOverride = textured;
                            EditorUtility.SetDirty(anchor);
                            anchor.Controller?.InvalidateLocalConfiguration();
                            if (anchor.Controller != null && anchor.Controller.BakedSceneRoot != null)
                                MOBakedMaterialSync.Apply(anchor, false);
                            if (anchor.gameObject.scene.IsValid())
                                EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                            Selection.activeObject = textured;
                        }
                        catch (System.Exception ex) { Debug.LogException(ex, anchor); }
                    }
                }
                if (anchor.Controller != null && anchor.Controller.BakedSceneRoot != null)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("BAKED FIELD MATERIAL", EditorStyles.boldLabel);
                    if (GUILayout.Button("Apply Material to Baked Asteroids", GUILayout.Height(28f)))
                    {
                        try { MOBakedMaterialSync.Apply(anchor, false); }
                        catch (System.Exception ex) { Debug.LogException(ex, anchor); }
                    }
                    if (GUILayout.Button("Force Replace All Baked LOD Materials", GUILayout.Height(24f)) &&
                        EditorUtility.DisplayDialog("Replace all generated asteroid materials?",
                            "This replaces material overrides on ALL generated asteroid LOD renderers, including protected asteroids. It does not change geometry, colliders, root Transforms, or custom child objects.",
                            "Replace all", "Cancel"))
                    {
                        try { MOBakedMaterialSync.Apply(anchor, true); }
                        catch (System.Exception ex) { Debug.LogException(ex, anchor); }
                    }
                    EditorGUILayout.HelpBox("Local Material Override syncs automatically to managed baked LOD renderers. Individually customized or protected asteroids are preserved; use Force Replace only when you want a full material reset.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("There is no baked field yet. The selected material will be assigned during the next Bake.", MessageType.None);
                }
                EditorGUILayout.HelpBox("Choose a texture-backed .mat and edit its albedo, normal and mask maps in the Material Inspector. Material data is not edited in Field Studio.", MessageType.None);
            }
            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Open Environment Generator", GUILayout.Height(32f)))
                AsteroidFieldStudio.OpenFor(anchor);
            EditorGUILayout.HelpBox("Move or rotate this empty object in Scene View. Formation and build actions are in Field Studio; surface presets are in the .mat Inspector.", MessageType.None);
        }
    }
}
