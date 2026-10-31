using System.Collections.Generic;
using UnityEngine;

namespace DachshundDash
{
    /// <summary>Loads the generated models (Resources/Models/&lt;name&gt;) and wraps each in a root whose pivot is the
    /// bottom centre, facing +X, at a given height. A missing model becomes a coloured box so the game always runs.</summary>
    public static class Art
    {
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        /// <summary>Yaw (degrees) that turns each model so it faces +X (the run direction). Measured on import.</summary>
        public static readonly Dictionary<string, float> Yaw = new Dictionary<string, float>
        {
            // rigged animals face -Z after import; -90 turns them to +X (the run direction). The cat faces +Z after
            // import, so it turns the other way (enemies are then flipped 180 to face the dog)
            { "dog", -90 }, { "cat", 90 }, { "crow", -90 },
            // props with a fixed turn instead of "long side along X": the fallen tree lies across the lane so the dog
            // can slide under the trunk
            { "tree", 90 },
        };

        public static GameObject Load(string name)
        {
            if (!Cache.TryGetValue(name, out var go))
            {
                go = Resources.Load<GameObject>("Models/" + name);
                Cache[name] = go;
            }
            return go;
        }

        /// <summary>New instance under a pivot root. height scales the model to that height (0 = keep).</summary>
        public static GameObject Spawn(string name, float height, Color fallbackColor, Vector3 fallbackSize, Transform parent = null, string variant = null)
        {
            var root = new GameObject(name);
            if (parent) root.transform.SetParent(parent, false);
            var prefab = Load(name);
            GameObject model;
            if (prefab != null)
            {
                model = Object.Instantiate(prefab, root.transform);
                model.transform.localRotation = Quaternion.Euler(0, Yaw.TryGetValue(name, out var y) ? y : 0, 0) * prefab.transform.localRotation;
                var mat = ModelMaterial(name, variant);
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
                if (!Yaw.ContainsKey(name))
                {
                    // props: the long side runs along X (the run direction), so cars and benches are seen side-on
                    var b = Bounds(model);
                    if (b.size.z > b.size.x * 1.1f)
                        model.transform.localRotation = Quaternion.Euler(0, 90, 0) * model.transform.localRotation;
                }
            }
            else
            {
                model = MeshUtil.Primitive(PrimitiveType.Cube);
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = fallbackSize;
                model.GetComponent<Renderer>().sharedMaterial = Colored(fallbackColor);
                height = fallbackSize.y;
            }
            Fit(root.transform, model.transform, height);
            return root;
        }

        /// <summary>Scales the model to the height and moves it so its bounds' bottom centre sits on the root.</summary>
        public static void Fit(Transform root, Transform model, float height)
        {
            var b = Bounds(model.gameObject);
            if (b.size.y <= 0) return;
            if (height > 0)
            {
                float k = height / b.size.y;
                model.localScale *= k;
                b = Bounds(model.gameObject);
            }
            var bottom = new Vector3(b.center.x, b.min.y, b.center.z);
            model.position += root.position - bottom;
        }

        public static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs)
            {
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                    b.Encapsulate(smr.bounds);
                    smr.updateWhenOffscreen = false;
                }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Matte lit material with the model's baked texture (Resources/Models/&lt;name&gt;_tex); flat colour
        /// overrides for models whose texture should not be used.</summary>
        public static readonly Dictionary<string, Color> FlatColor = new Dictionary<string, Color>
        {
            { "bone", new Color(0.97f, 0.93f, 0.82f) },
        };

        /// <summary>Models that glow a little from within so they read against the night (enemies, bones): emission
        /// strength, and whether it follows the texture (false: a flat cool glow, for near-black models like the crow).</summary>
        public static readonly Dictionary<string, (float strength, bool fromTexture)> Glow = new Dictionary<string, (float, bool)>
        {
            { "cat", (0.45f, true) }, { "mole", (0.5f, true) }, { "crow", (0.3f, false) }, { "bone", (0.55f, true) },
        };

