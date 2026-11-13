using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Generates persistent field records with time-sliced CPU work, and only builds
    /// visible Unity meshes near a viewer. Runtime can restore exact mined state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AsteroidFieldController : MonoBehaviour
    {
        [SerializeField] private AsteroidFieldSettings settings;
        [SerializeField] private string fieldId = "starter-belt";
        [SerializeField] private long fieldSeed = 80521L;
        [SerializeField] private Transform viewer;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool runtimeStreaming = true;
        [SerializeField] private bool displayGizmos = true;
        [SerializeField] private AsteroidFieldSnapshot snapshot;

        private readonly Dictionary<int, AsteroidInstance> active = new Dictionary<int, AsteroidInstance>();
        private List<AsteroidFieldPlacement> pending;
        private AsteroidFieldSnapshot building;
        private int cursor;
        private Coroutine generationRoutine;
        private float nextStreamingTick;

        public AsteroidFieldSettings Settings { get => settings; set => settings = value; }
        public string FieldId { get => fieldId; set => fieldId = value; }
        public long FieldSeed { get => fieldSeed; set => fieldSeed = value; }
        public Transform Viewer { get => viewer; set => viewer = value; }
        public bool RuntimeStreaming { get => runtimeStreaming; set => runtimeStreaming = value; }
        public AsteroidFieldSnapshot Snapshot => snapshot;
        public int ActiveCount => active.Count;
        public int BuildProgress => cursor;
        public int BuildTotal => pending != null ? pending.Count : 0;
        public bool IsBuilding => pending != null;

        private void OnEnable() { ReindexChildren(); }

        private void Start()
        {
            if (!Application.isPlaying) return;
            if (snapshot == null && generateOnStart)
                generationRoutine = StartCoroutine(GenerateCoroutine());
        }

        private IEnumerator GenerateCoroutine()
        {
            bool initialized = true;
            try { BeginBuild(); }
            catch (Exception e) { Debug.LogException(e, this); initialized = false; }
            if (!initialized) yield break;
            while (IsBuilding)
            {
                bool succeeded = true;
                try { BuildStep(settings.generatePerFrame); }
                catch (Exception e) { Debug.LogException(e, this); succeeded = false; }
                if (!succeeded)
                {
                    CancelBuild();
                    yield break;
                }
                yield return null;
            }
            generationRoutine = null;
        }

        private void Update()
        {
            if (!Application.isPlaying || !runtimeStreaming || IsBuilding || snapshot == null || settings == null) return;
            // Avoid expensive scans on every render frame in dense fields.
            if (Time.time < nextStreamingTick) return;
            nextStreamingTick = Time.time + 0.15f;
            Transform focus = viewer != null ? viewer : (Camera.main != null ? Camera.main.transform : null);
            if (focus == null) return;
            StreamAround(focus.position, settings.instantiatePerFrame);
        }

        public void BeginBuild()
        {
            if (settings == null) throw new InvalidOperationException("Missing Field Settings.");
            settings.ValidateOrThrow();
            CancelBuild();
            pending = AsteroidFieldLayout.Plan(settings, fieldSeed, fieldId);
            cursor = 0;
            building = new AsteroidFieldSnapshot
            {
                schemaVersion = AsteroidFieldSaveUtility.SchemaVersion,
                layoutVersion = AsteroidFieldLayout.LayoutVersion,
                fieldId = fieldId,
                fieldSeed = fieldSeed,
                formation = settings.formation,
                asteroids = new List<AsteroidFieldRecord>(pending.Count)
            };
        }

        /// <summary>Returns true once every asteroid record is generated.</summary>
        public bool BuildStep(int budget)
        {
            if (pending == null) return true;
            if (settings == null) throw new InvalidOperationException("Field Settings removed during build.");
            budget = Mathf.Max(1, budget);
            int until = Math.Min(pending.Count, cursor + budget);
            for (; cursor < until; cursor++)
            {
                var place = pending[cursor];
                var data = AsteroidGeneration.Create(place.asteroidSeed, settings.asteroidSettings, null, place.asteroidId);
                building.asteroids.Add(new AsteroidFieldRecord
                {
                    index = place.index,
                    localPosition = place.localPosition,
                    localRotation = place.localRotation,
                    asteroid = AsteroidFieldSaveUtility.CopyAsteroid(data)
                });
            }
            if (cursor != pending.Count) return false;
            AsteroidFieldSaveUtility.Validate(building);
            ClearSpawned();
            snapshot = building;
            pending = null;
            building = null;
            cursor = 0;
            return true;
        }

        public void CancelBuild()
        {
            pending = null;
            building = null;
            cursor = 0;
            if (Application.isPlaying && generationRoutine != null)
            {
                StopCoroutine(generationRoutine);
                generationRoutine = null;
            }
        }

        public void ClearField()
        {
            CancelBuild();
            ClearSpawned();
            snapshot = null;
        }

        /// <summary>
        /// Streams asteroids within spawnDistance, with hysteresis to avoid pop-in/out churn.
        /// The complete geological state persists in snapshot even when meshes are despawned.
        /// </summary>
        public void StreamAround(Vector3 worldPosition, int spawnBudget)
        {
            if (settings == null || snapshot == null) return;
            float nearSq = settings.spawnDistanceMeters * settings.spawnDistanceMeters;
            float farSq = settings.despawnDistanceMeters * settings.despawnDistanceMeters;
            // Refresh authoritative data before choosing which rocks remain visible.
            foreach (var pair in active)
            {
                AsteroidFieldRecord record = FindRecord(pair.Key);
                if (record != null && pair.Value != null && pair.Value.Data != null)
                    record.asteroid = pair.Value.Capture();
            }
            // Keep existing active asteroids until far range, but allow nearer
            // candidates to replace them when the active-object budget is full.
            var candidates = new List<KeyValuePair<AsteroidFieldRecord, float>>();
            foreach (AsteroidFieldRecord record in snapshot.asteroids)
            {
                if (record == null || record.asteroid == null ||
                    (settings.hideDepletedAsteroids && record.asteroid.remainingMassKg <= 0d)) continue;
                float d = (WorldPosition(record) - worldPosition).sqrMagnitude;
                bool isActive = active.ContainsKey(record.index);
                if (d <= nearSq || (isActive && d <= farSq))
                    candidates.Add(new KeyValuePair<AsteroidFieldRecord, float>(record, d));
            }
            candidates.Sort((a, b) =>
            {
                int comparison = a.Value.CompareTo(b.Value);
                return comparison != 0 ? comparison : a.Key.index.CompareTo(b.Key.index);
            });
            int permittedCount = Math.Min(settings.maxActiveAsteroids, candidates.Count);
            var permitted = new HashSet<int>();
            for (int i = 0; i < permittedCount; i++) permitted.Add(candidates[i].Key.index);
            var toRemove = new List<int>();
            foreach (var pair in active)
                if (pair.Value == null || !permitted.Contains(pair.Key)) toRemove.Add(pair.Key);
            foreach (int index in toRemove) Despawn(index);
            if (spawnBudget <= 0) return;
            int remaining = Math.Min(spawnBudget, Math.Max(0, settings.maxActiveAsteroids - active.Count));
            for (int i = 0; i < permittedCount && remaining > 0; i++)
            {
                AsteroidFieldRecord record = candidates[i].Key;
                if (candidates[i].Value > nearSq || active.ContainsKey(record.index)) continue;
                Spawn(record);
                remaining--;
            }
        }

        private AsteroidInstance Spawn(AsteroidFieldRecord record)
        {
            if (settings == null || settings.asteroidSettings == null) return null;
            var go = new GameObject(record.asteroid.asteroidId);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = record.localPosition;
            go.transform.localRotation = record.localRotation;
            go.transform.localScale = Vector3.one; // geometry volume/mass never scaled by scene objects
            var member = go.AddComponent<AsteroidFieldMember>();
            member.index = record.index;
            member.fieldId = snapshot.fieldId;
            var asteroid = go.AddComponent<AsteroidInstance>();
            asteroid.Settings = settings.asteroidSettings;
            asteroid.SurfaceMaterial = settings.asteroidMaterial;
            asteroid.AddMeshCollider = settings.meshColliders;
            try
            {
                asteroid.Restore(record.asteroid);
                active[record.index] = asteroid;
                return asteroid;
            }
            catch
            {
                go.SetActive(false);
                AsteroidGeneration.DestroyTemporary(go);
                throw;
            }
        }

        public AsteroidInstance GetActive(int index)
        {
            active.TryGetValue(index, out var asteroid);
            return asteroid;
        }

        public AsteroidFieldRecord FindRecord(int index)
        {
            if (snapshot == null || snapshot.asteroids == null) return null;
            foreach (var entry in snapshot.asteroids)
                if (entry.index == index) return entry;
            return null;
        }

        /// <summary>
        /// Mine by persistent ID even if the asteroid currently has no rendered GameObject.
        /// Intended for collectors, drones and background industrial simulation.
        /// Returns exact per-material mass in kilograms.
        /// </summary>
        public Dictionary<string, double> ExtractResourcesById(string asteroidId, double requestedKg)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);
            if (snapshot == null || string.IsNullOrWhiteSpace(asteroidId) ||
                double.IsNaN(requestedKg) || double.IsInfinity(requestedKg) || requestedKg <= 0d)
                return result;
            foreach (var record in snapshot.asteroids)
            {
                if (record.asteroid == null || record.asteroid.asteroidId != asteroidId) continue;
                if (active.TryGetValue(record.index, out var instance) && instance != null)
                {
                    result = instance.ExtractResources(requestedKg);
                    record.asteroid = instance.Capture();
                    return result;
                }
                if (record.asteroid.remainingMassKg <= 0d) return result;
                double actual = Math.Min(requestedKg, record.asteroid.remainingMassKg);
                record.asteroid.remainingMassKg = Math.Max(0d, record.asteroid.remainingMassKg - actual);
                foreach (var part in record.asteroid.composition)
                    result.Add(part.resourceId, actual * part.fraction);
                return result;
            }
            return result;
        }

        public AsteroidFieldRecord FindRecordById(string asteroidId)
        {
            if (snapshot == null || string.IsNullOrEmpty(asteroidId)) return null;
            foreach (var entry in snapshot.asteroids)
                if (entry.asteroid != null && entry.asteroid.asteroidId == asteroidId) return entry;
            return null;
        }

        private Vector3 WorldPosition(AsteroidFieldRecord record) { return transform.TransformPoint(record.localPosition); }

        public void Despawn(int index)
        {
            if (!active.TryGetValue(index, out var asteroid)) return;
            var record = FindRecord(index);
            if (asteroid != null)
            {
                if (record != null && asteroid.Data != null) record.asteroid = asteroid.Capture();
                asteroid.gameObject.SetActive(false);
                AsteroidGeneration.DestroyTemporary(asteroid.gameObject);
            }
            active.Remove(index);
        }

        public void ClearSpawned()
        {
            ReindexChildren();
            var indices = new List<int>(active.Keys);
            foreach (int index in indices) Despawn(index);
        }

        private void ReindexChildren()
        {
            active.Clear();
            var members = GetComponentsInChildren<AsteroidFieldMember>(true);
            foreach (var member in members)
            {
                if (member == null || member.gameObject == gameObject || !member.gameObject.activeSelf) continue;
                if (snapshot != null && member.fieldId != snapshot.fieldId) continue;
                var instance = member.GetComponent<AsteroidInstance>();
                if (instance != null && !active.ContainsKey(member.index)) active.Add(member.index, instance);
            }
        }

        public string CaptureJson()
        {
            if (IsBuilding) throw new InvalidOperationException("Cannot save a partially built field.");
            if (snapshot == null) throw new InvalidOperationException("Field has no generated snapshot.");
            foreach (var pair in active)
            {
                var record = FindRecord(pair.Key);
                if (record != null && pair.Value != null && pair.Value.Data != null)
                    record.asteroid = pair.Value.Capture();
            }
            return AsteroidFieldSaveUtility.ToJson(snapshot);
        }

        public void LoadJson(string json)
        {
            // Validate BEFORE touching the current field or its active objects.
            var loaded = AsteroidFieldSaveUtility.FromJson(json);
            CancelBuild();
            ClearSpawned();
            snapshot = loaded;
            fieldId = loaded.fieldId;
            fieldSeed = loaded.fieldSeed;
        }

        private void OnDrawGizmosSelected()
        {
            if (!displayGizmos || snapshot == null || snapshot.asteroids == null) return;
            foreach (var record in snapshot.asteroids)
            {
                if (record?.asteroid == null) continue;
                Gizmos.color = record.asteroid.asteroidClass == 3 ? new Color(1f, 0.56f, 0.22f, 0.85f) :
                    record.asteroid.asteroidClass == 2 ? new Color(1f, 0.85f, 0.35f, 0.8f) :
                    new Color(0.4f, 0.8f, 0.9f, 0.7f);
                Gizmos.DrawWireSphere(WorldPosition(record), Mathf.Max(20f, record.asteroid.diameterMeters * 0.5f));
            }
        }
    }
}
