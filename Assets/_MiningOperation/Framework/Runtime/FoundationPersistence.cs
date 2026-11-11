using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MiningOperation.Framework
{
    [Serializable]
    public sealed class FoundationSaveSection
    {
        public string key;
        public int version;
        public string json;
    }

    [Serializable]
    public sealed class FoundationSavePayload
    {
        public int schemaVersion = 1;
        public string saveId;
        public string sessionId;
        public string displayName;
        public string createdUtc;
        public string savedUtc;
        public List<FoundationSaveSection> sections = new List<FoundationSaveSection>();
    }

    [Serializable]
    public sealed class FoundationSaveEnvelope
    {
        public string signature = "MINING_OPERATION_FOUNDATION_SAVE";
        public string sha256;
        public string payloadJson;
    }

    /// <summary>One versioned, self-contained section of save data owned by a game module.</summary>
    public interface IFoundationSaveParticipant
    {
        string Key { get; }
        int Version { get; }
        string CaptureJson();
        void RestoreJson(string json, int version);
    }

    public sealed class FoundationSession
    {
        private readonly Dictionary<string, IFoundationSaveParticipant> _participants =
            new Dictionary<string, IFoundationSaveParticipant>(StringComparer.Ordinal);

        public readonly string SessionId;
        public readonly string CreatedUtc;
        public string SaveId;
        public string DisplayName;
        public IReadOnlyCollection<IFoundationSaveParticipant> Participants => _participants.Values;

        public FoundationSession(string sessionId, string createdUtc, string name, string saveId = null)
        {
            SessionId = sessionId;
            CreatedUtc = createdUtc;
            DisplayName = name;
            SaveId = saveId;
        }

        internal void AttachParticipant(IFoundationSaveParticipant participant)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.Key) || participant.Version < 1)
                throw new InvalidOperationException("Invalid save participant");
            if (!_participants.TryAdd(participant.Key, participant))
                throw new InvalidOperationException("Duplicate save section: " + participant.Key);
        }

        public bool TryGetParticipant<T>(string key, out T participant) where T : class, IFoundationSaveParticipant
        {
            if (_participants.TryGetValue(key, out var found) && found is T typed)
            {
                participant = typed;
                return true;
            }
            participant = null;
            return false;
        }
    }

    public sealed class FoundationSaveSummary
    {
        public string SaveId;
        public string DisplayName;
        public string SavedUtc;
        public bool Valid;
        public string Error;
    }

    public sealed class FoundationSessionService
    {
        private const string Signature = "MINING_OPERATION_FOUNDATION_SAVE";
        private sealed class ParticipantRegistration
        {
            public string Key;
            public Func<IFoundationSaveParticipant> Factory;
            public bool Required;
        }
        private readonly SortedDictionary<string, ParticipantRegistration> _participantFactories =
            new SortedDictionary<string, ParticipantRegistration>(StringComparer.Ordinal);
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly string _directory;
        private FoundationSession _active;

        // Fired only after a fully-created/restored session becomes active,
        // or after it is closed. An optional integration point for lifetime scopes.
        public event Action<FoundationSession> ActiveSessionChanged;
        public FoundationSession Active => _active;

        private void NotifySessionChanged()
        {
            // Observers must never invalidate a successfully committed save/load.
            var listeners = ActiveSessionChanged;
            if (listeners == null) return;
            foreach (Action<FoundationSession> observer in listeners.GetInvocationList())
            {
                try { observer(_active); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }
        public FoundationSessionService(string directory)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        /// <summary>
        /// Register before creating/loading a game. Each module supplies fresh state per session;
        /// old saves cannot be silently loaded without their registered sections.
        /// </summary>
        public void RegisterParticipant(string key, Func<IFoundationSaveParticipant> factory, bool required = true)
        {
            if (_active != null) throw new InvalidOperationException("Close active session before registering participants");
            if (string.IsNullOrWhiteSpace(key) || factory == null)
                throw new ArgumentException("Save participant key and factory are required");
            if (_participantFactories.ContainsKey(key))
                throw new InvalidOperationException("Duplicate save participant factory: " + key);
            _participantFactories.Add(key, new ParticipantRegistration { Key = key, Factory = factory, Required = required });
        }

        private FoundationSession CreateSession(string sessionId, string createdUtc, string name, string saveId = null)
        {
            var session = new FoundationSession(sessionId, createdUtc, name, saveId);
            foreach (var entry in _participantFactories.Values)
            {
                var participant = entry.Factory();
                if (participant == null || participant.Key != entry.Key || participant.Version < 1)
                    throw new InvalidOperationException("Invalid participant factory for " + entry.Key);
                session.AttachParticipant(participant);
            }
            return session;
        }

        public void NewSession(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "New Session";
            name = name.Trim();
            if (name.Length > 64) name = name.Substring(0, 64);
            if (_active != null) throw new InvalidOperationException("Close the current game before creating a new session");
            _active = CreateSession(Guid.NewGuid().ToString("N"), DateTime.UtcNow.ToString("o"), name);
            NotifySessionChanged();
        }

        public void CloseSession()
        {
            if (_active == null) return;
            _active = null;
            NotifySessionChanged();
        }

        public async Task<string> SaveAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_active == null) throw new InvalidOperationException("No active session to save");
                // Capture on the Unity main thread BEFORE background I/O. This is a coherent DTO snapshot.
                var session = _active;
                string id = string.IsNullOrEmpty(session.SaveId) ? Guid.NewGuid().ToString("N") : session.SaveId;
                var payload = new FoundationSavePayload
                {
                    saveId = id,
                    sessionId = session.SessionId,
                    displayName = session.DisplayName,
                    createdUtc = session.CreatedUtc,
                    savedUtc = DateTime.UtcNow.ToString("o")
                };
                foreach (var participant in session.Participants.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    var json = participant.CaptureJson();
                    if (string.IsNullOrWhiteSpace(json))
                        throw new InvalidDataException("Empty save section: " + participant.Key);
                    payload.sections.Add(new FoundationSaveSection
                    {
                        key = participant.Key, version = participant.Version, json = json
                    });
                }
                string payloadJson = JsonUtility.ToJson(payload);
                var envelope = new FoundationSaveEnvelope { signature = Signature, payloadJson = payloadJson, sha256 = Hash(payloadJson) };
                string document = JsonUtility.ToJson(envelope, true);
                await Task.Run(() => WriteSafely(_directory, id, document), cancellationToken);
                // Assign after durable write; a failed write must not create a fictional slot.
                session.SaveId = id;
                return id;
            }
            finally { _gate.Release(); }
        }

        public async Task LoadAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateId(saveId);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var payload = await Task.Run(() => ReadPayload(_directory, saveId), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (_active != null) throw new InvalidOperationException("Close the current game before loading a different session");
                var restored = CreateSession(payload.sessionId, payload.createdUtc, payload.displayName, payload.saveId);
                var savedSections = new Dictionary<string, FoundationSaveSection>(StringComparer.Ordinal);
                foreach (var section in payload.sections)
                {
                    if (section == null || string.IsNullOrWhiteSpace(section.key) || section.version < 1 ||
                        string.IsNullOrWhiteSpace(section.json) || !savedSections.TryAdd(section.key, section))
                        throw new InvalidDataException("Invalid or duplicate save section");
                    if (!_participantFactories.ContainsKey(section.key))
                        throw new InvalidDataException("Missing game module for save section: " + section.key);
                }
                foreach (var registration in _participantFactories.Values)
                {
                    if (!savedSections.TryGetValue(registration.Key, out var section))
                    {
                        if (registration.Required)
                            throw new InvalidDataException("Required save section missing: " + registration.Key);
                        continue;
                    }
                    if (!restored.TryGetParticipant<IFoundationSaveParticipant>(registration.Key, out var participant))
                        throw new InvalidDataException("Unregistered session participant: " + registration.Key);
                    participant.RestoreJson(section.json, section.version);
                }
                // Only publish fully validated session.
                _active = restored;
                NotifySessionChanged();
            }
            finally { _gate.Release(); }
        }

        public async Task<List<FoundationSaveSummary>> GetSlotsAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                return await Task.Run(() =>
                {
                    var result = new List<FoundationSaveSummary>();
                    if (!Directory.Exists(_directory)) return result;
                    foreach (string path in Directory.GetFiles(_directory, "*.mosave"))
                    {
                        string id = Path.GetFileNameWithoutExtension(path);
                        var summary = new FoundationSaveSummary { SaveId = id };
                        try
                        {
                            var data = ReadPayload(_directory, id);
                            summary.Valid = true;
                            summary.DisplayName = data.displayName;
                            summary.SavedUtc = data.savedUtc;
                        }
                        catch (Exception e)
                        {
                            summary.Valid = false;
                            summary.DisplayName = "Unreadable save";
                            summary.Error = e.Message;
                        }
                        result.Add(summary);
                    }
                    return result.OrderByDescending(x => x.SavedUtc ?? "").ToList();
                }, cancellationToken);
            }
            finally { _gate.Release(); }
        }

        public async Task DeleteAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateId(saveId);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(() =>
                {
                    string path = GetPath(_directory, saveId);
                    if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
                }, cancellationToken);
                if (_active != null && _active.SaveId == saveId) _active.SaveId = null;
            }
            finally { _gate.Release(); }
        }

        private static void WriteSafely(string folder, string id, string content)
        {
            Directory.CreateDirectory(folder);
            var path = GetPath(folder, id);
            var temp = path + ".tmp";
            var backup = path + ".bak";
            try
            {
                // WriteThrough/Flush keeps the write out of the normal buffered path where supported.
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(true);
                }
                // Verify the file before making it current.
                ReadAndValidate(File.ReadAllText(temp, Encoding.UTF8), id);
                if (File.Exists(path))
                {
                    try { File.Replace(temp, path, backup); }
                    catch (PlatformNotSupportedException) { FallbackReplace(path, temp, backup); }
                }
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private static void FallbackReplace(string path, string temp, string backup)
        {
            // Not atomic across all platforms. Keep a valid backup for best-effort recovery.
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
            try { File.Move(temp, path); }
            catch
            {
                if (!File.Exists(path) && File.Exists(backup)) File.Move(backup, path);
                throw;
            }
        }

        private static FoundationSavePayload ReadPayload(string folder, string id)
        {
            var path = GetPath(folder, id);
            try { return ReadAndValidate(File.ReadAllText(path, Encoding.UTF8), id); }
            catch (Exception primary) when (primary is IOException || primary is ArgumentException)
            {
                var backup = path + ".bak";
                if (File.Exists(backup)) return ReadAndValidate(File.ReadAllText(backup, Encoding.UTF8), id);
                throw;
            }
        }

        private static FoundationSavePayload ReadAndValidate(string json, string expectedId)
        {
            var env = JsonUtility.FromJson<FoundationSaveEnvelope>(json);
            if (env == null || env.signature != Signature || string.IsNullOrEmpty(env.payloadJson) ||
                !string.Equals(Hash(env.payloadJson), env.sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Save signature or checksum invalid");
            var p = JsonUtility.FromJson<FoundationSavePayload>(env.payloadJson);
            if (p == null || p.saveId != expectedId ||
                !Guid.TryParseExact(p.sessionId, "N", out _) || p.sections == null)
                throw new InvalidDataException("Save schema or metadata invalid");
            return FoundationSaveCompatibility.PrepareForLoad(p);
        }

        private static string Hash(string data)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void ValidateId(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _))
                throw new ArgumentException("Save ID must be a GUID without separators", nameof(id));
        }
        private static string GetPath(string directory, string id)
        {
            ValidateId(id);
            return Path.Combine(directory, id + ".mosave");
        }
    }

    public static class FoundationGameServices
    {
        private static FoundationSessionService _sessions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _sessions = null; }
        public static FoundationSessionService Sessions
        {
            get
            {
                if (_sessions == null)
                {
                    _sessions = new FoundationSessionService(FoundationPaths.SaveDirectory);
                    FoundationLifecycleRuntime.BindSessions(_sessions);
                }
                return _sessions;
            }
        }
    }
}
