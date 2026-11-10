using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DachshundDash
{
    /// <summary>Menu scene: painted background, the dog idling, name entry (first launch), main menu, highscores.</summary>
    public class MenuRoot : MonoBehaviour
    {
        RectTransform _main, _scores, _debug, _nameEntry, _scoreRows;
        Text _hello;
        InputField _nameField;
        Button _play;

        void Start()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            var cam = BuildStage();

            var bg = UIKit.Canvas("Background", -10, RenderMode.ScreenSpaceCamera, cam);
            var bgImg = UIKit.Stretch("bg", bg.transform);
            var bgSprite = Art.Sprite("menu_bg");
            var img = bgImg.gameObject.AddComponent<Image>();
            if (bgSprite != null)
            {
                img.sprite = bgSprite;
                var fit = bgImg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = bgSprite.rect.width / bgSprite.rect.height;
            }
            else img.color = new Color(0.1f, 0.09f, 0.25f);

            var ui = UIKit.Canvas("MenuUI", 10).transform;
            BuildMain(ui);
            BuildScores(ui);
            BuildDebug(ui);
            BuildNameEntry(ui);

            Show(null);
            IntroCutscene.PlayOnce(OpenMenu);
        }

        void OpenMenu()
        {
            Services.Audio.PlayMusic("music_menu");
            if (!Services.Profile.HasName) Show(_nameEntry);
            else if (Services.ShowHighscoresOnMenu) { Services.ShowHighscoresOnMenu = false; ShowScores(); }
            else Show(_main);
        }

        Camera BuildStage()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.2f);
            cam.fieldOfView = 30f;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0, 1.0f, -6f);
            go.transform.rotation = Quaternion.Euler(6, 0, 0);
            PostFx.Attach(cam);                                    // the same film grade as the run

            Art.SetAmbient(new Color(0.55f, 0.52f, 0.7f), new Color(0.45f, 0.42f, 0.6f), new Color(0.25f, 0.22f, 0.3f));
            RenderSettings.fog = false;
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional; key.color = new Color(1f, 0.8f, 0.6f); key.intensity = 1.1f;
            key.transform.rotation = Quaternion.Euler(30, -40, 0);
            var rim = new GameObject("Rim").AddComponent<Light>();
            rim.type = LightType.Directional; rim.color = new Color(0.5f, 0.6f, 1f); rim.intensity = 0.8f;
            rim.transform.rotation = Quaternion.Euler(20, 150, 0);
            var fill = new GameObject("Fill").AddComponent<Light>();            // from the camera side, so the black fur reads
            fill.type = LightType.Directional; fill.color = new Color(1f, 0.85f, 0.7f); fill.intensity = 1.5f;
            fill.transform.rotation = Quaternion.Euler(15, 10, 0);

            // the dog, idling at the right, a little turned towards the viewer; on a soft shadow blob
            var dog = Art.Spawn("dog", 0.95f, new Color(0.12f, 0.09f, 0.08f), new Vector3(1.6f, 0.95f, 0.5f));
            dog.transform.position = new Vector3(2.2f, -0.55f, 0f);
            dog.transform.rotation = Quaternion.Euler(0, -150, 0);   // looking down the street, head seen from the sidewalk
            var clips = ClipPlayer.Attach(dog, "dog");
            clips?.Play(clips.Has("Sit") ? "Sit" : "Idle", 0);
            var blob = MeshUtil.Make("shadow", MeshUtil.Ground(2.4f, 1.2f, 2.4f), Art.Mat("Shadow"), dog.transform);
            blob.transform.localPosition = new Vector3(-1.2f, 0.01f, -0.6f);

            // a cobblestone plinth under the dog, reaching below the bottom of the view (the vertical field of view is
            // fixed, so it does at any screen shape): the dog stands on something instead of floating over the backdrop
            var plinth = MeshUtil.Make("plinth", MeshUtil.Cylinder(1.05f, 2.2f, 1.2f), Art.Mat("Cobble"), null);
            plinth.GetComponent<MeshRenderer>().sharedMaterials = new[] { Art.Mat("Cobble"), Art.Mat("Brick") };
            plinth.transform.position = new Vector3(2.2f, -0.55f, 0.1f);
            var rimRing = MeshUtil.Make("plinth_rim", MeshUtil.Cylinder(1.12f, 0.12f, 1.2f), Art.Mat("Curb"), null);
            rimRing.GetComponent<MeshRenderer>().sharedMaterials = new[] { Art.Mat("Curb"), Art.Mat("Curb") };
            rimRing.transform.position = new Vector3(2.2f, -0.56f, 0.1f);
            return cam;
        }

        void BuildMain(Transform ui)
        {
            _main = UIKit.Stretch("main", ui);
            var logo = UIKit.Rect("logo", _main, new Vector2(0, 1), new Vector2(80, -40), new Vector2(900, 420), new Vector2(0, 1));
            var li = UIKit.Image(logo, "logo", new Color(0, 0, 0, 0), false);
            if (li.sprite == null) UIKit.Text(logo, "DACHSHUND\nDASH", 120, UIKit.Gold);
            _play = UIKit.Button(_main, "PLAY", new Vector2(0, 0.5f), new Vector2(160, -120), new Vector2(520, 150), () => SceneManager.LoadScene("Game"));
            ((RectTransform)_play.transform).pivot = new Vector2(0, 0.5f);
            var hs = UIKit.Button(_main, "HIGHSCORES", new Vector2(0, 0.5f), new Vector2(160, -300), new Vector2(520, 130), ShowScores);
            ((RectTransform)hs.transform).pivot = new Vector2(0, 0.5f);
            var dbg = UIKit.Button(_main, "DEBUG", new Vector2(0, 0.5f), new Vector2(710, -300), new Vector2(300, 130), () => Show(_debug));
            ((RectTransform)dbg.transform).pivot = new Vector2(0, 0.5f);
            var hello = UIKit.Rect("hello", _main, new Vector2(0, 0), new Vector2(170, 60), new Vector2(900, 70), new Vector2(0, 0));
            _hello = UIKit.Text(hello, "", 44, UIKit.Cream, TextAnchor.MiddleLeft);
            var change = UIKit.Rect("change", _main, new Vector2(0, 0), new Vector2(170, 10), new Vector2(300, 50), new Vector2(0, 0));
            var ct = UIKit.Text(change, "change name", 30, new Color(1, 0.85f, 0.5f), TextAnchor.MiddleLeft);
            ct.raycastTarget = true;
            var cb = change.gameObject.AddComponent<Button>();
            cb.targetGraphic = ct;
            cb.onClick.AddListener(() => { Services.Audio.Play(Sfx.Click, 0.7f); _nameField.text = Services.Profile.Name; Show(_nameEntry); });
        }

        void BuildScores(Transform ui)
        {
            _scores = UIKit.Stretch("scores", ui);
            UIKit.Dimmer(_scores, 0.45f);
            var board = UIKit.Panel(_scores, "board", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1000, 960));
            var title = UIKit.Rect("title", board, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(900, 100), new Vector2(0.5f, 1));
            UIKit.Text(title, "TOP DOGS", 84, UIKit.Gold);
            _scoreRows = UIKit.Rect("rows", board, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(760, 660), new Vector2(0.5f, 1));
            var back = UIKit.Button(_scores, "BACK", new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(420, 120), () => Show(_main));
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0);
        }

        /// <summary>Debug submenu: launches the test levels (one enemy type per lane / every prop per surface).</summary>
        void BuildDebug(Transform ui)
        {
            _debug = UIKit.Stretch("debug", ui);
            UIKit.Dimmer(_debug, 0.45f);
            var board = UIKit.Panel(_debug, "board", new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(800, 780));
            var title = UIKit.Rect("title", board, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(700, 100), new Vector2(0.5f, 1));
            UIKit.Text(title, "DEBUG", 84, UIKit.Gold);
            void Launch(string label, float y, Level.DebugLevel level)
            {
                var b = UIKit.Button(board, label, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(520, 140),
                    () => { GameRoot.PendingDebug = level; SceneManager.LoadScene("Game"); });
                ((RectTransform)b.transform).pivot = new Vector2(0.5f, 1);
            }
            Launch("ENEMIES", -180, Level.DebugLevel.Enemies);
            Launch("OBSTACLES", -350, Level.DebugLevel.Obstacles);
            Launch("POWER-UPS", -520, Level.DebugLevel.PowerUps);
            var back = UIKit.Button(_debug, "BACK", new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(420, 120), () => Show(_main));
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0);
        }

        void FillScores()
        {
            for (int i = _scoreRows.childCount - 1; i >= 0; i--) Destroy(_scoreRows.GetChild(i).gameObject);
            var list = Services.Highscores.Entries;
            Color[] medal = { new Color(1f, 0.85f, 0.35f), new Color(0.85f, 0.88f, 0.95f), new Color(0.95f, 0.65f, 0.4f) };
            for (int i = 0; i < list.Count; i++)
            {
                var row = UIKit.Rect("row" + i, _scoreRows, new Vector2(0.5f, 1), new Vector2(0, -i * 66), new Vector2(760, 60), new Vector2(0.5f, 1));
                var img = UIKit.Image(row, "row_plank", new Color(0.45f, 0.3f, 0.18f, 0.9f));
                bool mine = i == Services.LastRank;
                if (i < 3) img.color *= medal[i];
                if (mine) { img.color = new Color(1f, 0.75f, 0.45f); row.gameObject.AddComponent<Pulse>(); }
                var rank = UIKit.Rect("rank", row, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(90, 60), new Vector2(0, 0.5f));
                UIKit.Text(rank, (i + 1) + ".", 38, UIKit.Ink, TextAnchor.MiddleLeft, false);
                var nm = UIKit.Rect("name", row, new Vector2(0, 0.5f), new Vector2(130, 0), new Vector2(520, 60), new Vector2(0, 0.5f));
                UIKit.Text(nm, list[i].name, 38, UIKit.Ink, TextAnchor.MiddleLeft, false);
                var sc = UIKit.Rect("score", row, new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(200, 60), new Vector2(1, 0.5f));
                UIKit.Text(sc, list[i].score.ToString(), 38, UIKit.Ink, TextAnchor.MiddleRight, false);
            }
        }

        void BuildNameEntry(Transform ui)
        {
            _nameEntry = UIKit.Stretch("nameEntry", ui);
            UIKit.Dimmer(_nameEntry, 0.5f);
            var board = UIKit.Panel(_nameEntry, "board", new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1000, 560));
            var title = UIKit.Rect("title", board, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(900, 160), new Vector2(0.5f, 1));
            UIKit.Text(title, "WHO'S WALKING\nTHE DOG?", 64, UIKit.Gold);
            var field = UIKit.Rect("field", board, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(700, 110));
            var fi = UIKit.Image(field, "row_plank", new Color(0.95f, 0.9f, 0.8f));
            fi.color = new Color(1f, 0.95f, 0.85f);
            _nameField = field.gameObject.AddComponent<InputField>();
            _nameField.targetGraphic = fi;
            _nameField.characterLimit = PlayerProfile.MaxNameLength;
            var t = UIKit.Text(field, "", 56, UIKit.Ink, TextAnchor.MiddleCenter, false);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            _nameField.textComponent = t;
            var ph = UIKit.Text(field, "your name", 50, new Color(0.3f, 0.2f, 0.1f, 0.45f), TextAnchor.MiddleCenter, false);
            _nameField.placeholder = ph;
            _nameField.onEndEdit.AddListener(_ => { if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) SubmitName(); });
            var ok = UIKit.Button(board, "OK", new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(360, 120), SubmitName);
            ((RectTransform)ok.transform).pivot = new Vector2(0.5f, 0);
        }

        void SubmitName()
        {
            if (!Services.Profile.TrySetName(_nameField.text))
            {
                _nameField.ActivateInputField();
                return;
            }
            Show(_main);
        }

        void ShowScores()
        {
            FillScores();
            Show(_scores);
            Services.LastRank = -1;      // highlight only the first time
        }

        void Show(RectTransform panel)
        {
            _main.gameObject.SetActive(panel == _main);
            _scores.gameObject.SetActive(panel == _scores);
            _debug.gameObject.SetActive(panel == _debug);
            _nameEntry.gameObject.SetActive(panel == _nameEntry);
            if (panel == _main) _hello.text = "HI, " + Services.Profile.Name.ToUpperInvariant() + "!";
            if (panel == _nameEntry) _nameField.ActivateInputField();
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (_main.gameObject.activeSelf && !_nameEntry.gameObject.activeSelf && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                SceneManager.LoadScene("Game");
            if ((_scores.gameObject.activeSelf || _debug.gameObject.activeSelf) && (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame))
                Show(_main);
        }
    }

    /// <summary>Gentle scale pulse for the newly earned highscore row.</summary>
    public class Pulse : MonoBehaviour
    {
        void Update() => transform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(Time.unscaledTime * 6f));
    }
}
