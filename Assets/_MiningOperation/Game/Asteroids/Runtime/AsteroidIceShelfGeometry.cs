using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// V6/V7 ice-shelf geometry. Generates large closed extruded plates with hard cliff
    /// edges, broad surface uplift, long rifts and collision ridges. The same seed and
    /// saved profile reconstruct the same contour at every LOD.
    /// </summary>
    internal static class AsteroidIceShelfGeometry
    {
        private struct Band
        {
            public Vector2 normal;
            public Vector2 tangent;
            public float offset;
            public float width;
            public float phase;
            public float amplitude;
        }

        public static Mesh Generate(int seed, float diameter, AsteroidGeometryProfile profile,
            int subdivision, bool visualColors)
        {
            if (profile == null || !profile.iceShelf || diameter <= 0f ||
                float.IsNaN(diameter) || float.IsInfinity(diameter))
                throw new ArgumentException("Invalid ice-shelf geometry recipe.");

            bool continental = profile.continentalShelf;
            subdivision = Mathf.Clamp(subdivision, 0, 6);
            int sectors = continental ? Mathf.Clamp(16 << subdivision, 48, 256) : Mathf.Clamp(12 << subdivision, 32, 192);
            int rings = continental ? Mathf.Clamp(6 << subdivision, 6, 160) : Mathf.Clamp(4 << subdivision, 4, 128);
            float halfDiameter = diameter * 0.5f;
            float halfThickness = diameter * Mathf.Clamp(profile.shelfThicknessRatio, .025f, .28f) * .5f;
            float fracture = Mathf.Clamp01(profile.shelfFractureStrength);
            float collision = Mathf.Clamp01(profile.shelfCollisionStrength);
            float riftDepth = Mathf.Clamp01(profile.shelfRiftDepth);
            var rand = new AsteroidRandom(unchecked((uint)seed) ^ 0xC3E21A49u);
            var phase = new float[8];
            for (int i = 0; i < phase.Length; i++) phase[i] = rand.Range(0f, Mathf.PI * 2f);

            int riftCount = continental ? 4 + rand.Range(0, 4) : 2 + rand.Range(0, 3);
            var rifts = new Band[riftCount];
            for (int i = 0; i < riftCount; i++)
            {
                float angle = rand.Range(0f, Mathf.PI * 2f);
                rifts[i].normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                rifts[i].tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                rifts[i].offset = rand.Range(continental ? -.28f : -.37f, continental ? .28f : .37f);
                rifts[i].width = rand.Range(continental ? .018f : .007f, continental ? .055f : .018f);
                rifts[i].phase = rand.Range(0f, Mathf.PI * 2f);
                rifts[i].amplitude = rand.Range(continental ? .025f : .012f, continental ? .095f : .028f) * Mathf.Lerp(.35f, 1f, riftDepth);
            }

            int ridgeCount = continental ? 2 + rand.Range(0, 3) : 1 + rand.Range(0, 2);
            var ridges = new Band[ridgeCount];
            for (int i = 0; i < ridgeCount; i++)
            {
                float angle = rand.Range(0f, Mathf.PI * 2f);
                ridges[i].normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                ridges[i].tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                ridges[i].offset = rand.Range(-.22f, .22f);
                ridges[i].width = rand.Range(continental ? .045f : .028f, continental ? .12f : .065f);
                ridges[i].phase = rand.Range(0f, Mathf.PI * 2f);
                ridges[i].amplitude = rand.Range(continental ? .012f : .006f, continental ? .034f : .016f) * Mathf.Lerp(.25f, 1f, collision);
            }

            var vertices = new List<Vector3>((rings * sectors + 1) * 2 + sectors * 4);
            var uvs = new List<Vector2>((rings * sectors + 1) * 2 + sectors * 4);
            var colors = visualColors ? new List<Color32>((rings * sectors + 1) * 2 + sectors * 4) : null;
            var triangles = new List<int>(rings * sectors * 12 + sectors * 6);

            float Edge(float a)
            {
                float outline = continental
                    ? 1f
                      + .18f * Mathf.Sin(a * 2f + phase[0])
                      + .12f * Mathf.Cos(a * 3f + phase[1])
                      + .10f * Mathf.Sin(a * 5f + phase[2])
                      + .06f * Mathf.Sin(a * 9f + phase[3])
                      + .04f * Mathf.Sign(Mathf.Sin(a * 4f + phase[4])) * Mathf.Abs(Mathf.Sin(a * 4f + phase[4]))
                    : 1f
                      + .12f * Mathf.Sin(a * 3f + phase[0])
                      + .075f * Mathf.Cos(a * 5f + phase[1])
                      + .052f * Mathf.Sin(a * 9f + phase[2])
                      + .025f * Mathf.Sin(a * 17f + phase[3]);
                return Mathf.Clamp(outline, continental ? .58f : .71f, continental ? 1.42f : 1.28f);
            }

            float Surface(float x, float z)
            {
                float nx = x / halfDiameter;
                float nz = z / halfDiameter;
                if (continental)
                {
                    float macro = .65f * Mathf.Sin(nx * 4.5f + phase[2]) * Mathf.Sin(nz * 4f + phase[1])
                        + .34f * Mathf.Cos(nx * 8.5f - nz * 6f + phase[4])
                        + .16f * Mathf.Sin(nx * 15f + nz * 12f + phase[5]);
                    return macro * diameter * (.012f + .012f * fracture);
                }
                float waves = .57f * Mathf.Sin(nx * 11f + phase[2]) * Mathf.Sin(nz * 9f + phase[1])
                    + .28f * Mathf.Cos(nx * 23f - nz * 15f + phase[4])
                    + .15f * Mathf.Sin(nx * 41f + nz * 35f + phase[5]);
                return waves * diameter * (.008f + .006f * fracture);
            }

            float RiftShape(float x, float z)
            {
                var pt = new Vector2(x / halfDiameter, z / halfDiameter);
                float delta = 0f;
                foreach (Band band in rifts)
                {
                    float along = Vector2.Dot(pt, band.tangent);
                    float displacement = (.012f + .012f * riftDepth) * Mathf.Sin(along * (continental ? 8f : 17f) + band.phase) +
                                         (.005f + .006f * riftDepth) * Mathf.Sin(along * (continental ? 19f : 41f) - band.phase);
                    float d = Mathf.Abs(Vector2.Dot(pt, band.normal) - band.offset + displacement);
                    float t = Mathf.Clamp01(1f - d / band.width);
                    delta -= diameter * band.amplitude * t * t;
                    float bank = Mathf.Exp(-Mathf.Pow((d - band.width) / Mathf.Max(.0001f, band.width * .40f), 2f));
                    delta += diameter * band.amplitude * (continental ? .28f : .21f) * bank;
                }
                return delta;
            }

            float RidgeLift(float x, float z)
            {
                var pt = new Vector2(x / halfDiameter, z / halfDiameter);
                float lift = 0f;
                foreach (Band ridge in ridges)
                {
                    float along = Vector2.Dot(pt, ridge.tangent);
                    float d = Vector2.Dot(pt, ridge.normal) - ridge.offset;
                    float band = Mathf.Exp(-(d * d) / Mathf.Max(.0001f, ridge.width * ridge.width));
                    float serration = .72f + .28f * Mathf.Sin(along * (continental ? 9f : 13f) + ridge.phase);
                    float terrace = continental ? Mathf.Abs(Mathf.Sin(along * 3.5f + ridge.phase * .7f)) : 1f;
                    lift += diameter * ridge.amplitude * band * serration * terrace;
                }
                return lift;
            }

            float ShelfStep(float x, float z)
            {
                if (!continental) return 0f;
                float nx = x / halfDiameter;
                float nz = z / halfDiameter;
                float layer = Mathf.Sin(nx * 2.3f + phase[6]) + Mathf.Cos(nz * 2.1f + phase[7]);
                float terrace = Mathf.Round(layer * 1.7f) / 1.7f;
                return terrace * diameter * (.006f + .006f * collision);
            }

            float TopY(float x, float z) =>
                halfThickness + Surface(x, z) + RidgeLift(x, z) + RiftShape(x, z) + ShelfStep(x, z);

            float BottomY(float x, float z)
            {
                float baseUnderside = Surface(x, z) * (continental ? .16f : .27f);
                float shelfSag = continental ? -RidgeLift(x, z) * .22f : 0f;
                return -halfThickness + baseUnderside + shelfSag;
            }

            int upperCenter = AddVertex(vertices, uvs, colors,
                new Vector3(0f, TopY(0f, 0f), 0f), new Vector2(.5f, .5f), Color.white);
            int lowerCenter = AddVertex(vertices, uvs, colors,
                new Vector3(0f, BottomY(0f, 0f), 0f), new Vector2(.5f, .5f), Color.white);

            int[] upper = new int[rings * sectors];
            int[] lower = new int[rings * sectors];
            for (int ring = 0; ring < rings; ring++)
            {
                float radialFraction = (ring + 1f) / rings;
                // Continental shelves spend more area near the outer rings, like broad plateaus.
                radialFraction = continental ? Mathf.Pow(radialFraction, .82f) : radialFraction;
                for (int s = 0; s < sectors; s++)
                {
                    float angle = s / (float)sectors * Mathf.PI * 2f;
                    float r = halfDiameter * radialFraction * Edge(angle);
                    float x = Mathf.Cos(angle) * r;
                    float z = Mathf.Sin(angle) * r;
                    Vector2 uv = new Vector2(.5f + x / diameter, .5f + z / diameter);
                    float top = TopY(x, z);
                    float bottom = BottomY(x, z);
                    top = Mathf.Max(top, bottom + halfThickness * (continental ? .80f : .60f));
                    Color face = continental ? new Color(.90f, .97f, 1f, 1f) : new Color(.90f, .96f, 1f, 1f);
                    upper[ring * sectors + s] = AddVertex(vertices, uvs, colors, new Vector3(x, top, z), uv, face);
                    lower[ring * sectors + s] = AddVertex(vertices, uvs, colors, new Vector3(x, bottom, z), uv, face);
                }
            }

            for (int s = 0; s < sectors; s++)
            {
                int next = (s + 1) % sectors;
                AddTriangle(triangles, upperCenter, upper[next], upper[s]);
                AddTriangle(triangles, lowerCenter, lower[s], lower[next]);
                for (int ring = 1; ring < rings; ring++)
                {
                    int inA = (ring - 1) * sectors + s;
                    int inB = (ring - 1) * sectors + next;
                    int outA = ring * sectors + s;
                    int outB = ring * sectors + next;
                    AddTriangle(triangles, upper[inA], upper[outB], upper[outA]);
                    AddTriangle(triangles, upper[inA], upper[inB], upper[outB]);
                    AddTriangle(triangles, lower[inA], lower[outA], lower[outB]);
                    AddTriangle(triangles, lower[inA], lower[outB], lower[inB]);
                }

                int edgeA = (rings - 1) * sectors + s;
                int edgeB = (rings - 1) * sectors + next;
                Vector3 pa = vertices[upper[edgeA]], pb = vertices[upper[edgeB]];
                Vector3 qa = vertices[lower[edgeA]], qb = vertices[lower[edgeB]];
                float u0 = s / (float)sectors, u1 = (s + 1f) / sectors;
                int wa = AddVertex(vertices, uvs, colors, pa, new Vector2(u0, 1f), continental ? new Color(.80f, .90f, .97f) : new Color(.72f, .84f, .91f));
                int wb = AddVertex(vertices, uvs, colors, pb, new Vector2(u1, 1f), continental ? new Color(.80f, .90f, .97f) : new Color(.72f, .84f, .91f));
                int wc = AddVertex(vertices, uvs, colors, qa, new Vector2(u0, 0f), continental ? new Color(.63f, .79f, .90f) : new Color(.59f, .73f, .82f));
                int wd = AddVertex(vertices, uvs, colors, qb, new Vector2(u1, 0f), continental ? new Color(.63f, .79f, .90f) : new Color(.59f, .73f, .82f));
                AddTriangle(triangles, wa, wb, wc);
                AddTriangle(triangles, wb, wd, wc);
            }

            var mesh = new Mesh { name = continental ? $"MO_IceShelf_V7_{seed}_S{subdivision}" : $"MO_IceShelf_V6_{seed}_S{subdivision}" };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            if (colors != null) mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int AddVertex(List<Vector3> vertices, List<Vector2> uvs,
            List<Color32> colors, Vector3 vertex, Vector2 uv, Color tint)
        {
            int index = vertices.Count;
            vertices.Add(vertex);
            uvs.Add(uv);
            if (colors != null) colors.Add(tint);
            return index;
        }

        private static void AddTriangle(List<int> tris, int a, int b, int c)
        {
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }
    }
}
