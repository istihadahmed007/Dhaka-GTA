using System.Collections.Generic;
using DhakaGta.Core;
using UnityEngine;

namespace DhakaGta.Environment
{
    /// <summary>
    /// Generates authentic roadside sagging catenary electrical/telecom cables connecting consecutive utility poles.
    /// </summary>
    public sealed class OverheadWireCatenaryBuilder : MonoBehaviour
    {
        public static GameObject ConnectPolesWithCatenaryWires(
            IReadOnlyList<Vector3> poleBasePositions,
            Transform parentRoot,
            Material wireMaterial,
            float wireAttachHeightMeters = 7.8f,
            float maxSpanMeters = 62f)
        {
            if (poleBasePositions == null || poleBasePositions.Count < 2 || parentRoot == null)
            {
                return null;
            }

            var verts = new List<Vector3>();
            var tris = new List<int>();
            float[] crossOffsets = { -0.75f, 0f, 0.75f };
            float[] wireSags = { 0.75f, 0.90f, 0.75f };

            for (int i = 0; i < poleBasePositions.Count - 1; i++)
            {
                Vector3 a = poleBasePositions[i];
                Vector3 b = poleBasePositions[i + 1];
                float dist = Vector3.Distance(a, b);
                if (dist < 4f || dist > maxSpanMeters) continue;

                Vector3 dir = (b - a).normalized;
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);

                for (int w = 0; w < crossOffsets.Length; w++)
                {
                    Vector3 pStart = a + Vector3.up * wireAttachHeightMeters + right * crossOffsets[w];
                    Vector3 pEnd = b + Vector3.up * wireAttachHeightMeters + right * crossOffsets[w];
                    AppendCatenaryRibbon(verts, tris, pStart, pEnd, wireSags[w], right, halfThickness: 0.032f, steps: 6);
                }
            }

            if (verts.Count < 3 || tris.Count < 3) return null;

            var mesh = new Mesh
            {
                name = "DhakaOverheadWiresBatch",
                indexFormat = verts.Count > 60000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("RoadsideOverheadWires");
            go.layer = PhysicsLayers.Building;
            go.isStatic = true;
            go.transform.SetParent(parentRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (wireMaterial != null) mr.sharedMaterial = wireMaterial;
            return go;
        }

        private static void AppendCatenaryRibbon(
            List<Vector3> verts, List<int> tris,
            Vector3 start, Vector3 end, float sagMeters, Vector3 right, float halfThickness, int steps)
        {
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                Vector3 pos = Vector3.Lerp(start, end, t);
                // Parabolic catenary droop greatest at t = 0.5
                float droop = 4f * t * (1f - t) * sagMeters;
                pos.y -= droop;

                verts.Add(pos - right * halfThickness);
                verts.Add(pos + right * halfThickness);

                if (s > 0)
                {
                    int b = verts.Count - 4;
                    // Double-sided thin cable strip visible from below and above
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
                }
            }
        }
    }
}