        public static Material ModelMaterial(string name, string variant = null)
        {
            string key = "model_" + name + (variant == null ? "" : "_" + variant);
            if (Materials.TryGetValue(key, out var m)) return m;
            bool glow = Glow.TryGetValue(name, out var g);
            if (FlatColor.TryGetValue(name, out var flat))
            {
                m = new Material(glow ? Mat("LitGlow") : Colored(flat));
                m.color = flat;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", flat);
                if (glow) m.SetColor("_EmissionColor", flat * g.strength);
            }
            else
            {
                m = new Material(Mat(glow ? "LitGlow" : "Lit"));
                // colour variant <name>_tex_<variant> (tools/recolor.py), else the model's own texture
                var tex = variant != null ? Resources.Load<Texture2D>("Models/" + name + "_tex_" + variant) : null;
                if (tex == null) tex = Resources.Load<Texture2D>("Models/" + name + "_tex");
                if (tex != null)
                {
                    m.mainTexture = tex;
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                }
                m.color = Color.white;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                if (glow)
                {
                    if (g.fromTexture && tex != null) { m.SetTexture("_EmissionMap", tex); m.SetColor("_EmissionColor", Color.white * g.strength); }
                    else { m.SetTexture("_EmissionMap", null); m.SetColor("_EmissionColor", new Color(0.55f, 0.6f, 0.9f) * g.strength); }
                }
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            m.name = key;
            Materials[key] = m;
            return m;
        }

        /// <summary>Colour variants of props with one dominating colour (null = the original texture).</summary>
        public static readonly Dictionary<string, string[]> Variants = new Dictionary<string, string[]>
        {
            { "bush", new[] { null, "lightgreen", "darkgreen", "blue", "orange", "red" } },
            { "trashcan", new[] { null, "orange", "green", "blue" } },
            { "dumpster", new[] { null, "blue", "red" } },
            { "hydrant", new[] { null, "yellow" } },
            { "cat", new[] { "orange", "brown", "white" } },      // stronger coats than the grey original
        };

        public static Material Colored(Color c)
        {
            var key = ColorUtility.ToHtmlStringRGBA(c);
            if (!Materials.TryGetValue(key, out var m))
            {
                m = new Material(Mat("Lit"));
                m.color = c;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                Materials[key] = m;
            }
            return m;
        }

        /// <summary>Template materials made by the editor builder (so their shaders ship in the build).</summary>
        public static Material Mat(string name)
        {
            if (!Materials.TryGetValue("tpl_" + name, out var m))
            {
                m = Resources.Load<Material>("Materials/" + name);
                if (m == null)
                {
                    var sh = Shader.Find(name.StartsWith("Lit") ? "Universal Render Pipeline/Simple Lit" : "Universal Render Pipeline/Unlit");
                    m = new Material(sh ? sh : Shader.Find("Standard"));
                }
                Materials["tpl_" + name] = m;
            }
            return m;
        }

        /// <summary>Soft additive glow card behind an object (faces the camera), so it stands out from the dark
        /// background: centred at height y, size across, a little behind the object (away from the camera).</summary>
        public static GameObject Halo(Transform parent, float y, float size, Color color, float behind = 0.25f)
        {
            var q = MeshUtil.Primitive(PrimitiveType.Quad);
            q.name = "halo";
            q.transform.SetParent(parent, false);
            q.transform.localPosition = new Vector3(0, y, 0);
            q.transform.localScale = Vector3.one * size;
            var key = "halo_" + ColorUtility.ToHtmlStringRGBA(color);
            if (!Materials.TryGetValue(key, out var m))
            {
                m = new Material(Mat("Halo"));
                m.color = color;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                Materials[key] = m;
            }
            q.GetComponent<Renderer>().sharedMaterial = m;
            q.AddComponent<FaceCamera>().Push = behind;
            return q;
        }

        /// <summary>Multiplies every material of the object by a tint (instanced materials).</summary>
        public static void Tint(GameObject go, Color c)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                foreach (var m in mats)
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * c);
                    else if (m.HasProperty("_Color")) m.color *= c;
                }
                r.materials = mats;
            }
        }

        /// <summary>Sky / horizon / ground ambient written straight into the ambient probe: changing the RenderSettings
        /// colours at runtime does not reach the shaders on its own.</summary>
        public static void SetAmbient(Color sky, Color equator, Color ground)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2();
            sh.AddAmbientLight(equator);
            sh.AddDirectionalLight(Vector3.down, (sky - equator) * 0.5f, 1f);     // light from above: brighter tops
            sh.AddDirectionalLight(Vector3.up, (ground - equator) * 0.5f, 1f);
            RenderSettings.ambientProbe = sh;
        }

        public static Sprite Sprite(string name) => Resources.Load<Sprite>("UI/" + name);
        public static Texture2D Texture(string name) => Resources.Load<Texture2D>("Textures/" + name);
    }
}
