using System;
using System.IO;

namespace MiningOperation.Framework
{
    /// <summary>
    /// Version policy for the existing v0.3 .dwsave format. The current writer
    /// remains at schema 1. Future migrations must be registered explicitly.
    /// </summary>
    public static class FoundationSaveCompatibility
    {
        public const int CurrentSchema = 1;
        private static readonly FoundationMigrationRegistry<FoundationSavePayload> Steps =
            new FoundationMigrationRegistry<FoundationSavePayload>();

        public static void RegisterMigration(IFoundationSchemaMigration<FoundationSavePayload> step)
        {
            Steps.Register(step);
        }

        public static FoundationSavePayload PrepareForLoad(FoundationSavePayload source)
        {
            if (source == null) throw new InvalidDataException("Save payload missing");
            if (source.schemaVersion < 1 || source.schemaVersion > CurrentSchema)
                throw new InvalidDataException("Unsupported save schema: " + source.schemaVersion);
            if (source.schemaVersion == CurrentSchema) return source;
            if (!Steps.CanMigrate(source.schemaVersion, CurrentSchema))
                throw new InvalidDataException("Missing migration path from schema " + source.schemaVersion);
            // Caller must supply a detached DTO, never an active session object.
            var result = Steps.Migrate(source, source.schemaVersion, CurrentSchema);
            if (result.schemaVersion != CurrentSchema)
                throw new InvalidDataException("Migration did not update schemaVersion");
            return result;
        }
    }
}
