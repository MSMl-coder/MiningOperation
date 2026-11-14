using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AsteroidInstance : MonoBehaviour
    {
        [SerializeField] private AsteroidGeneratorSettings settings;
        [SerializeField] private int seed = 12345;
        [SerializeField] private bool forceClass;
        [SerializeField] private AsteroidClass asteroidClass = AsteroidClass.I;
        [SerializeField] private string persistentId = "";
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool addMeshCollider = true;
        [SerializeField] private Material surfaceMaterial;
        [SerializeField] private AsteroidData data;

        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private MeshFilter meshFilter;

        public AsteroidData Data => data;
        public AsteroidGeneratorSettings Settings { get => settings; set => settings = value; }
        public int Seed { get => seed; set => seed = value; }
        public bool ForceClass { get => forceClass; set => forceClass = value; }
        public AsteroidClass ForcedClass { get => asteroidClass; set => asteroidClass = value; }
        public string PersistentId { get => persistentId; set => persistentId = value; }
        public Material SurfaceMaterial { get => surfaceMaterial; set => surfaceMaterial = value; }
        public bool AddMeshCollider { get => addMeshCollider; set => addMeshCollider = value; }

        private void Awake()
        {
            // Rebuild in play mode: editor-created transient meshes may not survive reload.
            if (Application.isPlaying && data != null) SafeRebuild();
        }
        private void OnEnable()
        {
            // Editor domain reload or reopened scenes may have lost DontSave meshes.
            if (!Application.isPlaying && data != null && settings != null &&
                (GetComponent<MeshFilter>() == null || GetComponent<MeshFilter>().sharedMesh == null))
                SafeRebuild();
        }
        private void Start()
        {
            if (Application.isPlaying && data == null && generateOnStart) Generate();
        }
        private void OnDestroy() { ReleaseMeshes(); }

        private void SafeRebuild()
        {
            try { RebuildGeometry(); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }

        [ContextMenu("Generate Asteroid")]
        public void Generate()
        {
            if (settings == null) { Debug.LogError("AsteroidInstance requires Generator Settings.", this); return; }
            try
            {
                // Auto IDs should follow seed changes; custom field/manual IDs remain stable.
                string requestedId = persistentId;
                if (data != null && requestedId == "AST-" + unchecked((uint)data.seed).ToString("X8"))
                    requestedId = null;
                Apply(AsteroidGeneration.Create(seed, settings,
                    forceClass ? asteroidClass : (AsteroidClass?)null, requestedId));
            }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }

        public void Apply(AsteroidData asteroidData)
        {
            if (asteroidData == null) throw new ArgumentNullException(nameof(asteroidData));
            if (settings == null) throw new InvalidOperationException("Generator Settings are required for LOD configuration.");
            // Make a private copy: multiple scene objects must never share mutable extraction state.
            AsteroidData copy = CopyData(asteroidData);
            AsteroidSaveUtility.ValidateRecord(ToSaveRecord(copy));
            AsteroidData previous = data;
            int previousSeed = seed;
            string previousId = persistentId;
            data = copy;
            seed = copy.seed;
            persistentId = copy.asteroidId;
            try { RebuildGeometry(); }
            catch
            {
                // Mesh generation failed before replacing the visible object; retain the previous state.
                data = previous;
                seed = previousSeed;
                persistentId = previousId;
                throw;
            }
        }

        [ContextMenu("Rebuild Asteroid Meshes")]
        public void RebuildGeometry()
        {
            if (data == null) return;
            if (settings == null) throw new InvalidOperationException("Generator Settings are required for LOD configuration.");
            int high = Mathf.Clamp(settings.lod0Subdivisions, 0, 6);
            int mid = Mathf.Clamp(settings.lod1Subdivisions, 0, high);
            int low = Mathf.Clamp(settings.lod2Subdivisions, 0, mid);
            int farther = Mathf.Clamp(settings.lod3Subdivisions, 0, low);
            int farthest = Mathf.Clamp(settings.lod4Subdivisions, 0, farther);
            int[] divisions = { high, mid, low, farther, farthest };
            var generated = new List<Mesh>(5);
            try
            {
                foreach (int division in divisions)
                {
                    Mesh mesh = AsteroidGeometryGenerator.Generate(data, settings, division);
                    mesh.hideFlags = HideFlags.DontSave;
                    generated.Add(mesh);
                }
            }
            catch
            {
                foreach (Mesh mesh in generated) AsteroidGeneration.DestroyTemporary(mesh);
                throw;
            }

            ReleaseMeshes();
            ClearGeneratedLODChildren();

            // IMPORTANT: UnityEngine.Object has its own "fake-null" semantics.
            // GetComponent<T>() ?? AddComponent<T>() is unsafe: an absent Unity
            // component can fail a C# null-coalescing check. Use Unity's == null.
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();

            MeshRenderer primary = GetComponent<MeshRenderer>();
            if (primary == null)
                primary = gameObject.AddComponent<MeshRenderer>();

            if (surfaceMaterial != null)
                primary.sharedMaterial = surfaceMaterial;
            var levels = new LOD[5];
            float[] cutoffs = { 0.55f, 0.31f, 0.16f, 0.065f, 0.001f };
            for (int i = 0; i < generated.Count; i++)
            {
                MeshRenderer targetRenderer;
                if (i == 0)
                {
                    meshFilter.sharedMesh = generated[i];
                    targetRenderer = primary;
                }
                else
                {
                    var child = new GameObject("__AsteroidLOD" + i);
                    child.transform.SetParent(transform, false);
                    child.AddComponent<AsteroidGeneratedLODMarker>();
                    child.AddComponent<MeshFilter>().sharedMesh = generated[i];
                    targetRenderer = child.AddComponent<MeshRenderer>();
                    targetRenderer.sharedMaterials = primary.sharedMaterials;
                }
                levels[i] = new LOD(cutoffs[i], new[] { targetRenderer });
            }
            ownedMeshes.AddRange(generated);
            LODGroup lodGroup = GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = gameObject.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.SetLODs(levels);
            lodGroup.RecalculateBounds();
            MeshCollider collider = GetComponent<MeshCollider>();
            if (addMeshCollider)
            {
                if (collider == null) collider = gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = generated[0];
                collider.enabled = true;
            }
            else if (collider != null) collider.sharedMesh = null;
        }

        public void SetColliderEnabled(bool enabled)
        {
            MeshCollider collider = GetComponent<MeshCollider>();
            if (enabled && collider == null) collider = gameObject.AddComponent<MeshCollider>();
            if (collider == null) return;
            collider.sharedMesh = enabled ? (GetComponent<MeshFilter>() != null ? GetComponent<MeshFilter>().sharedMesh : null) : null;
            collider.enabled = enabled;
        }

        public double ExtractMass(double requestedKg)
        {
            if (data == null || !AsteroidCompositionGenerator.IsFinite(requestedKg) || requestedKg <= 0d ||
                !AsteroidCompositionGenerator.IsFinite(data.remainingMassKg) || data.remainingMassKg <= 0d)
                return 0d;
            double extracted = Math.Min(data.remainingMassKg, requestedKg);
            data.remainingMassKg = Math.Max(0d, data.remainingMassKg - extracted);
            return extracted;
        }

        public Dictionary<string, double> ExtractResources(double requestedKg)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);
            double extracted = ExtractMass(requestedKg);
            if (extracted <= 0d || data.composition == null) return result;
            foreach (MaterialFraction part in data.composition)
                result.Add(part.resourceId, extracted * part.fraction);
            return result;
        }

        public AsteroidSaveRecord Capture() { return data == null ? null : ToSaveRecord(data); }

        public void Restore(AsteroidSaveRecord record)
        {
            AsteroidSaveUtility.ValidateRecord(record);
            Apply(new AsteroidData
            {
                asteroidId = record.asteroidId,
                seed = record.seed,
                generatorVersion = record.generatorVersion,
                asteroidClass = (AsteroidClass)record.asteroidClass,
                diameterMeters = record.diameterMeters,
                volumeM3 = record.volumeM3,
                densityKgM3 = record.densityKgM3,
                initialMassKg = record.initialMassKg,
                remainingMassKg = record.remainingMassKg,
                porosity01 = record.porosity01,
                geometryProfile = record.geometryProfile != null ? record.geometryProfile.Copy() : null,
                composition = CopyComposition(record.composition)
            });
        }

        private static AsteroidData CopyData(AsteroidData src)
        {
            return new AsteroidData
            {
                asteroidId = src.asteroidId, seed = src.seed, generatorVersion = src.generatorVersion,
                asteroidClass = src.asteroidClass, diameterMeters = src.diameterMeters, volumeM3 = src.volumeM3,
                densityKgM3 = src.densityKgM3, initialMassKg = src.initialMassKg,
                remainingMassKg = src.remainingMassKg, porosity01 = src.porosity01,
                geometryProfile = src.geometryProfile != null ? src.geometryProfile.Copy() : null,
                composition = CopyComposition(src.composition)
            };
        }
        private static List<MaterialFraction> CopyComposition(List<MaterialFraction> source)
        {
            var result = new List<MaterialFraction>();
            if (source == null) return result;
            foreach (MaterialFraction part in source)
                result.Add(part == null ? null : new MaterialFraction(part.resourceId, part.fraction));
            return result;
        }
        private static AsteroidSaveRecord ToSaveRecord(AsteroidData src)
        {
            return new AsteroidSaveRecord
            {
                asteroidId = src.asteroidId, seed = src.seed, generatorVersion = src.generatorVersion,
                asteroidClass = (int)src.asteroidClass, diameterMeters = src.diameterMeters,
                volumeM3 = src.volumeM3, densityKgM3 = src.densityKgM3,
                initialMassKg = src.initialMassKg, remainingMassKg = src.remainingMassKg,
                porosity01 = src.porosity01,
                geometryProfile = src.geometryProfile != null ? src.geometryProfile.Copy() : null,
                composition = CopyComposition(src.composition)
            };
        }

        private void ClearGeneratedLODChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.GetComponent<AsteroidGeneratedLODMarker>() == null &&
                    !child.name.StartsWith("__AsteroidLOD", StringComparison.Ordinal)) continue;
                child.gameObject.SetActive(false); // avoid double draw during deferred runtime Destroy
                AsteroidGeneration.DestroyTemporary(child.gameObject);
            }
        }

        private static bool IsTemporaryGeneratedMesh(Mesh mesh)
        {
            return mesh != null && (mesh.hideFlags & HideFlags.DontSave) != 0 &&
                   mesh.name.StartsWith("MO_Asteroid_V2_", StringComparison.Ordinal);
        }

        private void ReleaseMeshes()
        {
            MeshFilter primary = GetComponent<MeshFilter>();
            MeshCollider collider = GetComponent<MeshCollider>();
            var cleanup = new HashSet<Mesh>();
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) cleanup.Add(mesh);
            if (primary != null && IsTemporaryGeneratedMesh(primary.sharedMesh)) cleanup.Add(primary.sharedMesh);
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.GetComponent<AsteroidGeneratedLODMarker>() == null) continue;
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter != null && IsTemporaryGeneratedMesh(filter.sharedMesh)) cleanup.Add(filter.sharedMesh);
                if (filter != null) filter.sharedMesh = null;
            }
            if (primary != null && cleanup.Contains(primary.sharedMesh)) primary.sharedMesh = null;
            if (collider != null && cleanup.Contains(collider.sharedMesh)) collider.sharedMesh = null;
            foreach (Mesh mesh in cleanup) AsteroidGeneration.DestroyTemporary(mesh);
            ownedMeshes.Clear();
        }
    }
}
