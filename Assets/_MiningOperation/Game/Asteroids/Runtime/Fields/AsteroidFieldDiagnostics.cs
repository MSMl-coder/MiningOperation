using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>Temporary Play Mode diagnostics. Not a production gameplay UI.</summary>
    [DisallowMultipleComponent]
    public sealed class AsteroidFieldDiagnostics : MonoBehaviour
    {
        public AsteroidFieldController field;
        public Transform viewer;
        [SerializeField] private bool show = true;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private string report;
        private float nextUpdate;

        private void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.25f;
            var controllers = FindObjectsByType<AsteroidFieldController>(FindObjectsSortMode.None);
            if (controllers.Length == 0) { report = "No AsteroidFieldController"; return; }
            int total = 0, n1 = 0, n2 = 0, n3 = 0, detailed = 0, cached = 0, far = 0, decor = 0, ready = 0, building = 0;
            float nearest = float.MaxValue, nearLimit = 0f;
            foreach (var item in controllers)
            {
                if (item == null) continue;
                if (item.IsBuilding) building++;
                if (item.NearestDistanceMeters >= 0f && item.NearestDistanceMeters < nearest)
                { nearest = item.NearestDistanceMeters; nearLimit = item.DetailedRangeMeters; }
                detailed += item.ActiveCount;
                cached += item.CachedAsteroidCount;
                far += item.FarRenderedCount;
                decor += item.DecorativeRenderedCount;
                if (item.FarGpuReady) ready++;
                var snapshot = item.Snapshot;
                if (snapshot == null || snapshot.asteroids == null) continue;
                total += snapshot.asteroids.Count;
                foreach (var rock in snapshot.asteroids)
                {
                    if (rock?.asteroid == null) continue;
                    switch ((AsteroidClass)rock.asteroid.asteroidClass)
                    {
                        case AsteroidClass.I: n1++; break;
                        case AsteroidClass.II: n2++; break;
                        case AsteroidClass.III: n3++; break;
                    }
                }
            }
            bool exact = false;
            foreach (var item in controllers)
                if (item != null && item.Settings != null && item.Settings.exactFarShape) exact = true;
            report = $"Fields: {controllers.Length} | Building: {building}\n" +
                $"Total asteroids: {total} | Classes: {n1}/{n2}/{n3}\n" +
                $"Detailed GameObjects: {detailed} | Cached: {cached}\n" +
                $"Far {(exact ? "unique meshes / draws" : "instances")}: {far} ({ready} renderers ON)\n" +
                $"Decorative fragments: {decor} (not mineable)\n" +
                $"Nearest: {(nearest < float.MaxValue ? nearest.ToString("F0") + " m" : "not checked")} / detailed range: {nearLimit:F0} m\n" +
                $"FPS: {(Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f):F0}";
        }

        private void OnGUI()
        {
            if (!show) return;
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }
            };
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, wordWrap = true, normal = { textColor = new Color(0.78f,0.86f,0.88f) }
            };
            GUILayout.BeginArea(new Rect(14, 14, 380, 204), GUI.skin.box);
            GUILayout.Label("MINING OPERATION / TEST ENVIRONMENT", titleStyle);
            GUILayout.Label(report ?? "Preparing asteroid field...", labelStyle);
            GUILayout.EndArea();
        }
    }
}
