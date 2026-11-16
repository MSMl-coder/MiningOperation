using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MiningOperation
{
    // Put this on the root of the NPC station ALREADY present in your scene.
    // The component never replaces, destroys, or repositions the artist's mesh.
    [DisallowMultipleComponent]
    public sealed class MOSceneStationAnchor : MonoBehaviour
    {
        [Tooltip("Persistent world station ID; keep unchanged across saves.")]
        public string stationId = "station.public";
        public string displayName = "Public Outpost";
        public string ownerId = "npc.public";
        public string marketRegionId = "region.alpha";
    }

    // Optional: mark existing asteroid meshes instead of creating spheres over them.
    [DisallowMultipleComponent]
    public sealed class MOSceneAsteroidAnchor : MonoBehaviour
    {
        [Tooltip("Persistent ID unique across ALL game scenes.")]
        public string asteroidId = "";
        public string displayName = "Asteroid";
        public string oreItemId = "ore.iron_nickel";
        public double startingOreKg = 800;
    }

    // Configures a world-scene without touching any of the existing camera, lights,
    // asteroid field, or outpost artwork. NOT a standalone generated-demo scene.
    [DefaultExecutionOrder(-550)]
    [DisallowMultipleComponent]
    public sealed class MOSceneCoordinator : MonoBehaviour
    {
        [Header("Optional existing player ship model")]
        [Tooltip("If left empty a small placeholder ship is created only at runtime.")]
        public Transform playerShipTransform;
        [Tooltip("Check if another script already drives the player ship.")]
        public bool externalShipController = true;
        public bool createShipPlaceholder = true;
        [Header("Scene simulation")]
        public bool loadPreviousSaveOnPlay = true;
        public bool saveOnSceneExit = true;
        public float simulationSpeed = 1f;
        public WorldRuntime Game { get; private set; }
        public WorldSaveService Saves { get; private set; }
        public string SceneKey { get; private set; }
        public string CurrentStationId { get; private set; }
        public string LastError { get; private set; } = "";
        public event Action WorldChanged;
        private bool initialized;
        private float autosaveTimer;
        private Transform placeholder;
        private readonly List<GameObject> ownedVisuals = new List<GameObject>();

        public void Awake()
        {
            var scene = gameObject.scene;
            SceneKey = !string.IsNullOrEmpty(scene.path) ? scene.path : scene.name;
            var duplicates = FindObjectsByType<MOSceneCoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(x => x.gameObject.scene == scene);
            if (duplicates != 1)
            { Debug.LogError("[MO] Only one MOSceneCoordinator is allowed in scene " + SceneKey); enabled = false; return; }
            Saves = WorldSaveService.Default();
            Reload(loadPreviousSaveOnPlay);
            initialized = true;
        }
        public void Reload(bool trySave)
        {
            ClearOwnedVisuals();
            try
            {
                Game = trySave && System.IO.File.Exists(Saves.PathOnDisk) ? Saves.Load() : WorldRuntime.NewGame();
            }
            catch (Exception ex)
            {
                // Never overwrite an unreadable save: the user should decide whether to reset.
                Debug.LogError("[MO] Save load failed. No implicit reset to protect your data: " + ex);
                Game = null; LastError = "Save load failed: " + ex.Message; return;
            }
            BindScene();
            WorldChanged?.Invoke();
        }
        public void NewWorld()
        {
            ClearOwnedVisuals();
            Game = WorldRuntime.NewGame();
            LastError = "New unsaved session";
            BindScene();
            WorldChanged?.Invoke();
        }
        public void LoadWorld() { Reload(true); }
        public void SaveWorld()
        {
            WorldUtil.Require(Game != null, "No active session");
            Game.Validate(); Saves.Save(Game); LastError = "Saved";
        }
        public void SetError(string message) { LastError = message ?? ""; }
        private void BindScene()
        {
            if (Game == null) return;
            var anchors = FindObjectsByType<MOSceneStationAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(x => x.gameObject.scene == gameObject.scene).ToArray();
            WorldUtil.Require(anchors.Length <= 1, "Only ONE NPC station may exist in one scene. Found " + anchors.Length);
            CurrentStationId = anchors.Length == 1 ? anchors[0].stationId : null;
            if (anchors.Length == 0)
                Debug.LogWarning("[MO] No station anchor in " + SceneKey + ". Select your outpost root and run Mining Operation > Bind Selected Outpost.");
            if (anchors.Length == 1)
            {
                var anchor = anchors[0];
                WorldUtil.Require(!string.IsNullOrWhiteSpace(anchor.stationId), "Station anchor ID must not be empty");
                var st = Game.State.stations.FirstOrDefault(x => x.id == anchor.stationId);
                if (st == null)
                {
                    // When opening a new sector, register its NPC station in the persistent world.
                    var owner = string.IsNullOrWhiteSpace(anchor.ownerId) ? "npc." + anchor.stationId : anchor.ownerId;
                    if (!Game.State.wallets.Any(w => w.ownerId == owner))
                        Game.State.wallets.Add(new WalletState { ownerId = owner, balance = 200000 });
                    st = new StationState { id = anchor.stationId, displayName = anchor.displayName,
                        ownerId = owner, kind = "public", regionId = anchor.marketRegionId,
                        moduleSlots = 20, powerLimit = 120 };
                    st.modules.Add(new StationModuleState { definitionId = "refinery" });
                    st.modules.Add(new StationModuleState { definitionId = "fabricator" });
                    Game.State.stations.Add(st);
                    st.storageId = Game.Inventory.Warehouse(st.ownerId, st.id).id;
                }
                WorldUtil.Require(st.kind == "public", "The scene NPC anchor must reference a public station");
                WorldUtil.Require(string.IsNullOrEmpty(st.sceneId) || st.sceneId==SceneKey ||
                    !st.sceneId.StartsWith("Assets/", StringComparison.Ordinal),
                    "Station " + st.id + " is already bound to another authored scene: " + st.sceneId);
                st.sceneId = SceneKey;
                st.x = anchor.transform.position.x; st.y = anchor.transform.position.y; st.z = anchor.transform.position.z;
                if (!string.IsNullOrWhiteSpace(anchor.displayName)) st.displayName = anchor.displayName;
                if (string.IsNullOrWhiteSpace(st.regionId)) st.regionId = "region.alpha";
                var stationedShip=Game.State.ships.FirstOrDefault(x=>x.id==Game.State.controlledShipId);
                if(stationedShip != null && stationedShip.dockedAtStationId==st.id)
                {
                    stationedShip.x=st.x;stationedShip.y=st.y;stationedShip.z=st.z;
                    stationedShip.sceneId=SceneKey;
                }
            }
            foreach (var marker in FindObjectsByType<MOSceneAsteroidAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(x => x.gameObject.scene == gameObject.scene))
            {
                WorldUtil.Require(!string.IsNullOrWhiteSpace(marker.asteroidId), "Asteroid anchor requires a stable ID: " + marker.name);
                Game.Catalog.Item(marker.oreItemId);
                var a = Game.State.asteroids.FirstOrDefault(x => x.id == marker.asteroidId);
                if (a == null)
                {
                    a = new AsteroidState { id = marker.asteroidId, displayName = marker.displayName,
                        oreItemId = marker.oreItemId, remainingKg = Math.Max(0, marker.startingOreKg) };
                    Game.State.asteroids.Add(a);
                }
                WorldUtil.Require(string.IsNullOrEmpty(a.sceneId) || a.sceneId==SceneKey ||
                    !a.sceneId.StartsWith("Assets/", StringComparison.Ordinal),
                    "Asteroid ID used in another scene: " + marker.asteroidId);
                a.sceneId = SceneKey; a.x = marker.transform.position.x;
                a.y = marker.transform.position.y; a.z = marker.transform.position.z;
            }
            var ship = Game.Ships.Get(Game.State.controlledShipId);
            // If this scene is a travel destination, arrive at its outpost without overwriting other sectors.
            if (Game.State.pendingArrivalScene == gameObject.scene.name)
            {
                WorldUtil.Require(CurrentStationId != null, "Destination scene must contain a station anchor");
                var st = Game.Stations.Get(CurrentStationId);
                ship.sceneId = SceneKey; ship.dockedAtStationId = null; ship.dockingTargetId = null;
                ship.x = st.x + 65; ship.y = st.y; ship.z = st.z + 35;
                Game.State.pendingArrivalScene = null;
            }
            else if (ship.sceneId == "starter" || string.IsNullOrEmpty(ship.sceneId))
            {
                // Old saves did not contain scene IDs. Bind only the starter ship.
                if (Game.State.stations.First(x => x.id == "station.public").sceneId == SceneKey)
                    ship.sceneId = SceneKey;
            }
            if (playerShipTransform != null)
            {
                if (!string.IsNullOrEmpty(ship.dockedAtStationId))
                { /* Player artist model can remain at its chosen parking location. */ }
                else if (ship.sceneId == SceneKey) playerShipTransform.position = new Vector3((float)ship.x, (float)ship.y, (float)ship.z);
            }
            else if (createShipPlaceholder && ship.sceneId == SceneKey)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                obj.name = "MO Player Ship [PLACEHOLDER - runtime only]";
                obj.transform.localScale = new Vector3(3, 2.2f, 7);
                obj.transform.position = new Vector3((float)ship.x, (float)ship.y, (float)ship.z);
                var renderer = obj.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = new Color(.22f, .53f, .66f);
                ownedVisuals.Add(obj); placeholder = obj.transform;
            }
            Game.Validate();
        }
        private void ClearOwnedVisuals()
        {
            foreach (var go in ownedVisuals) if (go != null) Destroy(go);
            ownedVisuals.Clear(); placeholder = null;
        }
        public void Advance(float seconds)
        {
            if (Game == null) return;
            Game.Advance(seconds); WorldChanged?.Invoke();
        }
        private void Update()
        {
            if (Game == null || !enabled) return;
            var ship = Game.Ships.Get(Game.State.controlledShipId);
            if (ship.sceneId == SceneKey)
            {
                var v = playerShipTransform != null ? playerShipTransform : placeholder;
                if (v != null)
                {
                    if (string.IsNullOrEmpty(ship.dockedAtStationId) && string.IsNullOrEmpty(ship.dockingTargetId))
                    {
                        if (playerShipTransform != null && externalShipController)
                        { ship.x = v.position.x; ship.y = v.position.y; ship.z = v.position.z; }
                        else if (placeholder != null) MovePlaceholder(ship);
                    }
                    else if (!string.IsNullOrEmpty(ship.dockedAtStationId))
                    {
                        v.position = new Vector3((float)ship.x, (float)ship.y, (float)ship.z);
                    }
                }
            }
            try { Game.Advance(Math.Min(0.1f, Time.deltaTime) * Mathf.Max(0, simulationSpeed)); }
            catch(Exception ex) { simulationSpeed=0; LastError=ex.Message; Debug.LogException(ex); }
            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer > 180 && saveOnSceneExit)
            {
                autosaveTimer = 0;
                try { SaveWorld(); } catch (Exception ex) { Debug.LogError("[MO] Autosave: " + ex.Message); }
            }
        }
        private void MovePlaceholder(ShipState ship)
        {
            // While the terminal is open, keyboard input belongs to the UI.
            var terminal=GetComponent<MOStationTerminal>();
            if(terminal != null && terminal.IsOpen) return;
            Vector3 direction = Vector3.zero;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) direction.z++;
                if (keyboard.sKey.isPressed) direction.z--;
                if (keyboard.aKey.isPressed) direction.x--;
                if (keyboard.dKey.isPressed) direction.x++;
                if (keyboard.eKey.isPressed) direction.y++;
                if (keyboard.qKey.isPressed) direction.y--;
            }
