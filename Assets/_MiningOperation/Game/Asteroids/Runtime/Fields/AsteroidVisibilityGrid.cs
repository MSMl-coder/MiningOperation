using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Spatial index for visual instances only. Never touches persistent asteroid IDs or saves.
    /// Bounds include the entire visual object. World bounds are conservative under rotation/scale.
    /// </summary>
    internal sealed class AsteroidVisibilityGrid
    {
        private sealed class Cell
        {
            public readonly List<int> indices = new List<int>();
            public Vector3 min;
            public Vector3 max;
            public bool hasBounds;
        }

        private readonly float cellSize;
        private readonly Dictionary<Vector3Int, Cell> cells = new Dictionary<Vector3Int, Cell>();
        public int CellCount => cells.Count;

        public AsteroidVisibilityGrid(float size) { cellSize = Mathf.Max(25f, size); }

        public void Add(int index, Vector3 localPosition, float objectRadius)
        {
            var key = new Vector3Int(Mathf.FloorToInt(localPosition.x / cellSize),
                Mathf.FloorToInt(localPosition.y / cellSize), Mathf.FloorToInt(localPosition.z / cellSize));
            if (!cells.TryGetValue(key, out var cell))
            {
                cell = new Cell();
                cells.Add(key, cell);
            }
            cell.indices.Add(index);
            Vector3 padding = Vector3.one * Mathf.Max(0f, objectRadius);
            Vector3 lower = localPosition - padding;
            Vector3 upper = localPosition + padding;
            if (!cell.hasBounds) { cell.min = lower; cell.max = upper; cell.hasBounds = true; }
            else { cell.min = Vector3.Min(cell.min, lower); cell.max = Vector3.Max(cell.max, upper); }
        }

        public void CollectVisible(Transform root, Vector3 viewer, Camera camera,
            float drawDistance, List<int> output)
        {
            output.Clear();
            if (root == null) return;
            Plane[] planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            Vector3 scale = root.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            foreach (var entry in cells)
            {
                Cell cell = entry.Value;
                Vector3 localCenter = (cell.min + cell.max) * 0.5f;
                float worldRadius = (cell.max - localCenter).magnitude * maxScale;
                Vector3 worldCenter = root.TransformPoint(localCenter);
                float reach = drawDistance + worldRadius;
                if ((viewer - worldCenter).sqrMagnitude > reach * reach) continue;
                if (planes != null && !GeometryUtility.TestPlanesAABB(planes,
                    new Bounds(worldCenter, Vector3.one * (worldRadius * 2f)))) continue;
                output.AddRange(cell.indices);
            }
        }
    }
}
