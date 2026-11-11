using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiningOperation.Framework
{
    // Foundation v0.5: small, explicit modular host. Does not replace the v0.4 ServiceRegistry.
    public interface IFoundationModule
    {
        string Id { get; }
        IReadOnlyList<string> Dependencies { get; }
        void Initialize(FoundationModuleContext context);
        void Shutdown();
    }

    public sealed class FoundationModuleContext
    {
        public FoundationDefinitionRegistry Definitions { get; }
        public IFrameworkLogger Logger { get; }

        public FoundationModuleContext(FoundationDefinitionRegistry definitions, IFrameworkLogger logger)
        {
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
    }

    public sealed class FoundationModuleHost : IDisposable
    {
        private readonly Dictionary<string, IFoundationModule> _modules = new Dictionary<string, IFoundationModule>(StringComparer.Ordinal);
        private readonly List<IFoundationModule> _started = new List<IFoundationModule>();
        private bool _initialized;
        private bool _disposed;
        public IReadOnlyList<string> StartedIds => _started.Select(m => m.Id).ToArray();

        public void Register(IFoundationModule module)
        {
            if (_initialized || _disposed) throw new InvalidOperationException("Module registration is closed.");
            if (module == null || string.IsNullOrWhiteSpace(module.Id)) throw new ArgumentException("Module ID is required.");
            if (!_modules.TryAdd(module.Id, module)) throw new InvalidOperationException("Duplicate module ID: " + module.Id);
        }

        public void InitializeAll(FoundationModuleContext context)
        {
            if (_disposed || _initialized) throw new InvalidOperationException("Module host cannot initialize twice.");
            if (context == null) throw new ArgumentNullException(nameof(context));
            var order = ResolveOrder(); // No side effects until dependency graph validates.
            try
            {
                foreach (var module in order)
                {
                    module.Initialize(context);
                    _started.Add(module);
                }
                _initialized = true;
            }
            catch
            {
                ShutdownAll();
                throw;
            }
        }

        public IReadOnlyList<string> ValidateAndGetOrder() => ResolveOrder().Select(m => m.Id).ToArray();

        private List<IFoundationModule> ResolveOrder()
        {
            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            var result = new List<IFoundationModule>();
            foreach (var id in _modules.Keys.OrderBy(x => x, StringComparer.Ordinal)) Visit(id);
            return result;

            void Visit(string id)
            {
                if (!_modules.TryGetValue(id, out var module)) throw new InvalidOperationException("Missing module dependency: " + id);
                if (states.TryGetValue(id, out int state))
                {
                    if (state == 1) throw new InvalidOperationException("Circular module dependency at: " + id);
                    if (state == 2) return;
                }
                states[id] = 1;
                var deps = module.Dependencies ?? Array.Empty<string>();
                foreach (string dependency in deps.OrderBy(x => x, StringComparer.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(dependency)) throw new InvalidOperationException("Empty dependency in module " + id);
                    Visit(dependency);
                }
                states[id] = 2;
                result.Add(module);
            }
        }

        public void ShutdownAll()
        {
            for (int i = _started.Count - 1; i >= 0; i--)
            {
                try { _started[i].Shutdown(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            _started.Clear();
            _initialized = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            ShutdownAll();
            _disposed = true;
        }
    }

    public abstract class FoundationDefinition : ScriptableObject
    {
        [SerializeField] private string definitionId;
        [SerializeField, Min(1)] private int schemaVersion = 1;
        public string DefinitionId => definitionId;
        public int SchemaVersion => schemaVersion;

        // Explicit setter for controlled Editor tooling and tests; not for gameplay mutation.
        public void ConfigureIdentity(string id, int version)
        {
            definitionId = id;
            schemaVersion = version;
        }
    }

    public interface IFoundationDefinitionReferences
    {
        IReadOnlyList<string> ReferencedDefinitionIds { get; }
    }

    public sealed class FoundationDefinitionRegistry
    {
        private readonly Dictionary<string, FoundationDefinition> _items = new Dictionary<string, FoundationDefinition>(StringComparer.Ordinal);
        private bool _sealed;
        public int Count => _items.Count;

        public void Register(FoundationDefinition definition)
        {
            if (_sealed) throw new InvalidOperationException("Definition registry already sealed.");
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(definition.DefinitionId)) throw new InvalidOperationException("Definition has empty ID: " + definition.name);
            if (definition.SchemaVersion < 1) throw new InvalidOperationException("Invalid schema version: " + definition.DefinitionId);
            if (!_items.TryAdd(definition.DefinitionId, definition)) throw new InvalidOperationException("Duplicate definition ID: " + definition.DefinitionId);
        }

        public void SealAndValidate()
        {
            if (_sealed) return;
            foreach (var item in _items.Values)
            {
                if (item is IFoundationDefinitionReferences withReferences && withReferences.ReferencedDefinitionIds != null)
                    foreach (var id in withReferences.ReferencedDefinitionIds)
                        if (string.IsNullOrWhiteSpace(id) || !_items.ContainsKey(id))
                            throw new InvalidOperationException("Missing referenced definition '" + id + "' in " + item.DefinitionId);
            }
            _sealed = true;
        }

        public bool TryGet<T>(string id, out T value) where T : FoundationDefinition
        {
            if (_items.TryGetValue(id, out var found) && found is T typed)
            {
                value = typed;
                return true;
            }
            value = null;
            return false;
        }

        public T Get<T>(string id) where T : FoundationDefinition
        {
            if (!TryGet<T>(id, out var value)) throw new KeyNotFoundException("Definition missing or type mismatch: " + id);
            return value;
        }

        public IReadOnlyList<T> GetAll<T>() where T : FoundationDefinition =>
            _items.Values.OfType<T>().OrderBy(x => x.DefinitionId, StringComparer.Ordinal).ToArray();
    }

    public sealed class FoundationDefinitionsModule : IFoundationModule
    {
        public string Id => "foundation.definitions";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();
        public void Initialize(FoundationModuleContext context)
        {
            // Files are optional until gameplay content is added.
            var definitions = Resources.LoadAll<FoundationDefinition>("MiningOperation/Definitions");
            foreach (var definition in definitions) context.Definitions.Register(definition);
            context.Definitions.SealAndValidate();
            context.Logger.Info("[Modules] Definitions ready; count=" + context.Definitions.Count);
        }
        public void Shutdown() { }
    }

}
