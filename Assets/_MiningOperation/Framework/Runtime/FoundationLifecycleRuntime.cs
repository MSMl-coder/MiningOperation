using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiningOperation.Framework
{
    /// <summary>
    /// A backward-compatible scope coordinator with independent scopes for additive scenes.
    /// Scene handle is the authoritative key. String-based OpenScene remains for legacy tests.
    /// </summary>
    public sealed class FoundationSessionScopeCoordinator : IDisposable
    {
        private readonly FoundationScope _application;
        private readonly Dictionary<ulong, FoundationScope> _scenes = new Dictionary<ulong, FoundationScope>();
        private readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();
        private FoundationScope _session;
        private FoundationSession _active;
        private ulong _lastSceneHandle = ulong.MaxValue;
        private bool _disposed;

        public FoundationScope ApplicationScope => _application;
        public FoundationScope SessionScope => _session;
        public FoundationScope SceneScope => GetSceneScope(_lastSceneHandle);
        public FoundationSession ActiveSession => _active;
        public string ActiveSceneName => _names.TryGetValue(_lastSceneHandle, out var name) ? name : null;
        public int SceneCount => _scenes.Count;

        public FoundationSessionScopeCoordinator(FoundationVersionInfo version)
        {
            _application = new FoundationScope(FoundationScopeKind.Application);
            _application.Register(version ?? throw new ArgumentNullException(nameof(version)), false);
        }

        public void ChangeSession(FoundationSession session)
        {
            Check();
            if (ReferenceEquals(session, _active)) return;
            CloseAllScenes();
            _session?.Dispose();
            _session = null;
            _active = session;
            if (session != null)
            {
                _session = _application.CreateChild(FoundationScopeKind.Session);
                _session.Register(session, false);
            }
        }

        // Legacy test API: one synthetic scene, no real Unity handle.
        public void OpenScene(string sceneName) => OpenScene(ulong.MaxValue, sceneName);

        // Legacy integer handles stay available for older integration tests.
        public void OpenScene(int sceneHandle, string sceneName) =>
            OpenScene(unchecked((ulong)(uint)sceneHandle), sceneName);
        public FoundationScope GetSceneScope(int sceneHandle) =>
            GetSceneScope(unchecked((ulong)(uint)sceneHandle));
        public void CloseScene(int sceneHandle) =>
            CloseScene(unchecked((ulong)(uint)sceneHandle));
        public void CloseScene() => CloseScene(_lastSceneHandle);

        public void OpenScene(ulong sceneHandle, string sceneName)
        {
            Check();
            if (string.IsNullOrWhiteSpace(sceneName)) throw new ArgumentException("Scene name required", nameof(sceneName));
            if (_scenes.ContainsKey(sceneHandle))
            {
                _lastSceneHandle = sceneHandle;
                return;
            }
            var scope = (_session ?? _application).CreateChild(FoundationScopeKind.Scene);
            scope.Register(new FoundationSceneIdentity(sceneName), false);
            _scenes.Add(sceneHandle, scope);
            _names.Add(sceneHandle, sceneName);
            _lastSceneHandle = sceneHandle;
        }

        public FoundationScope GetSceneScope(ulong sceneHandle)
        {
            Check();
            _scenes.TryGetValue(sceneHandle, out var scope);
            return scope;
        }

        public void CloseScene(ulong sceneHandle)
        {
            Check();
            if (!_scenes.TryGetValue(sceneHandle, out var scope)) return;
            scope.Dispose();
            _scenes.Remove(sceneHandle);
            _names.Remove(sceneHandle);
            if (_lastSceneHandle == sceneHandle)
            {
                _lastSceneHandle = ulong.MaxValue;
                foreach (var handle in _scenes.Keys) _lastSceneHandle = handle;
            }
        }

        public void CloseAllScenes()
        {
            Check();
            foreach (var scope in new List<FoundationScope>(_scenes.Values)) scope.Dispose();
            _scenes.Clear(); _names.Clear(); _lastSceneHandle = ulong.MaxValue;
        }

        public void Dispose()
        {
            if (_disposed) return;
            CloseAllScenes();
            _session?.Dispose();
            _application.Dispose();
            _session = null; _active = null; _disposed = true;
        }

        private void Check()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FoundationSessionScopeCoordinator));
        }
    }

    public sealed class FoundationSceneIdentity
    {
        public string Name { get; }
        public FoundationSceneIdentity(string name) { Name = name; }
    }

    public static class FoundationLifecycleRuntime
    {
        private static FoundationSessionScopeCoordinator _coordinator;
        private static FoundationSessionService _boundService;
        private static FoundationRuntimeFactoryBridge _factoryBridge;
        private static bool _subscribed;

        public static FoundationRuntimeFactoryBridge FactoryBridge
        {
            get { EnsureInitialized(); return _factoryBridge; }
        }

        public static FoundationSessionScopeCoordinator Coordinator
        {
            get { EnsureInitialized(); return _coordinator; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Unsubscribe();
            if (_boundService != null) _boundService.ActiveSessionChanged -= SessionChanged;
            _boundService = null;
            _factoryBridge?.Dispose();
            _factoryBridge = null;
            _coordinator?.Dispose();
            _coordinator = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Start() => EnsureInitialized();

        public static void BindSessions(FoundationSessionService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            EnsureInitialized();
            if (ReferenceEquals(_boundService, service)) return;
            if (_boundService != null) _boundService.ActiveSessionChanged -= SessionChanged;
            _factoryBridge?.Dispose();
            _factoryBridge = new FoundationRuntimeFactoryBridge(service,
                _coordinator.ApplicationScope.Resolve<FoundationVersionInfo>());
            _boundService = service;
            _boundService.ActiveSessionChanged += SessionChanged;
            SessionChanged(service.Active);
        }

        private static void SessionChanged(FoundationSession session)
        {
            EnsureInitialized();
            _coordinator.ChangeSession(session);
            _factoryBridge?.ChangeSession(session);
            RegisterLoadedScenes();
        }

        private static void EnsureInitialized()
        {
            if (_coordinator == null)
            {
                _coordinator = new FoundationSessionScopeCoordinator(
                    new FoundationVersionInfo(Application.version, "1.0.0-rc4-migration", 1, "mining-operation"));
                RegisterLoadedScenes();
            }
            if (_subscribed) return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Application.quitting += OnQuit;
            _subscribed = true;
        }

        private static void RegisterLoadedScenes()
        {
            if (_coordinator == null) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                {
                    ulong handle = scene.handle.GetRawData();
                    _coordinator.OpenScene(handle, scene.name);
                    _factoryBridge?.OpenScene(handle, scene.name);
                }
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_coordinator == null || !scene.IsValid()) return;
            ulong handle = scene.handle.GetRawData();
            _coordinator.OpenScene(handle, scene.name);
            _factoryBridge?.OpenScene(handle, scene.name);
        }
        private static void OnSceneUnloaded(Scene scene)
        {
            if (_coordinator == null) return;
            ulong handle = scene.handle.GetRawData();
            _factoryBridge?.CloseScene(handle);
            _coordinator.CloseScene(handle);
        }
        private static void OnQuit()
        {
            Unsubscribe();
            if (_boundService != null) _boundService.ActiveSessionChanged -= SessionChanged;
            _boundService = null;
            _factoryBridge?.Dispose();
            _factoryBridge = null;
            _coordinator?.Dispose();
            _coordinator = null;
        }
        private static void Unsubscribe()
        {
            if (!_subscribed) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Application.quitting -= OnQuit;
            _subscribed = false;
        }
    }
}
