using System;
using System.Collections.Generic;
using System.Linq;

namespace MiningOperation.Framework
{
    /// <summary>Independent versions of application, framework, save schema and game content.</summary>
    public sealed class FoundationVersionInfo
    {
        public string GameVersion { get; }
        public string FrameworkVersion { get; }
        public int SaveSchemaVersion { get; }
        public string ContentVersion { get; }

        public FoundationVersionInfo(string game, string framework, int schema, string content)
        {
            if (string.IsNullOrWhiteSpace(game) || string.IsNullOrWhiteSpace(framework) ||
                string.IsNullOrWhiteSpace(content) || schema < 1)
                throw new ArgumentException("Version fields must be nonempty and schema positive.");
            GameVersion = game;
            FrameworkVersion = framework;
            SaveSchemaVersion = schema;
            ContentVersion = content;
        }
    }

    public interface IFoundationSchemaMigration<T>
    {
        int FromVersion { get; }
        int ToVersion { get; }
        T Migrate(T source);
    }

    /// <summary>
    /// Versioned DTO migration pipeline. Does NOT automatically rewrite legacy .dwsave files.
    /// Call only on parsed, isolated copies of DTOs, never on the live active session.
    /// </summary>
    public sealed class FoundationMigrationRegistry<T>
    {
        private readonly Dictionary<int, IFoundationSchemaMigration<T>> _steps =
            new Dictionary<int, IFoundationSchemaMigration<T>>();

        public void Register(IFoundationSchemaMigration<T> migration)
        {
            if (migration == null) throw new ArgumentNullException(nameof(migration));
            if (migration.FromVersion < 1 || migration.ToVersion != migration.FromVersion + 1)
                throw new InvalidOperationException("Migration must advance exactly one schema version.");
            if (_steps.ContainsKey(migration.FromVersion))
                throw new InvalidOperationException("Duplicate migration from v" + migration.FromVersion);
            _steps.Add(migration.FromVersion, migration);
        }

        public bool CanMigrate(int from, int to)
        {
            if (from < 1 || to < from) return false;
            for (int version = from; version < to; version++)
                if (!_steps.ContainsKey(version)) return false;
            return true;
        }

        public T Migrate(T source, int from, int target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!CanMigrate(from, target))
                throw new InvalidOperationException("No complete migration chain " + from + " -> " + target);
            T current = source;
            for (int version = from; version < target; version++)
            {
                current = _steps[version].Migrate(current);
                if (current == null) throw new InvalidOperationException("Migration returned null at v" + version);
            }
            return current;
        }
    }
}
