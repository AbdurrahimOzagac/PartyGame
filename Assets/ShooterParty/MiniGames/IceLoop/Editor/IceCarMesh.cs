using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IceLoop.EditorTools
{
    // Procedural meshes for the race car (no Blender on the build machine). The body is a loft of rounded-rectangle
    // cross-sections along the car's length; Smooth shaded, no colliders.
    static class IceCarMesh
    {
        // z (nose is +z), half width, bottom y, top y, corner radius
        static readonly float[][] Sections =
        {
            new[] { 1.15f, 0.22f, 0.20f, 0.32f, 0.06f },
            new[] { 1.00f, 0.46f, 0.16f, 0.40f, 0.10f },
            new[] { 0.55f, 0.66f, 0.14f, 0.50f, 0.15f },   // front arches and hood
            new[] { 0.05f, 0.60f, 0.14f, 0.58f, 0.15f },   // cockpit shoulders
            new[] { -0.55f, 0.68f, 0.14f, 0.64f, 0.16f },  // rear haunches
            new[] { -1.00f, 0.54f, 0.16f, 0.54f, 0.11f },
            new[] { -1.15f, 0.24f, 0.20f, 0.40f, 0.06f },
        };
        const int Arc = 5;   // points per rounded corner

        public static Mesh Body()
        {
            int s = Sections.Length, n = Arc * 4;
            var v = new Vector3[s * n + 2];
            var tri = new List<int>();
            for (int i = 0; i < s; i++)
            {
                var d = Sections[i];
                float w = d[1], bot = d[2], top = d[3], r = Mathf.Min(d[4], w * 0.9f, (top - bot) * 0.45f);
                // corners clockwise seen from +z: top-right, bottom-right, bottom-left, top-left
                var c = new[] { new Vector2(w - r, top - r), new Vector2(w - r, bot + r), new Vector2(-w + r, bot + r), new Vector2(-w + r, top - r) };
                var a0 = new[] { 90f, 0f, -90f, -180f };
                for (int k = 0; k < 4; k++)
                    for (int j = 0; j < Arc; j++)
                    {
                        float a = (a0[k] - 90f * j / (Arc - 1)) * Mathf.Deg2Rad;
                        v[i * n + k * Arc + j] = new Vector3(c[k].x + Mathf.Cos(a) * r, c[k].y + Mathf.Sin(a) * r, d[0]);
                    }
            }
            for (int i = 0; i < s - 1; i++)
                for (int k = 0; k < n; k++)
                {
                    int a = i * n + k, b = i * n + (k + 1) % n, c = (i + 1) * n + k, e = (i + 1) * n + (k + 1) % n;
                    tri.AddRange(new[] { a, b, c, b, e, c });
                }
            v[s * n] = new Vector3(0f, (Sections[0][2] + Sections[0][3]) * 0.5f, Sections[0][0]);
            v[s * n + 1] = new Vector3(0f, (Sections[s - 1][2] + Sections[s - 1][3]) * 0.5f, Sections[s - 1][0]);
            for (int k = 0; k < n; k++)
            {
                tri.AddRange(new[] { s * n, (k + 1) % n, k });
                int o = (s - 1) * n;
                tri.AddRange(new[] { s * n + 1, o + k, o + (k + 1) % n });
            }
            return Finish(v, tri, "IceCar_Body");
        }

        static Mesh Finish(Vector3[] v, List<int> tri, string name)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
            m.vertices = v; m.triangles = tri.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
