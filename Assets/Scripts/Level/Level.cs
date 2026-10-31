using System.Collections.Generic;
using UnityEngine;

namespace DachshundDash
{
    public enum Biome { Street = 0, Alley = 1, Backyard = 2 }

    /// <summary>What a path is paved with; decides which props and enemies may stand on it.</summary>
    public enum Surface { Asphalt = 0, Cobble = 1, Grass = 2 }

    public class Decor : Entity { }

    /// <summary>Builds the endless level ahead of the dog from patterns, recycles what falls behind, and answers
    /// gameplay queries (surfaces, collisions).</summary>
    public class Level : MonoBehaviour
    {
        public const float TileLength = 10f;
        public const float RoadHalfWidth = 2.4f;
        public const float BackWalkDepth = 2.0f;
        public const float FrontWalkDepth = 3.0f;
        const float CurbDepth = 0.2f;                 // the far curb, between the paths and the back walk

        public readonly List<Solid> Solids = new List<Solid>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<BonePickup> Bones = new List<BonePickup>();
        public readonly List<PowerUpPickup> PowerUps = new List<PowerUpPickup>();
        readonly List<Entity> _decor = new List<Entity>();
        readonly List<Lamp> _lamps = new List<Lamp>();
        readonly Dictionary<string, Stack<Entity>> _pool = new Dictionary<string, Stack<Entity>>();

        GameConfig _cfg;
        System.Random _rng;
        float _cursor;            // patterns are built up to here
        float _groundCursor;      // ground tiles up to here
        float _nextPowerUp;
        string _lastPattern;
        int _lastPowerUp = -1;
        Transform _root;
        LightRig _lights;
        LaneLines _laneLines;

        public static Biome BiomeAt(float x, float biomeLength) => (Biome)(((int)Mathf.Floor(Mathf.Max(0, x) / biomeLength)) % 3);
        public Biome BiomeAt(float x) => BiomeAt(x, _cfg.biomeLength);

        /// <summary>Surface combinations (far, middle, near lane) a biome stretch can have; one is picked per stretch.</summary>
        static readonly Surface[][][] Combos =
        {
            new[] { new[] { Surface.Cobble, Surface.Asphalt, Surface.Cobble }, new[] { Surface.Grass, Surface.Cobble, Surface.Asphalt },
                    new[] { Surface.Cobble, Surface.Asphalt, Surface.Asphalt } },                                     // street
            new[] { new[] { Surface.Cobble, Surface.Cobble, Surface.Cobble }, new[] { Surface.Cobble, Surface.Grass, Surface.Cobble },
                    new[] { Surface.Cobble, Surface.Asphalt, Surface.Cobble } },                                      // alley
            new[] { new[] { Surface.Grass, Surface.Grass, Surface.Grass }, new[] { Surface.Grass, Surface.Cobble, Surface.Grass },
                    new[] { Surface.Asphalt, Surface.Asphalt, Surface.Grass } },                                      // backyard
        };
        static readonly string[] SurfaceMat = { "Asphalt", "Cobble", "Grass" };
        const float TransitionLength = 4f;               // a lane changing surface blends over the first 4 m of a stretch
        int _seed;

        /// <summary>The surfaces of the three lanes at x (fixed for a whole biome stretch, picked by the run seed).</summary>
        public Surface[] LaneSurfaces(float x)
        {
            if (DebugMode == DebugLevel.Enemies) return EnemySurfaces;
            if (DebugMode == DebugLevel.Obstacles || DebugMode == DebugLevel.PowerUps) return ObstacleSurfaces;
            int stretch = Mathf.FloorToInt(Mathf.Max(0, x) / _cfg.biomeLength);
            var options = Combos[stretch % 3];
            return options[new System.Random(_seed * 7919 + stretch).Next(options.Length)];
        }

        public Surface LaneSurface(int lane, float x) => LaneSurfaces(x)[lane];

        public void Init(int seed)
        {
            _cfg = GameConfig.I;
            _rng = new System.Random(seed);
            _seed = seed;
            _root = transform;
            _cursor = 14f;              // empty run-up in front of the start
            _groundCursor = -30f;
            _nextPowerUp = 120f;
            _lights = new GameObject("Lights").AddComponent<LightRig>();
            _lights.transform.SetParent(_root, false);
            _laneLines = new GameObject("LaneLines").AddComponent<LaneLines>();
            _laneLines.transform.SetParent(_root, false);
            _laneLines.Init();
        }

        /// <summary>Camera view changed: the lane lines stand up for the side view, lie flat for the behind view.</summary>
        public void SetView(bool behind) => _laneLines.SetView(behind);

        float R() => (float)_rng.NextDouble();
        float R(float a, float b) => a + (b - a) * R();
        int RI(int a, int bExclusive) => _rng.Next(a, bExclusive);

        // ---------------------------------------------------------------- update

        public void Tick(Player dog, float dt)
        {
            float x = dog.X;
            while (_groundCursor < x + _cfg.lookAhead + 20f) SpawnGroundTile(_groundCursor);
            while (_cursor < x + _cfg.lookAhead) SpawnNext(x);

            float behind = x - _cfg.keepBehind;
            TickList(Solids, dog, dt, behind);
            TickList(Enemies, dog, dt, behind);
            TickList(Bones, dog, dt, behind);
            TickList(PowerUps, dog, dt, behind);
            TickList(_decor, dog, dt, behind - 10f);
            for (int i = _lamps.Count - 1; i >= 0; i--)
                if (_lamps[i].X < behind - 10f) { Recycle(_lamps[i]); _lamps.RemoveAt(i); }
            _lights.Assign(_lamps, x);
            _laneLines.Follow(x, dog.transform.position.z);
        }

