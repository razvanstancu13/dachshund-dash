using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DachshundDash
{
    /// <summary>The game scene: builds the world and runs Countdown -> Running -> Dying -> Result.</summary>
    public class GameRoot : MonoBehaviour
    {
        public enum State { Countdown, Running, Dying, Result }
        public State Current { get; private set; }

        /// <summary>Tests set this to drive the dog themselves (no countdown wait, fixed seed).</summary>
        public static int? SeedOverride;
        public static float CountdownStep = 1f;

        /// <summary>Prop showcase test level: set by the editor menu (PlayerPrefs "dd_showcase", used once) or the
        /// "-ddshowcase" command line argument.</summary>
        public static bool Showcase;

        static bool TakeShowcaseRequest()
        {
            bool on = Showcase || PlayerPrefs.GetInt("dd_showcase", 0) == 1 ||
                      System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-ddshowcase") >= 0;
            PlayerPrefs.DeleteKey("dd_showcase");
            Showcase = false;
            return on;
        }

        /// <summary>Debug level (the menu's DEBUG submenu): set before loading the Game scene, used once.</summary>
        public static Level.DebugLevel PendingDebug;
        bool _debugRun;

        public Player Dog { get; private set; }
        public Level Level { get; private set; }
        public InputRouter Input { get; private set; }
        CameraRig _cam;
        Backdrop _backdrop;
        GameHud _hud;
        bool _paused, _resultShown, _leaving;
        float _dyingT;

        void Start()
        {
            Time.timeScale = 1f;
            SetupLighting();
            _cam = CameraRig.Create();
            Input = gameObject.AddComponent<InputRouter>();
            Input.Enabled = false;

            Level = new GameObject("Level").AddComponent<Level>();
            Level.DebugMode = PendingDebug;
            _debugRun = PendingDebug != Level.DebugLevel.None;
            PendingDebug = Level.DebugLevel.None;
            Level.Init(SeedOverride ?? System.Environment.TickCount);

            _backdrop = new GameObject("Backdrop").AddComponent<Backdrop>();
            _backdrop.Init(GameConfig.I.biomeLength, _cam.Cam, Level.RoadHalfWidth + Level.BackWalkDepth, 0.12f);   // horizon = back edge of the far sidewalk

            Dog = new GameObject("Dog").AddComponent<Player>();
            Dog.Init(Level, Input);
            if (TakeShowcaseRequest())
            {
                // every prop type along the middle lane; the dog runs beside it and passes through anything
                Level.BuildShowcase(1, 16f);
                Dog.SetLane(2);
                Dog.NoCollide = true;
            }
            Dog.BonesChanged += n => _hud.SetScore(n);
            Dog.Died += OnDied;

            _hud = new GameObject("HudRoot").AddComponent<GameHud>();
            _hud.Build(Input, Dog.PowerUps, () => SetPaused(false), Quit, ContinueAfterResult);
            Input.Pause += () => { if (Current == State.Running || Current == State.Countdown) SetPaused(!_paused); };
            Input.ToggleView += () => { if (!_leaving) SetView(!_cam.BehindView); };

            SetView(PlayerPrefs.GetInt(ViewPref, 0) == 1);
            Level.Tick(Dog, 0f);
            Services.Audio.PlayMusic("music_game");
            Services.Audio.SetMusicPitch(1f);
            Services.Audio.Duck(1f);
            StartCoroutine(Countdown());
        }

        const string ViewPref = "dd_view_behind";

        /// <summary>Side view or the view from just behind the dog (Tab / the view button); remembered between runs.</summary>
        void SetView(bool behind)
        {
            PlayerPrefs.SetInt(ViewPref, behind ? 1 : 0);
            _cam.SetView(behind);
            _cam.Snap(Dog.transform.position);
            Level.SetView(behind);
            _backdrop.SetView(behind);
            _backdrop.Follow(_cam.X, _cam.Z);
        }

        void SetupLighting()
        {
            Art.SetAmbient(new Color(0.42f, 0.43f, 0.7f), new Color(0.3f, 0.28f, 0.48f), new Color(0.16f, 0.14f, 0.22f));
            RenderSettings.fog = false;                // the backdrop sits 100 m out; fog would wash it away
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.1f, 0.1f, 0.25f);
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 110f;
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.62f, 0.68f, 1f);
            moon.intensity = 0.9f;
            moon.shadows = LightShadows.Hard;
            moon.shadowStrength = 0.6f;
            moon.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        IEnumerator Countdown()
        {
            Current = State.Countdown;
            for (int i = 3; i >= 1; i--)
            {
                _hud.SetCountdown(i.ToString());
                Services.Audio.Play(Sfx.Bone, 0.8f, 0.8f);
                yield return WaitUnpaused(CountdownStep);
            }
            _hud.SetCountdown("GO!");
            Services.Audio.Play(Sfx.Bone, 0.9f, 1.25f);
            Current = State.Running;
            Input.Enabled = true;
            Dog.StartRunning();
            yield return WaitUnpaused(0.7f);
            _hud.SetCountdown("");
        }

        IEnumerator WaitUnpaused(float t)
        {
            while (t > 0)
            {
                if (!_paused) t -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void Update()
        {
            if (_paused) return;
            float udt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // Slow power-up slows the whole world (and the music a little)
            bool slow = Dog.PowerUps.Active(PowerUpType.Slow) && Current == State.Running;
            float targetScale = slow ? GameConfig.I.slowFactor : 1f;
            if (Current == State.Dying && _dyingT < 0.12f) targetScale = 0.05f;   // hit-stop
            Time.timeScale = Mathf.MoveTowards(Time.timeScale, targetScale, udt * 4f);
            Services.Audio.SetMusicPitch(slow ? 0.9f : 1f);
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            Dog.Tick(dt, udt);
            if (Current == State.Dying || Current == State.Result)
            {
                _dyingT += udt;
                if (_dyingT > 0.3f && _dyingT < 1.6f) Dog.DeathSettle(dt);
                if (_dyingT > 1.6f && !_resultShown) { _resultShown = true; Current = State.Result; _hud.ShowResult(Dog.Bones); }
            }
            Level.Tick(Dog, Current == State.Running ? dt : 0f);
            _cam.Follow(Dog.transform.position, dt);
            _backdrop.Follow(_cam.X, _cam.Z);

            if (Current == State.Result && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                ContinueAfterResult();
        }

        public bool CanContinue => Current == State.Result && _dyingT > 2.2f;

        void OnDied()
        {
            Current = State.Dying;
            _dyingT = 0;
            Input.Enabled = false;
            Dog.PowerUps.StopAll();
            Services.Audio.Duck(0.3f);
            // the hit lands: the view jolts and the screen edges flash red (the crash sound plays in Player)
            _cam.Shake(Dog.HitObstacle ? 0.45f : 0.35f, Dog.HitObstacle ? 0.22f : 0.15f);
            _hud.FlashHurt();
        }

        void SetPaused(bool p)
        {
            if (Current == State.Dying || Current == State.Result) return;
            _paused = p;
            Time.timeScale = p ? 0f : 1f;
            AudioListener.pause = p;
            Input.Enabled = !p && Current == State.Running;
            _hud.ShowPause(p);
        }

        void ContinueAfterResult()
        {
            if (!CanContinue || _leaving) return;
            _leaving = true;
            var hs = Services.Highscores;
            int score = Dog.Bones;
            if (!_debugRun && hs.Qualifies(score))         // debug runs never enter the highscores
            {
                Services.LastRank = hs.Submit(Services.Profile.HasName ? Services.Profile.Name : HighscoreService.DefaultName, score);
                Services.ShowHighscoresOnMenu = true;
            }
            else
            {
                Services.LastRank = -1;
                Services.ShowHighscoresOnMenu = false;
            }
            LoadMenu();
        }

        void Quit()
        {
            _paused = false;
            AudioListener.pause = false;
            LoadMenu();
        }

        void LoadMenu()
        {
            Time.timeScale = 1f;
            Services.Audio.Duck(1f);
            Services.Audio.SetMusicPitch(1f);
            SceneManager.LoadScene("Menu");
        }
    }
}
