using UnityEngine;

namespace DachshundDash
{
    /// <summary>Small generated meshes whose UVs are in world metres / uvSize, so a tileable texture keeps its scale
    /// on any size.</summary>
    public static class MeshUtil
    {
        /// <summary>Box with its pivot at the bottom centre.</summary>
        public static Mesh Box(Vector3 size, float uvSize)
        {
            var m = new Mesh { name = "box" };
            float hx = size.x / 2, hz = size.z / 2, h = size.y;
            var v = new System.Collections.Generic.List<Vector3>();
            var n = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var t = new System.Collections.Generic.List<int>();

            void Face(Vector3 o, Vector3 a, Vector3 b, Vector3 normal, float ua, float ub)
            {
                int i = v.Count;
                v.Add(o); v.Add(o + a); v.Add(o + a + b); v.Add(o + b);
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(ua / uvSize, 0));
                uv.Add(new Vector2(ua / uvSize, ub / uvSize)); uv.Add(new Vector2(0, ub / uvSize));
                t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }

            // front (-z), back (+z), left (-x), right (+x), top
            Face(new Vector3(-hx, 0, -hz), new Vector3(size.x, 0, 0), new Vector3(0, h, 0), Vector3.back, size.x, h);
            Face(new Vector3(hx, 0, hz), new Vector3(-size.x, 0, 0), new Vector3(0, h, 0), Vector3.forward, size.x, h);
            Face(new Vector3(-hx, 0, hz), new Vector3(0, 0, -size.z), new Vector3(0, h, 0), Vector3.left, size.z, h);
            Face(new Vector3(hx, 0, -hz), new Vector3(0, 0, size.z), new Vector3(0, h, 0), Vector3.right, size.z, h);
            Face(new Vector3(-hx, h, -hz), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), Vector3.up, size.x, size.z);
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Horizontal quad (facing up), corner at the origin, size x by z.</summary>
        public static Mesh Ground(float sx, float sz, float uvSize, float uvOffsetX = 0)
        {
            var m = new Mesh { name = "ground" };
            m.vertices = new[] { new Vector3(0, 0, 0), new Vector3(sx, 0, 0), new Vector3(sx, 0, sz), new Vector3(0, 0, sz) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            float u0 = uvOffsetX / uvSize, u1 = (uvOffsetX + sx) / uvSize, vv = sz / uvSize;
            m.uv = new[] { new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, vv), new Vector2(u0, vv) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Upright strip in the XY plane: length along X from the origin, height centred on the origin;
        /// v runs 0..1 across the height (for a soft line texture).</summary>
        public static Mesh Strip(float length, float height, float uLength)
        {
            var m = new Mesh { name = "strip" };
            float h = height / 2, u = length / uLength;
            m.vertices = new[] { new Vector3(0, -h, 0), new Vector3(length, -h, 0), new Vector3(length, h, 0), new Vector3(0, h, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(u, 0), new Vector2(u, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Horizontal quad like Ground, but its UVs cover 0..1 once (for a texture made to fit it).</summary>
        public static Mesh GroundOnce(float sx, float sz)
        {
            var m = new Mesh { name = "ground_once" };
            m.vertices = new[] { new Vector3(0, 0, 0), new Vector3(sx, 0, 0), new Vector3(sx, 0, sz), new Vector3(0, 0, sz) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Upright cylinder with its top at the origin, reaching height down, as two sub-meshes (0 = top cap,
        /// 1 = side) for two materials; top UVs in metres / uvSize, the side wraps once around per circumference / uvSize.</summary>
        public static Mesh Cylinder(float radius, float height, float uvSize, int sides = 28)
        {
            var m = new Mesh { name = "cylinder" };
            var v = new System.Collections.Generic.List<Vector3>();
            var n = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var top = new System.Collections.Generic.List<int>();
            var side = new System.Collections.Generic.List<int>();
            // cap: centre + rim
            v.Add(Vector3.zero); n.Add(Vector3.up); uv.Add(Vector2.zero);
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                var p = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                v.Add(p); n.Add(Vector3.up); uv.Add(new Vector2(p.x, p.z) / uvSize);
            }
            for (int i = 1; i <= sides; i++) { top.Add(0); top.Add(i + 1); top.Add(i); }
            // side: a ring at the top and one at the bottom, facing out
            int s0 = v.Count;
            float around = 2 * Mathf.PI * radius / uvSize;
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(dir * radius); n.Add(dir); uv.Add(new Vector2(around * i / sides, height / uvSize));
                v.Add(dir * radius + Vector3.down * height); n.Add(dir); uv.Add(new Vector2(around * i / sides, 0));
            }
            for (int i = 0; i < sides; i++)
            {
                int a0 = s0 + i * 2, b0 = a0 + 2;
                side.Add(a0); side.Add(b0); side.Add(a0 + 1);
                side.Add(b0); side.Add(b0 + 1); side.Add(a0 + 1);
            }
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = 2;
            m.SetTriangles(top, 0); m.SetTriangles(side, 1);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Open cone from the apex (origin) down -Y, for fake light shafts.</summary>
        public static Mesh Cone(float height, float radius, int sides = 16)
        {
            var m = new Mesh { name = "cone" };
            var v = new Vector3[sides * 2];
            var uv = new Vector2[sides * 2];
            var t = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                v[i] = new Vector3(Mathf.Cos(a) * 0.05f, 0, Mathf.Sin(a) * 0.05f);
                v[i + sides] = new Vector3(Mathf.Cos(a) * radius, -height, Mathf.Sin(a) * radius);
                uv[i] = new Vector2(i / (float)sides, 1);
                uv[i + sides] = new Vector2(i / (float)sides, 0);
                int j = (i + 1) % sides;
                t[i * 6 + 0] = i; t[i * 6 + 1] = j; t[i * 6 + 2] = i + sides;
                t[i * 6 + 3] = j; t[i * 6 + 4] = j + sides; t[i * 6 + 5] = i + sides;
            }
            m.vertices = v; m.uv = uv; m.triangles = t;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Like GameObject.CreatePrimitive (quad or cube) but without a collider, whose class WebGL code
        /// stripping removes.</summary>
        public static GameObject Primitive(PrimitiveType type)
        {
            var go = new GameObject(type.ToString());
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(type == PrimitiveType.Cube ? "Cube.fbx" : "Quad.fbx");
            go.AddComponent<MeshRenderer>().sharedMaterial = Art.Mat("Lit");
            return go;
        }

        public static GameObject Make(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }

        /// <summary>Soft round dot (white, alpha falloff) for particles and glows.</summary>
        public static Texture2D SoftDot(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1 - d);
                    a = a * a * (3 - 2 * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>One travelling light pulse along u (a sharp bright head near the high-u end, a tail fading back over
        /// most of the period, a short dark gap), soft across v with a bright core and a wide faint glow; repeats in u,
        /// clamped in v.</summary>
        public static Texture2D FlowDash(int length = 128, int width = 32)
        {
            var tex = new Texture2D(length, width, TextureFormat.RGBA32, false) { wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            var px = new Color32[length * width];
            for (int y = 0; y < width; y++)
                for (int x = 0; x < length; x++)
                {
                    float u = (x + 0.5f) / length, v = (y + 0.5f) / width * 2 - 1;
                    const float start = 0.25f, head = 0.9f;
                    float along = u < start ? 0f
                        : u < head ? 0.15f + 0.45f * Mathf.Pow((u - start) / (head - start), 1.5f)      // tail
                        : Mathf.Lerp(1f, 0f, (u - head) / (1f - head));                                     // head, sharp front
                    if (u >= head - 0.04f && u < head) along = Mathf.Lerp(along, 1f, (u - head + 0.04f) / 0.04f);
                    float across = Mathf.Exp(-v * v * 18f) * 0.75f + Mathf.Clamp01(1 - v * v) * 0.25f;   // core + soft spread
                    px[y * length + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(along) * across * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Bold chevron arrow (white, soft edge) pointing up, or left when left = true.</summary>
        public static Texture2D Arrow(bool left, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2 - 1, v = (y + 0.5f) / size * 2 - 1;
                    if (left) { float tmp = u; u = v; v = -tmp; }       // rotate the up arrow a quarter turn
                    // chevron: |u| + offset distance to the line v = 0.45 - |u|, thickness 0.28, within |u| < 0.75
                    float d = Mathf.Abs(v - (0.35f - Mathf.Abs(u) * 0.9f));
                    float a = Mathf.Clamp01((0.18f - d) * size / 3f) * Mathf.Clamp01((0.8f - Mathf.Abs(u)) * size / 3f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Five-pointed star (white) for impact stars and sparkles.</summary>
        public static Texture2D Star(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx) + Mathf.PI / 2;
                    float k = Mathf.Abs(Mathf.Cos(a * 2.5f));
                    float edge = Mathf.Lerp(0.42f, 0.95f, k * k);
                    float al = Mathf.Clamp01((edge - r) * size / 3f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(al * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
