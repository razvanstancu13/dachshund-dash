using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DachshundDash
{
    /// <summary>The 2D backdrop, drawn as flat screen-aligned images (a camera-space canvas at the far end of the run
    /// camera's view, so every 3D object is in front of it); it shows wherever there is no 3D (above the ground's far
    /// edge). Layout is in canvas pixels of a 1080 px high screen (any width), measured from the horizon line where the
    /// 3D ground ends on screen. Two looping layers scroll at a fixed number of pixels per metre the run camera moves:
    /// the far skyline slowly, the per-biome near houses (laid out in tiles so they can change with the biome) faster.
    /// Between them the moon (not part of any loop) drifts slowly right to left over the whole run.
    /// Behind view (camera behind the dog, looking down the run): the near-house paintings become 3D facades lining
    /// both sides of the street (the houses on each side of a real street), and the far skyline and moon fill the sky
    /// behind them and at the far end of the street, from just below the eye-level horizon up, so no empty background
    /// shows anywhere.</summary>
    public class Backdrop : MonoBehaviour
    {
        const float ScreenH = 1080f;
        // behind view: the tallest near painting is WallHeight metres high, all at its scale; facade tiles are kept
        // from WallsBehind metres behind the camera to WallsAhead in front (past the end of the ground)
        const float WallHeight = 9f, WallsBehind = 10f, WallsAhead = 140f;
        const float BehindSink = 40f;            // skyline reaches this far below the horizon (3D in front covers it)
        const float BehindFarPxPerMetre = 8f;    // skyline shift per metre the view moves sideways

        // near houses: how far up the sky above the horizon they reach; far skyline: where the top of its towers lands
        // (share of the sky), knowing the towers fill the bottom SkylineShare of the far painting - so the skyline shows
        // above the near roofs. Both stand exactly on the horizon line (Sink = how far their feet go below it, in px)
        const float NearShare = 0.38f, Sink = 0f, SkylineTop = 0.62f, SkylineShare = 0.29f;
        const float NearPxPerMetre = 48.6f, FarPxPerMetre = 5.4f;
        // moon: its height and size as shares of the sky above the horizon, and its drift: it starts right of centre
        // and eases towards the left over the run (MoonDrift metres to cover ~63 % of it)
        const float MoonHeight = 0.74f, MoonSize = 0.3f, MoonStart = 0.4f, MoonTravel = 0.9f, MoonDrift = 800f;

        RawImage _far;
        RectTransform _moon, _nearRoot;
        readonly Dictionary<int, RectTransform> _nearTiles = new Dictionary<int, RectTransform>();
        readonly Texture2D[] _nearTex = new Texture2D[3];
        readonly float[] _nearTileH = new float[3];
        float _farTileW, _nearTileW, _nearH, _horizon, _biomeLength;
        Canvas _canvas;
        Camera _cam;
        Texture2D _farTex;
        float _groundFarZ, _groundY, _refH;
        bool _behind;

        // behind view: facade tiles along both sides of the street
        Transform _walls;
        readonly Dictionary<int, GameObject> _wallTiles = new Dictionary<int, GameObject>();
        readonly Material[] _wallMats = new Material[3];
        readonly Mesh[] _wallMeshes = new Mesh[3];
        readonly float[] _wallH = new float[3];
        float _wallW;

        public void Init(float biomeLength, Camera runCam, float groundFarZ, float groundY)
        {
            _biomeLength = biomeLength;
            _cam = runCam;
            _groundFarZ = groundFarZ;
            _groundY = groundY;

            // a screen-space canvas on the run camera, just inside its far plane: screen-aligned, so the images are drawn
            // flat and pixel-exact (as by an orthographic camera) behind every 3D object. (URP does not layer two base
            // cameras, and a stack draws its overlay camera with the base camera's lens.)
            var canvasGo = new GameObject("BackdropCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = runCam;
            _canvas.sortingOrder = -100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, ScreenH);
            scaler.matchWidthOrHeight = 1f;                         // always 1080 canvas pixels high

            _farTex = Tex("parallax_far");
            _far = Image("far", canvasGo.transform, _farTex, new Color(0.12f, 0.12f, 0.3f));
            var moonTex = Tex("moon");
            if (moonTex != null) moonTex.wrapMode = TextureWrapMode.Clamp;
            _moon = Image("moon", canvasGo.transform, moonTex, new Color(1, 1, 1, 0)).rectTransform;

            // near houses on top, in a container so tiles made later stay in front of the moon
            _nearRoot = new GameObject("near", typeof(RectTransform)).GetComponent<RectTransform>();
            _nearRoot.SetParent(canvasGo.transform, false);
            _nearRoot.anchorMin = Vector2.zero; _nearRoot.anchorMax = Vector2.one;
            _nearRoot.offsetMin = _nearRoot.offsetMax = Vector2.zero;
            string[] near = { "parallax_near_street", "parallax_near_alley", "parallax_near_backyard" };
            for (int i = 0; i < 3; i++) { _nearTex[i] = Tex(near[i]); if (_nearTex[i] != null) _refH = Mathf.Max(_refH, _nearTex[i].height); }

            // behind view facades: every painting at the tallest one's scale, as wide as the widest
            _walls = new GameObject("StreetFacades").transform;
            _walls.SetParent(transform, false);
            float wscale = _refH > 0 ? WallHeight / _refH : 1f;
            for (int i = 0; i < 3; i++)
                if (_nearTex[i] != null) _wallW = Mathf.Max(_wallW, _nearTex[i].width * wscale);
            if (_wallW <= 0) _wallW = 24f;
            for (int i = 0; i < 3; i++)
            {
                _wallH[i] = _nearTex[i] != null ? _nearTex[i].height * wscale : WallHeight * 0.8f;
                _wallMats[i] = new Material(Art.Mat("Backdrop"));
                if (_nearTex[i] != null)
                {
                    _wallMats[i].mainTexture = _nearTex[i];
                    if (_wallMats[i].HasProperty("_BaseMap")) _wallMats[i].SetTexture("_BaseMap", _nearTex[i]);
                }
                else _wallMats[i].color = new Color(0.15f, 0.12f, 0.2f);
                _wallMeshes[i] = MeshUtil.Strip(_wallW + 0.02f, _wallH[i], _wallW);        // +2 cm: no seams
            }

            SetView(false);
        }

        /// <summary>Lays the backdrop out for the run camera's current view (call after the camera is placed).</summary>
        public void SetView(bool behind)
        {
            _behind = behind;
            _canvas.planeDistance = _cam.farClipPlane - 10f;
            _nearRoot.gameObject.SetActive(!behind);
            _walls.gameObject.SetActive(behind);
            foreach (var t in _nearTiles.Values) Destroy(t.gameObject);
            _nearTiles.Clear();

            float farBottom;
            if (behind)
            {
                // horizon: eye level, where a point far down the run at the camera's height lands (the view only moves
                // along the run, sideways and up, never tilts, so it stays put)
                var eye = _cam.transform.position;
                _horizon = _cam.WorldToViewportPoint(eye + new Vector3(1000f, 0, 0)).y * ScreenH;
                farBottom = _horizon - BehindSink;
            }
            else
            {
                // horizon: where the back edge of the 3D ground lands on screen (the run camera only moves sideways)
                var edge = new Vector3(_cam.transform.position.x, _groundY, _groundFarZ);
                _horizon = _cam.WorldToViewportPoint(edge).y * ScreenH;
                farBottom = _horizon - Sink;
            }
            float sky = ScreenH - _horizon;

            // far skyline: from below the horizon up past the top of the screen, its towers rising above the near roofs
            float farH = Mathf.Max((SkylineTop * sky + _horizon - farBottom) / SkylineShare, ScreenH - farBottom + 10f);
            Place(_far.rectTransform, 0, farBottom, 1920, farH);
            _farTileW = _farTex != null ? farH * _farTex.width / _farTex.height : 1920f;

            float moonSize = MoonSize * sky;
            Place(_moon, 0, _horizon + MoonHeight * sky - moonSize / 2, moonSize, moonSize);

            // side view near houses: the tallest layer is NearShare of the sky high; all are drawn at its scale (the
            // others are cropped at their buildings' feet by tools/trim_parallax.py, so they are shorter but just as wide)
            _nearH = NearShare * sky + Sink;
            float scale = _refH > 0 ? _nearH / _refH : 1f;
            _nearTileW = 0;
            for (int i = 0; i < 3; i++)
            {
                _nearTileH[i] = _nearTex[i] != null ? _nearTex[i].height * scale : _nearH;
                if (_nearTex[i] != null) _nearTileW = Mathf.Max(_nearTileW, _nearTex[i].width * scale);
            }
            if (_nearTileW <= 0) _nearTileW = 640f;
        }

        static Texture2D Tex(string name)
        {
            var t = Art.Texture(name);
            if (t != null) t.wrapMode = TextureWrapMode.Repeat;
            return t;
        }

        static RawImage Image(string name, Transform parent, Texture2D tex, Color fallback)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<RawImage>();
            img.raycastTarget = false;
            if (tex != null) { img.texture = tex; img.color = Color.white; }
            else img.color = fallback;
            return img;
        }

        /// <summary>Bottom-left anchored rectangle in canvas pixels.</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        float ScreenW => ScreenH * Screen.width / Mathf.Max(1, Screen.height);

        /// <summary>camX, camZ: the run camera's position along the run and sideways (world metres).</summary>
        public void Follow(float camX, float camZ)
        {
            float w = ScreenW;

            // far layer: one image across the screen, its texture sliding left (behind view: it only shifts a little
            // as the view moves between lanes)
            float farShift = _behind ? -camZ * BehindFarPxPerMetre : camX * FarPxPerMetre;
            _far.rectTransform.sizeDelta = new Vector2(w, _far.rectTransform.sizeDelta.y);
            _far.uvRect = new Rect(farShift / _farTileW, 0, w / _farTileW, 1);

            float moonX = w / 2 + w / 2 * (MoonStart - MoonTravel * (1 - Mathf.Exp(-Mathf.Max(0, camX) / MoonDrift)));
            _moon.anchoredPosition = new Vector2(moonX - _moon.sizeDelta.x / 2, _moon.anchoredPosition.y);

            if (_behind) { FollowWalls(camX); return; }

            // near tiles: tile k's left edge at k * W - camX * px per metre
            float shift = camX * NearPxPerMetre;
            int first = Mathf.FloorToInt(shift / _nearTileW) - 1;
            int last = Mathf.FloorToInt((shift + w) / _nearTileW) + 1;
            var drop = new List<int>();
            foreach (var k in _nearTiles.Keys) if (k < first || k > last) drop.Add(k);
            foreach (var k in drop) { Destroy(_nearTiles[k].gameObject); _nearTiles.Remove(k); }
            for (int k = first; k <= last; k++)
            {
                if (!_nearTiles.TryGetValue(k, out var t))
                {
                    // the biome the run camera shows while this tile is in the middle of the screen
                    float worldX = ((k + 0.5f) * _nearTileW - w / 2) / NearPxPerMetre;
                    var biome = Level.BiomeAt(worldX, _biomeLength);
                    t = Image("near_" + k, _nearRoot, _nearTex[(int)biome], new Color(0.15f, 0.12f, 0.2f, 0f)).rectTransform;
                    t.sizeDelta = new Vector2(_nearTileW + 1f, _nearTileH[(int)biome]);           // +1 px: no seams
                    _nearTiles[k] = t;
                }
                Place(t, k * _nearTileW - shift, _horizon - Sink, t.sizeDelta.x, t.sizeDelta.y);
            }
        }

        /// <summary>Behind view: facade tile k covers x from k * W on the left side of the street (behind the far
        /// sidewalk) and half a tile further on the right side (behind the near one), so the sides never mirror.</summary>
        void FollowWalls(float camX)
        {
            int first = Mathf.FloorToInt((camX - WallsBehind) / _wallW) - 1;
            int last = Mathf.FloorToInt((camX + WallsAhead) / _wallW);
            var drop = new List<int>();
            foreach (var k in _wallTiles.Keys) if (k < first || k > last) drop.Add(k);
            foreach (var k in drop) { Destroy(_wallTiles[k]); _wallTiles.Remove(k); }
            float rightZ = -(Level.RoadHalfWidth + Level.FrontWalkDepth);
            for (int k = first; k <= last; k++)
            {
                if (_wallTiles.ContainsKey(k)) continue;
                float x0 = k * _wallW;
                var go = new GameObject("facades_" + k);
                go.transform.SetParent(_walls, false);
                // left: the strip faces -Z (the street), image left to right along the run
                int bl = (int)Level.BiomeAt(x0 + _wallW / 2, _biomeLength);
                var l = MeshUtil.Make("left", _wallMeshes[bl], _wallMats[bl], go.transform);
                l.transform.position = new Vector3(x0, _wallH[bl] / 2, _groundFarZ);
                // right: turned round to face +Z, so it reads left to right from the street too
                int br = (int)Level.BiomeAt(x0 + _wallW, _biomeLength);
                var r = MeshUtil.Make("right", _wallMeshes[br], _wallMats[br], go.transform);
                r.transform.SetPositionAndRotation(new Vector3(x0 + 1.5f * _wallW, _wallH[br] / 2, rightZ), Quaternion.Euler(0, 180, 0));
                _wallTiles[k] = go;
            }
        }
    }

    /// <summary>Run camera. Side view: follows the dog's X with the dog at ~18 % of the width; height fixed. Behind view:
    /// just above and behind the dog looking down the run, following its lane and, partly, its height.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public float Lead = 2.55f;
        public static readonly Vector3 Offset = new Vector3(0, 3.6f, -10.5f);   // narrow lens, close: big dog, little ground in front
        public const float Pitch = 16f;
        public const float Fov = 23f;
        static readonly Vector3 BehindOffset = new Vector3(-3.3f, 1.45f, 0);
        const float BehindPitch = 8f, BehindFov = 58f;
        const float BehindFollowY = 0.6f, BehindFollowZ = 0.85f;      // share of the dog's height / lane offset followed

        public bool BehindView { get; private set; }

        public void SetView(bool behind)
        {
            BehindView = behind;
            Cam.fieldOfView = behind ? BehindFov : Fov;
            Cam.farClipPlane = behind ? 160f : 120f;                   // behind: the ground runs ~120 m ahead
            transform.rotation = behind ? Quaternion.Euler(BehindPitch, 90, 0) : Quaternion.Euler(Pitch, 0, 0);
            Apply();
        }

        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = Fov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.2f);
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.Cam = cam;
            go.transform.rotation = Quaternion.Euler(Pitch, 0, 0);
            go.transform.position = Offset;
            return rig;
        }

        float _x, _y, _z;
        float LeadNow => BehindView ? 0f : Lead;

        public void Snap(Vector3 target) { _x = target.x + LeadNow; _y = target.y; _z = target.z; Apply(); }

        public void Follow(Vector3 target, float dt)
        {
            _x = Mathf.Lerp(_x, target.x + LeadNow, 1f - Mathf.Exp(-12f * dt));
            _y = Mathf.Lerp(_y, target.y, 1f - Mathf.Exp(-6f * dt));
            _z = Mathf.Lerp(_z, target.z, 1f - Mathf.Exp(-10f * dt));
            Apply();
        }

        void Apply()
        {
            var p = BehindView
                ? new Vector3(_x, _y * BehindFollowY, _z * BehindFollowZ) + BehindOffset
                : new Vector3(_x, 0, 0) + Offset;
            transform.position = p + transform.rotation * ShakeOffset();
        }

        public float X => _x;
        /// <summary>Sideways position of the view (behind view: follows the lanes; side view: 0).</summary>
        public float Z => BehindView ? _z * BehindFollowZ : 0f;

        float _shake, _shakeTime, _shakeAmp;

        /// <summary>A jolt that dies away over time seconds (real time, so the hit-stop does not freeze it).</summary>
        public void Shake(float time, float amplitude)
        {
            _shake = _shakeTime = time;
            _shakeAmp = amplitude;
        }

        Vector3 ShakeOffset()
        {
            if (_shake <= 0) return Vector3.zero;
            float k = _shake / _shakeTime;
            float a = _shakeAmp * k * k;
            float t = Time.unscaledTime * 38f;
            return new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0) * 2f * a;
        }

        void LateUpdate()
        {
            if (_shake <= 0) return;
            _shake = Mathf.Max(0, _shake - Time.unscaledDeltaTime);
            Apply();
        }
    }
}
