using System;
using UnityEngine;

namespace MiningOperation.Framework
{
    [DefaultExecutionOrder(-1000)]
    public sealed class FoundationBootstrap : MonoBehaviour
    {
        public static ApplicationHost Host { get; private set; }
        private static FoundationBootstrap _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            if (Host == null)
            {
                var services = new ServiceRegistry();
                IFrameworkLogger logger = new UnityFrameworkLogger();
                var states = new ApplicationStateMachine();
                ISceneService scenes = new UnitySceneService();
                services.Register<IFrameworkLogger>(logger);
                services.Register<ISceneService>(scenes);
                services.Register<ApplicationStateMachine>(states);
                Host = new ApplicationHost(services.Resolve<ISceneService>(), services.Resolve<IFrameworkLogger>(), states);
                logger.Info("Foundation initialized");
            }
        }

        private async void Start()
        {
            if (_instance != this) return;
            try
            {
                await FoundationSettingsServices.EnsureLoadedAsync();
                await Host.OpenMenuAsync();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            Host = null;
        }
    }
}
