using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    [Serializable]
    public sealed class AsteroidFieldRecord
    {
        public int index;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public AsteroidSaveRecord asteroid;
    }

    [Serializable]
    public sealed class AsteroidFieldSnapshot
    {
        public int schemaVersion = 1;
        public int layoutVersion = AsteroidFieldLayout.LayoutVersion;
        public string fieldId;
        public long fieldSeed;
        public AsteroidFormationType formation;
        public List<AsteroidFieldRecord> asteroids = new List<AsteroidFieldRecord>();
    }

    /// <summary>
    /// Standalone storage boundary: retains layout AND generated resource/mass snapshots.
    /// No implicit disk writes; the game's Save Manager owns the persistent storage.
    /// </summary>
    public static class AsteroidFieldSaveUtility
    {
        public const int SchemaVersion = 1;

        public static string ToJson(AsteroidFieldSnapshot snapshot)
        {
            Validate(snapshot);
            return JsonUtility.ToJson(snapshot, false);
        }

        public static AsteroidFieldSnapshot FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Empty field save JSON.", nameof(json));
            AsteroidFieldSnapshot parsed;
            try { parsed = JsonUtility.FromJson<AsteroidFieldSnapshot>(json); }
            catch (Exception e) { throw new InvalidOperationException("Invalid field JSON.", e); }
            Validate(parsed);
            return parsed;
        }

        public static AsteroidSaveRecord CopyAsteroid(AsteroidData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var result = new AsteroidSaveRecord
            {
                asteroidId = data.asteroidId,
                seed = data.seed,
                generatorVersion = data.generatorVersion,
                asteroidClass = (int)data.asteroidClass,
                diameterMeters = data.diameterMeters,
                volumeM3 = data.volumeM3,
                densityKgM3 = data.densityKgM3,
                initialMassKg = data.initialMassKg,
                remainingMassKg = data.remainingMassKg,
                porosity01 = data.porosity01,
                geometryProfile = data.geometryProfile != null ? data.geometryProfile.Copy() : null,
                composition = new List<MaterialFraction>()
            };
            if (data.composition != null)
                foreach (var p in data.composition)
                    result.composition.Add(new MaterialFraction(p.resourceId, p.fraction));
            AsteroidSaveUtility.ValidateRecord(result);
            return result;
        }

        public static void Validate(AsteroidFieldSnapshot snapshot)
        {
            if (snapshot == null || snapshot.schemaVersion != SchemaVersion ||
                snapshot.layoutVersion != AsteroidFieldLayout.LayoutVersion ||
                string.IsNullOrWhiteSpace(snapshot.fieldId) || snapshot.fieldId.Length > 80 ||
                snapshot.asteroids == null || snapshot.asteroids.Count > 2000 ||
                !Enum.IsDefined(typeof(AsteroidFormationType), snapshot.formation))
                throw new InvalidOperationException("Invalid or unsupported field snapshot.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var indices = new HashSet<int>();
            foreach (var entry in snapshot.asteroids)
            {
                if (entry == null || entry.index < 0 || !indices.Add(entry.index) ||
                    !Finite(entry.localPosition.x) || !Finite(entry.localPosition.y) || !Finite(entry.localPosition.z) ||
                    !Finite(entry.localRotation.x) || !Finite(entry.localRotation.y) ||
                    !Finite(entry.localRotation.z) || !Finite(entry.localRotation.w) ||
                    entry.localRotation.x * entry.localRotation.x + entry.localRotation.y * entry.localRotation.y +
                    entry.localRotation.z * entry.localRotation.z + entry.localRotation.w * entry.localRotation.w < 0.01f)
                    throw new InvalidOperationException("Invalid field placement.");
                AsteroidSaveUtility.ValidateRecord(entry.asteroid);
                if (!ids.Add(entry.asteroid.asteroidId))
                    throw new InvalidOperationException("Duplicate asteroid ID within field.");
            }
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
