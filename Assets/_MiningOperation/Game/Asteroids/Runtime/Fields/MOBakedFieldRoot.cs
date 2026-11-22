using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>Marker for the one dedicated generated root per asteroid field.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class MOBakedFieldRoot : MonoBehaviour
    {
        [SerializeField] private AsteroidFieldController owner;
        [SerializeField] private string fieldId;
        [SerializeField] private string assetFolder;
        [NonSerialized] private Dictionary<string, MOBakedAsteroid> index;
        public AsteroidFieldController Owner => owner;
        public string FieldId => fieldId;
        public string AssetFolder => assetFolder;

        public void Configure(AsteroidFieldController controller, string folder)
        {
            owner = controller;
            fieldId = controller != null ? controller.FieldId : string.Empty;
            assetFolder = folder;
            index = null;
        }

        private void OnEnable() { index = null; }

        public void Reindex()
        {
            index = new Dictionary<string, MOBakedAsteroid>(StringComparer.Ordinal);
            foreach (var body in GetComponentsInChildren<MOBakedAsteroid>(true))
                if (body != null && !string.IsNullOrEmpty(body.AsteroidId) && !index.ContainsKey(body.AsteroidId))
                    index.Add(body.AsteroidId, body);
        }

        public MOBakedAsteroid FindAsteroid(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (index == null) Reindex();
            index.TryGetValue(id, out var body);
            return body;
        }
    }

}
