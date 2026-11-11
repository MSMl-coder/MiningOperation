#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using MiningOperation.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MiningOperation.Framework.Editor
{
    /// <summary>Idempotent scene wiring for the migrated Foundation. Does not delete scene objects.</summary>
    public static class MiningOperationFoundationSetup
    {
        private const string Root = "Assets/_MiningOperation";
        private const string Scenes = Root + "/Scenes";
        private const string UI = Root + "/UI";
        private const string PanelPath = UI + "/FoundationPanelSettings.asset";
        private static readonly string[] SceneNames = { "00_Bootstrap", "01_MainMenu", "02_EmptyGame" };

        [MenuItem("Mining Operation/Framework/01 - Configure Foundation Scenes")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MO/SETUP] Exit Play Mode before configuring scenes.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureAssetFolder(Scenes);
            EnsureAssetFolder(UI);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            string[] paths = SceneNames.Select(n => FindOrCreateScene(n)).ToArray();
            for (var i = 0; i < paths.Length; i++)
            {
                var scene = EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Single);
                if (i == 0) EnsureBootstrap(scene);
                else EnsureScreen(scene, panel, i == 1 ? FoundationScreen.ScreenKind.MainMenu : FoundationScreen.ScreenKind.EmptyGame);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Could not save scene: " + paths[i]);
            }

            // Sets the traditional build scene list. Users with custom Build Profiles must also
            // verify the active profile's Scene List in the Unity 6 editor.
            var current = EditorBuildSettings.scenes
                .Where(s => s != null && !paths.Contains(s.path, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true)).Concat(current).ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(paths[0], OpenSceneMode.Single);
            Debug.Log("[MO/SETUP] Foundation scenes wired; Bootstrap opened. " +
                      "Verify the active Build Profile scene list, then press Play.");
        }

        [MenuItem("Mining Operation/Framework/02 - Run Preflight Checks")]
        public static void Preflight()
        {
            int errors = 0;
            foreach (var name in SceneNames)
            {
                var path = FindExistingScene(name);
                if (path == null)
                {
                    Debug.LogError("[MO/PREFLIGHT] Missing scene " + name);
                    errors++;
                }
                else if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path))
                {
                    Debug.LogWarning("[MO/PREFLIGHT] Scene not enabled in EditorBuildSettings: " + path);
                }
            }
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath) == null)
            {
                Debug.LogError("[MO/PREFLIGHT] Missing PanelSettings. Run Configure first.");
                errors++;
            }
            if (Type.GetType("UnityEngine.InputSystem.InputAction, Unity.InputSystem") == null)
            {
                Debug.LogError("[MO/PREFLIGHT] Unity Input System package is unavailable.");
                errors++;
            }
            Debug.Log(errors == 0
                ? "[MO/PREFLIGHT] Basic asset checks passed. Still test Play and Save/Load manually."
                : "[MO/PREFLIGHT] Problems found: " + errors);
        }

        [MenuItem("Mining Operation/Framework/03 - Run Save-Load Smoke Test")]
        public static async void SmokeTestSaveLoad()
        {
            var folder = Path.Combine(Path.GetTempPath(), "mo-foundation-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                var service = new FoundationSessionService(folder);
                service.RegisterParticipant("foundation.smoke", () => new SmokeParticipant());
                service.NewSession("Foundation Smoke Test");
                var oldId = service.Active.SessionId;
                if (!service.Active.TryGetParticipant<SmokeParticipant>("foundation.smoke", out var before))
                    throw new InvalidOperationException("Smoke participant was not registered");
                before.Value = 37;
                var saveId = await service.SaveAsync();
                service.CloseSession();
                await service.LoadAsync(saveId);
                if (service.Active.SessionId != oldId ||
                    !service.Active.TryGetParticipant<SmokeParticipant>("foundation.smoke", out var after) ||
                    after.Value != 37)
                    throw new InvalidOperationException("Smoke test failed: participant data did not survive Save/Load");
                service.CloseSession();
                Debug.Log("[MO/SMOKE] PASS: separate directory, save checksum, session ID and module state restored.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[MO/SMOKE] FAIL: " + exception);
            }
            finally
            {
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception e) { Debug.LogWarning("[MO/SMOKE] Temp cleanup failed: " + e.Message); }
            }
        }

        [Serializable]
        private sealed class SmokeData { public int value; }

        private sealed class SmokeParticipant : IFoundationSaveParticipant
        {
            public string Key => "foundation.smoke";
            public int Version => 1;
            public int Value { get; set; }
            public string CaptureJson() => JsonUtility.ToJson(new SmokeData { value = Value });
            public void RestoreJson(string json, int version)
            {
                if (version != Version) throw new InvalidDataException("Unsupported smoke section version");
                var data = JsonUtility.FromJson<SmokeData>(json);
                if (data == null) throw new InvalidDataException("Smoke section is empty");
                Value = data.value;
            }
        }

        private static string FindExistingScene(string name)
        {
            var expected = Scenes + "/" + name + ".unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(expected) != null) return expected;
            var results = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (results.Length > 1)
                throw new InvalidOperationException("Multiple scenes named " + name + "; keep only one before setup.");
            return results.FirstOrDefault();
        }

        private static string FindOrCreateScene(string name)
        {
            var existing = FindExistingScene(name);
            if (existing != null) return existing;
            var path = Scenes + "/" + name + ".unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new IOException("Failed to create scene " + path);
            return path;
        }

        private static void EnsureBootstrap(Scene scene)
        {
            if (FindInScene<FoundationBootstrap>(scene) != null) return;
            var host = new GameObject("MO_ApplicationBootstrap");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<FoundationBootstrap>();
        }

        private static void EnsureScreen(Scene scene, PanelSettings panel, FoundationScreen.ScreenKind screenKind)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            var foundationScreen = FindInScene<FoundationScreen>(scene);
            GameObject go;
            if (foundationScreen == null)
            {
                go = new GameObject("MO_Foundation_UI");
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            else go = foundationScreen.gameObject;

            var document = go.GetComponent<UIDocument>();
            if (document == null) document = go.AddComponent<UIDocument>();
            // Assign PanelSettings BEFORE adding FoundationScreen: AddComponent can
            // trigger OnEnable immediately in the Editor and during Play Mode setup.
            document.panelSettings = panel;
            if (foundationScreen == null) foundationScreen = go.AddComponent<FoundationScreen>();
            foundationScreen.Configure(screenKind);
            EditorUtility.SetDirty(document);
            EditorUtility.SetDirty(foundationScreen);

            if (document.panelSettings == null)
                throw new InvalidOperationException("PanelSettings assignment failed on " + go.name);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new IOException("Invalid asset path " + path);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
