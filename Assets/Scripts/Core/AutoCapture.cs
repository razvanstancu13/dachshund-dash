using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DachshundDash
{
    /// <summary>Test aid, off unless the player is started with "-ddcapture &lt;folder&gt;": walks through the menu,
    /// highscores and a long invincible run (weaving lanes, jumping, crouching, using every power-up), saving
    /// screenshots, then quits.</summary>
    public class AutoCapture : MonoBehaviour
    {
        string _dir;
        int _n;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = System.Environment.GetCommandLineArgs();
            int q = System.Array.IndexOf(args, "-ddquality");
            if (q >= 0 && q + 1 < args.Length) QualitySettings.SetQualityLevel(int.Parse(args[q + 1]), true);
            int g = System.Array.IndexOf(args, "-ddgif");
            if (g >= 0 && g + 1 < args.Length)
            {
                var rec = new GameObject("GameplayRecorder");
                DontDestroyOnLoad(rec);
                var r = rec.AddComponent<GameplayRecorder>();
                r.Dir = args[g + 1];
                Directory.CreateDirectory(r.Dir);
                return;
            }
            int i = System.Array.IndexOf(args, "-ddcapture");
            if (i < 0 || i + 1 >= args.Length) return;
            var go = new GameObject("AutoCapture");
            DontDestroyOnLoad(go);
            var c = go.AddComponent<AutoCapture>();
            c._dir = args[i + 1];
            Directory.CreateDirectory(c._dir);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"{_n++:00}_{name}.png"));
            yield return null;
        }

        IEnumerator Start()
        {
            if (!Services.Profile.HasName)
            {
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("name_entry");
                Services.Profile.TrySetName("Razvan");
                SceneManager.LoadScene("Menu");
            }
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("menu");
            Services.LastRank = 2;
            Services.ShowHighscoresOnMenu = true;
            SceneManager.LoadScene("Menu");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("highscores");
            Services.LastRank = -1;

            GameRoot.SeedOverride = 7;
            SceneManager.LoadScene("Game");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("countdown");
            var root = FindAnyObjectByType<GameRoot>();
            while (root.Current == GameRoot.State.Countdown) yield return null;

            float t = 0, nextShot = 1.5f, nextAct = 0.5f;
            int step = 0;
            var dog = root.Dog;
            bool usedFly = false, usedSlow = false;
            while (t < 75f)
            {
                dog.GodMode = true;
                if (t > nextAct)
                {
                    nextAct = t + 0.9f;
                    switch (step++ % 6)
                    {
                        case 0: root.Input.PressJump(); break;
                        case 1: root.Input.PressLeft(); break;
                        case 2: root.Input.SetCrouch(true); break;
                        case 3: root.Input.SetCrouch(false); root.Input.PressRight(); break;
                        case 4: root.Input.PressRight(); break;
                        case 5: root.Input.PressLeft(); break;
                    }
                }
                if (!usedFly && t > 20f) { usedFly = true; root.Input.PressCheat(PowerUpType.Fly); }
                if (!usedSlow && t > 32f) { usedSlow = true; root.Input.PressCheat(PowerUpType.Slow); root.Input.PressCheat(PowerUpType.DoubleJump); }
                if (t > nextShot)
                {
                    nextShot = t + 4.5f;
                    yield return Shot($"run_{(int)dog.X}m_{Level.BiomeAt(dog.X, GameConfig.I.biomeLength)}");
                }
                Time.timeScale = Mathf.Max(Time.timeScale, 1.6f);      // cover all three biomes faster
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Time.timeScale = 1f;
            root.Input.PressPause();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("pause");
            root.Input.PressPause();
            dog.PowerUps.StopAll();
            dog.GodMode = false;
            float wait = 0;
            while (root.Current != GameRoot.State.Result && wait < 60f) { wait += Time.unscaledDeltaTime; yield return null; }
            yield return Shot("result");
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }
    }

    /// <summary>Test aid, off unless started with "-ddgif &lt;folder&gt;" (optional "-ddseconds n", "-ddseed n",
    /// "-ddskip metres", "-dddebug Enemies|Obstacles|PowerUps"): plays a
    /// normal run with the autopilot and saves every second frame at a fixed 30 fps game clock (15 fps frames, however
    /// slow the capture), for turning into a GIF; then quits. God mode is on only as a safety net.</summary>
    public class GameplayRecorder : MonoBehaviour
    {
        public string Dir;

        static int Arg(string name, int fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) ? v : fallback;
        }

        IEnumerator Start()
        {
            if (!Services.Profile.HasName) Services.Profile.TrySetName("Razvan");
            GameRoot.SeedOverride = Arg("-ddseed", 11);
            // "-dddebug Enemies|Obstacles|PowerUps": record a debug level instead of a normal run
            var args = System.Environment.GetCommandLineArgs();
            int d = System.Array.IndexOf(args, "-dddebug");
            if (d >= 0 && d + 1 < args.Length && System.Enum.TryParse(args[d + 1], true, out Level.DebugLevel lvl))
                GameRoot.PendingDebug = lvl;
            SceneManager.LoadScene("Game");
            GameRoot root = null;
            while (root == null || root.Dog == null || root.Input == null) { yield return null; root = FindAnyObjectByType<GameRoot>(); }
            var pilot = root.gameObject.AddComponent<AutoPilot>();
            pilot.Root = root;
            Time.captureFramerate = 30;
            while (root.Current == GameRoot.State.Countdown) yield return null;   // the clip starts at GO!
            float skip = Arg("-ddskip", 0);                                       // or this far into the run
            while (root.Dog.X < skip) { root.Dog.GodMode = true; yield return null; }
            int frames = Arg("-ddseconds", 30) * 30, n = 0;
            for (int f = 0; f < frames; f++)
            {
                root.Dog.GodMode = true;
                yield return new WaitForEndOfFrame();
                if (f % 2 == 0) ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"f{n++:0000}.png"));
                yield return null;
            }
            Time.captureFramerate = 0;
            yield return new WaitForSecondsRealtime(1f);      // let the last screenshot write
            Application.Quit();
        }
    }

    /// <summary>A simple player for recordings: jumps low props, molehills and onto platforms, crouches under
    /// overhead props and diving crows, and changes lanes around blockers and cats (and towards a power-up ahead).</summary>
    public class AutoPilot : MonoBehaviour
    {
        public GameRoot Root;
        float _laneCooldown;
        readonly System.Collections.Generic.HashSet<Entity> _knocked = new System.Collections.Generic.HashSet<Entity>();

        void Update()
        {
            if (Root == null || Root.Current != GameRoot.State.Running) return;
            var dog = Root.Dog;
            var level = Root.Level;
            // report what god mode had to knock away (= a mistake of the pilot)
            foreach (var s in level.Solids)
                if (s.Knocked && _knocked.Add(s)) Debug.Log($"[PILOT] hit {s.name} lane {s.Lane} at {s.X:0.0} (dog lane {dog.TargetLane}, y {dog.transform.position.y:0.00}, t {Time.time:0.0})");
            foreach (var e in level.Enemies)
                if (e.Knocked && _knocked.Add(e)) Debug.Log($"[PILOT] hit {e.name} lane {e.Lane} at {e.X:0.0} (dog lane {dog.TargetLane}, y {dog.transform.position.y:0.00}, t {Time.time:0.0})");
            var input = Root.Input;
            var cfg = GameConfig.I;
            float x = dog.X, v = Mathf.Max(dog.Speed, 1f);
            float air = 2f * cfg.JumpVelocity / cfg.gravity;        // time in the air for a plain jump
            int lane = dog.TargetLane;
            _laneCooldown -= Time.deltaTime;

            bool jump = false, crouch = false, avoid = false;
            float dogY = dog.transform.position.y;
            foreach (var s in level.Solids)
            {
                if (s.Lane != lane || s.Knocked) continue;
                var b = s.Box;
                float front = b.Min.x - (x + cfg.dogLength * 0.5f), back = b.Max.x - (x - cfg.dogLength * 0.5f);
                if (back < 0 || front > v * 1.6f + 2f || b.Max.y <= dogY + 0.05f) continue;      // behind, far, or underfoot
                float rise = b.Max.y - dogY;
                if (!s.Walkable && s.Bottom > 0.2f) { if (front < 1.2f) crouch = true; }          // crawl under
                else if (!s.Walkable || b.Max.y > 1.0f + dogY) avoid = true;                     // blocker (too tall to clear)
                else if (front > -0.05f && front < v * RiseTime(cfg, rise + 0.05f) + 0.2f) jump = true;   // clear its front edge going up, land on top
            }
            foreach (var e in level.Enemies)
            {
                if (e.Lane != lane || e.Knocked) continue;
                float ahead = e.X - x;
                if (ahead < -0.6f || ahead > v * 1.6f + 3f) continue;
                if (e is Cat)
                {
                    avoid = true;
                    // no lane to dodge into: jump it (it charges at catSpeed, so the gap closes faster)
                    if (ahead > 0 && ahead < (v + cfg.catSpeed) * 0.22f + 0.7f && SafestLane(lane, x, v) == lane) jump = true;
                }
                else if (e is Crow) { if (ahead < 3.5f) crouch = true; }
                else if (e is Mole && ahead < v * air * 0.5f + 0.4f && ahead > -0.2f) jump = true;
            }

            // change lanes around blockers and cats; otherwise drift towards a power-up ahead
            int want = lane;
            if (avoid) want = SafestLane(lane, x, v);
            else
                foreach (var p in level.PowerUps)
                    if (!p.Taken && p.X - x > 2f && p.X - x < 14f && p.Lane != lane && LaneClear(p.Lane, x, p.X + 1f) && LaneClear(lane + (p.Lane > lane ? 1 : -1), x, x + 3f, x + v * 0.6f + 1.5f))
                    { want = lane + (p.Lane > lane ? 1 : -1); break; }
            if (want != lane && _laneCooldown <= 0)
            {
                if (want < lane) input.PressLeft(); else input.PressRight();
                Debug.Log($"[PILOT] lane {lane}->{want} at {x:0.00} avoid {avoid} t {Time.time:0.00}");
                _laneCooldown = 0.35f;
            }

            input.SetCrouch(crouch && dog.Grounded && !jump);
            if (jump && dog.Grounded) { input.PressJump(); Debug.Log($"[PILOT] jump at {x:0.00} lane {lane} t {Time.time:0.00}"); }
        }

        /// <summary>Seconds after take-off until a plain jump is h high.</summary>
        static float RiseTime(GameConfig cfg, float h)
        {
            float v0 = cfg.JumpVelocity;
            return (v0 - Mathf.Sqrt(Mathf.Max(0f, v0 * v0 - 2f * cfg.gravity * h))) / cfg.gravity;
        }

        /// <summary>One step towards the lane whose next blocker / cat is furthest away, if the neighbour on the way can
        /// be entered now (a lane two away is reached through the middle one).</summary>
        int SafestLane(int lane, float x, float v)
        {
            int best = lane;
            float far = FirstBlock(lane, x);
            for (int l = 0; l < 3; l++)
            {
                float f = FirstBlock(l, x) - Mathf.Abs(l - lane) * 0.5f;     // slight preference for nearer lanes
                if (f > far + 0.5f) { far = f; best = l; }
            }
            if (best == lane) return lane;
            int step = lane + (best > lane ? 1 : -1);
            return FirstBlock(step, x) > 1.2f && LaneClear(step, x - 0.8f, x + 1f, x + v * 0.6f + 1.5f) ? step : lane;
        }

        /// <summary>Distance from x to the first blocker (tall, solid to the ground) or cat in the lane (40 if none).</summary>
        float FirstBlock(int lane, float x)
        {
            float d = 40f;
            foreach (var s in Root.Level.Solids)
            {
                if (s.Lane != lane || s.Knocked) continue;
                var b = s.Box;
                if (b.Max.x < x - 0.8f || s.Bottom > 0.2f || (s.Walkable && b.Max.y <= 1.0f)) continue;   // jumpable / crawlable
                d = Mathf.Min(d, b.Min.x - x);
            }
            foreach (var e in Root.Level.Enemies)
                if (e.Lane == lane && !e.Knocked && e is Cat && e.X > x - 1f) d = Mathf.Min(d, e.X - x);
            return d;
        }

        /// <summary>Nothing tall, overhead or hostile in the lane between x0 and x1; low props are fine (jumpable) unless
        /// they start before lowUntil (too close to time a jump after the switch).</summary>
        bool LaneClear(int lane, float x0, float x1, float lowUntil = 0f)
        {
            if (lane < 0 || lane > 2) return false;
            foreach (var s in Root.Level.Solids)
            {
                if (s.Lane != lane || s.Knocked) continue;
                var b = s.Box;
                if (b.Max.x < x0 || b.Min.x > x1) continue;
                if (b.Max.y > 1.0f || !s.Walkable || b.Min.x < lowUntil) return false;
            }
            foreach (var e in Root.Level.Enemies)
                if (e.Lane == lane && !e.Knocked && e.X > x0 - 2f && e.X < x1 + 4f) return false;
            return true;
        }
    }
}
