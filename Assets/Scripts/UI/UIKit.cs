using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DachshundDash
{
    /// <summary>Builds the uGUI screens in code with the generated skin (Resources/UI); every skin piece has a flat
    /// fallback so screens work before the art exists.</summary>
    public static class UIKit
    {
        public static readonly Color Cream = new Color(1f, 0.95f, 0.82f);
        public static readonly Color Ink = new Color(0.22f, 0.13f, 0.08f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.3f);
        public static readonly Color NightBlue = new Color(0.09f, 0.08f, 0.22f, 0.85f);

        static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<Font>("Fonts/LuckiestGuy");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas Canvas(string name, int order, RenderMode mode = RenderMode.ScreenSpaceOverlay, Camera cam = null)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = mode;
            c.sortingOrder = order;
            if (mode == RenderMode.ScreenSpaceCamera) { c.worldCamera = cam; c.planeDistance = 60f; }
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(RectTransform rt, string sprite, Color fallback, bool sliced = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            var s = sprite != null ? Art.Sprite(sprite) : null;
            if (s != null)
            {
                img.sprite = s;
                img.type = sliced && s.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
                img.preserveAspect = !sliced;
                img.color = Color.white;
                // sliced: borders scale with the height of the element, not the sprite's pixel size
                if (img.type == UnityEngine.UI.Image.Type.Sliced && rt.rect.height > 1f)
                    img.pixelsPerUnitMultiplier = s.rect.height / rt.rect.height;
            }
            else img.color = fallback;
            return img;
        }

        public static Text Text(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool shadow = true)
        {
            var rt = Stretch("text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (shadow)
            {
                var o = rt.gameObject.AddComponent<Shadow>();
                o.effectColor = new Color(0, 0, 0, 0.6f);
                o.effectDistance = new Vector2(3, -3);
            }
            return t;
        }

        static Sprite _roundedButton, _edgeFrame;
        const int ButtonTex = 128, ButtonRadius = 40;

        /// <summary>Rounded rectangle for buttons, drawn once: an amber fill shading darker towards the bottom, a dark
        /// wood rim and a thin cream highlight under the top edge (matches the signboard panels). 9-sliced.</summary>
        public static Sprite RoundedButtonSprite()
        {
            if (_roundedButton != null) return _roundedButton;
            const int n = ButtonTex, r = ButtonRadius;
            const float rim = 7f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            var top = new Color(1f, 0.8f, 0.4f); var bottom = new Color(0.84f, 0.53f, 0.2f);
            var edge = new Color(0.3f, 0.17f, 0.09f); var light = new Color(1f, 0.95f, 0.8f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // signed distance to the rounded rectangle's edge (negative inside), in pixels
                    float qx = Mathf.Max(Mathf.Abs(x + 0.5f - n / 2f) - (n / 2f - r), 0);
                    float qy = Mathf.Max(Mathf.Abs(y + 0.5f - n / 2f) - (n / 2f - r), 0);
                    float d = Mathf.Sqrt(qx * qx + qy * qy) - r;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    var c = Color.Lerp(bottom, top, y / (n - 1f));
                    if (d > -rim) c = Color.Lerp(c, edge, Mathf.Clamp01(d + rim + 0.5f));
                    else if (y > n - rim - 6 && y < n - rim - 2) c = Color.Lerp(c, light, 0.55f);   // highlight under the rim
                    c.a = alpha;
                    px[y * n + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            _roundedButton = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                           new Vector4(r + 4, r + 4, r + 4, r + 4));
            return _roundedButton;
        }

        /// <summary>Screen frame: clear in the middle, solid at the edges (white; tinted by the Image colour).</summary>
        public static Sprite EdgeFrame()
        {
            if (_edgeFrame != null) return _edgeFrame;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float d = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));       // 0 at the edge .. 0.5 centre
                    float a = 1f - Mathf.SmoothStep(0f, 0.2f, d);
                    px[y * n + x] = new Color(1, 1, 1, a * a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _edgeFrame = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _edgeFrame;
        }

        /// <summary>Rounded-rectangle menu button with a label.</summary>
        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick)
        {
            var rt = Rect("btn_" + label, parent, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedButtonSprite();
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = (ButtonRadius + 4) / (0.3f * size.y);      // corner radius ~30 % of the height
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = new Color(1f, 0.93f, 0.75f);
            cb.pressedColor = new Color(0.8f, 0.7f, 0.55f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            b.colors = cb;
            var t = Text(rt, label, Mathf.RoundToInt(size.y * 0.42f), Ink, TextAnchor.MiddleCenter, false);
            t.rectTransform.offsetMin = new Vector2(size.x * 0.07f, 4);       // inside the rounded ends
            t.rectTransform.offsetMax = new Vector2(-size.x * 0.07f, 0);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 16;
            t.resizeTextMaxSize = Mathf.RoundToInt(size.y * 0.42f);
            b.onClick.AddListener(() =>
            {
                Services.Audio.Play(Sfx.Click, 0.7f);
                onClick?.Invoke();
            });
            return b;
        }

        /// <summary>Wooden signboard panel.</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            Image(rt, "panel_board", new Color(0.25f, 0.16f, 0.1f, 0.95f));
            return rt;
        }

        /// <summary>Full-screen dimmer that also blocks clicks behind a modal.</summary>
        public static RectTransform Dimmer(Transform parent, float alpha = 0.55f)
        {
            var rt = Stretch("dim", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.02f, 0.02f, 0.08f, alpha);
            return rt;
        }
    }

    /// <summary>Press / release callbacks for on-screen game buttons (react on press, not release).</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action Down, Up;
        bool _held;
        public void OnPointerDown(PointerEventData e) { _held = true; Down?.Invoke(); transform.localScale = Vector3.one * 0.92f; }
        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) => Release();
        void Release()
        {
            if (!_held) return;
            _held = false;
            transform.localScale = Vector3.one;
            Up?.Invoke();
        }
        void OnDisable() => Release();
    }
}
