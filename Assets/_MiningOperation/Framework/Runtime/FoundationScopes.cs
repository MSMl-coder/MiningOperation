using System;
using System.Collections.Generic;
using System.Linq;

namespace MiningOperation.Framework
{
    public enum FoundationScopeKind { Application, Session, Scene }

    /// <summary>
    /// Explicit lifetime scope: all registrations are owned by exactly one scope.
    /// Registrations are instances, not lazy factories; container is intentionally small and deterministic.
    /// </summary>
    public sealed class FoundationScope : IDisposable
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly List<object> _owned = new List<object>();
        private readonly List<FoundationScope> _children = new List<FoundationScope>();
        private readonly FoundationScope _parent;
        private bool _disposed;

        public FoundationScopeKind Kind { get; }
        public bool IsDisposed => _disposed;
        public int LocalRegistrationCount => _services.Count;
        public int ChildCount => _children.Count;

        public FoundationScope(FoundationScopeKind kind) : this(kind, null) { }

        private FoundationScope(FoundationScopeKind kind, FoundationScope parent)
        {
            Kind = kind;
            _parent = parent;
        }

        public FoundationScope CreateChild(FoundationScopeKind kind)
        {
            ThrowIfDisposed();
            if (Kind == FoundationScopeKind.Scene)
                throw new InvalidOperationException("Scene scope cannot own child scopes.");
            if (Kind == FoundationScopeKind.Session && kind != FoundationScopeKind.Scene)
                throw new InvalidOperationException("Session scope may only own scene scopes.");
            if (Kind == FoundationScopeKind.Application && kind != FoundationScopeKind.Session && kind != FoundationScopeKind.Scene)
                throw new InvalidOperationException("Application scope may only own session or scene scopes.");
            var child = new FoundationScope(kind, this);
            _children.Add(child);
            return child;
        }

        public void Register<T>(T instance, bool owned = true) where T : class
        {
            ThrowIfDisposed();
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            Type key = typeof(T);
            if (_services.ContainsKey(key)) throw new InvalidOperationException("Duplicate service: " + key.FullName);
            _services.Add(key, instance);
            if (owned && !_owned.Any(x => ReferenceEquals(x, instance))) _owned.Add(instance);
        }

        public T Resolve<T>() where T : class
        {
            ThrowIfDisposed();
            if (_services.TryGetValue(typeof(T), out var service)) return (T)service;
            if (_parent != null) return _parent.Resolve<T>();
            throw new InvalidOperationException("Service not registered: " + typeof(T).FullName);
        }

        public bool TryResolve<T>(out T value) where T : class
        {
            ThrowIfDisposed();
            if (_services.TryGetValue(typeof(T), out var service))
            {
                value = (T)service;
                return true;
            }
            if (_parent != null) return _parent.TryResolve(out value);
            value = null;
            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _children.Count - 1; i >= 0; i--) _children[i].Dispose();
            _children.Clear();
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] is IDisposable resource) resource.Dispose();
            }
            _owned.Clear();
            _services.Clear();
            _parent?._children.Remove(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FoundationScope));
        }
    }
}
