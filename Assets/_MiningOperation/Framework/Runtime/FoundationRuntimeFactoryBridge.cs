using System;
using System.Collections.Generic;
using System.Linq;

namespace MiningOperation.Framework
{
    /// <summary>
    /// Bridges the existing production session/scene lifecycle to the opt-in factory container.
    /// The legacy FoundationScope coordinator remains authoritative for previous extensions.
    /// The bridge does not own/dispose the session service or the FoundationSession DTO.
    /// </summary>
    public sealed class FoundationRuntimeFactoryBridge : IDisposable
    {
        private readonly FoundationSessionService _service;
        private readonly FoundationVersionInfo _version;
        private readonly FoundationFactoryScope _application;
        private readonly Dictionary<ulong, FoundationFactoryScope> _scenes =
            new Dictionary<ulong, FoundationFactoryScope>();
        private readonly Dictionary<FoundationFactoryScope, FoundationRuntimeSceneContext> _sceneContexts =
            new Dictionary<FoundationFactoryScope, FoundationRuntimeSceneContext>();
        private FoundationFactoryScope _sessionScope;
        private FoundationSession _active;
        private bool _disposed;

        public FoundationFactoryScope ApplicationScope { get { Check(); return _application; } }
        public FoundationFactoryScope SessionScope { get { Check(); return _sessionScope; } }
        public FoundationSession ActiveSession { get { Check(); return _active; } }
        public int SceneCount { get { Check(); return _scenes.Count; } }
        public bool IsDisposed => _disposed;

        public FoundationRuntimeFactoryBridge(FoundationSessionService service, FoundationVersionInfo version)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _version = version ?? throw new ArgumentNullException(nameof(version));
            var registry = new FoundationFactoryRegistry();
            registry.RegisterInstance(_service);
            registry.RegisterInstance(_version);
            // No ownership of session data; the returned DTO is never IDisposable.
            registry.Register(FoundationScopeKind.Session,
                s => _active ?? throw new InvalidOperationException("No active session"));
            registry.Register(FoundationScopeKind.Scene,
                s => _sceneContexts.TryGetValue(s, out var context)
                    ? context : throw new InvalidOperationException("Scene scope is not bound"));
            _application = registry.Build();
        }

        public void ChangeSession(FoundationSession session)
        {
            Check();
            if (ReferenceEquals(_active, session)) return;
            CloseAllScenes();
            _sessionScope?.Dispose();
            _sessionScope = null;
            _active = session;
            if (session != null) _sessionScope = _application.CreateChild(FoundationScopeKind.Session);
        }

        public void OpenScene(ulong handle, string name)
        {
            Check();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Scene name required", nameof(name));
            if (_scenes.ContainsKey(handle)) return;
            var parent = _sessionScope ?? _application;
            var scope = parent.CreateChild(FoundationScopeKind.Scene);
            _sceneContexts.Add(scope, new FoundationRuntimeSceneContext(handle, name));
            _scenes.Add(handle, scope);
        }

        public FoundationFactoryScope GetSceneScope(ulong handle)
        {
            Check();
            _scenes.TryGetValue(handle, out var result);
            return result;
        }

        public void CloseScene(ulong handle)
        {
            Check();
            if (!_scenes.TryGetValue(handle, out var scope)) return;
            _scenes.Remove(handle);
            _sceneContexts.Remove(scope);
            scope.Dispose();
        }

        public void CloseAllScenes()
        {
            Check();
            foreach (var scope in _scenes.Values.ToArray()) scope.Dispose();
            _scenes.Clear();
            _sceneContexts.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            CloseAllScenes();
            _sessionScope?.Dispose();
            _application.Dispose();
            _active = null;
            _sessionScope = null;
            _disposed = true;
        }

        private void Check()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FoundationRuntimeFactoryBridge));
        }
    }

    public sealed class FoundationRuntimeSceneContext
    {
        public ulong Handle { get; }
        public string Name { get; }
        public FoundationRuntimeSceneContext(ulong handle, string name)
        {
            Handle = handle;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
