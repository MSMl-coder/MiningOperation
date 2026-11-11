using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiningOperation.Framework
{
    public enum ApplicationState { Booting, MainMenu, Loading, InGame, ShuttingDown, Error }

    public interface IFrameworkLogger
    {
        void Info(string message);
        void Error(string message, Exception exception = null);
    }

    public sealed class UnityFrameworkLogger : IFrameworkLogger
    {
        public void Info(string message) => Debug.Log("[MO] " + message);
        public void Error(string message, Exception exception = null) =>
            Debug.LogError("[MO] " + message + (exception == null ? "" : "\n" + exception));
    }

    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (_services.ContainsKey(typeof(T)))
                throw new InvalidOperationException("Service registered twice: " + typeof(T).FullName);
            _services.Add(typeof(T), service);
        }

        public T Resolve<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service)) return (T)service;
            throw new InvalidOperationException("Service not registered: " + typeof(T).FullName);
        }
    }

    public sealed class ApplicationStateMachine
    {
        public ApplicationState State { get; private set; } = ApplicationState.Booting;
        public event Action<ApplicationState> Changed;

        public void Set(ApplicationState state)
        {
            if (State == state) return;
            State = state;
            Changed?.Invoke(state);
        }
    }

    public interface ISceneService
    {
        Task LoadAsync(string sceneName, CancellationToken cancellationToken = default);
    }

    public sealed class UnitySceneService : ISceneService
    {
        public async Task LoadAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) throw new ArgumentException("Scene name is required");
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException("Scene not found in Build Profiles: " + sceneName);
            // Unity's asynchronous scene operation is allowed to finish once started.
            // Cancellation is intentionally observed only before starting a transition.
            while (!operation.isDone) await Task.Yield();
        }
    }

    public sealed class ApplicationHost
    {
        private readonly ISceneService _scenes;
        private readonly IFrameworkLogger _log;
        private readonly ApplicationStateMachine _states;
        private bool _transitioning;

        public ApplicationHost(ISceneService scenes, IFrameworkLogger log, ApplicationStateMachine states)
        {
            _scenes = scenes;
            _log = log;
            _states = states;
        }

        public ApplicationState State => _states.State;

        public Task OpenMenuAsync() => TransitionAsync("01_MainMenu", ApplicationState.MainMenu);
        public Task OpenEmptyGameAsync() => TransitionAsync("02_EmptyGame", ApplicationState.InGame);

        private async Task TransitionAsync(string scene, ApplicationState target)
        {
            if (_transitioning) return;
            _transitioning = true;
            _states.Set(ApplicationState.Loading);
            try
            {
                _log.Info("Loading scene: " + scene);
                await _scenes.LoadAsync(scene);
                _states.Set(target);
                _log.Info("Scene ready: " + scene);
            }
            catch (Exception exception)
            {
                _states.Set(ApplicationState.Error);
                _log.Error("Scene transition failed", exception);
            }
            finally { _transitioning = false; }
        }

        public void Quit()
        {
            _states.Set(ApplicationState.ShuttingDown);
            _log.Info("Quit requested");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
