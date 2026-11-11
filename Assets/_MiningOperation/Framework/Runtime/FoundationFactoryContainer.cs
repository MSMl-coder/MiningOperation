using System;
using System.Collections.Generic;
using System.Linq;

namespace MiningOperation.Framework
{
    /// <summary>
    /// An opt-in factory-based dependency injection graph, separate from legacy ServiceRegistry.
    /// Create child scopes for session/scene usage. Registration is frozen when Build is called.
    /// No UnityEngine dependency; normal services use constructor injection via explicit factories.
    /// </summary>
    public sealed class FoundationFactoryRegistry
    {
        internal sealed class Descriptor
        {
            public Type ServiceType;
            public FoundationScopeKind Lifetime;
            public Type[] Dependencies;
            public Func<FoundationFactoryScope, object> Factory;
        }

        private readonly Dictionary<Type, Descriptor> _descriptors = new Dictionary<Type, Descriptor>();
        private bool _built;

        public void Register<T>(FoundationScopeKind lifetime,
            Func<FoundationFactoryScope, T> factory, params Type[] dependencies) where T : class
        {
            if (_built) throw new InvalidOperationException("Registry is already built");
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (_descriptors.ContainsKey(typeof(T)))
                throw new InvalidOperationException("Duplicate factory: " + typeof(T).FullName);
            _descriptors.Add(typeof(T), new Descriptor
            {
                ServiceType = typeof(T), Lifetime = lifetime,
                Dependencies = dependencies ?? Array.Empty<Type>(), Factory = s => factory(s)
            });
        }

        public void RegisterInstance<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            Register(FoundationScopeKind.Application, _ => instance);
        }

        public FoundationFactoryScope Build()
        {
            if (_built) throw new InvalidOperationException("Registry already built");
            Validate();
            _built = true;
            return new FoundationFactoryScope(_descriptors, FoundationScopeKind.Application, null);
        }

        public void Validate()
        {
            var marks = new Dictionary<Type, int>();
            foreach (var key in _descriptors.Keys) Visit(key, marks);
        }

        private void Visit(Type key, Dictionary<Type, int> marks)
        {
            if (marks.TryGetValue(key, out int mark))
            {
                if (mark == 1) throw new InvalidOperationException("Cyclic factory dependency at " + key.Name);
                if (mark == 2) return;
            }
            marks[key] = 1;
            var current = _descriptors[key];
            foreach (var dep in current.Dependencies)
            {
                if (!_descriptors.TryGetValue(dep, out var target))
                    throw new InvalidOperationException("Missing dependency " + dep.Name + " for " + key.Name);
                // Parent-owned services may not capture child-owned services.
                if ((int)target.Lifetime > (int)current.Lifetime)
                    throw new InvalidOperationException("Invalid lifetime: " + current.ServiceType.Name + " -> " + target.ServiceType.Name);
                Visit(dep, marks);
            }
            marks[key] = 2;
        }
    }

    public sealed class FoundationFactoryScope : IDisposable
    {
        private readonly IDictionary<Type, FoundationFactoryRegistry.Descriptor> _registrations;
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();
        private readonly List<object> _owned = new List<object>();
        private readonly List<FoundationFactoryScope> _children = new List<FoundationFactoryScope>();
        private readonly FoundationFactoryScope _parent;
        private readonly HashSet<Type> _resolving = new HashSet<Type>();
        private bool _disposed;
        public FoundationScopeKind Kind { get; }
        public bool IsDisposed => _disposed;
        public int ChildCount => _children.Count;

        internal FoundationFactoryScope(IDictionary<Type, FoundationFactoryRegistry.Descriptor> registrations,
            FoundationScopeKind kind, FoundationFactoryScope parent)
        {
            _registrations = registrations; Kind = kind; _parent = parent;
        }

        public FoundationFactoryScope CreateChild(FoundationScopeKind kind)
        {
            Check();
            if ((Kind == FoundationScopeKind.Application && kind != FoundationScopeKind.Session && kind != FoundationScopeKind.Scene) ||
                (Kind == FoundationScopeKind.Session && kind != FoundationScopeKind.Scene) || Kind == FoundationScopeKind.Scene)
                throw new InvalidOperationException("Invalid child scope hierarchy");
            var child = new FoundationFactoryScope(_registrations, kind, this);
            _children.Add(child);
            return child;
        }

        public T Resolve<T>() where T : class => (T)Resolve(typeof(T));

        public object Resolve(Type key)
        {
            Check();
            if (!_registrations.TryGetValue(key, out var descriptor))
                throw new InvalidOperationException("Unregistered service: " + key.Name);
            var owner = FindOwner(descriptor.Lifetime);
            if (owner._instances.TryGetValue(key, out var cached)) return cached;
            if (!owner._resolving.Add(key)) throw new InvalidOperationException("Factory cycle at " + key.Name);
            try
            {
                // Factories resolve from the owning scope, never from a narrower child.
                var value = descriptor.Factory(owner);
                if (value == null || !key.IsInstanceOfType(value))
                    throw new InvalidOperationException("Factory returned invalid instance for " + key.Name);
                owner._instances.Add(key, value);
                if (value is IDisposable && !owner._owned.Any(x => ReferenceEquals(x, value))) owner._owned.Add(value);
                return value;
            }
            finally { owner._resolving.Remove(key); }
        }

        private FoundationFactoryScope FindOwner(FoundationScopeKind lifetime)
        {
            var scope = this;
            while (scope != null)
            {
                if (scope.Kind == lifetime) return scope;
                scope = scope._parent;
            }
            throw new InvalidOperationException("No " + lifetime + " scope for service");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var child in _children.ToArray()) child.Dispose();
            _children.Clear();
            for (int i = _owned.Count - 1; i >= 0; i--)
                ((IDisposable)_owned[i]).Dispose();
            _owned.Clear(); _instances.Clear();
            _parent?._children.Remove(this);
        }

        private void Check()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FoundationFactoryScope));
        }
    }
}
