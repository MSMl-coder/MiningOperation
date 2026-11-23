using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiningOperation.Asteroids.Editor
{
    /// <summary>
    /// Mining Operation's single authoring window. No nested editor windows and no
    /// automatic destructive regeneration. Includes spawn points, configuration,
    /// 2D distribution preview, one real-mesh 3D sample, assets and build controls.
    /// </summary>
    public sealed class AsteroidFieldStudio : EditorWindow
    {
        private AsteroidFieldSpawnPoint selected;
        private AsteroidFieldSettings field;
        private readonly MOEmbeddedAsteroidPreview sample = new MOEmbeddedAsteroidPreview();
        private readonly List<AsteroidFieldPlacement> rocks = new List<AsteroidFieldPlacement>();
        private MOGasFieldSnapshot gasPreview;
        private bool gasVisualRefreshQueued;
        private bool? hasExistingSettingsCache;
        private string previewSignature;
        private string lastError;
        private Vector2 leftScroll, rightScroll;
        private int tab;
        private bool advanced;
        private bool advancedQuality;
        private bool advancedDebris;
        private bool showBySize = true;
        private MiningFieldContent newKind = MiningFieldContent.Rock;
        private float mapScale = 1f;
        private readonly Color panel = new Color(.115f, .16f, .195f);

        [MenuItem("Tools/Mining Operation/Asteroid Field Studio")]
        public static void Open()
        {
            var w = GetWindow<AsteroidFieldStudio>("Field Generator");
            w.minSize = new Vector2(680f, 480f);
            w.Show();
        }
        public static void OpenFor(AsteroidFieldSpawnPoint anchor)
        {
            var w = GetWindow<AsteroidFieldStudio>("Field Generator");
            w.minSize = new Vector2(680f, 480f);
            w.SelectAnchor(anchor);
            w.Show();
        }

        private void OnEnable()
        {
            hasExistingSettingsCache = null;
            AsteroidAuthoringWorkflow.StateChanged += Repaint;
            EditorApplication.update += Tick;
            if (selected == null)
            {
                var anchor = Selection.activeGameObject != null
                    ? Selection.activeGameObject.GetComponent<AsteroidFieldSpawnPoint>() : null;
                if (anchor == null)
                {
                    var anchors = UnityEngine.Object.FindObjectsByType<AsteroidFieldSpawnPoint>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    if (anchors.Length == 1 && anchors[0].gameObject.scene.IsValid()) anchor = anchors[0];
                }
                if (anchor != null) SelectAnchor(anchor);
            }
        }
        private void OnDisable()
        {
            AsteroidAuthoringWorkflow.StateChanged -= Repaint;
            EditorApplication.update -= Tick;
            sample.Cleanup();
        }
        private void Tick()
        {
            if (AsteroidAuthoringWorkflow.IsBuilding) Repaint();
        }
        private void OnProjectChange()
        {
            hasExistingSettingsCache = null;
            Repaint();
        }

        private bool HasExistingSettingsOnDisk()
        {
            if (!hasExistingSettingsCache.HasValue)
                hasExistingSettingsCache = AsteroidAuthoringWorkflow.FindExistingSettingsAssets(out _);
            return hasExistingSettingsCache.Value;
        }

        private void OnSelectionChange()
        {
            var a = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<AsteroidFieldSpawnPoint>() : null;
            if (a != null && a != selected) SelectAnchor(a);
        }
        private void SelectAnchor(AsteroidFieldSpawnPoint a)
        {
            selected = a;
            field = a != null ? a.settings : null;
            if (a != null && a.settings != null)
            {
                Undo.RecordObject(a, "Initialize spawn-specific settings");
                if (a.EnsureLocalSettings())
                {
                    EditorUtility.SetDirty(a);
                    if (a.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(a.gameObject.scene);
                }
            }
            rocks.Clear();
            gasPreview = null;
            previewSignature = null;
            sample.Invalidate();
            lastError = null;
            Repaint();
        }

        private void OnGUI()
        {
            try
            {
                DrawHeader();
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSidebar();
                    using (new EditorGUILayout.VerticalScope())
                    {
                        using (var scope = new EditorGUILayout.ScrollViewScope(rightScroll))
                        {
                            rightScroll = scope.scrollPosition;
                            DrawMain();
                        }
                    }
                }
                DrawFooter();
            }
            catch (ExitGUIException) { throw; }
            catch (Exception ex)
            {
                if (lastError != ex.Message) Debug.LogException(ex, this);
                lastError = ex.Message;
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("MINING OPERATION  /  ENVIRONMENT GENERATOR", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (selected != null)
                    GUILayout.Label(selected.contentType.ToString().ToUpperInvariant() + "  ·  " + selected.name,
                        EditorStyles.miniLabel);
                else GUILayout.Label("NO LOCATION", EditorStyles.miniLabel);
            }
        }
        private void DrawSidebar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(210f)))
            {
                using (var scope = new EditorGUILayout.ScrollViewScope(leftScroll))
                {
                    leftScroll = scope.scrollPosition;
                    EditorGUILayout.Space(8);
                    GUILayout.Label("LOCATIONS", EditorStyles.boldLabel);
                    var all = UnityEngine.Object.FindObjectsByType<AsteroidFieldSpawnPoint>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    Array.Sort(all, (x, y) => string.Compare(x.name, y.name, StringComparison.OrdinalIgnoreCase));
                    foreach (var anchor in all)
                    {
                        if (anchor == null || !anchor.gameObject.scene.IsValid()) continue;
                        bool active = anchor == selected;
                        string glyph = anchor.contentType == MiningFieldContent.Gas ? "☁" :
                            anchor.contentType == MiningFieldContent.IceShelf ? "▤" :
                            anchor.contentType == MiningFieldContent.Ice ? "❄" : "◆";
                        string text = glyph + "  " + anchor.gameObject.name;
                        var style = active ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                        if (GUILayout.Button(text, style, GUILayout.Height(27f)))
                        {
                            SelectAnchor(anchor);
                            Selection.activeGameObject = anchor.gameObject;
                        }
                    }
                    EditorGUILayout.Space(9);
                    GUILayout.Label("ADD LOCATION", EditorStyles.boldLabel);
                    newKind = (MiningFieldContent)EditorGUILayout.EnumPopup(newKind);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        if (GUILayout.Button("+ Create Spawn Point", GUILayout.Height(30f))) CreatePoint();
                        if (!HasExistingSettingsOnDisk())
                        {
                            if (GUILayout.Button("Create First-Time Setup", GUILayout.Height(23f))) CreateSetup();
                        }
                    }
                    GUILayout.Space(6);
                    EditorGUILayout.HelpBox("Choose a location, edit its parameters, then Build. Existing settings are never replaced automatically.", MessageType.None);
                }
            }
        }

        private void DrawMain()
        {
            if (selected == null)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox("Choose a Spawn Point or create one. Generation and previews are controlled here; materials are edited in their own Material Inspector.", MessageType.Info);
                return;
            }
            if (field != selected.settings)
            {
                field = selected.settings;
                InvalidatePreview();
            }
            tab = GUILayout.Toolbar(tab, new[] { "LOCATION", "SHAPE & QUALITY", "VISUALS" }, GUILayout.Height(27));
            EditorGUILayout.Space(8);
            if (tab == 0) DrawLocation();
            else if (tab == 1) DrawShape();
            else DrawVisuals();
            if (!string.IsNullOrEmpty(lastError)) EditorGUILayout.HelpBox(lastError, MessageType.Error);
        }

        private void DrawLocation()
        {
            Header("Content & identity");
            SerializedObject so = new SerializedObject(selected);
            so.Update();
            // Keep this property in scope for the type-specific controls below.
            SerializedProperty type = so.FindProperty("contentType");
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(type, new GUIContent("Type"));
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.PropertyField(so.FindProperty("settings"), new GUIContent("Shared library / defaults"));
            EditorGUILayout.PropertyField(so.FindProperty("fieldSeed"), new GUIContent("Seed"));
            EditorGUILayout.PropertyField(so.FindProperty("fieldId"), new GUIContent("Persistent ID"));
            using (new EditorGUI.DisabledScope(selected.settings == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Rename linked Field Settings to type + formation", GUILayout.Height(22f)))
                {
                    try { MOUnifiedAsteroidAssetTools.RenameFieldSettings(selected); }
                    catch (Exception ex) { lastError = ex.Message; }
                }
            }
            // Additional type-specific options are in this same panel, not another tool.
            MiningFieldContent kind = (MiningFieldContent)type.enumValueIndex;
            bool changed = false;
            if (kind == MiningFieldContent.Gas)
            {
                EditorGUILayout.Space(4);
                Header("Gas cloud formation (visual only)");
                Prop(so, "radiusMeters", "Field radius (m)");
                Prop(so, "thicknessMeters", "Vertical spread (m)");
                Prop(so, "gasCloudCount", "Cloud clusters");
                Prop(so, "gasCloudRadiusMeters", "Cloud radius (m)");
                EditorGUILayout.HelpBox("Environmental gas clouds are visual only. Gas is produced by industrial systems, not collected here. Select SoftParticles, Volumetric or GroundFog in VISUALS.", MessageType.None);
                if (selected.Controller != null && selected.Controller.Snapshot != null)
                {
                    EditorGUILayout.HelpBox("A saved asteroid field also exists here. It stays untouched and is not rendered in Gas mode.", MessageType.Warning);
                }
            }
            else
            {
                string formationHeader = kind == MiningFieldContent.IceShelf ? "Ice shelf / plateau formation" :
                    kind == MiningFieldContent.Ice ? "Ice asteroid formation" : "Rock asteroid formation";
                Header(formationHeader);
                Prop(so, "useOverrides", "Local layout settings");
                if (so.FindProperty("useOverrides").boolValue)
                {
                    Prop(so, "formation", "Formation");
                    int formationValue = so.FindProperty("formation").intValue;
                    bool iceShelf = formationValue == (int)AsteroidFormationType.IceShelfFragments ||
                        formationValue == (int)AsteroidFormationType.IceShelfContinents;
                    if (iceShelf)
                    {
                        if (kind != MiningFieldContent.IceShelf)
                        {
                            type.enumValueIndex = (int)MiningFieldContent.IceShelf;
                            EditorGUILayout.HelpBox("Ice Shelf automatically uses the dedicated ICE SHELF content type, ice resources and ice materials.", MessageType.Info);
                        }
                        Prop(so, "shelfThicknessRatio", "Plate thickness / diameter");
                        Prop(so, "shelfFractureStrength", "Surface fractures");
                        Prop(so, "shelfCollisionStrength", "Collision ridge strength");
                        Prop(so, "shelfRiftDepth", "Rift depth");
                        EditorGUILayout.HelpBox("Large ice plates with collision ridges and long rifts. Use the parameters above to shape this formation.", MessageType.None);
                    }
                    Prop(so, "countMode", "Count");
                    var countMode = (AsteroidCountMode)so.FindProperty("countMode").enumValueIndex;
                    if (countMode == AsteroidCountMode.Fixed) Prop(so, "fixedCount", "Number of bodies");
                    if (countMode == AsteroidCountMode.SeededRange)
                    {
                        Prop(so, "randomCountMin", "Minimum count");
                        Prop(so, "randomCountMax", "Maximum count");
                    }
                    if (countMode == AsteroidCountMode.DensityPerSquareKm)
                        Prop(so, "asteroidsPerSquareKm", "Density / km²");
                    Prop(so, "radiusMeters", "Field radius (m)");
                    Prop(so, "thicknessMeters", "Thickness (m)");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Prop(so, "minimumDiameterMeters", "Min diameter");
                        Prop(so, "maximumDiameterMeters", "Max diameter");
                    }
                    Prop(so, "smallAsteroidBias", "Small body bias");
                    AsteroidFormationType selectedFormation = (AsteroidFormationType)so.FindProperty("formation").intValue;
                    if (kind != MiningFieldContent.IceShelf &&
                        (selectedFormation == AsteroidFormationType.CrescentBelt ||
                         selectedFormation == AsteroidFormationType.BrokenBelt))
                    {
                        Header("Natural belt / no repeating arcs");
                        Prop(so, "useNaturalBeltDistribution", "Natural distribution");
                        if (so.FindProperty("useNaturalBeltDistribution").boolValue)
                        {
                            Prop(so, "naturalBeltIrregularity", "Belt irregularity");
                            Prop(so, "naturalDensityContrast", "Density island contrast");
                            Prop(so, "naturalSatelliteChance", "Satellite fraction");
                            EditorGUILayout.HelpBox("Belt bands will have non-periodic warping, uneven widths and independently located density pockets. The layout changes only after BUILD / REBUILD.", MessageType.None);
                        }
                    }
                    advanced = EditorGUILayout.Foldout(advanced, "Advanced formation", true);
                    if (advanced)
                    {
                        Prop(so, "useComposedLayout", "Composed layout (local)");
                        Prop(so, "arcDegrees", "Arc degrees");
                        Prop(so, "clusterCount", "Bands / groups");
                        Prop(so, "clusterStrength", "Cluster strength");
                        Prop(so, "densityPockets", "Density pockets");
                        Prop(so, "pocketWidthFraction", "Pocket width fraction");
                        Prop(so, "cloudVerticalScale", "Vertical density scale");
                        Prop(so, "clusterRadiusFraction", "Group radius fraction");
                        Prop(so, "surfaceGapMeters", "Spacing (m)");
                        Prop(so, "placementAttempts", "Placement attempts");
                    }
                }
                else EditorGUILayout.HelpBox("Using the shared formation parameters in Field Settings.", MessageType.None);
                Header("2D gameplay height constraint (Y)");
                Prop(so, "depthConstraints.depthMode", "Height constraint (Y)");
                int depthMode = so.FindProperty("depthConstraints.depthMode").enumValueIndex;
                if (depthMode != (int)MOFieldDepthMode.Free3D)
                {
                    Prop(so, "depthConstraints.yCenter", "Y center");
                    if (depthMode == (int)MOFieldDepthMode.PlanarSlab)
                        Prop(so, "depthConstraints.yHalfRange", "Y half-range");
                    else
                        Prop(so, "depthConstraints.yJitter", "Y jitter");
                    Prop(so, "depthConstraints.flattenRotationToPlane", "Flatten rotations to plane");
                    EditorGUILayout.HelpBox("Use this for 2D gameplay lanes: asteroids still keep full 3D geometry, but their generated positions are clamped on local Y.", MessageType.None);
                }
            }
            changed = EditorGUI.EndChangeCheck();
            if (so.ApplyModifiedProperties() || changed)
            {
                selected.SyncToController();
                selected.Controller?.InvalidateLocalConfiguration();
                field = selected.settings;
                EditorUtility.SetDirty(selected);
                InvalidatePreview();
                Repaint();
            }
            EditorGUILayout.Space(6);
            DrawPreview();
        }

        private static void ApplyShapePresetDefaults(SerializedObject so, string prefix)
        {
            var preset = (MOAsteroidShapePreset)so.FindProperty(prefix + "shapePreset").enumValueIndex;
            float irregularity = .55f, roughness = .45f, crater = .55f, minAxis = .5f;
            int minCraters = 2, maxCraters = 12;
            switch (preset)
            {
                case MOAsteroidShapePreset.Rounded:
                    irregularity = .33f; roughness = .12f; crater = .30f;
                    minAxis = .76f; minCraters = 1; maxCraters = 5; break;
                case MOAsteroidShapePreset.Fractured:
                    irregularity = .76f; roughness = .28f; crater = .28f;
                    minAxis = .42f; minCraters = 0; maxCraters = 4; break;
                case MOAsteroidShapePreset.Elongated:
                    irregularity = .52f; roughness = .20f; crater = .25f;
                    minAxis = .38f; minCraters = 1; maxCraters = 5; break;
                case MOAsteroidShapePreset.ContactBinary:
                    irregularity = .63f; roughness = .23f; crater = .27f;
                    minAxis = .54f; minCraters = 0; maxCraters = 5; break;
                case MOAsteroidShapePreset.Cratered:
                    irregularity = .41f; roughness = .19f; crater = .72f;
                    minAxis = .65f; minCraters = 4; maxCraters = 9; break;
                case MOAsteroidShapePreset.Blocky:
                    irregularity = .82f; roughness = .25f; crater = .24f;
                    minAxis = .38f; minCraters = 0; maxCraters = 4; break;
            }
            so.FindProperty(prefix + "shapeIrregularity").floatValue = irregularity;
            so.FindProperty(prefix + "surfaceRoughness").floatValue = roughness;
            so.FindProperty(prefix + "craterIntensity").floatValue = crater;
            so.FindProperty(prefix + "minimumAxisScale").floatValue = minAxis;
            so.FindProperty(prefix + "minCraters").intValue = minCraters;
            so.FindProperty(prefix + "maxCraters").intValue = maxCraters;
        }

        private void DrawShape()
        {
            if (selected.contentType == MiningFieldContent.Gas)
            {
                Header("Gas cloud geometry");
                EditorGUILayout.HelpBox("Gas parameters are local to this Spawn Point and appear in LOCATION and VISUALS. There is no gas mining.", MessageType.None);
                return;
            }
            if (field == null || field.asteroidSettings == null)
            {
                EditorGUILayout.HelpBox("Assign Shared Field Settings with a resource catalog first.", MessageType.Warning);
                return;
            }
            selected.EnsureLocalSettings();
            var so = new SerializedObject(selected);
            so.Update();
            bool ice = selected.contentType == MiningFieldContent.Ice ||
                       selected.contentType == MiningFieldContent.IceShelf;
            string prefix = ice ? "iceGeology." : "rockGeology.";

            if (selected.contentType == MiningFieldContent.IceShelf)
            {
                Header("Continental shelf geology — this spawn");
                EditorGUILayout.HelpBox("IceShelf V7 does not use ordinary asteroid crater/axis settings. Adjust Plate Thickness, Surface Fractures, Collision Ridges and Rift Depth in LOCATION instead.", MessageType.None);
            }
            else
            {
                Header(ice ? "Ice geometry — this spawn" : "Rock geometry — this spawn");
                Prop(so, prefix + "shapePreset", "Shape preset");
                if (GUILayout.Button("Apply shape preset values", GUILayout.Height(22)))
                {
                    ApplyShapePresetDefaults(so, prefix);
                    GUI.FocusControl(null);
                }
                Prop(so, prefix + "shapeIrregularity", "Shape irregularity");
                Prop(so, prefix + "craterIntensity", "Crater depth");
                Prop(so, prefix + "surfaceRoughness", "Surface roughness");
                using (new EditorGUILayout.HorizontalScope())
                {
                    Prop(so, prefix + "minCraters", "Minimum craters");
                    Prop(so, prefix + "maxCraters", "Maximum craters");
                }
                Prop(so, prefix + "minimumAxisScale", "Axis variation");
            }

            Header("Baked editable asteroids — this spawn");
            Prop(so, "bakedLODCount", "Baked mesh LOD levels");
            Prop(so, "bakedMeshColliders", "Create mesh colliders");
            Prop(so, "bakedRebuildMode", "Rebake mode");
            bool isBaked = selected.Controller != null && selected.Controller.BakedSceneMode;
            EditorGUILayout.HelpBox(isBaked
                ? "BAKED: every mineable asteroid is a real GameObject with persistent Mesh assets. Runtime procedural rendering and decorative debris are OFF. Select Preserve Edited, Update Geometry or Rebuild Entire Field, then click BAKE / REBAKE below."
                : "Bake creates persistent editable scene GameObjects. Existing gameplay field records are used unless you choose Rebuild Entire Field. Decorative debris is never included.", MessageType.Info);

            Header("LOD & rendering — this spawn");
            if (isBaked)
                EditorGUILayout.HelpBox("The streaming controls below are used only by procedural mode. Baking uses the LOD levels above and does not render GPU far field.", MessageType.None);
            Prop(so, "quality.useGpuFarField", "GPU far rendering");
            Prop(so, "quality.exactFarShape", "Preserve exact silhouettes");
            Prop(so, "quality.exactFarSubdivisions", "Far detail (LOD)");
            Prop(so, "quality.streamedMeshSubdivisions", "Initial near detail");
            Prop(so, "quality.maximumStreamedSubdivisions", "Max near detail");
            Prop(so, "quality.maxDetailedAsteroids", "Max detailed asteroids");
            Prop(so, "quality.detailedDistanceMeters", "Detailed range (m)");
            Prop(so, "quality.spawnDistanceMeters", "Spawn distance (m)");
            Prop(so, "quality.despawnDistanceMeters", "Despawn distance (m)");
            Prop(so, "quality.distantDrawDistanceMeters", "Distant draw distance (m)");
            Prop(so, "quality.debrisDrawDistance", "Debris draw distance (m)");
            EditorGUILayout.HelpBox("The effective near range is the SMALLER of Detailed range and Spawn distance. Set Spawn distance >= Detailed range.", MessageType.None);
            advancedQuality = EditorGUILayout.Foldout(advancedQuality, "Advanced streaming budgets", true);
            if (advancedQuality)
            {
                Prop(so, "quality.fastStreamedAsteroids", "Fast streamed meshes");
                Prop(so, "quality.cachedStreamedAsteroids", "Cached asteroid meshes");
                Prop(so, "quality.exactFarBuildsPerFrame", "Far mesh builds per frame");
                Prop(so, "quality.farBuildBudgetMs", "Far build budget (ms)");
                Prop(so, "quality.maxFarRenderDraws", "Max far draws");
                Prop(so, "quality.maxActiveAsteroids", "Max active GameObjects");
                Prop(so, "quality.generatePerFrame", "Records generated per frame");
                Prop(so, "quality.instantiatePerFrame", "Instances spawned per tick");
                Prop(so, "quality.streamingTickSeconds", "Streaming interval (s)");
                Prop(so, "quality.meshColliders", "Mesh colliders");
                Prop(so, "quality.hideDepletedAsteroids", "Hide depleted asteroids");
            }
            if (so.ApplyModifiedProperties())
            {
                selected.Controller?.InvalidateLocalConfiguration();
                EditorUtility.SetDirty(selected);
                InvalidatePreview();
                Repaint();
            }
            EditorGUILayout.HelpBox("These values belong ONLY to the selected Spawn Point. Geology changes require rebuilding that field; LOD/streaming changes do not change saved asteroid mass or ore.", MessageType.Info);
        }

        private void DrawVisuals()
        {
            if (field == null)
            {
                EditorGUILayout.HelpBox("Assign Shared Field Settings to provide the common material assets.", MessageType.Warning);
                return;
            }
            selected.EnsureLocalSettings();
            MiningFieldContent content = selected.contentType;
            var local = new SerializedObject(selected);
            local.Update();
            if (content == MiningFieldContent.Gas)
            {
                Header("Gas cloud appearance — this spawn");
                Prop(local, "gasPresentation", "Rendering mode");
                var presentation = (MOGasPresentation)local.FindProperty("gasPresentation").enumValueIndex;
                bool volumeMode = presentation != MOGasPresentation.SoftParticles;
                if (volumeMode)
                {
                    Prop(local, "gasVolumeSteps", "Raymarch samples");
                    Prop(local, "gasVolumeNoiseScale", "3D noise scale");
                    Prop(local, "gasVolumeBrightness", "Brightness");
                    if (presentation == MOGasPresentation.GroundFog)
                    {
                        EditorGUILayout.Space(6f);
                        Header("Ground fog / absolute world height");
                        Prop(local, "groundFogFloorY", "Base world Y (m)");
                        Prop(local, "groundFogHeightMeters", "Height (m)");
                        Prop(local, "groundFogRadiusMultiplier", "Horizontal radius multiplier");
                        Prop(local, "groundFogTopSoftness", "Soft upper fade");
                        Prop(local, "groundFogLights", "Local fog lights (up to 4)");
                        EditorGUILayout.HelpBox("Visual mode only. Fog is flattened on WORLD Y, not Spawn Point Y. The original cloud records and gas materials are preserved.", MessageType.Info);
                    }
                }
                else
                {
                    Prop(local, "gasPuffsPerCloud", "Puffs per cluster");
                }
                Prop(local, "gasVisibilityDistanceMeters", "Visibility range (m)");
                Prop(local, "gasOpacity", "Opacity");
                Prop(local, "gasColorA", "Color A");
                Prop(local, "gasColorB", "Color B");
                Prop(local, "gasLayerContrast", "Layer irregularity");
                Prop(local, "gasDriftStrength", "Gentle drift");
                EditorGUILayout.HelpBox("Gas appearance is per Spawn Point and does not affect other clouds. Gas is not harvestable. Material overrides are assigned on the Spawn Point Inspector.", MessageType.None);
            }
            else
            {
                // Material authoring is intentionally NOT part of Field Studio.
                // Select the .mat asset in the Project/Spawn Point inspector to
                // choose a rock/ice library preset and edit shader layers.
                bool isBaked = selected.Controller != null && selected.Controller.BakedSceneMode;
                if (isBaked)
                {
                    Header("Runtime debris — baked field");
                    EditorGUILayout.HelpBox(
                        "Main asteroids remain editable scene GameObjects. Decorative fragments are " +
                        "GPU-instanced only in Play Mode; they are not baked, saved, mined or given colliders. " +
                        "Their positions are regenerated deterministically from the field snapshot.",
                        MessageType.Info);
                }
                else Header("Debris & motion — this spawn");
                Prop(local, "visuals.enableDecorativeDebris", "Decorative debris at runtime");
                if (local.FindProperty("visuals.enableDecorativeDebris").boolValue)
                {
                    Prop(local, "visuals.decorativeRockCount", "Debris count");
                    advancedDebris = EditorGUILayout.Foldout(advancedDebris, "Advanced debris", true);
                    if (advancedDebris)
                    {
                        Prop(local, "visuals.microFragmentCount", "Micro fragments");
                        Prop(local, "visuals.debrisSpreadMeters", "Debris spread (m)");
                        Prop(local, "visuals.fragmentMinDiameter", "Min fragment diameter (m)");
                        Prop(local, "visuals.fragmentMaxDiameter", "Max fragment diameter (m)");
                        Prop(local, "visuals.debrisMeshVariants", "Debris variants");
                    }
                }
                Prop(local, "visuals.enableAsteroidRotation",
                    isBaked ? "Animate debris spin" : "Asteroid rotation");
                if (local.FindProperty("visuals.enableAsteroidRotation").boolValue)
                {
                    Prop(local, "visuals.rotationSpeedMultiplier", "Master speed");
                    if (!isBaked)
                        Prop(local, "visuals.mineableRotationMultiplier", "Mineable speed");
                    Prop(local, "visuals.decorativeRotationMultiplier", "Debris speed");
                }
            }
            if (local.ApplyModifiedProperties())
            {
                selected.Controller?.InvalidateLocalConfiguration();
                EditorUtility.SetDirty(selected);
                if (content == MiningFieldContent.Gas && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // Heavy GameObject/mesh changes must not happen inside IMGUI layout.
                    // Queue a single Editor tick instead, preserving the saved gas layout.
                    if (!gasVisualRefreshQueued)
                    {
                        gasVisualRefreshQueued = true;
                        var anchorToRefresh = selected;
                        EditorApplication.delayCall += () =>
                        {
                            gasVisualRefreshQueued = false;
                            if (anchorToRefresh == null || EditorApplication.isPlayingOrWillChangePlaymode)
                                return;
                            var renderer = anchorToRefresh.GetComponent<MOGasFieldRenderer>();
                            if (renderer == null || renderer.Snapshot == null) return;
                            try
                            {
                                renderer.RebuildVisuals();
                                EditorUtility.SetDirty(renderer);
                                if (anchorToRefresh.gameObject.scene.IsValid())
                                    EditorSceneManager.MarkSceneDirty(anchorToRefresh.gameObject.scene);
                            }
                            catch (Exception ex) { Debug.LogException(ex, renderer); }
                        };
                    }
                }
                sample.Invalidate();
                Repaint();
            }
            // No material editor, material installer or material presets in Field Studio.
            // Material selection and presets live in the Material Inspector exclusively.
        }

        private void DrawPreview()
        {
            Header("Preview  ·  no saved gameplay changes");
            string currentSignature = CurrentPreviewSignature();
            if (previewSignature == null)
                EditorGUILayout.HelpBox("Preview not generated. Click Refresh Preview.", MessageType.None);
            else if (string.IsNullOrEmpty(currentSignature) || previewSignature != currentSignature)
                EditorGUILayout.HelpBox("Settings changed since preview. Click Refresh Preview to see the new formation.", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Preview", GUILayout.Width(145f), GUILayout.Height(25f))) RefreshPreview();
                GUILayout.FlexibleSpace();
                if (selected.contentType != MiningFieldContent.Gas)
                    showBySize = GUILayout.Toggle(showBySize, "By size", EditorStyles.miniButton);
                GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(34f));
                mapScale = GUILayout.HorizontalSlider(mapScale, .6f, 2.5f, GUILayout.Width(90f));
            }
            Rect map = GUILayoutUtility.GetRect(100f, 230f, GUILayout.ExpandWidth(true));
            DrawMap(map);
            if (selected.contentType == MiningFieldContent.Gas)
            {
                if (gasPreview != null) GUILayout.Label(gasPreview.clouds.Count + " gas clusters / " +
                    (selected.gasPresentation == MOGasPresentation.SoftParticles ? selected.gasCloudCount * selected.gasPuffsPerCloud + " visual puffs" : selected.gasPresentation.ToString()), EditorStyles.miniLabel);
            }
            else if (rocks.Count > 0)
                GUILayout.Label(rocks.Count + " planned asteroids / " + selected.ResolveCount() + " requested", EditorStyles.miniLabel);

            if (selected.contentType != MiningFieldContent.Gas)
            {
                Rect sampleRect = GUILayoutUtility.GetRect(130f, 144f, GUILayout.ExpandWidth(true));
                int sampleSeed = rocks.Count > 0 ? rocks[0].asteroidSeed : AsteroidIdUtility.DeriveSeed(selected.fieldSeed, 77);
                bool isShelf = selected.contentType == MiningFieldContent.IceShelf ||
                    selected.useOverrides && (selected.formation == AsteroidFormationType.IceShelfFragments ||
                        selected.formation == AsteroidFormationType.IceShelfContinents) ||
                    !selected.useOverrides && field != null && (field.formation == AsteroidFormationType.IceShelfFragments ||
                        field.formation == AsteroidFormationType.IceShelfContinents);
                sample.Draw(sampleRect, field, sampleSeed,
                    selected.contentType == MiningFieldContent.Ice || selected.contentType == MiningFieldContent.IceShelf || isShelf, isShelf,
                    selected.shelfThicknessRatio, selected.shelfFractureStrength,
                    selected.shelfCollisionStrength, selected.shelfRiftDepth,
                    selected.ActiveGeology,
                    selected.visuals.unifiedAsteroidMaterialOverride != null
                        ? selected.visuals.unifiedAsteroidMaterialOverride :
                    (field != null && field.unifiedAsteroidMaterial != null ? field.unifiedAsteroidMaterial :
                    (selected.contentType == MiningFieldContent.Ice || selected.contentType == MiningFieldContent.IceShelf
                        ? selected.visuals.iceMaterialOverride : selected.visuals.rockMaterialOverride)));
                EditorGUILayout.LabelField("Real geometry preview · drag to rotate", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawMap(Rect rect)
        {
            EditorGUI.DrawRect(rect, panel);
            if (Event.current.type != EventType.Repaint) return;
            float extent = Mathf.Max(200f, selected.radiusMeters +
                (selected.contentType == MiningFieldContent.Gas ? selected.gasCloudRadiusMeters : 0f));
            if (selected.contentType == MiningFieldContent.Gas)
            {
                if (gasPreview == null) { GUI.Label(new Rect(rect.x+12f,rect.y+12f,rect.width-24f,24f), "Refresh to preview gas clusters", EditorStyles.whiteMiniLabel); return; }
                Handles.BeginGUI();
                foreach (var cloud in gasPreview.clouds)
                {
                    Vector2 point = Project(rect, cloud.localCenter, extent);
                    Color tint = Color.Lerp(selected.gasColorA, selected.gasColorB,
                        (Mathf.Abs(cloud.seed % 997) / 997f));
                    Handles.color = new Color(tint.r, tint.g, tint.b, 0.18f);
                    float pixelRadius = Mathf.Clamp(cloud.radiusMeters / extent * rect.height * .44f * mapScale, 4f, 70f);
                    Handles.DrawSolidDisc(point, Vector3.forward, pixelRadius);
                    Handles.color = new Color(tint.r, tint.g, tint.b, 0.78f);
                    Handles.DrawWireDisc(point, Vector3.forward, pixelRadius);
                }
                Handles.EndGUI();
            }
            else
            {
                if (rocks.Count == 0) { GUI.Label(new Rect(rect.x+12f,rect.y+12f,rect.width-24f,24f), "Refresh to preview rock positions", EditorStyles.whiteMiniLabel); return; }
                foreach (var point in rocks)
                {
                    Vector2 xy = Project(rect, point.localPosition, extent);
                    if (!rect.Contains(xy)) continue;
                    float r = showBySize ? Mathf.Clamp(point.nominalDiameter / extent * rect.height * mapScale * .4f, 2f, 12f) : 3f;
                    Color c = selected.contentType == MiningFieldContent.IceShelf ? new Color(.84f,.95f,1f) :
                        selected.contentType == MiningFieldContent.Ice ? new Color(.68f,.88f,1f) : new Color(.74f,.70f,.62f);
                    EditorGUI.DrawRect(new Rect(xy.x-r, xy.y-r, r*2f, r*2f), c);
                }
            }
            GUI.Label(new Rect(rect.x+9f,rect.y+8f,rect.width-18f,22f), "TOP VIEW  /  X–Z", EditorStyles.whiteMiniLabel);
        }

        private Vector2 Project(Rect rect, Vector3 position, float extent)
        {
            float scale = Mathf.Min(rect.width, rect.height) * .45f * mapScale / extent;
            return new Vector2(rect.center.x + position.x * scale, rect.center.y - position.z * scale);
        }
        private void RefreshPreview()
        {
            lastError = null;
            rocks.Clear();
            gasPreview = null;
            try
            {
                string validation = AsteroidAuthoringWorkflow.Validate(selected);
                if (validation != null) throw new InvalidOperationException(validation);
                if (selected.contentType == MiningFieldContent.Gas)
                    gasPreview = MOGasFieldLayout.Plan(selected);
                else
                {
                    rocks.AddRange(AsteroidFieldLayout.Plan(field, selected.fieldSeed,
                        selected.fieldId, selected.BuildOptions(field)));
                }
                previewSignature = CurrentPreviewSignature();
            }
            catch (Exception ex) { lastError = ex.Message; }
            Repaint();
        }

        private string CurrentPreviewSignature()
        {
            if (selected == null) return string.Empty;
            return selected.contentType == MiningFieldContent.Gas ?
                MOGasFieldLayout.BuildSignature(selected) : AsteroidAuthoringWorkflow.Signature(selected);
        }

        private void InvalidatePreview()
        {
            previewSignature = null;
            rocks.Clear();
            gasPreview = null;
            sample.Invalidate();
        }

        private void DrawFooter()
        {
            if (selected == null) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string status;
                if (selected.contentType == MiningFieldContent.Gas)
                {
                    var cloud = selected.GetComponent<MOGasFieldRenderer>();
                    if (cloud == null || cloud.Snapshot == null || cloud.CloudCount == 0)
                        status = "GAS VISUALS NOT BUILT";
                    else if (string.IsNullOrEmpty(cloud.Snapshot.authoringSignature))
                        status = "GAS VISUALS  ·  LEGACY LAYOUT";
                    else if (cloud.Snapshot.authoringSignature != MOGasFieldLayout.BuildSignature(selected))
                        status = "GAS LAYOUT CHANGED";
                    else status = "GAS VISUALS READY  ·  " + cloud.CloudCount + " clusters";
                }
                else
                {
                    var controller = selected.Controller;
                    bool built = controller != null && controller.Snapshot != null;
                    status = controller != null && controller.BakedSceneMode
                        ? "BAKED · " + (controller.Snapshot != null && controller.Snapshot.asteroids != null
                            ? controller.Snapshot.asteroids.Count.ToString() : "0") + " records"
                        : !built ? "NOT BUILT" :
                            AsteroidAuthoringWorkflow.IsOutdated(selected) ? "CHANGES NOT BUILT" : "UP TO DATE";
                }
                GUILayout.Label(status, EditorStyles.miniLabel);
                if (selected.contentType != MiningFieldContent.Gas)
                {
                    GUILayout.Space(5f);
                    EditorGUI.BeginChangeCheck();
                    var mode = (MOBakeRebuildMode)EditorGUILayout.EnumPopup(
                        selected.bakedRebuildMode, GUILayout.Width(130f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(selected, "Change asteroid bake mode");
                        selected.bakedRebuildMode = mode;
                        EditorUtility.SetDirty(selected);
                        EditorSceneManager.MarkSceneDirty(selected.gameObject.scene);
                    }
                    if (GUILayout.Button("BAKE / REBAKE", EditorStyles.toolbarButton, GUILayout.Width(125f)))
                    {
                        var toBake = selected;
                        EditorApplication.delayCall += () =>
                        {
                            if (toBake != null) MOBakedFieldBaker.BakeSelected(toBake);
                        };
                    }
                }
                GUILayout.FlexibleSpace();
                if (AsteroidAuthoringWorkflow.IsBuilding)
                {
                    GUILayout.Label(Mathf.RoundToInt(AsteroidAuthoringWorkflow.Progress * 100f) + "%");
                    if (GUILayout.Button("Cancel", EditorStyles.toolbarButton)) AsteroidAuthoringWorkflow.CancelBuild();
                }
                else using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    using (new EditorGUI.DisabledScope(selected.contentType != MiningFieldContent.Gas &&
                        selected.Controller != null && selected.Controller.BakedSceneMode))
                    {
                        if (GUILayout.Button(selected.contentType == MiningFieldContent.Gas ?
                            "BUILD CLOUD VISUALS" : "BUILD RECORDS",
                            EditorStyles.toolbarButton, GUILayout.Width(125f))) BuildSelected();
                    }
                }
            }
        }

        private void BuildSelected()
        {
            if (selected == null) return;
            if (selected.contentType != MiningFieldContent.Gas)
            {
                AsteroidAuthoringWorkflow.BeginBuild(selected);
                return;
            }
            string invalid = AsteroidAuthoringWorkflow.Validate(selected);
            if (invalid != null) { lastError = invalid; return; }
            if (field == null) { lastError = "Field Settings are required for the gas material."; return; }
            if (field.gasParticleMaterial == null ||
                (selected.gasPresentation == MOGasPresentation.Volumetric && field.gasVolumeMaterial == null) ||
                (selected.gasPresentation == MOGasPresentation.GroundFog && field.groundFogMaterial == null))
            {
                if (!MOVisualAssetInstaller.Install(field)) return;
            }
            var renderer = selected.GetComponent<MOGasFieldRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<MOGasFieldRenderer>(selected.gameObject);
            if (renderer.Snapshot != null && !EditorUtility.DisplayDialog("Replace gas visuals?",
                "Only the procedural cloud layout will be rebuilt. No resource inventory or mined material is affected.",
                "Rebuild", "Cancel")) return;
            Undo.RecordObject(renderer, "Generate gas visuals");
            renderer.BuildRecords();
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(selected.gameObject.scene);
            RefreshPreview();
        }

        private void CreatePoint()
        {
            if (field == null || !AsteroidAuthoringWorkflow.IsReady(field))
            {
                // Prefer an existing loaded settings asset over duplicating persistent assets.
                var guid = AssetDatabase.FindAssets("t:AsteroidFieldSettings", new[] { "Assets/_MiningOperation" });
                AsteroidFieldSettings candidate = null;
                foreach (string id in guid)
                {
                    var found = AssetDatabase.LoadAssetAtPath<AsteroidFieldSettings>(AssetDatabase.GUIDToAssetPath(id));
                    if (!AsteroidAuthoringWorkflow.IsReady(found)) continue;
                    if (candidate != null) { candidate = null; break; } // ambiguous: do not guess
                    candidate = found;
                }
                if (candidate == null)
                {
                    EditorUtility.DisplayDialog("Select shared settings",
                        "Assign an existing, working Field Settings asset on a Spawn Point first. " +
                        "If assets show Script: None, repair them before creating new configurations.", "OK");
                    return;
                }
                field = candidate;
            }
            if (newKind == MiningFieldContent.Ice || newKind == MiningFieldContent.IceShelf ||
                newKind == MiningFieldContent.Gas)
            {
                try
                {
                    if (newKind == MiningFieldContent.Gas) MOVisualAssetInstaller.Install(field);
                    else
                    {
                        MOVisualAssetInstaller.InstallIceResources(field);
                        MOUnifiedAsteroidAssetTools.EnsureShared(field);
                    }
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("Material setup failed", ex.Message, "OK");
                    return;
                }
            }
            AsteroidFieldSpawnPoint anchor = AsteroidAuthoringWorkflow.CreateSpawnPoint(field);
            Undo.RecordObject(anchor, "Set new field type");
            anchor.contentType = newKind;
            if (newKind == MiningFieldContent.IceShelf) anchor.formation = AsteroidFormationType.IceShelfContinents;
            if (newKind == MiningFieldContent.Gas)
                anchor.gasPresentation = MOGasPresentation.Volumetric;
            anchor.gameObject.name = newKind + " Spawn Point";
            anchor.SyncToController();
            EditorUtility.SetDirty(anchor);
            SelectAnchor(anchor);
        }
        private void CreateSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AsteroidAuthoringWorkflow.FindExistingSettingsAssets(out string existing))
            {
                EditorUtility.DisplayDialog("Existing settings found",
                    "Cannot create a second setup while these assets exist: " + existing +
                    ". Repair and reuse the original assets instead.", "OK");
                return;
            }
            try { field = AsteroidAuthoringWorkflow.CreateReadyAssets(newKind); }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Setup cancelled", ex.Message, "OK");
                return;
            }
            if (newKind == MiningFieldContent.Ice || newKind == MiningFieldContent.IceShelf ||
                newKind == MiningFieldContent.Gas)
            {
                try
                {
                    if (newKind == MiningFieldContent.Gas) MOVisualAssetInstaller.Install(field);
                    else
                    {
                        MOVisualAssetInstaller.InstallIceResources(field);
                        MOUnifiedAsteroidAssetTools.EnsureShared(field);
                    }
                }
                catch (Exception ex)
                {
                    // The core assets have already been saved. Do not leave an orphaned
                    // first-time setup without a Spawn Point if optional visuals fail.
                    Debug.LogWarning("[MO] Optional ice/gas material setup incomplete: " + ex.Message);
                }
            }
            // When an empty Spawn Point already exists, attach the first-time assets
            // to that anchor instead of creating a duplicate location.
            if (selected != null && selected.settings == null)
            {
                Undo.RecordObject(selected, "Assign initial field settings");
                selected.settings = field;
                selected.SyncToController();
                EditorUtility.SetDirty(selected);
                EditorSceneManager.MarkSceneDirty(selected.gameObject.scene);
                SelectAnchor(selected);
                return;
            }
            var anchor = AsteroidAuthoringWorkflow.CreateSpawnPoint(field);
            anchor.contentType = newKind;
            if (newKind == MiningFieldContent.IceShelf) anchor.formation = AsteroidFormationType.IceShelfContinents;
            if (newKind == MiningFieldContent.Gas)
                anchor.gasPresentation = MOGasPresentation.Volumetric;
            anchor.gameObject.name = newKind + " Spawn Point";
            anchor.SyncToController();
            SelectAnchor(anchor);
        }
        private static void Header(string title)
        {
            EditorGUILayout.Space(5f);
            GUILayout.Label(title.ToUpperInvariant(), EditorStyles.boldLabel);
        }
        private static void Prop(SerializedObject target, string name, string title)
        {
            var property = target.FindProperty(name);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(title));
        }
    }
}
