using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace MiningOperation.Framework
{
    public sealed class FoundationLogEntry
    {
        public readonly DateTime Utc;
        public readonly LogType Type;
        public readonly string Message;
        public readonly string Trace;
        public FoundationLogEntry(DateTime utc, LogType type, string message, string trace)
        { Utc = utc; Type = type; Message = message; Trace = trace; }
    }

    /// <summary>Observability only: subscribes to Unity logs, retains bounded history and writes diagnostics in batches.</summary>
    public sealed class FoundationDiagnosticsStore : IDisposable
    {
        private readonly Queue<FoundationLogEntry> _entries = new Queue<FoundationLogEntry>();
        private readonly StringBuilder _pending = new StringBuilder(4096);
        private readonly string _logPath;
        private bool _disposed;
        private bool _flushing;
        private int _errors;
        private int _warnings;
        private const int MaxEntries = 120;
        public int ErrorCount => _errors;
        public int WarningCount => _warnings;
        public string LogPath => _logPath;
        public FoundationLogEntry[] Snapshot() => _entries.ToArray();

        public FoundationDiagnosticsStore(string path, bool subscribeUnityLogs = true)
        {
            _logPath = path ?? throw new ArgumentNullException(nameof(path));
            if (subscribeUnityLogs) Application.logMessageReceived += OnUnityLog;
        }

        private void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            Add(condition, stackTrace, type);
        }

        public void Add(string message, string trace, LogType type)
        {
            if (_disposed) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors++;
            if (type == LogType.Warning) _warnings++;
            if (_entries.Count >= MaxEntries) _entries.Dequeue();
            var entry = new FoundationLogEntry(DateTime.UtcNow, type, message ?? "", trace ?? "");
            _entries.Enqueue(entry);
            _pending.Append(entry.Utc.ToString("o")).Append(" [").Append(type).Append("] ")
                .AppendLine((message ?? "").Replace("\r", " ").Replace("\n", " "));
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                _pending.AppendLine((trace ?? "").Length > 4000 ? trace.Substring(0, 4000) : trace);
        }

        public void Flush()
        {
            if (_disposed || _flushing || _pending.Length == 0) return;
            _flushing = true;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath));
                File.AppendAllText(_logPath, _pending.ToString(), Encoding.UTF8);
                _pending.Clear();
            }
            catch (Exception ex)
            {
                // Never log with Debug.Log here: logging from a log sink would recursively re-enter the sink.
                System.Diagnostics.Debug.WriteLine("Mining Operation diagnostics write failed: " + ex.Message);
            }
            finally { _flushing = false; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Application.logMessageReceived -= OnUnityLog;
            Flush();
            _disposed = true;
        }
    }

    public sealed class FoundationDiagnosticsRuntime : MonoBehaviour
    {
        private static FoundationDiagnosticsRuntime _instance;
        public static FoundationDiagnosticsRuntime Instance => _instance;
        public FoundationDiagnosticsStore Store { get; private set; }
        public bool Visible { get; private set; }
        public bool ConsoleOpen { get; private set; }
        private float _fpsTimer;
        private int _frames;
        private float _fps;
        private float _flushTimer;
        private string _command = "";
        private string _commandResult = "help — list commands";
        private Vector2 _scroll;
        private GUIStyle _textStyle;
        private GUIStyle _smallStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (_instance != null) return;
            var go = new GameObject("MO_Diagnostics_v07");
            _instance = go.AddComponent<FoundationDiagnosticsRuntime>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            var path = Path.Combine(FoundationPaths.LogsDirectory,
                "foundation-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log");
            Store = new FoundationDiagnosticsStore(path);
            Debug.Log("[MO/DIAG] v0.7 loaded. F10 = diagnostics; Ctrl+Shift+K = developer console.");
        }

        private void Update()
        {
            // Input System was introduced in the tested v0.6 package.
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f10Key.wasPressedThisFrame) Visible = !Visible;
                bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
                bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                if (ctrl && shift && keyboard.kKey.wasPressedThisFrame)
                {
                    ConsoleOpen = !ConsoleOpen;
                    Visible = true;
                }
            }
            _frames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f) { _fps = _frames / Mathf.Max(_fpsTimer, 0.001f); _frames = 0; _fpsTimer = 0f; }
            _flushTimer += Time.unscaledDeltaTime;
            if (_flushTimer > 5f) { _flushTimer = 0; Store?.Flush(); }
        }

        private void OnGUI()
        {
            if (!Visible) return;
            // IMGUI is deliberately isolated to this developer-only overlay; regular menus remain UI Toolkit.
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                _textStyle.normal.textColor = Color.white;
                _smallStyle = new GUIStyle(_textStyle) { fontSize = 11 };
            }
            float w = Mathf.Min(520f, Screen.width - 20f);
            float h = ConsoleOpen ? Mathf.Min(580f, Screen.height - 20f) : 235f;
            GUI.Box(new Rect(10, 10, w, h), GUIContent.none);
            GUILayout.BeginArea(new Rect(22, 18, w - 24, h - 16));
            GUILayout.Label("MINING OPERATION / DIAGNOSTICS v0.7  (F10 / Ctrl+Shift+K)", _textStyle);
            GUILayout.Label("FPS: " + _fps.ToString("F1") + "   Managed memory: " +
                (GC.GetTotalMemory(false) / (1024 * 1024)).ToString() + " MB", _textStyle);
            GUILayout.Label("Scene: " + SceneManager.GetActiveScene().name +
                "  |  App: " + (FoundationBootstrap.Host != null ? FoundationBootstrap.Host.State.ToString() : "No host"), _textStyle);
            var session = FoundationGameServices.Sessions.Active;
            GUILayout.Label("Session: " + (session != null ? session.SessionId : "none") +
                "  |  Save: " + (session != null ? (session.SaveId ?? "unsaved") : "-"), _textStyle);
            GUILayout.Label("Modules: " + (FoundationModuleRuntime.Host != null ?
                string.Join(", ", FoundationModuleRuntime.Host.StartedIds) : "not initialized"), _smallStyle);
            GUILayout.Label("Warnings: " + Store.WarningCount + "   Errors: " + Store.ErrorCount, _textStyle);
            if (ConsoleOpen)
            {
                GUILayout.Space(8);
                GUILayout.Label("Commands: help | status | modules | saves | logpath | clear | hide | test-warning", _smallStyle);
                GUILayout.BeginHorizontal();
                _command = GUILayout.TextField(_command, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("RUN", GUILayout.Width(65))) RunCommand();
                GUILayout.EndHorizontal();
                GUILayout.Label(_commandResult, _smallStyle);
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Max(80, h - 300)));
                var entries = Store.Snapshot();
                for (int i = Mathf.Max(0, entries.Length - 18); i < entries.Length; i++)
                    GUILayout.Label(entries[i].Utc.ToString("HH:mm:ss") + " [" + entries[i].Type + "] " + entries[i].Message, _smallStyle);
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }

        public string ExecuteCommand(string line)
        {
            switch ((line ?? "").Trim().ToLowerInvariant())
            {
                case "help": return "help, status, modules, saves, logpath, clear, hide, test-warning";
                case "status": return "Scene=" + SceneManager.GetActiveScene().name + "; App=" +
                    (FoundationBootstrap.Host != null ? FoundationBootstrap.Host.State.ToString() : "none");
                case "modules": return FoundationModuleRuntime.Host == null ? "Module host not started" :
                    string.Join(", ", FoundationModuleRuntime.Host.StartedIds);
                case "saves": return "Save directory: " + FoundationPaths.SaveDirectory;
                case "logpath": return Store.LogPath;
                case "clear": return "The in-memory log remains intact (read-only diagnostic history).";
                case "hide": Visible = false; ConsoleOpen = false; return "Overlay hidden";
                case "test-warning": Debug.LogWarning("[MO/DIAG] Test warning (safe)"); return "Warning emitted";
                default: return "Unknown command. Enter help.";
            }
        }
        private void RunCommand() { _commandResult = ExecuteCommand(_command); _command = ""; }
        private void OnApplicationQuit() { Store?.Flush(); }
        private void OnDestroy()
        {
            Store?.Dispose();
            Store = null;
            if (_instance == this) _instance = null;
        }
    }
}
