using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Repair fast Ground Fog assets without rebuilding or overwriting saved gas records.
    /// The menu entry is visible independently of the selected Field Studio tab/mode.
    /// </summary>
    public static class MOGroundFogAssetRepair
    {
        private const string MenuPath = "Tools/Mining Operation/Repair Fast Ground Fog Assets";

        [MenuItem(MenuPath)]
        public static void RepairFromToolsMenu()
        {
            var anchor = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<AsteroidFieldSpawnPoint>()
                : null;
            if (anchor == null)
            {
                EditorUtility.DisplayDialog("Fast Ground Fog assets",
                    "Select the Gas Spawn Point object in the Hierarchy, then run\n" +
                    MenuPath + ".\n\nThis keeps other fields and materials untouched.", "OK");
                return;
            }
            Repair(anchor);
        }

        public static bool Repair(AsteroidFieldSpawnPoint anchor)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MO Fog] Stop Play Mode before repairing editor assets.");
                return false;
            }
            if (anchor == null || anchor.settings == null)
            {
                Debug.LogError("[MO Fog] Select a Spawn Point with assigned Field Settings.");
                return false;
            }

            try
            {
                if (!MOVisualAssetInstaller.InstallFastGroundFogAssets(anchor.settings)) return false;

                // Rebuild only the procedural visual mesh, never BuildRecords(),
                // the asteroid snapshot, or gas field layout.
                if (anchor.contentType == MiningFieldContent.Gas &&
                    anchor.gasPresentation == MOGasPresentation.GroundFog &&
                    anchor.groundFogQuality == MOGroundFogQuality.FastSurface)
                {
                    MOGasFieldRenderer renderer = anchor.GetComponent<MOGasFieldRenderer>();
                    if (renderer != null && renderer.Snapshot != null)
                    {
                        renderer.RebuildVisuals();
                        EditorUtility.SetDirty(renderer);
                    }
                }

                if (anchor.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);

                Debug.Log("[MO Fog] Fast fog assets ready. Shared asset: " +
                    AssetDatabase.GetAssetPath(anchor.settings.groundFogFastMaterial), anchor);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, anchor);
                return false;
            }
        }
    }
}
