using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Applies the material selected on a Spawn Point to its baked mesh renderers.
    /// Mesh assets, colliders, root transforms, user components and manually assigned
    /// renderer materials are never modified in the default (managed-only) mode.
    /// </summary>
    public static class MOBakedMaterialSync
    {
        public struct Result
        {
            public int bodies;
            public int changedRenderers;
            public int protectedBodies;
            public int customRenderers;
        }

        public static Material ResolveSelectedMaterial(AsteroidFieldSpawnPoint anchor)
        {
            if (anchor == null) return null;
            if (anchor.visuals != null && anchor.visuals.unifiedAsteroidMaterialOverride != null)
                return anchor.visuals.unifiedAsteroidMaterialOverride;
            var settings = anchor.settings;
            if (settings == null) return null;
            if (settings.unifiedAsteroidMaterial != null) return settings.unifiedAsteroidMaterial;
            if ((anchor.contentType == MiningFieldContent.Ice || anchor.contentType == MiningFieldContent.IceShelf) &&
                settings.iceMaterial != null) return settings.iceMaterial;
            return settings.asteroidMaterial;
        }

        public static Result Apply(AsteroidFieldSpawnPoint anchor, bool force, bool log = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Material synchronization is available only in Edit Mode.");
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            var controller = anchor.Controller;
            var root = controller != null ? controller.BakedSceneRoot : null;
            if (root == null || root.Owner != controller)
                throw new InvalidOperationException("This Spawn Point has no baked field. The material will be used on the next Bake.");
            var material = ResolveSelectedMaterial(anchor);
            if (material == null)
                throw new InvalidOperationException("No material assigned. Choose Local Material Override or a shared material in Field Settings.");
            if (material.shader == null || !material.shader.isSupported)
                Debug.LogWarning("[MO Baked Material] The selected shader is missing/unsupported; asteroids may appear pink.", material);

            var result = new Result();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(force ? "Replace all baked asteroid materials" : "Sync baked asteroid materials");
            try
            {
                foreach (var body in root.GetComponentsInChildren<MOBakedAsteroid>(true))
                {
                    if (body == null || body.FieldController != controller) continue;
                    result.bodies++;
                    if (!force && body.ProtectFromRebake)
                    {
                        result.protectedBodies++;
                        continue;
                    }
                    var visual = body.transform.Find("__MO_BakedVisuals");
                    if (visual == null) continue;
                    Material baseline = body.BakedMaterialBaseline;
                    if (!force && HasCustomGeneratedMaterial(visual, baseline))
                    {
                        // A manually overridden LOD should not produce an inconsistent
                        // near/far mix when another material is applied to the field.
                        result.customRenderers++;
                        continue;
                    }
                    bool primaryIsManaged = false;

                    UpdateRenderer(visual.GetComponent<MeshRenderer>(), baseline, material,
                        force, ref result, out primaryIsManaged);
                    // Touch ONLY baker-created LOD children; not arbitrary custom child visuals.
                    foreach (Transform child in visual)
                    {
                        if (child == null || !IsGeneratedLODName(child.name)) continue;
                        bool ignored;
                        UpdateRenderer(child.GetComponent<MeshRenderer>(), baseline, material,
                            force, ref result, out ignored);
                    }
                    // Never recapture the entire baseline: the asteroid may have a manual
                    // Transform, collider or custom component that Preserve Edited protects.
                    if (primaryIsManaged && body.BakedMaterialBaseline != material)
                    {
                        Undo.RecordObject(body, "Sync asteroid material baseline");
                        body.UpdateBakedMaterialBaseline(material);
                        EditorUtility.SetDirty(body);
                    }
                }
                if (result.changedRenderers > 0 && anchor.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
            }
            finally { Undo.CollapseUndoOperations(group); }

            if (log)
                Debug.Log("[MO Baked Material] " + anchor.name + ": " +
                    result.changedRenderers + " renderers updated across " + result.bodies +
                    " asteroids. Protected: " + result.protectedBodies +
                    ", manual material overrides kept: " + result.customRenderers +
                    (force ? ". FORCE mode." : ". Managed-only mode."), anchor);
            return result;
        }

        private static void UpdateRenderer(MeshRenderer renderer, Material baseline, Material material,
            bool force, ref Result result, out bool usesManagedMaterial)
        {
            usesManagedMaterial = false;
            if (renderer == null) return;
            bool managed = renderer.sharedMaterial == null || renderer.sharedMaterial == baseline;
            if (!force && !managed)
            {
                result.customRenderers++;
                return;
            }
            usesManagedMaterial = true;
            if (renderer.sharedMaterial == material) return;
            Undo.RecordObject(renderer, "Apply baked asteroid surface");
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
            result.changedRenderers++;
        }

        private static bool HasCustomGeneratedMaterial(Transform visual, Material baseline)
        {
            var primary = visual.GetComponent<MeshRenderer>();
            if (primary != null && primary.sharedMaterial != null && primary.sharedMaterial != baseline)
                return true;
            foreach (Transform child in visual)
            {
                if (child == null || !IsGeneratedLODName(child.name)) continue;
                var renderer = child.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial != baseline)
                    return true;
            }
            return false;
        }

        private static bool IsGeneratedLODName(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("__LOD", StringComparison.Ordinal)) return false;
            return int.TryParse(name.Substring(5), out var n) && n >= 1 && n <= 4;
        }
    }
}
