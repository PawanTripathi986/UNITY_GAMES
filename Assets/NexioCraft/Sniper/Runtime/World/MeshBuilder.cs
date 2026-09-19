using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NexioCraft.Sniper
{
    /// <summary>
    /// Accumulates flat-shaded, vertex-coloured shapes into one mesh (one draw call). Faces are wound
    /// clockwise as seen from the front, which is Unity's front face. A vertex colour's alpha below 1
    /// makes that surface self-lit in the sniper shader.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>(4096);
        readonly List<Vector3> normals = new List<Vector3>(4096);
        readonly List<Color32> colors = new List<Color32>(4096);
        readonly List<int> triangles = new List<int>(8192);

        /// <summary>Applied to every point added while it is set.</summary>
        public Matrix4x4 Matrix = Matrix4x4.identity;

        public int VertexCount => vertices.Count;
        public bool IsEmpty => vertices.Count == 0;

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            colors.Clear();
            triangles.Clear();
            Matrix = Matrix4x4.identity;
        }

        /// <summary>A colour for the sniper shader; <paramref name="glow"/> 1 makes it fully self-lit.</summary>
        public static Color32 Tone(Color color, float glow = 0f)
        {
            Color32 c = color;
            c.a = (byte)Mathf.RoundToInt(255f * (1f - Mathf.Clamp01(glow)));
            return c;
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color32 color)
        {
            a = Matrix.MultiplyPoint3x4(a);
            b = Matrix.MultiplyPoint3x4(b);
            c = Matrix.MultiplyPoint3x4(c);
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            int i = vertices.Count;
            Add(a, n, color);
            Add(b, n, color);
            Add(c, n, color);
            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color) => Quad(a, b, c, d, color, color, color, color);

        /// <summary>Quad a-b-c-d, clockwise from the front, with a colour per corner.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            a = Matrix.MultiplyPoint3x4(a);
            b = Matrix.MultiplyPoint3x4(b);
            c = Matrix.MultiplyPoint3x4(c);
            d = Matrix.MultiplyPoint3x4(d);
            // Sum of both triangles' normals: robust when two corners meet (sphere poles, wedges).
            var n = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            int i = vertices.Count;
            Add(a, n, ca);
            Add(b, n, cb);
            Add(c, n, cc);
            Add(d, n, cd);
            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
            triangles.Add(i);
            triangles.Add(i + 2);
            triangles.Add(i + 3);
        }

        /// <summary>
        /// Six-sided solid from eight corners: 0-3 on the −z side (−x−y, +x−y, +x+y, −x+y) and 4-7 on the +z side
        /// in the same order. Lets boxes taper (torsos, roofs, car cabins).
        /// </summary>
        public void Hexahedron(Vector3[] p, Color32 side, Color32 top, Color32 bottom, bool withBottom = true)
        {
            Quad(p[0], p[3], p[2], p[1], side);
            Quad(p[5], p[6], p[7], p[4], side);
            Quad(p[4], p[7], p[3], p[0], side);
            Quad(p[1], p[2], p[6], p[5], side);
            Quad(p[3], p[7], p[6], p[2], top);
            if (withBottom) Quad(p[1], p[5], p[4], p[0], bottom);
        }

        readonly Vector3[] corners = new Vector3[8];

        public void Box(Vector3 center, Vector3 size, Color32 color, bool withBottom = true) =>
            Box(center, size, Quaternion.identity, color, color, withBottom);

        public void Box(Vector3 center, Vector3 size, Color32 side, Color32 top, bool withBottom = true) =>
            Box(center, size, Quaternion.identity, side, top, withBottom);

        public void Box(Vector3 center, Vector3 size, Quaternion rotation, Color32 side, Color32 top, bool withBottom = true)
        {
            var h = size * 0.5f;
            corners[0] = center + rotation * new Vector3(-h.x, -h.y, -h.z);
            corners[1] = center + rotation * new Vector3(h.x, -h.y, -h.z);
            corners[2] = center + rotation * new Vector3(h.x, h.y, -h.z);
            corners[3] = center + rotation * new Vector3(-h.x, h.y, -h.z);
            corners[4] = center + rotation * new Vector3(-h.x, -h.y, h.z);
            corners[5] = center + rotation * new Vector3(h.x, -h.y, h.z);
            corners[6] = center + rotation * new Vector3(h.x, h.y, h.z);
            corners[7] = center + rotation * new Vector3(-h.x, h.y, h.z);
            Hexahedron(corners, side, top, side, withBottom);
        }

        /// <summary>Box whose top face is <paramref name="topScale"/> of its bottom face (x and z).</summary>
        public void TaperedBox(Vector3 center, Vector3 size, Vector2 topScale, Color32 side, Color32 top)
        {
            var h = size * 0.5f;
            corners[0] = center + new Vector3(-h.x, -h.y, -h.z);
            corners[1] = center + new Vector3(h.x, -h.y, -h.z);
            corners[2] = center + new Vector3(h.x * topScale.x, h.y, -h.z * topScale.y);
            corners[3] = center + new Vector3(-h.x * topScale.x, h.y, -h.z * topScale.y);
            corners[4] = center + new Vector3(-h.x, -h.y, h.z);
            corners[5] = center + new Vector3(h.x, -h.y, h.z);
            corners[6] = center + new Vector3(h.x * topScale.x, h.y, h.z * topScale.y);
            corners[7] = center + new Vector3(-h.x * topScale.x, h.y, h.z * topScale.y);
            Hexahedron(corners, side, top, side);
        }

        /// <summary>Upright cylinder standing on <paramref name="bottom"/>.</summary>
        public void Cylinder(Vector3 bottom, float radius, float height, int sides, Color32 side, Color32 cap, bool withCaps = true)
        {
            var top = bottom + Vector3.up * height;
            for (int k = 0; k < sides; k++)
            {
                var r0 = Ring(k, sides, radius);
                var r1 = Ring(k + 1, sides, radius);
                Quad(bottom + r0, top + r0, top + r1, bottom + r1, side);
                if (!withCaps) continue;
                Triangle(top, top + r1, top + r0, cap);
                Triangle(bottom, bottom + r0, bottom + r1, cap);
            }
        }

        /// <summary>Cylinder between two points (pipes, barrels of guns, crane arms).</summary>
        public void Tube(Vector3 from, Vector3 to, float radius, int sides, Color32 color)
        {
            var saved = Matrix;
            var axis = to - from;
            float length = axis.magnitude;
            if (length < 1e-4f) return;
            Matrix = saved * Matrix4x4.TRS(from, Quaternion.FromToRotation(Vector3.up, axis / length), Vector3.one);
            Cylinder(Vector3.zero, radius, length, sides, color, color);
            Matrix = saved;
        }

        public void Cone(Vector3 bottom, float radius, float height, int sides, Color32 side, Color32 cap)
        {
            var apex = bottom + Vector3.up * height;
            for (int k = 0; k < sides; k++)
            {
                var r0 = Ring(k, sides, radius);
                var r1 = Ring(k + 1, sides, radius);
                Triangle(bottom + r0, apex, bottom + r1, side);
                Triangle(bottom, bottom + r0, bottom + r1, cap);
            }
        }

        /// <summary>Low-poly ellipsoid.</summary>
        public void Sphere(Vector3 center, Vector3 radii, int rings, int segments, Color32 color) =>
            Sphere(center, radii, rings, segments, color, color);

        /// <summary>Ellipsoid shaded from <paramref name="bottomColor"/> to <paramref name="topColor"/>.</summary>
        public void Sphere(Vector3 center, Vector3 radii, int rings, int segments, Color32 topColor, Color32 bottomColor)
        {
            for (int i = 0; i < rings; i++)
            {
                float lat0 = Mathf.Lerp(-90f, 90f, i / (float)rings) * Mathf.Deg2Rad;
                float lat1 = Mathf.Lerp(-90f, 90f, (i + 1) / (float)rings) * Mathf.Deg2Rad;
                Color32 c = Color32.Lerp(bottomColor, topColor, (i + 0.5f) / rings);
                for (int k = 0; k < segments; k++)
                {
                    float lon0 = k / (float)segments * Mathf.PI * 2f;
                    float lon1 = (k + 1) / (float)segments * Mathf.PI * 2f;
                    Quad(center + Point(lat0, lon0, radii), center + Point(lat1, lon0, radii),
                        center + Point(lat1, lon1, radii), center + Point(lat0, lon1, radii), c);
                }
            }
        }

        static Vector3 Point(float lat, float lon, Vector3 radii) =>
            new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon) * radii.x, Mathf.Sin(lat) * radii.y, Mathf.Cos(lat) * Mathf.Sin(lon) * radii.z);

        static Vector3 Ring(int k, int sides, float radius)
        {
            float angle = k / (float)sides * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        void Add(Vector3 position, Vector3 normal, Color32 color)
        {
            vertices.Add(position);
            normals.Add(normal);
            colors.Add(color);
        }

        public Mesh ToMesh(string name, bool keepReadable = false)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            if (!keepReadable) mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>Creates a child object that renders what has been built so far.</summary>
        public GameObject ToObject(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }
    }
}
