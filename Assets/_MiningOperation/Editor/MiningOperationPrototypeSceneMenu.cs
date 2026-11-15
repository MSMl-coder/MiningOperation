#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiningOperation.Editor
{
    public static class MiningOperationPrototypeSceneMenu
    {
        [MenuItem("Mining Operation/Create or Open Prototype Scene")]
        public static void CreateScene()
        {
            const string scenePath="Assets/_MiningOperation/Scenes/MO_Prototype.unity";
            if(File.Exists(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath);
                Debug.Log("[MO] Opened existing prototype scene: "+scenePath);
                return;
            }
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()==false)return;
            Directory.CreateDirectory("Assets/_MiningOperation/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=new GameObject("Mining Operation Prototype Root");
            go.AddComponent<MiningOperationDemoBootstrap>();
            EditorSceneManager.SaveScene(scene,scenePath);
            AssetDatabase.Refresh();
            Debug.Log("[MO] Created prototype scene: "+scenePath);
        }
    }
}
#endif
