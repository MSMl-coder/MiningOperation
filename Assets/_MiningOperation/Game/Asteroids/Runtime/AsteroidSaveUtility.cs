using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    [Serializable]
    public sealed class AsteroidSaveCollection
    {
        public int schemaVersion = 2;
        public List<AsteroidSaveRecord> asteroids = new List<AsteroidSaveRecord>();
    }

    // Storage-agnostic JSON boundary. No hidden filesystem writes or world-spawning side effects.
    public static class AsteroidSaveUtility
    {
        public const int SchemaVersion = 2;

        public static string CaptureJson(IEnumerable<AsteroidInstance> instances)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            var result = new AsteroidSaveCollection { schemaVersion = SchemaVersion };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var instance in instances)
            {
                if (instance == null) continue;
                AsteroidSaveRecord record = instance.Capture();
                if (record == null) continue;
                ValidateRecord(record);
                if (!ids.Add(record.asteroidId))
                    throw new InvalidOperationException("Duplicate asteroid ID: " + record.asteroidId);
                result.asteroids.Add(record);
            }
            return JsonUtility.ToJson(result, false);
        }

        public static AsteroidSaveCollection ParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Save JSON is empty.", nameof(json));
            AsteroidSaveCollection result;
            try { result = JsonUtility.FromJson<AsteroidSaveCollection>(json); }
            catch (Exception ex) { throw new InvalidOperationException("Invalid asteroid save JSON.", ex); }
            if (result == null || (result.schemaVersion != 1 && result.schemaVersion != SchemaVersion) || result.asteroids == null)
                throw new InvalidOperationException("Unsupported or invalid asteroid save schema.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in result.asteroids)
            {
                ValidateRecord(record);
                if (!ids.Add(record.asteroidId))
                    throw new InvalidOperationException("Duplicate asteroid ID in save: " + record.asteroidId);
            }
            return result;
        }

        public static void RestoreExisting(string json, IEnumerable<AsteroidInstance> instances)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            AsteroidSaveCollection collection = ParseJson(json);
            var records = new Dictionary<string, AsteroidSaveRecord>(StringComparer.Ordinal);
            foreach (var record in collection.asteroids) records.Add(record.asteroidId, record);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var matches = new List<KeyValuePair<AsteroidInstance, AsteroidSaveRecord>>();
            foreach (AsteroidInstance instance in instances)
            {
                if (instance == null) continue;
                string id = instance.Data != null ? instance.Data.asteroidId : instance.PersistentId;
                if (string.IsNullOrEmpty(id)) continue;
                if (!seen.Add(id)) throw new InvalidOperationException("Duplicate active asteroid ID: " + id);
                if (records.TryGetValue(id, out var record)) matches.Add(new KeyValuePair<AsteroidInstance, AsteroidSaveRecord>(instance, record));
            }
            // Verify dependencies before mutating any matching instance.
            foreach (var match in matches)
            {
                if (match.Key.Settings == null)
                    throw new InvalidOperationException("Matching asteroid has no Generator Settings: " + match.Value.asteroidId);
            }
            foreach (var match in matches) match.Key.Restore(match.Value);
        }

        public static void ValidateRecord(AsteroidSaveRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.asteroidId) || record.asteroidId.Length > 256)
                throw new InvalidOperationException("Asteroid save is missing a valid ID.");
            if (record.generatorVersion != 1 && record.generatorVersion != 2 && record.generatorVersion != 3)
                throw new InvalidOperationException("Unsupported geometry version for " + record.asteroidId);
            if (record.asteroidClass < 1 || record.asteroidClass > 3 ||
                !Finite(record.diameterMeters) || record.diameterMeters <= 0d ||
                !Finite(record.volumeM3) || record.volumeM3 <= 0d ||
                !Finite(record.densityKgM3) || record.densityKgM3 <= 0d ||
                !Finite(record.initialMassKg) || record.initialMassKg <= 0d ||
                !Finite(record.remainingMassKg) || record.remainingMassKg < 0d ||
                record.remainingMassKg > record.initialMassKg)
                throw new InvalidOperationException("Invalid physical values for " + record.asteroidId);
            if (record.generatorVersion >= 2 && (record.geometryProfile == null ||
                !Finite(record.porosity01) || record.porosity01 < 0d || record.porosity01 >= 1d))
                throw new InvalidOperationException("V2 asteroid lacks its shape snapshot or valid porosity: " + record.asteroidId);
            if (record.generatorVersion >= 2)
            {
                AsteroidGeometryProfile p = record.geometryProfile;
                if (!Finite(p.shapeIrregularity) || p.shapeIrregularity < 0 || p.shapeIrregularity > 1 ||
                    !Finite(p.surfaceRoughness) || p.surfaceRoughness < 0 || p.surfaceRoughness > 1 ||
                    !Finite(p.craterIntensity) || p.craterIntensity < 0 || p.craterIntensity > 1 ||
                    !Finite(p.minimumAxisScale) || p.minimumAxisScale < 0.1 || p.minimumAxisScale > 1 ||
                    p.minCraters < 0 || p.maxCraters < p.minCraters || p.maxCraters > 60)
                    throw new InvalidOperationException("Invalid V2 geometry snapshot for " + record.asteroidId);
            }
            if (record.composition == null || record.composition.Count == 0)
                throw new InvalidOperationException("Missing composition for " + record.asteroidId);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            double sum = 0;
            foreach (var entry in record.composition)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.resourceId) || !ids.Add(entry.resourceId) ||
                    !Finite(entry.fraction) || entry.fraction < 0d || entry.fraction > 1d)
                    throw new InvalidOperationException("Invalid resource in " + record.asteroidId);
                sum += entry.fraction;
            }
            if (Math.Abs(sum - 1d) > 1e-7)
                throw new InvalidOperationException("Resource fractions must sum to 1: " + record.asteroidId);
        }

        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
