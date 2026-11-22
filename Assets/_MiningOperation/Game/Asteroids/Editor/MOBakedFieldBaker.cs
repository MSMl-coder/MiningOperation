using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Editor-only conversion of authoritative field records to ordinary scene objects
    /// referencing persistent Mesh assets. No decorative/micro debris are baked.
    /// Editable roots and their custom MonoBehaviours survive mesh-only updates.
    /// </summary>
    public static class MOBakedFieldBaker
    {
        private const string BakedBase = "Assets/_MiningOperation/BakedFields";
        private const string VisualName = "__MO_BakedVisuals";

        [MenuItem("Tools/Mining Operation/Bake Selected Asteroid Field", priority = 161)]
        private static void BakeMenu()
        {
            var anchor = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<AsteroidFieldSpawnPoint>() : null;
            if (anchor == null && Selection.activeGameObject != null)
                anchor = Selection.activeGameObject.GetComponentInParent<AsteroidFieldSpawnPoint>();
            BakeSelected(anchor);
        }

        [MenuItem("Tools/Mining Operation/Bake Selected Asteroid Field", true)]
        private static bool ValidateBakeMenu() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void BakeSelected(AsteroidFieldSpawnPoint anchor)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Edit Mode required", "Stop Play Mode to bake scene objects.", "OK");
                return;
            }
            if (anchor == null || anchor.contentType == MiningFieldContent.Gas)
            {
                EditorUtility.DisplayDialog("Select a rock/ice location", "Select a Rock, Ice or Ice Shelf Spawn Point in the Hierarchy.", "OK");
                return;
            }
            if (AsteroidAuthoringWorkflow.IsBuilding)
            {
                EditorUtility.DisplayDialog("Builder busy", "Finish the current field build before baking.", "OK");
                return;
            }
            var controller = anchor.Controller;
            if (controller == null || anchor.settings == null || anchor.settings.asteroidSettings == null)
            {
                EditorUtility.DisplayDialog("Missing field assets", "Field Settings and Generator Settings are required. Existing saved assets were not modified.", "OK");
                return;
            }
            if (string.IsNullOrEmpty(anchor.gameObject.scene.path))
            {
                EditorUtility.DisplayDialog("Save the scene first", "Save the Unity scene before baking. Baked Mesh assets must have a stable scene owner.", "OK");
                return;
            }
            MOBakeRebuildMode mode = anchor.bakedRebuildMode;
            bool hasBaked = controller.BakedSceneRoot != null;
            if (mode == MOBakeRebuildMode.RebuildEntireField &&
                !EditorUtility.DisplayDialog("REBUILD ENTIRE ASTEROID FIELD?",
                    "This regenerates the entire snapshot from the current settings and DELETES the baked GameObjects, custom scripts, child objects, manual positions, and mined resource state. Generated Mesh assets also get replaced. Create a scene/Git backup first.",
                    "Delete edits and rebuild", "Cancel")) return;

            // An existing snapshot is the authority for non-destructive rebakes.
            // Full rebuild alone may replace field records and ore state.
            if ((controller.Snapshot == null || mode == MOBakeRebuildMode.RebuildEntireField) &&
                !EditorUtility.DisplayDialog("Generate field records?",
                    controller.Snapshot == null
                        ? "No saved field records exist. Generate them once now, then bake editable objects?"
                        : "Rebuild the entire field layout and resource records from current settings?",
                    "Generate and Bake", "Cancel")) return;

            string folder = hasBaked && !string.IsNullOrEmpty(controller.BakedSceneRoot.AssetFolder)
                ? controller.BakedSceneRoot.AssetFolder : GetFieldFolder(anchor, controller);
            if (hasBaked && controller.BakedSceneRoot.Owner != controller)
                throw new InvalidOperationException("Baked root is attached to a different field controller.");

            try
            {
                anchor.SyncToController();
                if (controller.Snapshot == null || mode == MOBakeRebuildMode.RebuildEntireField)
                {
                    controller.BeginBuild(true);
                    while (controller.IsBuilding)
                    {
                        EditorUtility.DisplayProgressBar("Mining Operation / Field Records",
                            "Generating the permanent geological records", controller.BuildTotal > 0
                                ? (float)controller.BuildProgress / controller.BuildTotal : 0f);
                        controller.BuildStep(Mathf.Max(1, anchor.settings.generatePerFrame * 12));
                    }
                }
                if (controller.Snapshot?.asteroids == null || controller.Snapshot.asteroids.Count == 0)
                    throw new InvalidOperationException("No asteroid records to bake. Check the formation layout.");

                // Before switching renderer systems, retain all manually enhanced legacy
                // instances rather than deleting them; just deactivate duplicate visuals.
                // The new baked components replace their gameplay role.
                foreach (var member in controller.GetComponentsInChildren<AsteroidFieldMember>(true))
                {
                    if (member == null || member.gameObject == controller.gameObject) continue;
                    var legacy = member.GetComponent<AsteroidInstance>();
                    if (legacy == null || !legacy.gameObject.activeSelf) continue;
                    var oldRecord = controller.FindRecord(member.index);
                    if (oldRecord != null && legacy.Data != null &&
                        oldRecord.asteroid != null && legacy.Data.asteroidId == oldRecord.asteroid.asteroidId)
                        oldRecord.asteroid = legacy.Capture();
                    legacy.gameObject.SetActive(false);
                }

                // Staging and validation completed; only now modify the scene and asset files.
                EnsureAssetFolder(folder);
                MOBakedFieldRoot root = controller.BakedSceneRoot;
                if (mode == MOBakeRebuildMode.RebuildEntireField && root != null)
                {
                    Undo.DestroyObjectImmediate(root.gameObject);
                    root = null;
                    // The generated-only folder is exclusively owned by this baker.
                    // Delete only explicitly named generated mesh files, not arbitrary assets.
                    RemoveGeneratedMeshes(folder);
                }
                if (root == null)
                {
                    var go = new GameObject("__MO_BakedField");
                    Undo.RegisterCreatedObjectUndo(go, "Create baked asteroid field");
                    go.transform.SetParent(controller.transform, false);
                    root = Undo.AddComponent<MOBakedFieldRoot>(go);
                    root.transform.localPosition = Vector3.zero;
                    root.transform.localRotation = Quaternion.identity;
                    root.transform.localScale = Vector3.one;
                }
                // An existing baked root might have been moved as a group by the user.
                // Never reset its Transform for Preserve / Update Geometry.
                root.Configure(controller, folder);

                var existing = new Dictionary<string, MOBakedAsteroid>(StringComparer.Ordinal);
                foreach (var body in root.GetComponentsInChildren<MOBakedAsteroid>(true))
                {
                    if (body == null || string.IsNullOrEmpty(body.AsteroidId)) continue;
                    if (!existing.ContainsKey(body.AsteroidId)) existing.Add(body.AsteroidId, body);
                }

                AsteroidGeneratorSettings generator = SelectGenerator(anchor);
                if (generator == null)
                    throw new InvalidOperationException("Missing asteroid Generator Settings; restore its .asset script link first.");
                Material material = SelectMaterial(anchor);
                if (material == null)
                    Debug.LogWarning("[MO Bake] No surface material assigned. The generated scene objects will need a .mat.", anchor);
                int created = 0, updated = 0, protectedCount = 0;
                bool cancelled = false;
                var records = controller.Snapshot.asteroids;
                for (int i = 0; i < records.Count; ++i)
                {
                    var record = records[i];
                    if (record?.asteroid == null) continue;
                    string progressMessage = "Meshes: " + (i + 1) + " / " + records.Count +
                                             "  ·  " + record.asteroid.asteroidId;
                    float progress = (float)(i + 1) / records.Count;
                    if (mode == MOBakeRebuildMode.RebuildEntireField)
                        EditorUtility.DisplayProgressBar("Mining Operation / Full Baked Field Rebuild", progressMessage, progress);
                    else if (EditorUtility.DisplayCancelableProgressBar("Mining Operation / Editable Field Bake", progressMessage, progress))
                    {
                        cancelled = true;
                        break;
                    }
                    existing.TryGetValue(record.asteroid.asteroidId, out var body);
                    bool isNew = body == null;
                    if (!isNew && mode == MOBakeRebuildMode.PreserveEdited && body.HasManualEdits)
                    {
                        protectedCount++;
                        continue;
                    }
                    bool previouslyEdited = !isNew && body.HasManualEdits;
                    if (isNew)
                    {
                        var go = new GameObject(record.asteroid.asteroidId);
                        Undo.RegisterCreatedObjectUndo(go, "Bake asteroid object");
                        go.transform.SetParent(root.transform, false);
                        go.transform.SetPositionAndRotation(
                            controller.transform.TransformPoint(record.localPosition),
                            controller.transform.rotation * record.localRotation);
                        go.transform.localScale = Vector3.one;
                        body = Undo.AddComponent<MOBakedAsteroid>(go);
                        created++;
                    }
                    else updated++;
                    body.Configure(controller, record);
                    UpdateVisuals(body, record, generator, material, anchor.bakedLODCount,
                        anchor.bakedMeshColliders && (isNew || !previouslyEdited), folder);
                    body.ApplySharedShaderProperties();
                    // PreserveManual mode remembers last clean authoring state;
                    // UpdateGeometry preserves user edits and keeps them recognizable as edited.
                    if (isNew || !previouslyEdited) body.CaptureBakeBaseline();
                    else
                    {
                        // Updating geometry on a manually moved or extended asteroid must
                        // not silently reset its other edit baselines.
                        var visual = body.transform.Find(VisualName);
                        var primaryRenderer = visual != null ? visual.GetComponent<MeshRenderer>() : null;
                        if (primaryRenderer != null && primaryRenderer.sharedMaterial == material && material != null)
                            body.UpdateBakedMaterialBaseline(material);
                    }
                    EditorUtility.SetDirty(body);
                }

                root.Reindex();
                Undo.RecordObject(controller, "Enable persistent baked field");
                controller.SetBakedScene(root);
                controller.SyncBakedSceneToSnapshot();
                EditorUtility.SetDirty(root);
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(anchor);
                EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[MO Bake] " + anchor.name + ": " + created + " created, " + updated +
                          " mesh-updated, " + protectedCount + " protected/manual skipped." +
                          (cancelled ? " Cancelled — partial scene is retained; run Bake again to complete." : " Complete.") +
                          " Decorative fragments: not baked (runtime GPU instancing, if enabled).", anchor);
                if (cancelled)
                    EditorUtility.DisplayDialog("Partial bake retained", "Bake was cancelled. Existing objects were left in place; run the same Bake mode again to finish.", "OK");
                else
                    EditorUtility.DisplayDialog("Editable field ready", $"{created} new asteroids, {updated} updated, {protectedCount} protected.\n\nCustom scripts, colliders and manual Transform changes are preserved in non-destructive modes.\n\nSave the scene (Ctrl/Cmd+S) and commit the generated Mesh assets with their .meta files.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e, controller);
                EditorUtility.DisplayDialog("Bake did not complete", e.Message + "\n\nExisting assets were not automatically deleted. Check Console and use Bake again after fixing the error.", "OK");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private static AsteroidGeneratorSettings SelectGenerator(AsteroidFieldSpawnPoint anchor)
        {
            AsteroidFieldSettings settings = anchor.settings;
            return (anchor.contentType == MiningFieldContent.Ice || anchor.contentType == MiningFieldContent.IceShelf) &&
                settings.iceGeneratorSettings != null ? settings.iceGeneratorSettings : settings.asteroidSettings;
        }

        private static Material SelectMaterial(AsteroidFieldSpawnPoint anchor)
        {
            return MOBakedMaterialSync.ResolveSelectedMaterial(anchor);
        }

        private static void UpdateVisuals(MOBakedAsteroid body, AsteroidFieldRecord rec,
            AsteroidGeneratorSettings generator, Material material, int lodCount, bool addCollider, string folder)
        {
            var visuals = body.transform.Find(VisualName);
            if (visuals == null)
            {
                var go = new GameObject(VisualName);
                Undo.RegisterCreatedObjectUndo(go, "Create asteroid baked visuals");
                go.transform.SetParent(body.transform, false);
                visuals = go.transform;
            }
            // Do not move or delete user-made children. Generated visual transform is
            // fixed to the origin; user-authored transforms live on the asteroid root.
            int count = Mathf.Clamp(lodCount, 1, 5);
            int[] quality =
            {
                Mathf.Clamp(generator.lod0Subdivisions, 0, 6),
                Mathf.Clamp(generator.lod1Subdivisions, 0, 6),
                Mathf.Clamp(generator.lod2Subdivisions, 0, 6),
                Mathf.Clamp(generator.lod3Subdivisions, 0, 6),
                Mathf.Clamp(generator.lod4Subdivisions, 0, 6)
            };
            for (int i = 1; i < quality.Length; i++) quality[i] = Mathf.Min(quality[i], quality[i - 1]);
            float[] thresholds = { .53f, .30f, .14f, .045f, .001f };
            var levels = new LOD[count];
            Mesh primaryMesh = null;
            for (int i = 0; i < count; i++)
            {
                Transform target = i == 0 ? visuals : visuals.Find("__LOD" + i);
                if (target == null)
                {
                    var child = new GameObject("__LOD" + i);
                    Undo.RegisterCreatedObjectUndo(child, "Create baked LOD");
                    child.transform.SetParent(visuals, false);
                    target = child.transform;
                }
                var filter = target.GetComponent<MeshFilter>();
                if (filter == null) filter = Undo.AddComponent<MeshFilter>(target.gameObject);
                var renderer = target.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = Undo.AddComponent<MeshRenderer>(target.gameObject);
                var geometry = new AsteroidData
                {
                    asteroidId = rec.asteroid.asteroidId,
                    seed = rec.asteroid.seed,
                    generatorVersion = rec.asteroid.generatorVersion,
                    diameterMeters = rec.asteroid.diameterMeters,
                    geometryProfile = rec.asteroid.geometryProfile
                };
                Mesh generated = AsteroidGeometryGenerator.Generate(geometry, generator, quality[i]);
                Mesh asset = StoreMesh(generated, folder, rec.asteroid.asteroidId, i);
                filter.sharedMesh = asset;
                // Only replace the material automatically if this renderer still uses
                // the last baker-assigned material. Preserve manual per-asteroid overrides.
                if (material != null && (renderer.sharedMaterial == null ||
                    renderer.sharedMaterial == body.BakedMaterialBaseline))
                    renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; // compatible with existing far/near shader setup
                if (i == 0) primaryMesh = asset;
                levels[i] = new LOD(i == count - 1 ? .001f : thresholds[i], new[] { renderer });
                EditorUtility.SetDirty(filter);
                EditorUtility.SetDirty(renderer);
            }
            // Unused former generated levels are disabled, not destroyed: deleting
            // them might also delete custom scripts or custom child GameObjects.
            for (int i = count; i <= 4; i++)
            {
                var unused = visuals.Find("__LOD" + i);
                if (unused != null) unused.gameObject.SetActive(false);
            }
            for (int i = 1; i < count; i++)
            {
                var used = visuals.Find("__LOD" + i);
                if (used != null) used.gameObject.SetActive(true);
            }
            var group = body.GetComponent<LODGroup>();
            if (group == null) group = Undo.AddComponent<LODGroup>(body.gameObject);
            group.fadeMode = LODFadeMode.None; // Textured shader currently lacks Unity LOD crossfade clip.
            group.SetLODs(levels);
            group.RecalculateBounds();
            EditorUtility.SetDirty(group);
            if (addCollider)
            {
                var collider = body.GetComponent<MeshCollider>();
                if (collider == null) collider = Undo.AddComponent<MeshCollider>(body.gameObject);
                // Preserve a manually substituted collider mesh.
                if (collider.sharedMesh == null || IsGeneratedMesh(collider.sharedMesh))
                {
                    collider.sharedMesh = primaryMesh;
                    EditorUtility.SetDirty(collider);
                }
            }
        }

        private static bool IsGeneratedMesh(Mesh mesh)
        {
            if (mesh == null) return false;
            var path = AssetDatabase.GetAssetPath(mesh);
            return path.StartsWith(BakedBase + "/", StringComparison.Ordinal) &&
                   Path.GetFileName(path).StartsWith("MO_Baked__", StringComparison.Ordinal);
        }

        private static Mesh StoreMesh(Mesh generated, string folder, string id, int level)
        {
            string name = "MO_Baked__" + SafeName(id, 65) + "_LOD" + level + ".asset";
            string path = folder + "/" + name;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                // Copy into the existing Mesh asset so its GUID and all references stay stable.
                Undo.RecordObject(mesh, "Rebake asteroid mesh");
                try { EditorUtility.CopySerialized(generated, mesh); EditorUtility.SetDirty(mesh); }
                finally { UnityEngine.Object.DestroyImmediate(generated); }
                return mesh;
            }
            if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                UnityEngine.Object.DestroyImmediate(generated);
                throw new IOException("Mesh path exists but is not a valid Mesh asset: " + path);
            }
            generated.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        private static void RemoveGeneratedMeshes(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith("MO_Baked__", StringComparison.Ordinal))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        private static string GetFieldFolder(AsteroidFieldSpawnPoint anchor, AsteroidFieldController controller)
        {
            string scene = SafeName(Path.GetFileNameWithoutExtension(anchor.gameObject.scene.path), 35);
            // GlobalObjectId is stable across editor restarts after the scene is saved.
            string global = GlobalObjectId.GetGlobalObjectIdSlow(controller).ToString();
            string source = scene + "|" + global + "|" + controller.FieldId;
            uint hash = 2166136261u;
            unchecked { foreach (char c in source) { hash ^= c; hash *= 16777619u; } }
            return BakedBase + "/" + scene + "_" + hash.ToString("X8");
        }

        private static string SafeName(string s, int limit)
        {
            if (string.IsNullOrEmpty(s)) return "Field";
            var chars = s.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray();
            return new string(chars, 0, Math.Min(limit, chars.Length));
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
