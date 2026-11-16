#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MiningOperation.Editor
{
    // Non-destructive installer. NEVER regenerates or clears an existing authored scene.
    public static class MiningOperationPrototypeSceneMenu
    {
        private const string SettingsPath = "Assets/_MiningOperation/UI/MO_TerminalPanel.asset";
        [MenuItem("Mining Operation/1 - Bind Selected NPC Outpost")]
        public static void BindOutpost()
        {
            var scene = SceneManager.GetActiveScene();
            if(!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            { EditorUtility.DisplayDialog("Mining Operation", "Save the scene first.", "OK"); return; }
            var selected = Selection.activeGameObject;
            if(selected == null || selected.scene != scene)
            { EditorUtility.DisplayDialog("Mining Operation", "Select the EXISTING outpost root in the Hierarchy.", "OK"); return; }
            var existing = UnityEngine.Object.FindObjectsByType<MOSceneStationAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(a=>a.gameObject.scene == scene && a.gameObject != selected).ToList();
            if(existing.Count > 0)
            { EditorUtility.DisplayDialog("Mining Operation", "There can be only ONE NPC outpost per scene. Remove the other MOSceneStationAnchor first.", "OK"); return; }
            var anchor = selected.GetComponent<MOSceneStationAnchor>() ?? Undo.AddComponent<MOSceneStationAnchor>(selected);
            Undo.RecordObject(anchor, "Bind Mining Operation NPC station");
            if(scene.name == "02_EmptyGame") anchor.stationId = "station.public";
            else if(string.IsNullOrWhiteSpace(anchor.stationId) || anchor.stationId == "station.public")
                anchor.stationId = "station.npc." + Sanitize(scene.path);
            anchor.displayName = selected.name;
            EditorUtility.SetDirty(anchor);EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[MO] Bound artist's existing station " + selected.name + " to " + anchor.stationId + " in " + scene.path);
        }
        [MenuItem("Mining Operation/2 - Setup Current Scene (Preserve Models)")]
        public static void SetupCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            if(!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            { EditorUtility.DisplayDialog("Mining Operation", "Save the scene first.", "OK"); return; }
            var stationAnchors=UnityEngine.Object.FindObjectsByType<MOSceneStationAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(a=>a.gameObject.scene == scene).ToList();
            if(stationAnchors.Count != 1)
            { EditorUtility.DisplayDialog("Mining Operation", "Bind exactly ONE NPC outpost before scene setup. Select your station model and choose '1 - Bind Selected NPC Outpost'.", "OK");return; }
            var roots=UnityEngine.Object.FindObjectsByType<MOSceneCoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(a=>a.gameObject.scene == scene).ToList();
            if(roots.Count>1)
            {EditorUtility.DisplayDialog("Mining Operation", "Multiple MOSceneCoordinator components detected. Keep only one.", "OK");return;}
            var root=roots.Count==1?roots[0].gameObject:new GameObject("MO_GAME_RUNTIME");
            if(roots.Count==0)Undo.RegisterCreatedObjectUndo(root,"Create Mining Operation runtime");
            var coord=root.GetComponent<MOSceneCoordinator>()??Undo.AddComponent<MOSceneCoordinator>(root);
            if(roots.Count==0){coord.loadPreviousSaveOnPlay=true;coord.createShipPlaceholder=true;coord.externalShipController=false;}
            var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(SettingsPath);
            if(panel==null)
            {
                Directory.CreateDirectory("Assets/_MiningOperation/UI");
                panel=ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel,SettingsPath);
            }
            var document=root.GetComponent<UIDocument>()??Undo.AddComponent<UIDocument>(root);
            document.panelSettings=panel;document.sortingOrder=200;
            var ui=root.GetComponent<MOStationTerminal>()??Undo.AddComponent<MOStationTerminal>(root);
            if(root.GetComponent<MOSceneVisualPlaceholders>()==null)Undo.AddComponent<MOSceneVisualPlaceholders>(root);
            // Disable only legacy test UI components in THIS SCENE (no edits to Foundation code).
            int disabled=0;
            foreach(var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(component == null || component.gameObject.scene != scene)continue;
                if(component.GetType().Name=="FoundationScreen" || component.GetType().Name=="MiningOperationDemoBootstrap")
                {
                    Undo.RecordObject(component,"Disable obsolete UI in this scene"); component.enabled=false;disabled++;
                    if(component.GetType().Name=="FoundationScreen")
                    {
                        var oldDoc=component.GetComponent<UIDocument>();
                        if(oldDoc!=null){Undo.RecordObject(oldDoc,"Disable old game UI document");oldDoc.enabled=false;}
                    }
                }
                if(component.GetType().Name=="PrototypeWorldView")
                {Undo.RecordObject(component,"Disable model-spawning prototype view");component.enabled=false;disabled++;}
            }
            EditorUtility.SetDirty(coord);EditorUtility.SetDirty(document);EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject=root;
            Debug.Log("[MO] Setup complete on EXISTING scene " + scene.path + ". Kept all authored objects. Disabled " + disabled + " legacy components. Save scene with Ctrl/Cmd+S.");
        }
        [MenuItem("Mining Operation/3 - Bind Selected Asteroid Mesh")]
        public static void BindAsteroid()
        {
            var obj=Selection.activeGameObject;var scene=SceneManager.GetActiveScene();
            if(obj==null || obj.scene!=scene){EditorUtility.DisplayDialog("Mining Operation","Select an existing asteroid GameObject in this scene.","OK");return;}
            var anchor=obj.GetComponent<MOSceneAsteroidAnchor>()??Undo.AddComponent<MOSceneAsteroidAnchor>(obj);
            Undo.RecordObject(anchor,"Bind asteroid model");
            if(string.IsNullOrWhiteSpace(anchor.asteroidId))anchor.asteroidId="asteroid."+Guid.NewGuid().ToString("N");
            anchor.displayName=obj.name;
            EditorUtility.SetDirty(anchor);EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[MO] Asteroid mesh bound to " + anchor.asteroidId + ". Set oreItemId and startingOreKg in Inspector.");
        }
        [MenuItem("Mining Operation/4 - Validate Current Scene")]
        public static void ValidateCurrentScene()
        {
            var scene=SceneManager.GetActiveScene();
            var stations=UnityEngine.Object.FindObjectsByType<MOSceneStationAnchor>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(x=>x.gameObject.scene==scene).ToArray();
            var roots=UnityEngine.Object.FindObjectsByType<MOSceneCoordinator>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(x=>x.gameObject.scene==scene).ToArray();
            var asteroids=UnityEngine.Object.FindObjectsByType<MOSceneAsteroidAnchor>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(x=>x.gameObject.scene==scene).ToArray();
            var unique=asteroids.Select(x=>x.asteroidId).Distinct().Count()==asteroids.Length;
            bool okay=stations.Length==1 && roots.Length==1 && unique &&
                roots[0].GetComponent<UIDocument>()?.panelSettings!=null &&
                roots[0].GetComponent<MOStationTerminal>()!=null;
            var report="NPC stations: "+stations.Length+" (required 1)\nGame runtime roots: "+roots.Length+
                " (required 1)\nAsteroid anchors: "+asteroids.Length+"\nIDs unique: "+unique+"\n"+
                (okay?"READY TO TEST":"FIX THE ISSUES ABOVE");
            Debug.Log("[MO] " + scene.name + " validation: " + report.Replace("\n"," | "));
            EditorUtility.DisplayDialog("Mining Operation / Scene Validation",report,"OK");
        }
        private static string Sanitize(string path)
        {
            return new string(path.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }
    }
}
#endif
