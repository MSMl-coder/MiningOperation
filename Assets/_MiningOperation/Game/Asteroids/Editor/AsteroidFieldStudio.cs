using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Authoring-first field window. Preview is non-destructive; persistent generation
    /// is an explicit async action. Existing scene records are never rebuilt on selection.
    /// </summary>
    public sealed class AsteroidFieldStudio : EditorWindow
    {
        private AsteroidFieldSpawnPoint anchor;
        private AsteroidFieldSettings settings;
        private long seed = 80521L;
        private List<AsteroidFieldPlacement> preview;
        private string previewSignature;
        private string observedSignature;
        private string lastError;
        private string failedPreviewSignature;
        private double lastEditTime;
        private bool autoPreview = true;
        private bool showByClass = true;
        private bool showVertical;
        private int selectedIndex = -1;
        private Vector2 scroll;
        private float mapZoom = 1f;

        [MenuItem("Tools/Mining Operation/Asteroid Field Studio")]
        public static void Open() => GetWindow<AsteroidFieldStudio>("Field Studio");

        public static void OpenFor(AsteroidFieldSpawnPoint selected)
        {
            var window = GetWindow<AsteroidFieldStudio>("Field Studio");
            window.SetAnchor(selected);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            AsteroidAuthoringWorkflow.StateChanged += Repaint;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            AsteroidAuthoringWorkflow.StateChanged -= Repaint;
        }
        private void OnSelectionChange()
        {
            if (Selection.activeGameObject == null) return;
            var selected = Selection.activeGameObject.GetComponent<AsteroidFieldSpawnPoint>();
            if (selected != null && anchor != selected) SetAnchor(selected);
        }

        private void SetAnchor(AsteroidFieldSpawnPoint selected)
        {
            anchor = selected;
            if (anchor != null)
            {
                settings = anchor.settings;
                seed = anchor.fieldSeed;
            }
            Invalidate();
            Repaint();
        }

        private void Invalidate()
        {
            lastEditTime = EditorApplication.timeSinceStartup;
            selectedIndex = -1;
            observedSignature = null;
            lastError = null;
            failedPreviewSignature = null;
        }

        private bool Ready => AsteroidAuthoringWorkflow.IsReady(settings);
        private string CurrentSignature()
        {
            if (!Ready) return string.Empty;
            if (anchor != null) return AsteroidAuthoringWorkflow.Signature(anchor);
            // Legacy/manual preview without a Scene Spawn Point: only transient UI state.
            return seed.ToString() + "|" + JsonUtility.ToJson(settings) + "|" +
                JsonUtility.ToJson(settings.asteroidSettings) + "|" +
                JsonUtility.ToJson(settings.asteroidSettings.catalog);
        }
        private bool PreviewOutdated => preview == null || previewSignature != CurrentSignature();

        private void Tick()
        {
            if (anchor != null && anchor.settings != settings)
            {
                settings = anchor.settings;
                Invalidate();
            }
            if (!Ready) return;
            string current = CurrentSignature();
            if (current != observedSignature)
            {
                observedSignature = current;
                lastEditTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
            if (!autoPreview || !PreviewOutdated ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                AsteroidAuthoringWorkflow.IsBuilding) return;
            int count = anchor != null && anchor.useOverrides ? anchor.ResolveCount() : settings.asteroidCount;
            if (count > 350) return; // heavy previews require an explicit click
            if (EditorApplication.timeSinceStartup - lastEditTime < 0.4d) return;
            if (lastError != null && failedPreviewSignature == current) return;
            RefreshPreview(false);
        }

        private void OnGUI()
        {
            try
            {
                using (var scope = new EditorGUILayout.ScrollViewScope(scroll))
                {
                    scroll = scope.scrollPosition;
                    DrawContents();
                }
            }
            catch (ExitGUIException) { throw; }
            catch (Exception error)
            {
                // Never abandon an open ScrollView/HorizontalScope on an exception.
                if (lastError != error.Message)
                    Debug.LogException(error, this);
                lastError = error.Message;
            }
        }

        private void DrawContents()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("MINING OPERATION  /  FIELD AUTHORING", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Create    2. Place    3. Preview    4. Build", EditorStyles.miniLabel);
            EditorGUILayout.Space(7);
            DrawQuickStart();
            EditorGUILayout.Space(7);
            DrawPerformanceAndScene();
            EditorGUILayout.Space(7);
            DrawAnchorPicker();
            EditorGUILayout.Space(6);
            DrawSettings();
            EditorGUILayout.Space(6);
            DrawPreviewSection();
            EditorGUILayout.Space(6);
            DrawBuildSection();
            EditorGUILayout.Space(10);
        }

        private void DrawQuickStart()
        {
            EditorGUILayout.LabelField("QUICK START", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Ready Setup", GUILayout.Height(28)))
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    if (Ready && !EditorUtility.DisplayDialog("Create another asset set?",
                        "This creates a NEW Resource Catalog, Generator Settings and Field Settings. " +
                        "Your current assets and saved fields are kept.", "Create New Set", "Cancel")) return;
                    AsteroidFieldSettings created = AsteroidAuthoringWorkflow.CreateReadyAssets();
                    SetAnchor(AsteroidAuthoringWorkflow.CreateSpawnPoint(created));
                }
                using (new EditorGUI.DisabledScope(!Ready || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("Add Spawn Point", GUILayout.Height(28)))
                        SetAnchor(AsteroidAuthoringWorkflow.CreateSpawnPoint(settings));
                }
            }
            var legacy = Selection.activeGameObject != null ?
                Selection.activeGameObject.GetComponent<AsteroidFieldController>() : null;
            if (legacy != null && legacy.GetComponent<AsteroidFieldSpawnPoint>() == null &&
                !EditorApplication.isPlayingOrWillChangePlaymode &&
                GUILayout.Button("Use Selected Legacy Field (Preserve Snapshot)"))
                SetAnchor(AsteroidAuthoringWorkflow.ConvertLegacyField(legacy));
            EditorGUILayout.HelpBox("Create Ready Setup makes a catalog, asteroid settings, field settings " +
                "and one empty Spawn Point. Existing assets and saved fields are not replaced.", MessageType.None);
        }

        private void DrawPerformanceAndScene()
        {
            EditorGUILayout.LabelField("SCENE & PERFORMANCE", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Repair Sky + Lighting / Remove Old Visuals", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog("Repair Mining scene visuals?",
                        "Disable known older lighting/volume objects, remove only the generated background planet, " +
                        "and install one working gradient-sky environment. No asteroid data or field layout changes.",
                        "Repair", "Cancel")) AsteroidV2SceneSetup.InstallEnvironment();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(anchor == null || anchor.settings == null))
                    {
                        if (GUILayout.Button("Balanced Rendering"))
                            AsteroidV2SceneSetup.ApplyBalancedPreset(anchor);
                    }
                    if (GUILayout.Button("Frame Real Asteroid"))
                        AsteroidV2SceneSetup.FrameBelt();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rock Material Setup"))
                        AsteroidV2SceneSetup.UpgradeRockMaterials();
                    if (GUILayout.Button("Remove Old Editor Tools..."))
                        AsteroidV2SceneSetup.RemoveObsoleteEditorTools();
                }
            }
            EditorGUILayout.HelpBox("Balanced: 1,600 decorative rocks, no micro-fragment layer, " +
                "frustum/cell culling. These are visual-only settings; regenerate only when editing " +
                "the actual mineable formation.", MessageType.None);
        }

        private void DrawAnchorPicker()
        {
            EditorGUILayout.LabelField("FIELD IN SCENE", EditorStyles.boldLabel);
            var anchors = UnityEngine.Object.FindObjectsByType<AsteroidFieldSpawnPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(anchors, (a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            string[] names = new string[anchors.Length + 1];
            names[0] = "(Preview only - no scene anchor)";
            int selected = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                names[i + 1] = anchors[i].gameObject.name + "  [" + anchors[i].ResolveCount() + "]";
                if (anchors[i] == anchor) selected = i + 1;
            }
            int next = EditorGUILayout.Popup("Spawn Point", selected, names);
            if (next != selected) SetAnchor(next == 0 ? null : anchors[next - 1]);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(anchor == null))
                {
                    if (GUILayout.Button("Select in Hierarchy")) Selection.activeGameObject = anchor.gameObject;
                    if (GUILayout.Button("Frame in Scene")) FrameAnchor();
                }
                if (GUILayout.Button("Asteroid Studio")) AsteroidGeneratorStudio.Open();
                if (GUILayout.Button("Open 3D Preview"))
                {
                    if (anchor != null) AsteroidField3DPreview.OpenFor(anchor);
                    else AsteroidField3DPreview.Open();
                }
            }
            if (anchor != null && AsteroidAuthoringWorkflow.DuplicateId(anchor))
                EditorGUILayout.HelpBox("Duplicate Field ID in this scene. Give each spawn point a unique ID.", MessageType.Error);
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("AUTHORING SETTINGS", EditorStyles.boldLabel);
            AsteroidFieldSettings next = (AsteroidFieldSettings)EditorGUILayout.ObjectField(
                "Field Settings", settings, typeof(AsteroidFieldSettings), false);
            if (next != settings)
            {
                settings = next;
                if (anchor != null)
                {
                    Undo.RecordObject(anchor, "Set asteroid field settings");
                    if (anchor.Controller != null)
                        Undo.RecordObject(anchor.Controller, "Set asteroid field settings");
                    anchor.settings = settings;
                    anchor.SyncToController();
                    EditorUtility.SetDirty(anchor);
                    if (anchor.Controller != null) EditorUtility.SetDirty(anchor.Controller);
                    EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                }
                Invalidate();
            }
            if (!Ready)
            {
                EditorGUILayout.HelpBox("A valid field needs Field Settings → Generator Settings → Resource Catalog. " +
                    "Use Create Ready Setup for a complete preset.", MessageType.Warning);
                return;
            }
            if (anchor == null)
            {
                long nextSeed = EditorGUILayout.LongField("Preview Seed", seed);
                if (nextSeed != seed) { seed = nextSeed; Invalidate(); }
                EditorGUILayout.LabelField("Formation", settings.formation.ToString());
                EditorGUILayout.LabelField("Count", settings.asteroidCount.ToString());
                EditorGUILayout.HelpBox("No scene Spawn Point selected. Changes to this asset affect " +
                    "every anchor using its shared values. Select a point to edit its overrides.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("Field ID", anchor.fieldId);
                EditorGUILayout.LabelField("Seed", anchor.fieldSeed.ToString());
                AsteroidFieldLayoutOptions opt = anchor.BuildOptions(settings);
                EditorGUILayout.LabelField("Formation", opt.formation.ToString());
                EditorGUILayout.LabelField("Target asteroids", opt.asteroidCount.ToString());
                EditorGUILayout.LabelField("Radius", opt.radiusMeters.ToString("N0") + " m");
                if (GUILayout.Button("Edit Spawn Point Settings in Inspector"))
                    Selection.activeGameObject = anchor.gameObject;
            }
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("LAYOUT PREVIEW  /  NO GAME DATA CHANGED", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                autoPreview = GUILayout.Toggle(autoPreview, "Live preview (≤350)", GUILayout.Width(160));
                using (new EditorGUI.DisabledScope(!Ready || AsteroidAuthoringWorkflow.IsBuilding))
                {
                    if (GUILayout.Button("Refresh Preview")) RefreshPreview(true);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                showVertical = GUILayout.Toggle(showVertical, "Side (X/Y)", GUI.skin.button, GUILayout.Width(104));
                if (GUILayout.Button("Reset Zoom", GUILayout.Width(100))) mapZoom = 1f;
                mapZoom = EditorGUILayout.Slider("Zoom", mapZoom, 0.4f, 4f);
            }
            showByClass = EditorGUILayout.ToggleLeft("Color markers by resource class (otherwise by diameter)", showByClass);
            if (PreviewOutdated && Ready)
                EditorGUILayout.HelpBox("Preview is out of date. It does not change saved asteroids. " +
                    "For large fields use Refresh Preview.", MessageType.Warning);
            if (!string.IsNullOrEmpty(lastError))
                EditorGUILayout.HelpBox(lastError, MessageType.Error);
            Rect map = GUILayoutUtility.GetRect(100f, 390f, GUILayout.ExpandWidth(true));
            DrawMap(map);
            if (preview != null && !PreviewOutdated) DrawPreviewStats();
        }

        private void DrawMap(Rect rect)
        {
            if (Event.current == null) return;
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.075f, 0.105f, 0.155f));
                EditorGUI.DrawRect(new Rect(rect.center.x, rect.y + 10, 1, rect.height - 20),
                    new Color(0.25f, 0.34f, 0.42f));
                EditorGUI.DrawRect(new Rect(rect.x + 10, rect.center.y, rect.width - 20, 1),
                    new Color(0.25f, 0.34f, 0.42f));
                GUI.Label(new Rect(rect.x + 12, rect.y + 8, 220, 20),
                    showVertical ? "SIDE VIEW  ·  LOCAL X/Y" : "TOP VIEW  ·  LOCAL X/Z", EditorStyles.whiteMiniLabel);
            }
            if (preview == null || PreviewOutdated || preview.Count == 0)
            {
                if (Event.current.type == EventType.Repaint)
                    GUI.Label(new Rect(rect.x + 12, rect.y + 35, rect.width - 24, 22),
                        "Generate a preview to inspect the field", EditorStyles.whiteMiniLabel);
                return;
            }
            float extent = 10f;
            foreach (AsteroidFieldPlacement point in preview)
            {
                if (point == null) continue;
                extent = Mathf.Max(extent, Mathf.Abs(point.localPosition.x),
                    Mathf.Abs(showVertical ? point.localPosition.y : point.localPosition.z));
            }
            extent *= 1.15f;
            float scale = Mathf.Max(0.001f, Mathf.Min(rect.width - 36f, rect.height - 36f) * 0.5f / extent) * mapZoom;
            if (Event.current.type == EventType.Repaint)
            {
                float maxDiameter = anchor != null && anchor.useOverrides ?
                    anchor.maximumDiameterMeters : settings.asteroidSettings.maximumDiameterMeters;
                foreach (AsteroidFieldPlacement point in preview)
                {
                    if (point == null) continue;
                    Vector2 center = MapPoint(point, rect, scale);
                    if (!rect.Contains(center)) continue;
                    Color color = showByClass ? ClassColor(PredictClassForPreview(point.asteroidSeed)) :
                        Color.Lerp(new Color(0.25f, 0.67f, 0.86f), new Color(1f, 0.8f, 0.45f),
                            Mathf.Clamp01(point.nominalDiameter / Mathf.Max(1f, maxDiameter)));
                    float d = Mathf.Clamp(2f + Mathf.Sqrt(point.nominalDiameter * scale) * 1.8f, 3f, 15f);
                    EditorGUI.DrawRect(new Rect(center.x - d * 0.5f, center.y - d * 0.5f, d, d), color);
                    if (point.index == selectedIndex)
                        GUI.Label(new Rect(center.x + 9f, center.y - 12f, 180f, 20f),
                            point.index.ToString() + " / " + point.nominalDiameter.ToString("F0") + " m", EditorStyles.whiteMiniLabel);
                }
                GUI.Label(new Rect(rect.x + 12, rect.yMax - 24, rect.width - 24, 18),
                    showByClass ? "Class I: cyan   II: yellow   III: orange" : "Blue: small   Orange: large", EditorStyles.whiteMiniLabel);
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                float closest = 16f * 16f;
                int hit = -1;
                foreach (AsteroidFieldPlacement p in preview)
                {
                    if (p == null) continue;
                    float sq = (MapPoint(p, rect, scale) - Event.current.mousePosition).sqrMagnitude;
                    if (sq < closest) { closest = sq; hit = p.index; }
                }
                selectedIndex = hit;
                Event.current.Use();
                Repaint();
            }
        }

        private Vector2 MapPoint(AsteroidFieldPlacement point, Rect rect, float scale)
        {
            return new Vector2(rect.center.x + point.localPosition.x * scale,
                rect.center.y - (showVertical ? point.localPosition.y : point.localPosition.z) * scale);
        }
        private static Color ClassColor(AsteroidClass cls)
        {
            return cls == AsteroidClass.III ? new Color(1f, 0.55f, 0.25f) :
                cls == AsteroidClass.II ? new Color(1f, 0.83f, 0.34f) :
                new Color(0.42f, 0.80f, 0.96f);
        }
        private AsteroidClass PredictClassForPreview(int asteroidSeed)
        {
            if (!Ready) return AsteroidClass.I;
            var r = new AsteroidRandom(unchecked((uint)asteroidSeed) ^ 0xA3C59AC3u);
            r.Value();
            float roll = r.Value();
            float p3 = Mathf.Clamp01(settings.asteroidSettings.classIIIProbability);
            float p2 = Mathf.Min(Mathf.Clamp01(settings.asteroidSettings.classIIProbability), 1f - p3);
            return roll < p3 ? AsteroidClass.III : roll < p3 + p2 ? AsteroidClass.II : AsteroidClass.I;
        }

        private void DrawPreviewStats()
        {
            int first = 0, second = 0, third = 0;
            float smallest = float.MaxValue, largest = 0f;
            AsteroidFieldPlacement chosen = null;
            foreach (var p in preview)
            {
                AsteroidClass cls = PredictClassForPreview(p.asteroidSeed);
                if (cls == AsteroidClass.I) first++;
                else if (cls == AsteroidClass.II) second++;
                else third++;
                smallest = Mathf.Min(smallest, p.nominalDiameter);
                largest = Mathf.Max(largest, p.nominalDiameter);
                if (p.index == selectedIndex) chosen = p;
            }
            int targetCount = anchor != null && anchor.useOverrides ? anchor.ResolveCount() : settings.asteroidCount;
            EditorGUILayout.LabelField("Placed / Requested", preview.Count + " / " + targetCount);
            EditorGUILayout.LabelField("Class I / II / III (estimated)", first + " / " + second + " / " + third);
            EditorGUILayout.LabelField("Diameter range", smallest.ToString("F0") + "–" + largest.ToString("F0") + " m");
            if (preview.Count < targetCount)
                EditorGUILayout.HelpBox("Not all asteroids fit. Increase the radius or " +
                    "reduce surface gap / requested count.", MessageType.Warning);
            if (chosen != null)
                EditorGUILayout.HelpBox("Selected #" + chosen.index + "  |  seed " + chosen.asteroidSeed +
                    "  |  diameter " + chosen.nominalDiameter.ToString("F1") + " m  |  local position " +
                    chosen.localPosition.ToString("F0"), MessageType.None);
        }

        private void RefreshPreview(bool reportError)
        {
            if (!Ready) return;
            try
            {
                if (anchor != null)
                {
                    string warning = AsteroidAuthoringWorkflow.Validate(anchor);
                    if (warning != null) throw new InvalidOperationException(warning);
                    preview = AsteroidFieldLayout.Plan(settings, anchor.fieldSeed, anchor.fieldId,
                        anchor.BuildOptions(settings));
                }
                else preview = AsteroidFieldLayout.Plan(settings, seed, "PREVIEW-ONLY");
                previewSignature = CurrentSignature();
                lastError = null;
                failedPreviewSignature = null;
                Repaint();
            }
            catch (Exception e)
            {
                preview = null;
                lastError = e.Message;
                failedPreviewSignature = CurrentSignature();
                if (reportError) Debug.LogException(e, this);
                Repaint();
            }
        }

        private void DrawBuildSection()
        {
            EditorGUILayout.LabelField("SAVE TO SCENE", EditorStyles.boldLabel);
            if (anchor == null)
            {
                EditorGUILayout.HelpBox("Select a scene Spawn Point before building. Preview-only mode " +
                    "never modifies scene records.", MessageType.Info);
                return;
            }
            string invalid = AsteroidAuthoringWorkflow.Validate(anchor);
            if (invalid != null) EditorGUILayout.HelpBox(invalid, MessageType.Error);
            AsteroidFieldController field = anchor.Controller;
            if (field != null)
            {
                var snapshot = field.Snapshot;
                if (snapshot == null) EditorGUILayout.HelpBox("NOT BUILT — preview is not yet saved.", MessageType.Info);
                else if (AsteroidAuthoringWorkflow.IsOutdated(anchor))
                    EditorGUILayout.HelpBox("OUTDATED — generated asteroid records do not match current inputs. " +
                        "Rebuild to apply changes. Mined state will be reset.", MessageType.Warning);
                else EditorGUILayout.HelpBox("UP TO DATE — " + snapshot.asteroids.Count +
                    " asteroid records are stored in the scene.", MessageType.Info);
            }
            if (AsteroidAuthoringWorkflow.IsBuilding)
            {
                EditorGUILayout.LabelField("Building", AsteroidAuthoringWorkflow.BuildingController == field ?
                    (AsteroidAuthoringWorkflow.Progress * 100f).ToString("F0") + "%" : "another field");
                if (GUILayout.Button("Cancel Current Build")) AsteroidAuthoringWorkflow.CancelBuild();
            }
            else
            {
                using (new EditorGUI.DisabledScope(invalid != null || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(field != null && field.Snapshot != null ? "Rebuild Saved Field…" :
                        "Build Field in Scene", GUILayout.Height(35)))
                        AsteroidAuthoringWorkflow.BeginBuild(anchor);
                }
            }
            EditorGUILayout.HelpBox("Build changes stored asteroid records. Editing settings and " +
                "refreshing previews does NOT change mined data.", MessageType.None);
        }

        private void FrameAnchor()
        {
            if (anchor == null) return;
            Selection.activeGameObject = anchor.gameObject;
            if (SceneView.lastActiveSceneView == null) return;
            float radius = anchor.useOverrides ? anchor.radiusMeters :
                (anchor.settings != null ? anchor.settings.radiusMeters : 1000f);
            SceneView.lastActiveSceneView.Frame(new Bounds(anchor.transform.position,
                Vector3.one * Mathf.Max(200f, radius * 2f)), false);
        }
    }
}
