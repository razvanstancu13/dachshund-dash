using UnityEngine;

namespace DachshundDash
{
    /// <summary>The soft glowing lines between the three paths and along both outer edges: four long strips that move
    /// with the dog (their texture does not change along the length, so they look fixed to the ground). The two lines
    /// beside the dog glow brighter and thicker and carry light pulses running ahead in the run direction; the others
    /// dim. Side view: upright strips facing the camera, standing on the surface they border (so the ground never
    /// covers them, while the dog and props in front do), each as tall as needed for the same thickness on screen at
    /// its depth. Behind view: flat strips on the ground.</summary>
    public class LaneLines : MonoBehaviour
    {
        const float Behind = 40f, Length = 170f;            // strip from 40 m behind the dog to 130 m ahead
        const float LineScreenHeight = 0.01f;               // side view: thickness as a share of the screen height
        const float FlatWidth = 0.07f;                       // behind view: width on the ground, metres
        const float ActiveThick = 1.6f;                      // the dog's lines are this much thicker
        const float PulseLength = 9f, PulseSpeed = 7f;       // one pulse per 9 m, running ahead of the dog at 7 m/s
        const float PulseWidth = 1.5f;                       // pulse strip thickness / line thickness
        static readonly Color Base = new Color(0.6f, 0.9f, 1f, 1f);
        static readonly Color PulseColor = new Color(1.2f, 1.6f, 1.8f);

        readonly Transform[] _lines = new Transform[4], _flows = new Transform[4];
        readonly Material[] _lineMats = new Material[4], _flowMats = new Material[4];
        readonly float[] _z = new float[4], _surface = new float[4], _thick = new float[4], _glow = new float[4];
        float _phase, _x0;

        public void Init()
        {
            var cfg = GameConfig.I;
            var dash = MeshUtil.FlowDash();
            for (int i = 0; i < 4; i++)
            {
                _z[i] = cfg.LaneZ(0) + cfg.laneSpacing / 2 - i * cfg.laneSpacing;
                _surface[i] = i == 0 ? 0.12f : i == 3 ? 0.06f : 0f;      // back walk / front walk / paths
                _lineMats[i] = new Material(Art.Mat("LaneGlow"));
                _lines[i] = MeshUtil.Make("glow_line" + i, MeshUtil.Strip(Length, 1f, 1f), _lineMats[i], transform).transform;
                _flowMats[i] = new Material(Art.Mat("LaneGlow"));
                _flowMats[i].mainTexture = dash;
                if (_flowMats[i].HasProperty("_BaseMap")) _flowMats[i].SetTexture("_BaseMap", dash);
                _flows[i] = MeshUtil.Make("glow_flow" + i, MeshUtil.Strip(Length, 1f, PulseLength), _flowMats[i], transform).transform;
            }
            SetView(false);
        }

        /// <summary>false = side view (upright strips sized for the side camera), true = flat on the ground.</summary>
        public void SetView(bool behind)
        {
            var camRot = Quaternion.Euler(CameraRig.Pitch, 0, 0);
            var camFwd = camRot * Vector3.forward;
            float perMetre = 2f * Mathf.Tan(CameraRig.Fov * 0.5f * Mathf.Deg2Rad);     // view height per metre of depth
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(0, _surface[i], _z[i]);
                Quaternion rot;
                if (behind)
                {
                    // the outer two lie on the road just inside its edges (not on the curb / walk edge), full width
                    // clear of them even when thick
                    _thick[i] = FlatWidth;
                    float inset = FlatWidth * ActiveThick / 2 + 0.02f;
                    if (i == 0) { at.y = 0; at.z -= inset; }
                    if (i == 3) { at.y = 0; at.z += inset; }
                    at.y += 0.012f;
                    rot = Quaternion.Euler(90, 0, 0);
                }
                else
                {
                    float depth = Vector3.Dot(at - new Vector3(0, CameraRig.Offset.y, CameraRig.Offset.z), camFwd);
                    _thick[i] = LineScreenHeight * perMetre * depth;
                    at.y += _thick[i] / 2 * ActiveThick + 0.005f;    // bottom edge above the surface, even when thick
                    rot = camRot;
                }
                _lines[i].localPosition = _flows[i].localPosition = at;
                _lines[i].localRotation = _flows[i].localRotation = rot;
            }
        }

        /// <summary>Keeps the strips around the dog; dogZ (its actual depth, also mid lane change) picks the bright lines.</summary>
        public void Follow(float dogX, float dogZ)
        {
            float spacing = GameConfig.I.laneSpacing;
            _x0 = Mathf.Floor(dogX) - Behind;
            transform.position = new Vector3(_x0, 0, 0);
            for (int i = 0; i < 4; i++)
            {
                // 1 for the two lines bordering the dog's lane, falling to 0 one lane further out
                float k = Mathf.Clamp01(1f - (Mathf.Abs(_z[i] - dogZ) - spacing / 2) / spacing);
                _glow[i] = k * k * (3 - 2 * k);
            }
        }

        void LateUpdate()
        {
            if (_lineMats[0] == null) return;
            _phase += Time.deltaTime * PulseSpeed;
            // pulses fixed to the world, then carried forward: texture u = (world x - phase) / pulse length
            var off = new Vector2(Mathf.Repeat((_x0 - _phase) / PulseLength, 1f), 0);
            for (int i = 0; i < 4; i++)
            {
                float k = _glow[i];
                var c = Base * Mathf.Lerp(0.55f, 1.6f, k);
                c.a = Mathf.Lerp(0.6f, 1f, k);
                SetColor(_lineMats[i], c);
                SetColor(_flowMats[i], new Color(PulseColor.r, PulseColor.g, PulseColor.b, k));
                _flowMats[i].mainTextureOffset = off;
                if (_flowMats[i].HasProperty("_BaseMap")) _flowMats[i].SetTextureOffset("_BaseMap", off);
                _flows[i].gameObject.SetActive(k > 0.01f);
                float thick = _thick[i] * Mathf.Lerp(1f, ActiveThick, k);
                _lines[i].localScale = new Vector3(1, thick, 1);
                // the pulse is wider than the line: its glow spills onto the dark ground beside it, where it shows
                _flows[i].localScale = new Vector3(1, thick * PulseWidth, 1);
            }
        }

        static void SetColor(Material m, Color c)
        {
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }
    }
}
