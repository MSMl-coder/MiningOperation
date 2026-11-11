using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiningOperation.Framework
{
    /// <summary>One test action, rebindable at runtime, with independently persisted overrides.</summary>
    public enum FoundationInputContext { Menu, Gameplay, Modal }

    public sealed class FoundationInputService : MonoBehaviour
    {
        public static FoundationInputService Instance { get; private set; }
        public event Action TestActionTriggered;
        public FoundationInputContext Context { get; private set; } = FoundationInputContext.Menu;
        public void SetContext(FoundationInputContext context) => Context = context;
        private InputAction _testAction;
        private InputActionRebindingExtensions.RebindingOperation _rebinding;
        private string _bindingPath;
        private string _rebindPath;

        public string BindingDisplay => _testAction?.GetBindingDisplayString() ?? "F8";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _bindingPath = FoundationPaths.BindingFile;
            _testAction = new InputAction("FoundationTestAction", InputActionType.Button, "<Keyboard>/f8");
            _testAction.performed += OnTestAction;
            LoadOverrides();
            _testAction.Enable();
        }

        private void OnTestAction(InputAction.CallbackContext context)
        {
            Debug.Log("[MO/INPUT] Test action triggered: " + BindingDisplay);
            TestActionTriggered?.Invoke();
            FoundationAudioService.Instance?.PlayTestClick();
        }

        private void LoadOverrides()
        {
            try
            {
                if (File.Exists(_bindingPath))
                    _testAction.LoadBindingOverridesFromJson(File.ReadAllText(_bindingPath, Encoding.UTF8));
            }
            catch (Exception e) { Debug.LogWarning("[MO/INPUT] Could not load bindings: " + e.Message); }
        }

        public async Task<bool> StartRebindAsync()
        {
            if (_rebinding != null) throw new InvalidOperationException("Already rebinding");
            var completion = new TaskCompletionSource<bool>();
            _testAction.Disable();
            try
            {
                _rebinding = _testAction.PerformInteractiveRebinding()
                    .WithControlsHavingToMatchPath("<Keyboard>")
                    .WithCancelingThrough("<Keyboard>/escape")
                    .OnComplete(op => completion.TrySetResult(true))
                    .OnCancel(op => completion.TrySetResult(false));
                _rebinding.Start();
                bool applied = await completion.Task;
                if (applied) SaveOverrides();
                return applied;
            }
            finally
            {
                _rebinding?.Dispose();
                _rebinding = null;
                _testAction.Enable();
            }
        }

        public void RestoreDefaultBinding()
        {
            if (_rebinding != null) throw new InvalidOperationException("Finish rebinding first");
            _testAction.RemoveAllBindingOverrides();
            SaveOverrides();
        }

        private void SaveOverrides()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_bindingPath));
            _rebindPath = _bindingPath + ".tmp";
            File.WriteAllText(_rebindPath, _testAction.SaveBindingOverridesAsJson(), Encoding.UTF8);
            if (File.Exists(_bindingPath)) File.Delete(_bindingPath);
            File.Move(_rebindPath, _bindingPath);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            _rebinding?.Cancel();
            _rebinding?.Dispose();
            if (_testAction != null)
            {
                _testAction.performed -= OnTestAction;
                _testAction.Dispose();
            }
        }
    }
}
