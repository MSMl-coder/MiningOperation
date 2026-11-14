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
        private readonly Dictionary<int, AsteroidInstance> inactiveCache = new Dictionary<int, AsteroidInstance>();
        private readonly LinkedList<int> cacheOrder = new LinkedList<int>();
        private readonly Dictionary<int, LinkedListNode<int>> cacheNodes =
            new Dictionary<int, LinkedListNode<int>>();
        private Material runtimeNearMaterial;
        public int CachedAsteroidCount => inactiveCache.Count;
        public Material RuntimeAsteroidMaterial => GetRuntimeNearMaterial();
        private Material GetRuntimeNearMaterial()
        {
            if (settings == null) return null;
            if (runtimeNearMaterial == null)
                runtimeNearMaterial = AsteroidStableMaterials.Create(settings.asteroidMaterial,
                    settings.allowExperimentalRockShaders, new Color(0.65f, 0.64f, 0.61f, 1f),
                    "MO Near Stable URP (runtime)");
            return runtimeNearMaterial;
        }

        private List<AsteroidFieldPlacement> pending;
        private AsteroidFieldSnapshot building;
        private int cursor;
        private AsteroidFormationType buildFormation;
        private Coroutine generationRoutine;
        private float nextStreamingTick;
        private AsteroidFieldFarRenderer farRenderer;
        private AsteroidDebrisRenderer debrisRenderer;
        public int DecorativeRenderedCount => debrisRenderer != null ? debrisRenderer.VisibleCount : 0;
        private readonly HashSet<int> detailedIdsScratch = new HashSet<int>();
        public int FarRenderedCount => farRenderer != null ? farRenderer.VisibleCount : 0;
        public bool FarGpuReady => farRenderer != null && farRenderer.Ready;
        public float NearestDistanceMeters { get; private set; } = -1f;
        public float DetailedRangeMeters => settings != null ?
            (settings.useGpuFarField ? Mathf.Min(settings.spawnDistanceMeters, settings.detailedDistanceMeters) : settings.spawnDistanceMeters) : 0f;

        public AsteroidFieldSettings Settings { get => settings; set => settings = value; }
        public string FieldId { get => fieldId; set => fieldId = value; }
        public long FieldSeed { get => fieldSeed; set => fieldSeed = value; }
        public Transform Viewer { get => viewer; set => viewer = value; }
        public bool RuntimeStreaming { get => runtimeStreaming; set => runtimeStreaming = value; }
        public bool GenerateOnStart { get => generateOnStart; set => generateOnStart = value; }
        public void ForceRebuildImmediately()
        {
            BeginBuild();
            while (IsBuilding) BuildStep(Mathf.Max(1, settings.generatePerFrame));
        }
        public AsteroidFieldSnapshot Snapshot => snapshot;
        public int ActiveCount => active.Count;
        public int BuildProgress => cursor;
        public int BuildTotal => pending != null ? pending.Count : 0;
        public bool IsBuilding => pending != null;

        private void OnEnable() { ReindexChildren(); }

        private void Start()
        {
            if (!Application.isPlaying) return;
            // Scenes authored with v1.2 may contain hundreds of persisted child GameObjects.
            // Remove the transient graphics in play mode; retain their authoritative records.
            if (settings != null && settings.useGpuFarField)
                ClearSpawned();
            InitializeFarRendering();
            if (snapshot == null && generateOnStart)
                generationRoutine = StartCoroutine(GenerateCoroutine());
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || !runtimeStreaming || IsBuilding || snapshot == null) return;
            if (settings != null && settings.enableAsteroidRotation)
            {
                foreach (var pair in active)
                {
                    var instance = pair.Value;
                    var record = FindRecord(pair.Key);
                    if (instance == null || record == null || record.asteroid == null) continue;
                    instance.transform.localRotation = AsteroidSpin.Evaluate(record.localRotation,
                        record.asteroid.seed, record.asteroid.diameterMeters,
                        snapshot.simulationSeconds, settings.rotationSpeedMultiplier);
                }
            }
            farRenderer?.Render(snapshot.simulationSeconds);
            debrisRenderer?.Render();
        }

        private void OnDisable()
        {
            farRenderer?.Dispose();
            farRenderer = null;
            debrisRenderer?.Dispose();
            debrisRenderer = null;
        }

        private void OnDestroy()
        {
            if (runtimeNearMaterial != null) AsteroidGeneration.DestroyTemporary(runtimeNearMaterial);
            runtimeNearMaterial = null;
        }

        private void InitializeFarRendering()
        {
            farRenderer?.Dispose();
            farRenderer = null;
            debrisRenderer?.Dispose();
            debrisRenderer = null;
            if (!Application.isPlaying || !runtimeStreaming || snapshot == null ||
                settings == null || !settings.useGpuFarField) return;
            farRenderer = new AsteroidFieldFarRenderer(transform, settings);
            farRenderer.Initialize(snapshot);
            if (settings.enableDecorativeDebris &&
                settings.decorativeRockCount + settings.microFragmentCount > 0)
            {
                debrisRenderer = new AsteroidDebrisRenderer(transform, settings);
                debrisRenderer.Initialize(snapshot);
            }
            nextStreamingTick = 0f;
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
            if (settings.enableAsteroidRotation)
                snapshot.simulationSeconds += Math.Max(0d, (double)Time.deltaTime);
            // Avoid expensive scans on every render frame in dense fields.
            if (Time.time < nextStreamingTick) return;
            nextStreamingTick = Time.time + Mathf.Max(0.1f, settings.streamingTickSeconds);
            Transform focus = viewer != null ? viewer : (Camera.main != null ? Camera.main.transform : null);
            if (focus == null) return;
            StreamAround(focus.position, settings.instantiatePerFrame);
        }

        public void BeginBuild()
        {
            if (settings == null) throw new InvalidOperationException("Missing Field Settings.");
            settings.ValidateOrThrow();
            CancelBuild();
            var anchor = GetComponent<AsteroidFieldSpawnPoint>();
            AsteroidFieldLayoutOptions layout = anchor != null && anchor.useOverrides
                ? anchor.BuildOptions(settings) : AsteroidFieldLayoutOptions.FromSettings(settings);
            buildFormation = layout.formation;
            pending = AsteroidFieldLayout.Plan(settings, fieldSeed, fieldId, layout);
            cursor = 0;
            building = new AsteroidFieldSnapshot
            {
                schemaVersion = AsteroidFieldSaveUtility.SchemaVersion,
                layoutVersion = layout.useComposedLayout ? AsteroidFieldLayout.LayoutVersion : 2,
                fieldId = fieldId,
                fieldSeed = fieldSeed,
                formation = buildFormation,
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
                var data = AsteroidGeneration.Create(place.asteroidSeed, settings.asteroidSettings,
                    null, place.asteroidId, place.nominalDiameter);
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
            InitializeFarRendering();
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
            farRenderer?.Dispose();
            farRenderer = null;
            debrisRenderer?.Dispose();
            debrisRenderer = null;
        }

        /// <summary>
        /// Streams asteroids within spawnDistance, with hysteresis to avoid pop-in/out churn.
        /// The complete geological state persists in snapshot even when meshes are despawned.
        /// </summary>
        public void StreamAround(Vector3 worldPosition, int spawnBudget)
        {
            if (settings == null || snapshot == null) return;
            bool optimized = settings.useGpuFarField;
            // Pre-1.3 serialized spawnDistanceMeters can remain 13km; optimized mode
            // deliberately uses a MUCH smaller range for expensive unique meshes.
            float nearMeters = optimized ? Mathf.Min(settings.spawnDistanceMeters,
                settings.detailedDistanceMeters) : settings.spawnDistanceMeters;
            float farMeters = optimized ? Mathf.Min(settings.despawnDistanceMeters,
                Mathf.Max(nearMeters + 200f, nearMeters * 1.3f)) : settings.despawnDistanceMeters;
            float nearSq = nearMeters * nearMeters;
            float closestSq = float.MaxValue;
            float farSq = farMeters * farMeters;
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
                if (d < closestSq) closestSq = d;
                bool isActive = active.ContainsKey(record.index);
                if (d <= nearSq || (isActive && d <= farSq))
                    candidates.Add(new KeyValuePair<AsteroidFieldRecord, float>(record, d));
            }
            NearestDistanceMeters = closestSq == float.MaxValue ? -1f : Mathf.Sqrt(closestSq);
            candidates.Sort((a, b) =>
            {
                int comparison = a.Value.CompareTo(b.Value);
                return comparison != 0 ? comparison : a.Key.index.CompareTo(b.Key.index);
            });
            int maxDetailed = optimized ? Mathf.Min(settings.maxActiveAsteroids,
                settings.maxDetailedAsteroids) : settings.maxActiveAsteroids;
            int permittedCount = Math.Min(maxDetailed, candidates.Count);
            var permitted = new HashSet<int>();
            for (int i = 0; i < permittedCount; i++) permitted.Add(candidates[i].Key.index);
            var toRemove = new List<int>();
            foreach (var pair in active)
                if (pair.Value == null || !permitted.Contains(pair.Key)) toRemove.Add(pair.Key);
            foreach (int index in toRemove) Despawn(index);
            if (spawnBudget <= 0)
            {
                UpdateDistantInstances(worldPosition);
                return;
            }
            int remaining = Math.Min(spawnBudget, Math.Max(0, maxDetailed - active.Count));
            int initialBudget = remaining;
            for (int i = 0; i < permittedCount && remaining > 0; i++)
            {
                AsteroidFieldRecord record = candidates[i].Key;
                if (candidates[i].Value > nearSq || active.ContainsKey(record.index)) continue;
                Spawn(record);
                remaining--;
            }
            // Upgrade at most one currently near asteroid per streaming tick.
            // The first visible mesh is borrowed from far rendering (identical silhouette).
            if (optimized && settings.fastStreamedAsteroids && settings.exactFarShape &&
                remaining == initialBudget)
                UpgradeClosestVisible(candidates, permittedCount);
            UpdateDistantInstances(worldPosition);
        }

        private void UpgradeClosestVisible(
            List<KeyValuePair<AsteroidFieldRecord, float>> candidates, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var record = candidates[i].Key;
                if (record == null || record.asteroid == null ||
                    !active.TryGetValue(record.index, out AsteroidInstance instance) || instance == null)
                    continue;
                float distance = Mathf.Sqrt(candidates[i].Value);
                float ratio = distance / Mathf.Max(1f, record.asteroid.diameterMeters);
                int target = settings.exactFarSubdivisions;
                if (ratio < 14f) target = Mathf.Max(target, 3);
                if (ratio < 3.5f) target = Mathf.Max(target, 4);
                if (ratio < 1.2f) target = Mathf.Max(target, 5);
                target = Mathf.Min(target, settings.maximumStreamedSubdivisions);
                if (instance.StreamedSubdivisionLevel >= 0 && instance.StreamedSubdivisionLevel < target)
                {
                    instance.UpgradeStreamedGeometry(instance.StreamedSubdivisionLevel + 1);
                    break;
                }
            }
        }

        private void UpdateDistantInstances(Vector3 viewerWorldPosition)
        {
            if (farRenderer == null && debrisRenderer == null) return;
            detailedIdsScratch.Clear();
            foreach (int index in active.Keys) detailedIdsScratch.Add(index);
            Camera gameCamera = viewer != null ? viewer.GetComponent<Camera>() : null;
            if (gameCamera == null) gameCamera = Camera.main;
            farRenderer?.Refresh(snapshot, viewerWorldPosition, detailedIdsScratch, gameCamera);
            debrisRenderer?.Refresh(viewerWorldPosition, gameCamera, snapshot.simulationSeconds);
        }

        private AsteroidInstance Spawn(AsteroidFieldRecord record)
        {
            if (settings == null || settings.asteroidSettings == null) return null;
            if (inactiveCache.TryGetValue(record.index, out var cached) && cached != null &&
                cached.Data != null && cached.Data.asteroidId == record.asteroid.asteroidId)
            {
                inactiveCache.Remove(record.index);
                if (cacheNodes.TryGetValue(record.index, out var node))
                {
                    cacheOrder.Remove(node);
                    cacheNodes.Remove(record.index);
                }
                cached.transform.localPosition = record.localPosition;
                cached.transform.localRotation = record.localRotation;
                cached.SynchronizeMass(record.asteroid);
                cached.gameObject.SetActive(true);
                active[record.index] = cached;
                return cached;
            }
            if (inactiveCache.ContainsKey(record.index))
            {
                if (inactiveCache[record.index] != null)
                    AsteroidGeneration.DestroyTemporary(inactiveCache[record.index].gameObject);
                inactiveCache.Remove(record.index);
                if (cacheNodes.TryGetValue(record.index, out var node))
                {
                    cacheOrder.Remove(node);
                    cacheNodes.Remove(record.index);
                }
            }
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
            asteroid.SurfaceMaterial = GetRuntimeNearMaterial() != null ? GetRuntimeNearMaterial() : settings.asteroidMaterial;
            asteroid.AddMeshCollider = settings.meshColliders;
            try
            {
                if (settings.fastStreamedAsteroids && settings.exactFarShape && farRenderer != null &&
                    farRenderer.Ready)
                {
                    Mesh shared = farRenderer.GetOrCreateMesh(record);
                    if (shared != null)
                        asteroid.RestoreStreamedShared(record.asteroid, settings.exactFarSubdivisions, shared);
                    else asteroid.RestoreStreamed(record.asteroid, settings.streamedMeshSubdivisions);
                }
                else if (settings.fastStreamedAsteroids)
                    asteroid.RestoreStreamed(record.asteroid, settings.streamedMeshSubdivisions);
                else
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
                int cacheLimit = Application.isPlaying && settings != null && settings.fastStreamedAsteroids
                    ? settings.cachedStreamedAsteroids : 0;
                if (cacheLimit > 0)
                {
                    inactiveCache[index] = asteroid;
                    if (cacheNodes.TryGetValue(index, out var existingNode))
                        cacheOrder.Remove(existingNode);
                    cacheNodes[index] = cacheOrder.AddLast(index);
                    while (inactiveCache.Count > cacheLimit && cacheOrder.First != null)
                    {
                        int oldest = cacheOrder.First.Value;
                        cacheOrder.RemoveFirst();
                        cacheNodes.Remove(oldest);
                        if (!inactiveCache.TryGetValue(oldest, out var old)) continue;
                        inactiveCache.Remove(oldest);
                        if (old != null) AsteroidGeneration.DestroyTemporary(old.gameObject);
                    }
                }
                else AsteroidGeneration.DestroyTemporary(asteroid.gameObject);
            }
            active.Remove(index);
        }

        public void ClearSpawned()
        {
            ReindexChildren();
            // A rebuild/load/scene reset must not recycle meshes from a previous field snapshot.
            foreach (var pair in active)
            {
                if (pair.Value == null) continue;
                var record = FindRecord(pair.Key);
                if (record != null && pair.Value.Data != null) record.asteroid = pair.Value.Capture();
                pair.Value.gameObject.SetActive(false);
                AsteroidGeneration.DestroyTemporary(pair.Value.gameObject);
            }
            active.Clear();
            foreach (var pair in inactiveCache)
                if (pair.Value != null) AsteroidGeneration.DestroyTemporary(pair.Value.gameObject);
            inactiveCache.Clear();
            cacheOrder.Clear();
            cacheNodes.Clear();
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
            var anchor = GetComponent<AsteroidFieldSpawnPoint>();
            if (anchor != null)
            {
                anchor.fieldId = loaded.fieldId;
                anchor.fieldSeed = loaded.fieldSeed;
            }
            InitializeFarRendering();
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
