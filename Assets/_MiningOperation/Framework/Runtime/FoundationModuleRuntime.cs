using System;
using UnityEngine;

namespace MiningOperation.Framework
{
    // Integration point v0.5: additive, no edits to the working v0.4 Bootstrap.
    // Later Foundation versions can move this installer into ApplicationHost composition.
    public static class FoundationModuleRuntime
    {
        private static FoundationModuleHost _host;
        public static FoundationModuleHost Host => _host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _host?.Dispose();
            _host = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartModules()
        {
            if (_host != null) return;
            var host = new FoundationModuleHost();
            var context = new FoundationModuleContext(new FoundationDefinitionRegistry(), new UnityFrameworkLogger());
            try
            {
                host.Register(new FoundationDefinitionsModule());
                host.InitializeAll(context);
                _host = host;
                var go = new GameObject("MO_ModuleLifetime_v05");
                go.AddComponent<FoundationModuleLifetime>();
                UnityEngine.Object.DontDestroyOnLoad(go);
                context.Logger.Info("[Modules] v0.5 initialized: " + string.Join(", ", host.StartedIds));
            }
            catch (Exception e)
            {
                host.Dispose();
                Debug.LogException(e);
                Debug.LogError("[MO/Modules] Module startup failed. No gameplay modules will run.");
            }
        }

        internal static void ShutdownModules()
        {
            _host?.Dispose();
            _host = null;
        }
    }

    public sealed class FoundationModuleLifetime : MonoBehaviour
    {
        private void OnApplicationQuit() => FoundationModuleRuntime.ShutdownModules();
        private void OnDestroy()
        {
            if (!Application.isPlaying) FoundationModuleRuntime.ShutdownModules();
        }
    }
}