#endif
            var delta = direction.normalized * (Mathf.Min(Time.deltaTime,.1f) * 45);
            placeholder.position += delta;
            ship.x = placeholder.position.x;ship.y = placeholder.position.y;ship.z = placeholder.position.z;
        }
        public void Undock()
        {
            var s = Game.Ships.Get(Game.State.controlledShipId);
            Game.Docking.Undock(s.id);
            var v = playerShipTransform != null ? playerShipTransform : placeholder;
            if (v != null) v.position = new Vector3((float)s.x,(float)s.y,(float)s.z);
        }
        public void RequestTravel(string sceneName)
        {
            WorldUtil.Require(Game != null, "No world");
            WorldUtil.Require(!string.IsNullOrWhiteSpace(sceneName), "Enter destination scene name");
            WorldUtil.Require(Application.CanStreamedLevelBeLoaded(sceneName), "Destination missing from Build Profiles: " + sceneName);
            var ship = Game.Ships.Get(Game.State.controlledShipId);
            WorldUtil.Require(string.IsNullOrEmpty(ship.dockedAtStationId) && string.IsNullOrEmpty(ship.dockingTargetId), "Undock before traveling");
            Game.State.pendingArrivalScene = sceneName;
            SaveWorld();
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        private void OnDestroy()
        {
            if (!initialized || !saveOnSceneExit || Game == null) return;
            // Avoid silent overwrite if save was already moved to a new scene or failed to load.
            try { SaveWorld(); } catch(Exception ex) { Debug.LogError("[MO] Scene-exit save failed: " + ex.Message); }
        }
    }
}
