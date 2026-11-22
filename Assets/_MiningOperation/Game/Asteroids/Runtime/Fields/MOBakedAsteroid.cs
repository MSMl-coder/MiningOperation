using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// A real, editable scene GameObject. Geometry lives in generated children and is
    /// persisted as Mesh assets. Gameplay scripts, colliders, placement and custom
    /// children belong to this root and are not touched by ordinary geometry rebakes.
    /// Resource state remains authoritative in AsteroidFieldController.Snapshot.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mining Operation/Asteroids/Baked Asteroid")]
    public sealed class MOBakedAsteroid : MonoBehaviour
    {
        [SerializeField] private AsteroidFieldController fieldController;
        [SerializeField] private string asteroidId;
        [SerializeField] private int recordIndex;
        [SerializeField] private bool protectFromRebake;

        [SerializeField, HideInInspector] private Vector3 originalLocalPosition;
        [SerializeField, HideInInspector] private Quaternion originalLocalRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private Vector3 originalLocalScale = Vector3.one;
        [SerializeField, HideInInspector] private int originalComponentCount;
        [SerializeField, HideInInspector] private int originalChildCount;
        [SerializeField, HideInInspector] private Material originalMaterial;
        [SerializeField, HideInInspector] private Mesh originalColliderMesh;
        [SerializeField, HideInInspector] private bool originalColliderEnabled;
        [SerializeField, HideInInspector] private bool originalColliderConvex;
        [SerializeField, HideInInspector] private Vector3 originalVisualLocalPosition;
        [SerializeField, HideInInspector] private Quaternion originalVisualLocalRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private Vector3 originalVisualLocalScale = Vector3.one;
        [SerializeField, HideInInspector] private int originalVisualChildCount;
        [SerializeField, HideInInspector] private int originalVisualComponentCount;
        [SerializeField, HideInInspector] private Mesh originalVisualMesh;

        public string AsteroidId => asteroidId;
        /// <summary>Material last assigned by the baker; helps distinguish authoring changes from custom material overrides.</summary>
        public Material BakedMaterialBaseline => originalMaterial;

        /// <summary>Records only a material update, preserving manual Transform/component edits for rebake protection.</summary>
        public void UpdateBakedMaterialBaseline(Material material) { originalMaterial = material; }
        public int RecordIndex => recordIndex;
        public AsteroidFieldController FieldController => fieldController;
        public bool ProtectFromRebake { get => protectFromRebake; set => protectFromRebake = value; }
        public AsteroidFieldRecord Record => fieldController != null ? fieldController.FindRecordById(asteroidId) : null;
        public AsteroidSaveRecord Data => Record?.asteroid;

        public void Configure(AsteroidFieldController owner, AsteroidFieldRecord record)
        {
            if (owner == null || record?.asteroid == null) throw new ArgumentNullException(nameof(record));
            fieldController = owner;
            asteroidId = record.asteroid.asteroidId;
            recordIndex = record.index;
        }

        public Dictionary<string, double> ExtractResources(double massKg)
        {
            return fieldController != null
                ? fieldController.ExtractResourcesById(asteroidId, massKg)
                : new Dictionary<string, double>();
        }

        /// <summary>Called by Editor baker after controlled modifications.</summary>
        public void CaptureBakeBaseline()
        {
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;
            originalLocalScale = transform.localScale;
            originalComponentCount = GetComponents<Component>().Length;
            originalChildCount = transform.childCount;
            var visual = transform.Find("__MO_BakedVisuals");
            var renderer = visual != null ? visual.GetComponent<MeshRenderer>() : null;
            originalMaterial = renderer != null ? renderer.sharedMaterial : null;
            if (visual != null)
            {
                originalVisualLocalPosition = visual.localPosition;
                originalVisualLocalRotation = visual.localRotation;
                originalVisualLocalScale = visual.localScale;
                originalVisualChildCount = visual.childCount;
                originalVisualComponentCount = visual.GetComponents<Component>().Length;
                var filter = visual.GetComponent<MeshFilter>();
                originalVisualMesh = filter != null ? filter.sharedMesh : null;
            }
            var collider = GetComponent<MeshCollider>();
            originalColliderMesh = collider != null ? collider.sharedMesh : null;
            originalColliderEnabled = collider != null && collider.enabled;
            originalColliderConvex = collider != null && collider.convex;
        }

        public bool HasManualEdits
        {
            get
            {
                if (protectFromRebake) return true;
                if ((transform.localPosition - originalLocalPosition).sqrMagnitude > .0001f ||
                    Quaternion.Angle(transform.localRotation, originalLocalRotation) > .01f ||
                    (transform.localScale - originalLocalScale).sqrMagnitude > .000001f) return true;
                if (GetComponents<Component>().Length != originalComponentCount ||
                    transform.childCount != originalChildCount) return true;
                var visual = transform.Find("__MO_BakedVisuals");
                if (visual == null) return true;
                if ((visual.localPosition - originalVisualLocalPosition).sqrMagnitude > .0001f ||
                    Quaternion.Angle(visual.localRotation, originalVisualLocalRotation) > .01f ||
                    (visual.localScale - originalVisualLocalScale).sqrMagnitude > .000001f ||
                    visual.childCount != originalVisualChildCount ||
                    visual.GetComponents<Component>().Length != originalVisualComponentCount) return true;
                var renderer = visual.GetComponent<MeshRenderer>();
                var filter = visual.GetComponent<MeshFilter>();
                if (renderer == null || renderer.sharedMaterial != originalMaterial ||
                    (filter != null ? filter.sharedMesh : null) != originalVisualMesh) return true;
                var collider = GetComponent<MeshCollider>();
                if ((collider != null ? collider.sharedMesh : null) != originalColliderMesh ||
                    (collider != null && collider.enabled) != originalColliderEnabled ||
                    (collider != null && collider.convex) != originalColliderConvex) return true;
                return false;
            }
        }

        private void OnEnable() { ApplySharedShaderProperties(); }

        public void ApplySharedShaderProperties()
        {
            var record = Data;
            if (record == null) return;
            var block = new MaterialPropertyBlock();
            block.SetFloat("_MO_ObjectRadius", Mathf.Max(.001f, record.diameterMeters * .5f));
            block.SetFloat("_MO_SurfaceSeed", (unchecked((uint)record.seed) & 65535u) / 65535f);
            block.SetFloat("_MO_LODVisibility", 1f);
            var visuals = transform.Find("__MO_BakedVisuals");
            if (visuals == null) return;
            var renderers = visuals.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
                if (renderer != null) renderer.SetPropertyBlock(block);
        }
    }

}
