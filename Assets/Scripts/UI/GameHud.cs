using System;
using UnityEngine;
using UnityEngine.UI;

namespace DachshundDash
{
    /// <summary>In-game UI: score, active power-up indicator, direction buttons, pause, countdown, result.</summary>
    public class GameHud : MonoBehaviour
    {
        Text _score, _countdown, _result;
        RectTransform _pausePanel, _resultPanel;
        RectTransform _pu;
        Image _puIcon, _puRing;
        PowerUps _powerUps;
        float _scorePop;
        Image _hurt;
        float _hurtT = -1f;
        const float HurtIn = 0.08f, HurtOut = 0.7f;

        public void Build(InputRouter input, PowerUps powerUps, Action resume, Action quit, Action continueAfterResult)
        {
            _powerUps = powerUps;
            var canvas = UIKit.Canvas("HUD", 10);
            var root = canvas.transform;

            // red frame round the screen on a crash (behind the rest of the HUD)
            _hurt = UIKit.Stretch("hurt", root).gameObject.AddComponent<Image>();
            _hurt.sprite = UIKit.EdgeFrame();
            _hurt.color = new Color(0.9f, 0.05f, 0.05f, 0);
            _hurt.raycastTarget = false;

            // score - top right
            var sp = UIKit.Panel(root, "score", new Vector2(1, 1), new Vector2(-30, -30), new Vector2(330, 120));
            var icon = UIKit.Rect("bone", sp, new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(100, 70), new Vector2(0, 0.5f));
            UIKit.Image(icon, "icon_bone", UIKit.Cream, false);
            _score = UIKit.Text(sp, "0", 64, UIKit.Cream, TextAnchor.MiddleRight);
            _score.rectTransform.offsetMax = new Vector2(-40, 0);

            // pause - top left
            var pb = UIKit.Rect("pause", root, new Vector2(0, 1), new Vector2(30, -30), new Vector2(110, 110), new Vector2(0, 1));
            UIKit.Image(pb, "btn_round", new Color(0.4f, 0.3f, 0.2f), false);
            UIKit.Text(pb, "II", 48, UIKit.Cream);
            pb.gameObject.AddComponent<HoldButton>().Down = () => input.PressPause();

            // view switch (side / behind the dog) - next to pause
            var vb = UIKit.Rect("view", root, new Vector2(0, 1), new Vector2(30 + 110 + 20, -30), new Vector2(110, 110), new Vector2(0, 1));
            UIKit.Image(vb, "btn_round", new Color(0.4f, 0.3f, 0.2f), false);
            UIKit.Text(vb, "VIEW", 30, UIKit.Cream);
            vb.gameObject.AddComponent<HoldButton>().Down = () => input.PressView();

            bool touch = InputRouter.TouchUI;
            Vector2[] dirs = { new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, 0) };

            // active power-up - left of the bone counter: icon and a ring that runs down with its time
            _pu = UIKit.Rect("powerup", root, new Vector2(1, 1), new Vector2(-30 - 330 - 16, -30), new Vector2(120, 120), new Vector2(1, 1));
            UIKit.Image(_pu, "btn_round", new Color(0.35f, 0.25f, 0.18f, 0.9f), false).raycastTarget = false;
            _puRing = UIKit.Stretch("ring", _pu).gameObject.AddComponent<Image>();
            _puRing.sprite = Art.Sprite("btn_round");
            _puRing.type = Image.Type.Filled; _puRing.fillMethod = Image.FillMethod.Radial360; _puRing.fillOrigin = 2; _puRing.fillClockwise = false;
            _puRing.raycastTarget = false;
            var ic = UIKit.Rect("icon", _pu, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76, 76));
            _puIcon = UIKit.Image(ic, PowerUpInfo.IconName(PowerUpType.Fly), Color.white, false);
            _puIcon.raycastTarget = false;
            _pu.gameObject.SetActive(false);
            // direction buttons - bottom right diamond (touch only)
            if (touch)
            {
                var dc = new Vector2(-250, 250);
                string[] names = { "up", "left", "down", "right" };
                float[] rot = { 0, 90, 180, -90 };
                for (int i = 0; i < 4; i++)
                {
                    var b = UIKit.Rect("dir_" + names[i], root, new Vector2(1, 0), dc + dirs[i] * 160, new Vector2(150, 150), new Vector2(0.5f, 0.5f));
                    UIKit.Image(b, "btn_round", new Color(0.35f, 0.25f, 0.18f, 0.9f), false);
                    var ar = UIKit.Rect("arrow", b, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
                    var img = UIKit.Image(ar, "icon_arrow", UIKit.Cream, false);
                    img.raycastTarget = false;
                    ar.localRotation = Quaternion.Euler(0, 0, rot[i]);
                    var hb = b.gameObject.AddComponent<HoldButton>();
                    switch (i)
                    {
                        case 0: hb.Down = input.PressJump; break;
                        case 1: hb.Down = input.PressLeft; break;
                        case 2: hb.Down = () => input.SetCrouch(true); hb.Up = () => input.SetCrouch(false); break;
                        case 3: hb.Down = input.PressRight; break;
                    }
                }
            }

            // countdown
            var cd = UIKit.Rect("countdown", root, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(600, 300));
            _countdown = UIKit.Text(cd, "", 220, UIKit.Gold);
            _countdown.gameObject.AddComponent<Outline>().effectColor = UIKit.Ink;

            // pause panel
            _pausePanel = UIKit.Stretch("pausePanel", root);
            UIKit.Dimmer(_pausePanel);
            var pp = UIKit.Panel(_pausePanel, "board", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 520));
            var ptitle = UIKit.Rect("title", pp, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(600, 100), new Vector2(0.5f, 1));
            UIKit.Text(ptitle, "PAUSED", 80, UIKit.Cream);
            UIKit.Button(pp, "RESUME", new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(440, 130), resume);
            UIKit.Button(pp, "MENU", new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(440, 130), quit);
            _pausePanel.gameObject.SetActive(false);