        void TickList<T>(List<T> list, Player dog, float dt, float behind) where T : Entity
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (!e.gameObject.activeSelf || e.X < behind)
                {
                    Recycle(e);
                    list.RemoveAt(i);
                    continue;
                }
                e.Tick(dog, dt);
            }
        }

        // ---------------------------------------------------------------- queries

        /// <summary>Top of the highest walkable solid under x in the lane that is not above maxY (0 = ground).</summary>
        public float SurfaceAt(int lane, float x, float maxY, float halfX = 0f)
        {
            float best = 0f;
            foreach (var s in Solids)
            {
                if (s.Lane != lane || s.Knocked || !s.Walkable) continue;
                var b = s.Box;
                if (x + halfX <= b.Min.x || x - halfX >= b.Max.x) continue;
                float top = b.Max.y;
                if (top <= maxY + 0.001f && top > best) best = top;
            }
            return best;
        }

        public bool LaneBlocked(int lane, Box dogBox)
        {
            float z = _cfg.LaneZ(lane);
            var probe = new Box(new Vector3(dogBox.Min.x, dogBox.Min.y + 0.05f, z - 0.3f), new Vector3(dogBox.Max.x, dogBox.Max.y, z + 0.3f));
            foreach (var s in Solids)
                if (s.Lane == lane && !s.Knocked && s.Box.Overlaps(probe)) return true;
            return false;
        }

        // ---------------------------------------------------------------- patterns

        void SpawnNext(float dogX)
        {
            if (DebugMode == DebugLevel.Enemies) { SpawnEnemyDebugSection(); return; }
            if (DebugMode == DebugLevel.Obstacles) { SpawnObstacleDebugSection(); return; }
            if (DebugMode == DebugLevel.PowerUps) { SpawnPowerUpDebugSection(); return; }
            float dist = _cursor;
            int maxTier = 0;
            for (int t = 0; t <= 4; t++) if (dist >= Pattern.TierStart(t)) maxTier = t;

            // breather before the pattern (shorter as the run goes on), sometimes with a bone line
            int breather = 1 + RI(0, 2);                     // short gaps from the start: the street is busy
            float bx = _cursor;
            int boneLane = RI(0, 3);
            bool bonesInBreather = R() < 0.5f;
            for (int i = 0; i < breather; i++)
            {
                float cx = bx + (i + 0.5f) * _cfg.slotLength;
                if (bonesInBreather) SpawnBone(boneLane, cx, 0.45f);
            }
            _cursor += breather * _cfg.slotLength;

            if (_cursor >= _nextPowerUp)
            {
                int lane = RI(0, 3);
                SpawnPowerUp(lane, _cursor - _cfg.slotLength * 0.5f);
                _nextPowerUp = _cursor + R(_cfg.powerUpSpacingMin, _cfg.powerUpSpacingMax);
            }

            var p = PickPattern(maxTier);
            if (R() < 0.5f) p = p.Mirrored();
            BuildPattern(p, _cursor);
            _cursor += p.Length * _cfg.slotLength;
        }

        Pattern PickPattern(int maxTier)
        {
            // newest tier is favoured; earlier tiers stay in the mix
            var weights = new List<(Pattern p, float w)>();
            float sum = 0;
            foreach (var p in Patterns.All)
            {
                if (p.Tier > maxTier || p.Name == _lastPattern) continue;
                float w = p.Tier == maxTier ? 3f : 1f + p.Tier * 0.3f;
                weights.Add((p, w)); sum += w;
            }
            float r = R() * sum;
            foreach (var (p, w) in weights)
            {
                r -= w;
                if (r <= 0) { _lastPattern = p.Name; return p; }
            }
            _lastPattern = weights[weights.Count - 1].p.Name;
            return weights[weights.Count - 1].p;
        }

        /// <summary>Adds extra enemies to free cells of a pattern, more as the run goes on. A cell gets one only when
        /// its lane is clear two slots either side (and, for a cat, three slots in front for its charge), so the
        /// pattern rules still hold.</summary>
        string[] SprinkleEnemies(Pattern p, float x0)
        {
            var rows = new string[3];
            float chance = Mathf.Clamp(0.10f + x0 / 2500f, 0.10f, 0.28f);
            for (int lane = 0; lane < 3; lane++)
            {
                var r = p.Rows[lane].ToCharArray();
                for (int c = PatternValidator.EntryColumns + 1; c < r.Length - 1; c++)
                {
                    if (!Free(r, c - 2, c + 2) || R() > chance) continue;
                    var options = new List<char>();
                    if (LaneSurface(lane, x0 + (c + 0.5f) * _cfg.slotLength) == Surface.Grass) options.Add('M');
                    if (x0 >= 40f && Free(r, c - 3, c - 1)) options.Add('C');
                    if (x0 >= 80f) options.Add('R');
                    if (options.Count == 0) continue;
                    r[c] = options[RI(0, options.Count)];
                    c += 3;
                }
                rows[lane] = new string(r);
            }
            return rows;
        }

        /// <summary>Early in the run (before the cats and crows of the later tiers fill the street) adds single low
        /// props to free cells, and from 40 m a crouch prop now and then; same spacing rule as the enemies, so a lone
        /// jump or crouch is always possible.</summary>
        string[] SprinkleProps(string[] rows, float x0)
        {
            if (x0 >= 300f) return rows;
            float chance = 0.16f;
            for (int lane = 0; lane < 3; lane++)
            {
                var r = rows[lane].ToCharArray();
                for (int c = PatternValidator.EntryColumns + 1; c < r.Length - 1; c++)
                {
                    if (!Free(r, c - 2, c + 2) || R() > chance) continue;
                    r[c] = x0 >= 40f && R() < 0.25f ? 'O' : 'L';
                    c += 3;
                }
                rows[lane] = new string(r);
            }
            return rows;
        }

        static bool Free(char[] r, int from, int to)
        {
            for (int i = Mathf.Max(0, from); i <= Mathf.Min(r.Length - 1, to); i++)
                if (r[i] != '.' && r[i] != 'b') return false;
            return true;
        }

        void BuildPattern(Pattern p, float x0)
        {
            float slot = _cfg.slotLength;
            var rows = SprinkleProps(SprinkleEnemies(p, x0), x0);
            for (int lane = 0; lane < 3; lane++)
            {
                string row = rows[lane];
                for (int c = 0; c < row.Length; c++)
                {
                    char ch = row[c];
                    float cx = x0 + (c + 0.5f) * slot;
                    if (ch == 'X' || Pattern.IsPlatform(ch))
                    {
                        int e = c;
                        bool plat = Pattern.IsPlatform(ch);
                        while (e + 1 < row.Length && (plat ? Pattern.IsPlatform(row[e + 1]) : row[e + 1] == 'X')) e++;
                        float sx = x0 + c * slot, ex = x0 + (e + 1) * slot;
                        if (plat) SpawnPlatform(lane, sx, ex, row, c, e, x0);
                        else SpawnBlockers(lane, sx, ex);
                        c = e;
                        continue;
                    }
                    switch (ch)
                    {
                        case 'b': SpawnBone(lane, cx, 0.45f); break;
                        case 'L': SpawnLow(lane, cx, false); break;
                        case 'l': SpawnLow(lane, cx, true); break;
                        case 'O': SpawnOverhead(lane, cx, false); break;
                        case 'o': SpawnOverhead(lane, cx, true); break;
                        case 'M':                                // molehills only on grass; elsewhere the same jump over a low obstacle
                            if (LaneSurface(lane, cx) == Surface.Grass) SpawnMole(lane, cx, 0f);
                            else SpawnLow(lane, cx, false);
                            break;
                        case 'C': SpawnCat(lane, cx, 0f); break;
                        case 'R': SpawnCrow(lane, cx); break;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- spawners

        public struct PropDef
        {
            public string Model; public float Height; public Vector3 Fallback; public Color Color;
            public float MaxDepth;                       // squeezed along Z to fit the lane (0 = keep)
            public PropDef(string m, float h, Vector3 fb, Color c, float maxDepth = 0f) { Model = m; Height = h; Fallback = fb; Color = c; MaxDepth = maxDepth; }
        }

        static readonly PropDef Crate = new PropDef("crate", 0.6f, new Vector3(0.6f, 0.6f, 0.6f), new Color(0.6f, 0.42f, 0.25f));
        static readonly PropDef TrashCan = new PropDef("trashcan", 0.85f, new Vector3(0.55f, 0.85f, 0.55f), new Color(0.6f, 0.65f, 0.7f));
        static readonly PropDef Hydrant = new PropDef("hydrant", 0.6f, new Vector3(0.35f, 0.6f, 0.35f), new Color(0.85f, 0.15f, 0.12f));
        static readonly PropDef Bush = new PropDef("bush", 0.6f, new Vector3(0.8f, 0.6f, 0.8f), new Color(0.25f, 0.55f, 0.25f));
        static readonly PropDef Bench = new PropDef("bench", 0.9f, new Vector3(1.6f, 0.9f, 0.5f), new Color(0.6f, 0.4f, 0.25f));
        static readonly PropDef Sawhorse = new PropDef("sawhorse", 0.8f, new Vector3(1.4f, 0.8f, 0.4f), new Color(0.9f, 0.3f, 0.25f));
        static readonly PropDef Car = new PropDef("car", 1.5f, new Vector3(3.4f, 1.5f, 1.6f), new Color(0.75f, 0.75f, 0.78f));
        static readonly PropDef Dumpster = new PropDef("dumpster", 1.3f, new Vector3(1.7f, 1.3f, 1.2f), new Color(0.2f, 0.5f, 0.25f));
        static readonly PropDef LampDef = new PropDef("lamp", 3.2f, new Vector3(0.2f, 3.2f, 0.2f), new Color(0.1f, 0.1f, 0.12f));
        static readonly PropDef Molehill = new PropDef("molehill", 0.3f, new Vector3(0.7f, 0.25f, 0.7f), new Color(0.4f, 0.28f, 0.17f));
        static readonly PropDef BoneDef = new PropDef("bone", 0.2f, new Vector3(0.45f, 0.15f, 0.15f), new Color(0.95f, 0.92f, 0.8f));
        // crouch-through obstacles: taller than the jump and solid on top, so the only way past is to crouch (or switch)
        static readonly PropDef Pipe = new PropDef("pipe", 1.5f, new Vector3(2.6f, 1.5f, 1.5f), new Color(0.6f, 0.62f, 0.65f));
        static readonly PropDef CardBox = new PropDef("box", 1.45f, new Vector3(2.2f, 1.45f, 1.4f), new Color(0.65f, 0.48f, 0.3f));
        static readonly PropDef PlayTunnel = new PropDef("tunnel", 1.5f, new Vector3(2.6f, 1.5f, 1.4f), new Color(0.9f, 0.4f, 0.4f));
        static readonly PropDef FallenTree = new PropDef("tree", 1.5f, new Vector3(1.2f, 1.5f, 1.8f), new Color(0.45f, 0.3f, 0.2f), maxDepth: 1.8f);

        static readonly Color[] CarColors =
        {
            new Color(0.95f, 0.35f, 0.3f), new Color(0.35f, 0.55f, 0.95f), new Color(1f, 0.85f, 0.35f),
            new Color(0.45f, 0.8f, 0.5f), new Color(0.95f, 0.95f, 0.95f),
        };

        T Take<T>(string key, System.Func<GameObject> make) where T : Entity
        {
            if (_pool.TryGetValue(key, out var st) && st.Count > 0)
            {
                var e = (T)st.Pop();
                e.gameObject.SetActive(true);
                return e;
            }
            var go = make();
            go.transform.SetParent(_root, false);
            var ent = go.GetComponent<T>() ?? go.AddComponent<T>();
            ent.PoolKey = key;
            ent.Level = this;
            return ent;
        }

        void Recycle(Entity e)
        {
            e.gameObject.SetActive(false);
            if (string.IsNullOrEmpty(e.PoolKey)) { Destroy(e.gameObject); return; }
            if (!_pool.TryGetValue(e.PoolKey, out var st)) _pool[e.PoolKey] = st = new Stack<Entity>();
            st.Push(e);
        }

        Vector3 At(int lane, float x, float y) => new Vector3(x, y, _cfg.LaneZ(lane));

        /// <summary>A random colour variant for props that have them (null = original).</summary>
        string Variant(PropDef d) => Art.Variants.TryGetValue(d.Model, out var v) ? v[RI(0, v.Length)] : null;

        Solid PlaceSolid(PropDef d, string key, int lane, float x, float y, float clearance = 0f, float length = 0f,
                         float heightOverride = 0f, string variant = null)
        {
            if (variant != null) key += "_" + variant;
            var s = Take<Solid>(key, () =>
            {
                var go = Art.Spawn(d.Model, heightOverride > 0 ? heightOverride : d.Height, d.Color, d.Fallback, null, variant);
                if (d.MaxDepth > 0f && Art.Bounds(go).size.z > d.MaxDepth)
                {
                    // squeeze the (unrotated) holder along world Z; its pivot is the bottom centre, so it stays put
                    float k = d.MaxDepth / Art.Bounds(go).size.z;
                    go.transform.localScale = new Vector3(1, 1, k);
                }
                if (length > 0f)
                {
                    var m = go.transform.GetChild(0);
                    float k = length / Mathf.Max(0.01f, Art.Bounds(go).size.x);
                    m.localScale = Vector3.Scale(m.localScale, new Vector3(k, 1, 1));
                    Art.Fit(go.transform, m, 0f);
                }
                var b = Art.Bounds(go);
                var sol = go.AddComponent<Solid>();
                sol.HalfX = b.extents.x * 0.92f;
                sol.Top = b.size.y;
                sol.Bottom = clearance;
                sol.HalfZ = Mathf.Min(b.extents.z, 0.7f);
                return go;
            });
            s.transform.position = At(lane, x, y);
            s.Lane = lane;
            s.OnSpawn();
            Solids.Add(s);
            return s;
        }

        void SpawnLow(int lane, float x, bool arc)
        {
            // by surface: hydrants only on cobble, bushes only on grass
            var surface = LaneSurface(lane, x);
            PropDef d;
            float r = R();
            if (surface == Surface.Asphalt) d = r < 0.5f ? Crate : TrashCan;
            else if (surface == Surface.Cobble) d = r < 0.4f ? Hydrant : r < 0.7f ? TrashCan : Crate;
            else d = r < 0.7f ? Bush : Crate;
            var s = PlaceSolid(d, "low_" + d.Model, lane, x, 0f, variant: Variant(d));
            if (arc)
            {
                float top = s.Top;
                for (int i = -2; i <= 2; i++)
                {
                    float k = 1f - (i / 2.6f) * (i / 2.6f);
                    SpawnBone(lane, x + i * 1.1f, 0.45f + (top + 0.25f) * k);
                }
            }
        }

        void SpawnOverhead(int lane, float x, bool bones)
        {
            var surface = LaneSurface(lane, x);
            var d = surface == Surface.Asphalt ? Pipe : surface == Surface.Cobble ? (R() < 0.5f ? CardBox : Pipe)
                  : (R() < 0.5f ? PlayTunnel : FallenTree);
            var s = PlaceSolid(d, "over_" + d.Model, lane, x, 0f, clearance: 0.42f);
            s.Walkable = false;                                              // jumping onto it is a crash, not a landing
            if (bones) for (int i = -1; i <= 1; i++) SpawnBone(lane, x + i * 0.9f, 0.22f);
        }

        void SpawnBlockers(int lane, float sx, float ex)
        {
            // by surface: cars on asphalt (a dumpster where a car does not fit), dumpsters on cobble, hedges on grass
            var surface = LaneSurface(lane, sx);
            float x = sx + 0.2f;
            while (ex - x > 0.5f)
            {
                float room = ex - x;
                if (surface == Surface.Asphalt && room >= 3.6f)
                {
                    var s = PlaceSolid(Car, "car_" + RI(0, CarColors.Length), lane, 0, 0f);
                    float len = s.HalfX * 2 / 0.92f;
                    s.transform.position = At(lane, x + len / 2, 0f);
                    TintOnce(s, CarColors[int.Parse(s.PoolKey.Substring(4))]);
                    x += len + 0.3f;
                }
                else if (surface == Surface.Grass)
                {
                    // trimmed hedge sections (up to 2.5 m each) side by side
                    float len = Mathf.Max(1f, Mathf.Round(Mathf.Min(room - 0.2f, 2.5f) * 2f) / 2f);
                    SpawnHedge(lane, x + len / 2, len, HedgeVariants[RI(0, HedgeVariants.Length)]);
                    x += len + 0.05f;
                }
                else
                {
                    var s = PlaceSolid(Dumpster, "dumpster", lane, 0, 0f, variant: Variant(Dumpster));
                    float len = s.HalfX * 2 / 0.92f;
                    s.transform.position = At(lane, x + len / 2, 0f);
                    x += len + 0.25f;
                }
            }
        }

        static readonly string[] HedgeVariants = { null, "darkgreen", "lightgreen" };
        static readonly Dictionary<string, Material> HedgeMats = new Dictionary<string, Material>();

        static Material HedgeMat(string variant)
        {
            string name = "tex_hedge" + (variant == null ? "" : "_" + variant);
            if (!HedgeMats.TryGetValue(name, out var m) || m == null)
            {
                m = new Material(Art.Mat("Lit"));
                var tex = Art.Texture(name) ?? Art.Texture("tex_hedge");
                if (tex != null) { m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); }
                else { m.color = new Color(0.25f, 0.5f, 0.22f); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.color); }
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
                HedgeMats[name] = m;
            }
            return m;
        }

        /// <summary>A trimmed hedge block (blocker): a leafy box with a slightly inset top layer for a softer silhouette.</summary>
        Solid SpawnHedge(int lane, float x, float len, string variant)
        {
            const float H = 1.55f, D = 1.1f;          // above the jump's reach: a blocker, not a step
            var s = Take<Solid>("hedge_" + len + "_" + variant, () =>
            {
                var go = new GameObject("hedge");
                var mat = HedgeMat(variant);
                MeshUtil.Make("body", MeshUtil.Box(new Vector3(len, H - 0.12f, D), 0.9f), mat, go.transform);
                var top = MeshUtil.Make("top", MeshUtil.Box(new Vector3(len - 0.12f, 0.12f, D - 0.12f), 0.9f), mat, go.transform);
                top.transform.localPosition = new Vector3(0, H - 0.12f, 0);
                var sol = go.AddComponent<Solid>();
                sol.HalfX = len / 2 * 0.95f; sol.Top = H; sol.Bottom = 0; sol.HalfZ = D / 2;
                return go;
            });
            s.transform.position = At(lane, x, 0f);
            s.Lane = lane;
            s.OnSpawn();
            Solids.Add(s);
            return s;
        }

        static Material _capMat;
        /// <summary>Cobble for platform tops, drawn with the props (the ground copy draws before the path lines).</summary>
        static Material CapMaterial
        {
            get
            {
                if (_capMat == null) _capMat = new Material(Art.Mat("Cobble")) { renderQueue = 2000 };
                return _capMat;
            }
        }

        readonly HashSet<Solid> _tinted = new HashSet<Solid>();
        void TintOnce(Solid s, Color c)
        {
            if (_tinted.Contains(s)) return;
            _tinted.Add(s);
            Art.Tint(s.gameObject, c);
        }

        void SpawnPlatform(int lane, float sx, float ex, string row, int c0, int c1, float x0)
        {
            float len = ex - sx - 0.3f;
            int key = Mathf.RoundToInt(len * 2);
            var s = Take<Solid>("plat_" + key, () =>
            {
                var go = new GameObject("platform");
                var box = MeshUtil.Make("brick", MeshUtil.Box(new Vector3(len, 0.6f, 1.3f), 1.2f), Art.Mat("Brick"), go.transform);
                var cap = MeshUtil.Make("cap", MeshUtil.Box(new Vector3(len + 0.1f, 0.06f, 1.4f), 1.2f), CapMaterial, go.transform);
                cap.transform.localPosition = new Vector3(0, 0.6f, 0);
                var sol = go.AddComponent<Solid>();
                sol.HalfX = len / 2; sol.Top = 0.66f; sol.Bottom = 0; sol.HalfZ = 0.65f;
                return go;
            });
            s.transform.position = At(lane, (sx + ex) / 2, 0f);
            s.Lane = lane;
            s.OnSpawn();
            Solids.Add(s);
            float top = s.Top;
            for (int c = c0; c <= c1; c++)
            {
                float cx = x0 + (c + 0.5f) * _cfg.slotLength;
                switch (row[c])
                {
                    case 'p': SpawnBone(lane, cx, top + 0.45f); break;
                    case '^': break;                             // no molehills on platforms (not grass)
                    case '&': SpawnCat(lane, cx, top); break;
                }
            }
        }

        void SpawnBone(int lane, float x, float y)
        {
            var b = Take<BonePickup>("bone", () =>
            {
                var go = new GameObject("bone");
                var spin = new GameObject("spin").transform;
                spin.SetParent(go.transform, false);
                spin.localPosition = new Vector3(0, 0.1f, 0);
                var art = Art.Spawn(BoneDef.Model, BoneDef.Height, BoneDef.Color, BoneDef.Fallback, spin);
                art.transform.localPosition = new Vector3(0, -0.1f, 0);
                Art.Halo(go.transform, 0.1f, 0.65f, new Color(1f, 0.85f, 0.55f, 0.45f));     // warm glow behind it
                return go;
            });
            b.transform.position = At(lane, x, y - 0.1f);
            b.Lane = lane;
            b.OnSpawn();
            Bones.Add(b);
        }

        void SpawnPowerUp(int lane, float x)
        {
            int type;
            do type = RI(0, 4); while (type == _lastPowerUp);
            _lastPowerUp = type;
            SpawnPowerUp(lane, x, (PowerUpType)type);
        }

        void SpawnPowerUp(int lane, float x, PowerUpType t)
        {
            int type = (int)t;
            var p = Take<PowerUpPickup>("powerup_" + type, () => PowerUpVisual(t));
            p.Type = t;
            p.transform.position = At(lane, x, 0.6f);
            p.Lane = lane;
            p.OnSpawn();
            PowerUps.Add(p);
        }

        static GameObject PowerUpVisual(PowerUpType t)
        {
            var go = new GameObject("powerup_" + t);
            var bubble = MeshUtil.Make("bubble", Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), new Material(Art.Mat("Bubble")), go.transform);
            bubble.transform.localScale = Vector3.one * 0.6f;
            var c = PowerUpInfo.Color(t);
            var bm = bubble.GetComponent<Renderer>().sharedMaterial;
            bm.color = new Color(c.r, c.g, c.b, 0.35f);
            if (bm.HasProperty("_BaseColor")) bm.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.35f));
            var icon = Art.Sprite(PowerUpInfo.IconName(t));
            var q = MeshUtil.Primitive(PrimitiveType.Quad);
            q.transform.SetParent(go.transform, false);
            q.transform.localScale = Vector3.one * 0.4f;
            var qm = new Material(Art.Mat("Icon"));
            if (icon != null) { qm.mainTexture = icon.texture; if (qm.HasProperty("_BaseMap")) qm.SetTexture("_BaseMap", icon.texture); }
            else { qm.color = c; if (qm.HasProperty("_BaseColor")) qm.SetColor("_BaseColor", c); }
            q.GetComponent<Renderer>().sharedMaterial = qm;
            q.AddComponent<FaceCamera>();
            return go;
        }

        void SpawnMole(int lane, float x, float surface)
        {
            var m = Take<Mole>("mole", () =>
            {
                var go = new GameObject("mole");
                var model = Art.Spawn("mole", 0.32f, new Color(0.25f, 0.25f, 0.3f), new Vector3(0.45f, 0.3f, 0.3f), go.transform);
                Art.Halo(model.transform, 0.2f, 0.9f, EnemyHalo, 0f);       // on the turning model: centred, rises with it
                var mole = go.AddComponent<Mole>();
                mole.Model = model.transform;
                mole.Hill = MoleHint(go.transform).transform;           // only a faint patch of turned soil shows
                mole.Clips = ClipPlayer.Attach(model, "mole");
                return go;
            });
            m.transform.position = At(lane, x, surface);
            m.Surface = surface;
            m.Lane = lane;
            m.OnSpawn();
            Enemies.Add(m);
        }

        /// <summary>Cool glow behind enemies, so they read against the night.</summary>
        static readonly Color EnemyHalo = new Color(0.65f, 0.75f, 1f, 0.4f);
        static Material _moleHintMat;

        /// <summary>A faint ground decal of turned soil (Textures/mole_hint, tools/make_decal.py), centred on the
        /// object, lit like the ground around it.</summary>
        static GameObject MoleHint(Transform parent)
        {
            var go = new GameObject("molehint");
            if (parent) go.transform.SetParent(parent, false);
            if (_moleHintMat == null)
            {
                _moleHintMat = new Material(Art.Mat("LitFade"));
                var tex = Art.Texture("mole_hint");
                if (tex != null) { tex.wrapMode = TextureWrapMode.Clamp; _moleHintMat.mainTexture = tex; if (_moleHintMat.HasProperty("_BaseMap")) _moleHintMat.SetTexture("_BaseMap", tex); }
                _moleHintMat.color = Color.white;
                if (_moleHintMat.HasProperty("_BaseColor")) _moleHintMat.SetColor("_BaseColor", Color.white);
            }
            var q = MeshUtil.Make("decal", MeshUtil.GroundOnce(0.9f, 0.9f), _moleHintMat, go.transform);
            q.transform.localPosition = new Vector3(-0.45f, 0.01f, -0.45f);
            return go;
        }

        void SpawnCat(int lane, float x, float surface)
        {
            var coats = Art.Variants["cat"];
            var coat = coats[RI(0, coats.Length)];
            var c = Take<Cat>("cat_" + coat, () =>
            {
                var go = new GameObject("cat");
                var model = Art.Spawn("cat", 0.42f, new Color(0.55f, 0.55f, 0.58f), new Vector3(0.6f, 0.38f, 0.25f), go.transform, coat);
                Art.Halo(go.transform, 0.25f, 1.0f, EnemyHalo);
                var cat = go.AddComponent<Cat>();
                cat.Model = model.transform;
                cat.Clips = ClipPlayer.Attach(model, "cat");
                return go;
            });
            c.transform.position = At(lane, x, surface);
            c.Surface = surface;
            c.Lane = lane;
            c.OnSpawn();
            Enemies.Add(c);
        }

        void SpawnCrow(int lane, float x)
        {
            var c = Take<Crow>("crow", () =>
            {
                var go = new GameObject("crow");
                var model = Art.Spawn("crow", 0.35f, new Color(0.08f, 0.08f, 0.1f), new Vector3(0.6f, 0.3f, 0.9f), go.transform);
                Art.Halo(go.transform, 0.2f, 1.1f, EnemyHalo);
                var crow = go.AddComponent<Crow>();
                crow.Model = model.transform;
                crow.Clips = ClipPlayer.Attach(model, "crow");
                return go;
            });
            c.transform.position = At(lane, x, _cfg.crowHeight);
            c.Surface = 0f;
            c.Lane = lane;
            c.OnSpawn();
            Enemies.Add(c);
        }

        // ---------------------------------------------------------------- ground and set dressing

        void SpawnGroundTile(float x)
        {
            var biome = BiomeAt(x + 0.01f);
            var cur = LaneSurfaces(x + 0.01f);
            var prev = LaneSurfaces(x - 0.01f);        // differs only on the first tile of a new stretch
            string key = "ground_" + (int)biome + "_" + (int)cur[0] + (int)cur[1] + (int)cur[2] + "_" + (int)prev[0] + (int)prev[1] + (int)prev[2];
            var t = Take<Decor>(key, () => GroundTile(biome, cur, prev));
            t.transform.position = new Vector3(x, 0, 0);
            _decor.Add(t);
            Dress(biome, x);
            _groundCursor += TileLength;
        }

        static readonly Dictionary<string, Material> TransitionMats = new Dictionary<string, Material>();

        /// <summary>Lane-sized patch fading surface a into b (textures made by tools/make_transitions.py).</summary>
        static Material TransitionMat(Surface a, Surface b)
        {
            string name = "trans_" + a.ToString().ToLowerInvariant() + "_" + b.ToString().ToLowerInvariant();
            if (!TransitionMats.TryGetValue(name, out var m) || m == null)
            {
                m = new Material(Art.Mat(SurfaceMat[(int)b]));
                var tex = Art.Texture(name);
                if (tex != null)
                {
                    m.mainTexture = tex;
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                }
                TransitionMats[name] = m;
            }
            return m;
        }

        /// <summary>Three parallel paths with the stretch's surfaces (a lane that changes surface fades over the first
        /// 4 m), divided only by soft glowing lines; the strip behind the far path and the thin one in front change
        /// with the biome.</summary>
        GameObject GroundTile(Biome b, Surface[] cur, Surface[] prev)
        {
            var go = new GameObject("ground_" + b);
            var cfg = GameConfig.I;
            for (int lane = 0; lane < 3; lane++)
            {
                float z0 = cfg.LaneZ(lane) - cfg.laneSpacing / 2;
                var mat = Art.Mat(SurfaceMat[(int)cur[lane]]);
                if (prev[lane] != cur[lane])
                {
                    var tr = MeshUtil.Make("path_blend" + lane, MeshUtil.GroundOnce(TransitionLength, cfg.laneSpacing), TransitionMat(prev[lane], cur[lane]), go.transform);
                    tr.transform.localPosition = new Vector3(0, 0, z0);
                    var rest = MeshUtil.Make("path" + lane, MeshUtil.Ground(TileLength - TransitionLength, cfg.laneSpacing, 2.5f, TransitionLength), mat, go.transform);
                    rest.transform.localPosition = new Vector3(TransitionLength, 0, z0);
                }
                else
                {
                    var p = MeshUtil.Make("path" + lane, MeshUtil.Ground(TileLength, cfg.laneSpacing, 2.5f), mat, go.transform);
                    p.transform.localPosition = new Vector3(0, 0, z0);
                }
            }
            // (the glowing lines between the paths are one long set that follows the dog: LaneLines)
            string back = b == Biome.Backyard ? "Grass" : "Cobble";     // never asphalt: lamps and benches stand here
            var backMat = new Material(Art.Mat(back));            // darker than the paths, so it never reads as a fourth lane
            var dim = new Color(0.55f, 0.55f, 0.6f);
            backMat.color = dim;
            if (backMat.HasProperty("_BaseColor")) backMat.SetColor("_BaseColor", dim);
            // the walk starts behind the curb (not over it: the curb's top is at the same height and would flicker)
            var bw = MeshUtil.Make("walk_back", MeshUtil.Ground(TileLength, BackWalkDepth - CurbDepth, 2.5f), backMat, go.transform);
            bw.transform.localPosition = new Vector3(0, 0.12f, RoadHalfWidth + CurbDepth);
            var fw = MeshUtil.Make("walk_front", MeshUtil.Ground(TileLength, FrontWalkDepth, 2.5f), Art.Mat("Curb"), go.transform);
            fw.transform.localPosition = new Vector3(0, 0.06f, -RoadHalfWidth - FrontWalkDepth);
            var curbB = MeshUtil.Make("curb_b", MeshUtil.Box(new Vector3(TileLength, 0.12f, CurbDepth), 1f), Art.Mat("Curb"), go.transform);
            curbB.transform.localPosition = new Vector3(TileLength / 2, 0, RoadHalfWidth + CurbDepth / 2);
            return go;
        }

        void Dress(Biome b, float x)
        {
            // one lamp every second tile, on the far sidewalk
            if (Mathf.RoundToInt(x / TileLength) % 2 == 0)
            {
                var lamp = Take<Lamp>("lamp", () => Lamp.Make(LampDef));
                lamp.transform.position = new Vector3(x + R(2f, 8f), 0.12f, RoadHalfWidth + 0.7f);
                lamp.OnSpawn();
                _lamps.Add(lamp);
            }
            // far sidewalk props (cobble in town, grass in backyards): hydrants only on cobble, bushes and molehills only
            // on grass, lamps and benches never on asphalt
            int n = RI(1, 3);
            for (int i = 0; i < n; i++)
            {
                PropDef d;
                float r = R();
                if (b == Biome.Street) d = r < 0.35f ? Hydrant : r < 0.7f ? TrashCan : Bench;
                else if (b == Biome.Alley) d = r < 0.35f ? Dumpster : r < 0.7f ? Crate : TrashCan;
                else d = r < 0.5f ? Bush : r < 0.75f ? Molehill : Bench;
                SpawnDecor(d, new Vector3(x + R(0.5f, 9.5f), 0.12f, RoadHalfWidth + R(1.2f, 1.8f)), R() < 0.5f ? 0 : 180);
                if (b == Biome.Alley && d.Model == "crate" && R() < 0.5f)
                    SpawnDecor(Crate, new Vector3(x + R(0.5f, 9.5f), 0.12f, RoadHalfWidth + R(1.2f, 1.8f)), R(0, 360));
            }
        }

        void SpawnDecor(PropDef d, Vector3 pos, float yaw)
        {
            if (d.Model == Molehill.Model)
            {
                // molehills are only a faint patch of turned soil now (the same one a hidden mole sits under)
                var h = Take<Decor>("decor_molehint", () => MoleHint(null));
                h.transform.position = pos;
                h.transform.rotation = Quaternion.Euler(0, yaw, 0);
                _decor.Add(h);
                return;
            }
            var variant = Variant(d);
            var e = Take<Decor>("decor_" + d.Model + "_" + variant, () => Art.Spawn(d.Model, d.Height, d.Color, d.Fallback, null, variant));
            e.transform.position = pos;
            e.transform.rotation = Quaternion.Euler(0, yaw, 0);
            _decor.Add(e);
        }

        // ---------------------------------------------------------------- prop showcase (test level)

        /// <summary>Test level: every prop, enemy, platform, bone and power-up type one after another along a lane,
        /// each with a floating name label, starting at x; the normal level continues after it.</summary>
        int _showLane = 1;

        public void BuildShowcase(int lane, float x)
        {
            _showLane = lane;
            void Next(float length) => x += Mathf.Max(4f, length + 3f);
            Solid Prop(PropDef d, string label, float clearance = 0f, bool walkable = true)
            {
                var s = PlaceSolid(d, "show_" + d.Model, lane, x, 0f, clearance);
                s.transform.position = At(lane, x + s.HalfX, 0f);
                s.Walkable = walkable;
                Label(label, x + s.HalfX, s.Top);
                Next(s.HalfX * 2);
                return s;
            }
            void DecorItem(PropDef d, string label)
            {
                SpawnDecor(d, At(lane, x, 0f), 0);
                Label(label, x, d.Height);
                Next(1f);
            }

            Prop(Crate, "crate"); Prop(TrashCan, "trashcan"); Prop(Hydrant, "hydrant"); Prop(Bush, "bush");
            Prop(Pipe, "pipe (crouch)", 0.42f, false); Prop(CardBox, "box (crouch)", 0.42f, false);
            Prop(PlayTunnel, "tunnel (crouch)", 0.42f, false); Prop(FallenTree, "fallen tree (crouch)", 0.42f, false);
            var car = Prop(Car, "car");
            TintOnce(car, CarColors[0]);
            Prop(Dumpster, "dumpster");
            SpawnHedge(lane, x + 1.25f, 2.5f, null); SpawnHedge(lane, x + 3.8f, 2.5f, "darkgreen");
            Label("hedge", x + 2.5f, 1.3f); Next(5f);
            SpawnPlatform(lane, x, x + 6f, "PpP", 0, 2, x); Label("platform", x + 3f, 0.66f); Next(6f);
            SpawnMole(lane, x, 0f); Label("mole", x, 0.4f); Next(1f);
            SpawnCat(lane, x, 0f); Label("cat", x, 0.5f); Next(1f);
            SpawnCrow(lane, x); Label("crow", x, _cfg.crowHeight + 0.4f); Next(1f);
            DecorItem(Bench, "bench (decor)"); DecorItem(Sawhorse, "sawhorse (decor)"); DecorItem(Molehill, "molehill (decor)");
            DecorItem(LampDef, "lamp (decor)");
            for (int i = 0; i < 4; i++) SpawnBone(lane, x + i * 0.8f, 0.45f);
            Label("bones", x + 1.2f, 0.6f); Next(3f);
            for (int i = 0; i < 4; i++)
            {
                var t = (PowerUpType)i;
                SpawnPowerUp(lane, x, t);
                Label(PowerUpInfo.Label(t), x, 1.3f);
                Next(1f);
            }
            _cursor = Mathf.Max(_cursor, x + 10f);
            _nextPowerUp = _cursor + 120f;
        }

        // ---------------------------------------------------------------- debug levels

        public enum DebugLevel { None, Enemies, Obstacles, PowerUps }
        [HideInInspector] public DebugLevel DebugMode;

        /// <summary>Enemy debug level: one enemy type per lane (mole far on grass, cat middle, crow near), repeated
        /// forever and staggered so only one lane is in danger at a time; no props or power-ups.</summary>
        static readonly Surface[] EnemySurfaces = { Surface.Grass, Surface.Cobble, Surface.Asphalt };
        const float DebugGap = 12f;                      // between one enemy and the next (in the next lane)
        bool _debugLabelled;

        void SpawnEnemyDebugSection()
        {
            float x = _cursor + DebugGap;
            SpawnMole(0, x, 0f);
            SpawnCat(1, x + DebugGap, 0f);
            SpawnCrow(2, x + DebugGap * 2);
            if (!_debugLabelled)
            {
                _debugLabelled = true;
                Label("mole", x, 0.4f, 0);
                Label("cat", x + DebugGap, 0.5f, 1);
                Label("crow", x + DebugGap * 2, _cfg.crowHeight + 0.4f, 2);
            }
            for (int i = 0; i < 3; i++) SpawnBone(i, x + DebugGap * (i + 1.5f), 0.45f);   // something to collect between them
            _cursor += DebugGap * 3;
            _nextPowerUp = float.MaxValue;
        }

        /// <summary>Obstacle debug level: cobble far, asphalt middle, grass near; each lane cycles through every prop
        /// its surface allows, evenly spaced and staggered between lanes, each with an arrow saying how to pass it
        /// (up = jump, down = crouch, sideways = blocker, switch lanes).</summary>
        static readonly Surface[] ObstacleSurfaces = { Surface.Cobble, Surface.Asphalt, Surface.Grass };
        enum Pass { Jump, Crouch, Switch }
        static readonly (PropDef d, Pass pass)[][] SurfaceProps =       // by Surface; default PropDef = hedge
        {
            new[] { (Crate, Pass.Jump), (TrashCan, Pass.Jump), (Pipe, Pass.Crouch), (Car, Pass.Switch) },
            new[] { (Hydrant, Pass.Jump), (TrashCan, Pass.Jump), (Crate, Pass.Jump), (CardBox, Pass.Crouch), (Pipe, Pass.Crouch),
                    (Dumpster, Pass.Switch) },
            new[] { (Bush, Pass.Jump), (Crate, Pass.Jump), (PlayTunnel, Pass.Crouch), (FallenTree, Pass.Crouch), (default(PropDef), Pass.Switch) },
        };
        const float ObstacleGap = 15f;                   // between props in a lane; lanes are offset by a third of it
        readonly int[] _debugNext = new int[3];

        void SpawnObstacleDebugSection()
        {
            float x0 = _cursor + 6f;
            for (int lane = 0; lane < 3; lane++)
            {
                var list = SurfaceProps[(int)ObstacleSurfaces[lane]];
                var (d, pass) = list[_debugNext[lane]++ % list.Length];
                float x = x0 + lane * ObstacleGap / 3f;
                SpawnPassProp(d, pass, lane, x);
                if (_debugNext[lane] == 1) Label(ObstacleSurfaces[lane].ToString(), x - 4f, 0f, lane);
            }
            _cursor += ObstacleGap;
            _nextPowerUp = float.MaxValue;
        }

        /// <summary>One prop of the debug levels (default PropDef = hedge), with its how-to-pass arrow above it.</summary>
        void SpawnPassProp(PropDef d, Pass pass, int lane, float x)
        {
            Solid s;
            if (pass == Pass.Crouch)
            {
                s = PlaceSolid(d, "over_" + d.Model, lane, x, 0f, clearance: 0.42f);
                s.Walkable = false;
            }
            else if (d.Model == null) s = SpawnHedge(lane, x, 2.5f, HedgeVariants[RI(0, HedgeVariants.Length)]);
            else if (d.Model == "car")
            {
                int c = RI(0, CarColors.Length);
                s = PlaceSolid(Car, "car_" + c, lane, x, 0f);
                TintOnce(s, CarColors[c]);
            }
            else s = PlaceSolid(d, "low_" + d.Model, lane, x, 0f, variant: Variant(d));
            PassArrow(pass, lane, x, s.Top);
        }

        /// <summary>Power-up debug level (same lanes as the obstacle one): the four power-ups in turn, each laid across
        /// all three lanes so it is always picked up, followed by a stretch of hazards suited to trying it (one every
        /// 5 m, rotating through the lanes, as passable as the obstacle level without the power-up). The stretch ends
        /// before the power-up can run out, then a short empty run-up leads to the next one.</summary>
        enum Hazard { Jump, Crouch, Blocker, Enemy }
        static readonly Hazard[][] PowerUpHazards =     // by PowerUpType: Slow, Invincible, DoubleJump, Fly
        {
            new[] { Hazard.Jump, Hazard.Crouch, Hazard.Enemy },
            new[] { Hazard.Jump, Hazard.Crouch, Hazard.Blocker, Hazard.Enemy },
            new[] { Hazard.Blocker, Hazard.Jump },
            new[] { Hazard.Jump, Hazard.Crouch, Hazard.Blocker, Hazard.Enemy },
        };
        const float HazardGap = 5f;
        int _debugPowerUp;

        void SpawnPowerUpDebugSection()
        {
            var t = (PowerUpType)(_debugPowerUp++ % 4);
            float x = _cursor + 6f;
            for (int lane = 0; lane < 3; lane++) SpawnPowerUp(lane, x, t);
            Label(PowerUpInfo.Label(t), x, 1.3f, 2);

            // hazards over the distance the slowest dog covers before the power-up ends (a little less, to be safe)
            float duration = t == PowerUpType.Fly ? _cfg.flyDuration : t == PowerUpType.Slow ? _cfg.slowDuration
                           : t == PowerUpType.Invincible ? _cfg.invincibleDuration : _cfg.doubleJumpDuration;
            int count = Mathf.FloorToInt(duration * _cfg.startSpeed * 0.8f / HazardGap);
            var kinds = PowerUpHazards[(int)t];
            for (int i = 0; i < count; i++)
            {
                int lane = i % 3;
                float hx = x + 6f + i * HazardGap;
                var kind = kinds[RI(0, kinds.Length)];
                if (kind == Hazard.Enemy)
                {
                    int e = RI(0, ObstacleSurfaces[lane] == Surface.Grass ? 3 : 2);
                    if (e == 0) SpawnCat(lane, hx, 0f);
                    else if (e == 1) SpawnCrow(lane, hx);
                    else SpawnMole(lane, hx, 0f);
                    continue;
                }
                var pass = kind == Hazard.Jump ? Pass.Jump : kind == Hazard.Crouch ? Pass.Crouch : Pass.Switch;
                var options = new List<PropDef>();
                foreach (var (d, p) in SurfaceProps[(int)ObstacleSurfaces[lane]]) if (p == pass) options.Add(d);
                SpawnPassProp(options[RI(0, options.Count)], pass, lane, hx);
            }
            _cursor = x + 6f + count * HazardGap + 10f;
            _nextPowerUp = float.MaxValue;
        }

        static readonly Dictionary<Pass, Material> ArrowMats = new Dictionary<Pass, Material>();
        static Texture2D _arrowTex;

        /// <summary>A camera-facing chevron above a prop: green up (jump), orange down (crouch), red sideways (switch).</summary>
        void PassArrow(Pass pass, int lane, float x, float top)
        {
            var e = Take<Decor>("arrow_" + pass, () =>
            {
                var go = new GameObject("arrow_" + pass);
                go.AddComponent<FaceCamera>();
                var q = MeshUtil.Primitive(PrimitiveType.Quad);
                q.transform.SetParent(go.transform, false);
                q.transform.localScale = Vector3.one * 0.55f;
                q.transform.localRotation = Quaternion.Euler(0, 0, pass == Pass.Jump ? 0 : pass == Pass.Crouch ? 180 : -90);
                if (!ArrowMats.TryGetValue(pass, out var m) || m == null)
                {
                    if (_arrowTex == null) _arrowTex = MeshUtil.Arrow(false);
                    var c = pass == Pass.Jump ? new Color(0.4f, 1f, 0.4f) : pass == Pass.Crouch ? new Color(1f, 0.6f, 0.15f) : new Color(1f, 0.3f, 0.3f);
                    m = new Material(Art.Mat("Icon")) { mainTexture = _arrowTex, color = c };
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", _arrowTex);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    ArrowMats[pass] = m;
                }
                q.GetComponent<Renderer>().sharedMaterial = m;
                return go;
            });
            e.transform.position = At(lane, x, top + 0.5f);
            _decor.Add(e);
        }

        void Label(string text, float x, float top) => Label(text, x, top, _showLane);

        void Label(string text, float x, float top, int lane)
        {
            var e = Take<Decor>("label_" + text, () =>
            {
                var go = new GameObject("label_" + text);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(400, 60);
                rt.localScale = Vector3.one * 0.006f;
                UIKit.Text(go.transform, text.ToUpperInvariant(), 44, UIKit.Cream);
                return go;
            });
            e.transform.position = new Vector3(x, top + 0.35f, _cfg.LaneZ(lane) - 0.9f);   // just in front of the lane
            _decor.Add(e);
        }

        public void ClearAll()
        {
            foreach (var e in Solids) Destroy(e.gameObject);
            foreach (var e in Enemies) Destroy(e.gameObject);
            foreach (var e in Bones) Destroy(e.gameObject);
            foreach (var e in PowerUps) Destroy(e.gameObject);
            Solids.Clear(); Enemies.Clear(); Bones.Clear(); PowerUps.Clear();
        }

        /// <summary>A lamp post with a fake light shaft and a glow pool; a real light is lent by the LightRig.</summary>
        public class Lamp : Entity
        {
            public Vector3 HeadLocal = new Vector3(0, 2.95f, 0);

            public static GameObject Make(PropDef d)
            {
                var go = Art.Spawn(d.Model, d.Height, d.Color, d.Fallback);
                var head = go.transform.position + new Vector3(0, d.Height * 0.9f, 0);
                var cone = MeshUtil.Make("shaft", MeshUtil.Cone(d.Height * 0.88f, 1.6f), Art.Mat("LightCone"), go.transform);
                cone.transform.localPosition = new Vector3(0, d.Height * 0.88f, 0);
                var pool = MeshUtil.Make("pool", MeshUtil.Ground(5f, 5f, 5f), Art.Mat("Glow"), go.transform);
                pool.transform.localPosition = new Vector3(-2.5f, 0.05f, -2.5f - 1.4f);    // clear of road and walk, centred towards the road
                var halo = MeshUtil.Primitive(PrimitiveType.Quad);
                halo.transform.SetParent(go.transform, false);
                halo.transform.localPosition = new Vector3(0, d.Height * 0.9f, 0);
                halo.transform.localScale = Vector3.one * 1.1f;
                halo.GetComponent<Renderer>().sharedMaterial = Art.Mat("Halo");
                halo.AddComponent<FaceCamera>();
                var lamp = go.AddComponent<Lamp>();
                lamp.HeadLocal = new Vector3(0, d.Height * 0.88f, 0);
                return go;
            }
        }
    }

    /// <summary>Billboard towards the main camera; Push moves it that far behind its place as seen from the camera
    /// (glows behind an object, in any view).</summary>
    public class FaceCamera : MonoBehaviour
    {
        public float Push;
        Vector3 _local;
        bool _placed;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            if (Push > 0 && transform.parent != null)
            {
                if (!_placed) { _local = transform.localPosition; _placed = true; }
                var anchor = transform.parent.TransformPoint(_local);
                transform.position = anchor + (anchor - cam.transform.position).normalized * Push;
            }
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
