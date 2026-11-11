using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MiningOperation.Framework
{
    [Serializable]
    public sealed class FoundationSettingsData
    {
        public int schemaVersion = 1;
        public int qualityLevel = 2;
        public bool vsync = true;
        public int fpsLimit = 60;
        public float masterVolume = 0.8f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.8f;
        public float uiVolume = 0.8f;
        public float ambientVolume = 0.7f;
        public float voiceVolume = 0.8f;
        public string language = "en";
        public bool autosaveEnabled = true;
        public int autosaveMinutes = 10;
        public float uiScale = 1.0f;
        public bool notificationsEnabled = true;
    }

    /// <summary>Global preferences, independent of session saves. No scene or UI references.</summary>
    public sealed class FoundationSettingsService
    {
        private readonly string _path;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        public FoundationSettingsData Current { get; private set; }
        public string FilePath => _path;

        public FoundationSettingsService(string filePath)
        {
            _path = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Current = Defaults();
        }

        public static FoundationSettingsData Defaults()
        {
            var data = new FoundationSettingsData();
            data.qualityLevel = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0,
                Mathf.Max(0, QualitySettings.names.Length - 1));
            return data;
        }

        public FoundationSettingsData CreateDraft() => Clone(Current);

        public static FoundationSettingsData Clone(FoundationSettingsData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return JsonUtility.FromJson<FoundationSettingsData>(JsonUtility.ToJson(source));
        }

        public async Task LoadAsync()
        {
            try
            {
                if (File.Exists(_path))
                {
                    string json = await Task.Run(() => File.ReadAllText(_path, Encoding.UTF8));
                    var loaded = JsonUtility.FromJson<FoundationSettingsData>(json);
                    if (loaded == null || loaded.schemaVersion != 1)
                        throw new InvalidDataException("Unsupported settings format");
                    Current = Validate(Clone(loaded));
                }
                else Current = Defaults();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MO/SETTINGS] Could not read preferences, using defaults: " + ex.Message);
                Current = Defaults();
            }
            ApplyRuntime(Current);
        }

        public async Task ApplyAsync(FoundationSettingsData draft)
        {
            var proposed = Validate(Clone(draft));
            await _writeLock.WaitAsync();
            try
            {
                // Save first. An I/O failure must not leave a supposedly committed setting.
                var json = JsonUtility.ToJson(proposed, true);
                await Task.Run(() => WriteSafely(_path, json));
                Current = proposed;
                ApplyRuntime(Current);
                Debug.Log("[MO/SETTINGS] Preferences saved");
            }
            finally { _writeLock.Release(); }
        }

        private static FoundationSettingsData Validate(FoundationSettingsData d)
        {
            d.qualityLevel = Mathf.Clamp(d.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            if (d.fpsLimit != -1) d.fpsLimit = Mathf.Clamp(d.fpsLimit, 30, 240);
            d.masterVolume = Mathf.Clamp01(d.masterVolume);
            d.musicVolume = Mathf.Clamp01(d.musicVolume);
            d.sfxVolume = Mathf.Clamp01(d.sfxVolume);
            d.uiVolume = Mathf.Clamp01(d.uiVolume);
            d.ambientVolume = Mathf.Clamp01(d.ambientVolume);
            d.voiceVolume = Mathf.Clamp01(d.voiceVolume);
            d.autosaveMinutes = Mathf.Clamp(d.autosaveMinutes, 1, 120);
            d.uiScale = Mathf.Clamp(d.uiScale, 0.75f, 1.5f);
            if (d.language != "en" && d.language != "uk") d.language = "en";
            return d;
        }

        private static void ApplyRuntime(FoundationSettingsData d)
        {
            // Unity APIs are only called on the Unity main thread.
            if (QualitySettings.names.Length > 0)
                QualitySettings.SetQualityLevel(d.qualityLevel, true);
            QualitySettings.vSyncCount = d.vsync ? 1 : 0;
            Application.targetFrameRate = d.vsync ? -1 : d.fpsLimit;
            // Master volume is live. Individual buses are persisted for a later AudioMixer integration.
            AudioListener.volume = d.masterVolume;
        }

        private static void WriteSafely(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            string backup = path + ".bak";
            try
            {
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }
                var verified = JsonUtility.FromJson<FoundationSettingsData>(File.ReadAllText(tmp, Encoding.UTF8));
                if (verified == null || verified.schemaVersion != 1)
                    throw new InvalidDataException("Preferences verification failed");
                if (File.Exists(path))
                {
                    try { File.Replace(tmp, path, backup); }
                    catch (PlatformNotSupportedException) { FallbackReplace(path, tmp, backup); }
                }
                else File.Move(tmp, path);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }

        private static void FallbackReplace(string path, string tmp, string backup)
        {
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
            try { File.Move(tmp, path); }
            catch
            {
                if (!File.Exists(path) && File.Exists(backup)) File.Move(backup, path);
                throw;
            }
        }
    }

    /// <summary>Temporary v0.4 composition point. Formal DI registration follows in v0.5.</summary>
    public static class FoundationSettingsServices
    {
        private static FoundationSettingsService _instance;
        private static Task _loadTask;
        public static FoundationSettingsService Settings
        {
            get
            {
                if (_instance == null)
                    _instance = new FoundationSettingsService(FoundationPaths.SettingsFile);
                return _instance;
            }
        }
        public static Task EnsureLoadedAsync()
        {
            if (_loadTask == null) _loadTask = Settings.LoadAsync();
            return _loadTask;
        }
    }
}