            // result
            _resultPanel = UIKit.Stretch("resultPanel", root);
            UIKit.Dimmer(_resultPanel, 0.35f);
            var rp = UIKit.Panel(_resultPanel, "board", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 420));
            var rtitle = UIKit.Rect("title", rp, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(700, 100), new Vector2(0.5f, 1));
            UIKit.Text(rtitle, "OUCH!", 90, UIKit.Gold);
            var rbody = UIKit.Rect("body", rp, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(700, 100));
            _result = UIKit.Text(rbody, "", 70, UIKit.Cream);
            var rhint = UIKit.Rect("hint", rp, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(700, 60), new Vector2(0.5f, 0));
            UIKit.Text(rhint, touch ? "tap to continue" : "press any key", 36, new Color(1, 1, 1, 0.7f));
            var hb2 = _resultPanel.gameObject.AddComponent<HoldButton>();
            hb2.Down = continueAfterResult;
            _resultPanel.gameObject.SetActive(false);

            powerUps.Changed += Refresh;
            Refresh();
        }

        public void SetScore(int s)
        {
            _score.text = s.ToString();
            _scorePop = 1f;
        }

        /// <summary>Red screen frame: fades in fast, out slowly.</summary>
        public void FlashHurt() => _hurtT = 0f;

        public void SetCountdown(string s) => _countdown.text = s;
        public void ShowPause(bool on) => _pausePanel.gameObject.SetActive(on);

        public void ShowResult(int bones)
        {
            _result.text = "BONES: " + bones;
            _resultPanel.gameObject.SetActive(true);
        }

        void Refresh()
        {
            var cur = _powerUps.Current;
            _pu.gameObject.SetActive(cur != null);
            if (cur == null) return;
            var t = cur.Value;
            var s = Art.Sprite(PowerUpInfo.IconName(t));
            if (s != null) { _puIcon.sprite = s; _puIcon.color = Color.white; }
            else _puIcon.color = PowerUpInfo.Color(t);
            _puRing.color = PowerUpInfo.Color(t) * new Color(1, 1, 1, 0.75f);
            _pu.localScale = Vector3.one * 1.25f;                         // pops in, settles in Update
        }

        void Update()
        {
            if (_hurtT >= 0)
            {
                _hurtT += Time.unscaledDeltaTime;
                float a = _hurtT < HurtIn ? _hurtT / HurtIn : 1f - (_hurtT - HurtIn) / HurtOut;
                _hurt.color = new Color(0.9f, 0.05f, 0.05f, 0.85f * Mathf.Clamp01(a));
                if (_hurtT > HurtIn + HurtOut) _hurtT = -1f;
            }
            if (_powerUps == null) return;
            if (_pu.gameObject.activeSelf)
            {
                _puRing.fillAmount = _powerUps.Remaining01;
                _pu.localScale = Vector3.one * Mathf.MoveTowards(_pu.localScale.x, 1f, Time.unscaledDeltaTime * 2f);
            }
            if (_scorePop > 0)
            {
                _scorePop = Mathf.Max(0, _scorePop - Time.unscaledDeltaTime * 6f);
                _score.transform.localScale = Vector3.one * (1f + 0.15f * _scorePop);
            }
        }
    }
}
