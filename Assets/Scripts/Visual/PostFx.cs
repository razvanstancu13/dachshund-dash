using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DachshundDash
{
    /// <summary>Cinematic post-processing on one camera: a global Volume with a night-film grade (tonemapping, cool
    /// shadows / warm highlights, bloom on the glowing bits, vignette, light grain, soft far background) plus moods
    /// the game drives each frame: speed, power-ups, the crash punch, the dying / result fade and the pause dim.
    /// The profile asset (Resources/PostFx.asset, made by ProjectBuilder.Setup with <see cref="Configure"/>) is not
    /// loaded; it only tells the build which post-processing shader variants to keep. At runtime a fresh profile
    /// with the same values is animated.
    /// Command line: -nopostfx turns it off (for comparisons), -postfxlow forces the light (phone) tier.</summary>
    public class PostFx : MonoBehaviour
    {
        // ---- the base look (approve / tune these) ----
        const float Exposure = 0.15f, Contrast = 14f, Saturation = 6f;
        static readonly Color Shadows = new Color(0.30f, 0.45f, 0.85f);     // split toning: moonlit blue shadows ...
        static readonly Color Highlights = new Color(1f, 0.72f, 0.42f);     // ... lamp-warm highlights
        const float SplitBalance = -20f;
        const float BloomThreshold = 0.95f, BloomIntensity = 0.75f, BloomScatter = 0.68f;
        static readonly Color BloomTint = new Color(1f, 0.93f, 0.85f);
        const float VignetteIntensity = 0.32f, VignetteSmooth = 0.45f;
        static readonly Color VignetteColor = new Color(0.02f, 0.02f, 0.08f);
        const float Grain = 0.16f, BaseAberration = 0.04f;
        const float DofStart = 35f, DofEnd = 120f, DofRadius = 0.8f;      // only what lies far behind the play area

        public static bool LowTier => Application.isMobilePlatform || HasArg("-postfxlow");
        public static bool Disabled => HasArg("-nopostfx");
        static bool HasArg(string a) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), a) >= 0;

        /// <summary>Writes the base look into a profile (adds the overrides it lacks). Shared by the editor asset and
        /// the runtime copy, so the two never drift.</summary>
        public static void Configure(VolumeProfile p, bool low)
        {
            var tone = Get<Tonemapping>(p);
            tone.mode.Override(TonemappingMode.Neutral);

            var col = Get<ColorAdjustments>(p);
            col.postExposure.Override(Exposure);
            col.contrast.Override(Contrast);
            col.saturation.Override(Saturation);
            col.colorFilter.Override(Color.white);

            var split = Get<SplitToning>(p);
            split.shadows.Override(Shadows);
            split.highlights.Override(Highlights);
            split.balance.Override(SplitBalance);

            var bloom = Get<Bloom>(p);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            bloom.tint.Override(BloomTint);
            bloom.highQualityFiltering.Override(!low);
            bloom.downscale.Override(low ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);

            var vig = Get<Vignette>(p);
            vig.color.Override(VignetteColor);
            vig.intensity.Override(VignetteIntensity);
            vig.smoothness.Override(VignetteSmooth);

            var ca = Get<ChromaticAberration>(p);
            ca.intensity.Override(low ? 0f : BaseAberration);

            var lens = Get<LensDistortion>(p);
            lens.intensity.Override(0f);

            var grain = Get<FilmGrain>(p);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(low ? 0f : Grain);
            grain.response.Override(0.85f);

            var dof = Get<DepthOfField>(p);
            dof.mode.Override(low ? DepthOfFieldMode.Off : DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(DofStart);
            dof.gaussianEnd.Override(DofEnd);
            dof.gaussianMaxRadius.Override(DofRadius);
            dof.highQualitySampling.Override(false);
        }

        static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (!p.TryGet(out T c)) c = p.Add<T>();
            c.active = true;
            return c;
        }

        // ---- runtime ----

        /// <summary>Set by the game every frame (0..1 run speed between start and top speed).</summary>
        public float Speed01;
        public bool Slow, Shield, Fly, Dead, Paused;

        VolumeProfile _p;
        ColorAdjustments _col;
        Bloom _bloom;
        Vignette _vig;
        ChromaticAberration _ca;
        LensDistortion _lens;
        float _punch, _flash, _dead, _pause, _slow, _shield, _fly;
        bool _low;

        public static PostFx Attach(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (Disabled) { data.renderPostProcessing = false; return null; }
            bool low = LowTier;
            data.renderPostProcessing = true;
            data.antialiasing = low ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
            data.volumeLayerMask = 1;                                   // Default layer: the volume below lives there

            var go = new GameObject("PostFx");
            var fx = go.AddComponent<PostFx>();
            fx._low = low;
            // a fresh profile (Instantiate would share the asset's override objects, and animating them would edit it)
            fx._p = ScriptableObject.CreateInstance<VolumeProfile>();
            Configure(fx._p, low);
            fx._p.TryGet(out fx._col);
            fx._p.TryGet(out fx._bloom);
            fx._p.TryGet(out fx._vig);
            fx._p.TryGet(out fx._ca);
            fx._p.TryGet(out fx._lens);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            vol.sharedProfile = fx._p;
            return fx;
        }

        /// <summary>The crash: a chromatic / lens jolt and a short bright flash that die away (real time).</summary>
        public void Punch(float strength = 1f)
        {
            _punch = Mathf.Max(_punch, strength);
            _flash = Mathf.Max(_flash, strength);
        }

        /// <summary>A short soft flash (power-up pickup), tinted by the vignette state of that power-up.</summary>
        public void Flash(float strength = 0.35f) => _flash = Mathf.Max(_flash, strength);

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _punch = Mathf.MoveTowards(_punch, 0, dt * 2.2f);
            _flash = Mathf.MoveTowards(_flash, 0, dt * 3.5f);
            _dead = Ease(_dead, Dead ? 1 : 0, dt, 1.2f);
            _pause = Ease(_pause, Paused ? 1 : 0, dt, 8f);
            _slow = Ease(_slow, Slow ? 1 : 0, dt, 4f);
            _shield = Ease(_shield, Shield ? 1 : 0, dt, 4f);
            _fly = Ease(_fly, Fly ? 1 : 0, dt, 3f);

            float p2 = _punch * _punch;
            _col.postExposure.value = Exposure + _flash * 0.9f - _dead * 0.25f - _pause * 0.45f;
            _col.saturation.value = Saturation - 30f * _slow - 55f * _dead - 45f * _pause;
            _col.contrast.value = Contrast + 10f * _dead;
            _col.colorFilter.value = Color.Lerp(Color.white, new Color(0.85f, 0.8f, 1f), _slow);

            _bloom.intensity.value = BloomIntensity + 0.5f * _shield + 0.35f * _fly + 1.2f * _flash;

            var tint = VignetteColor;
            tint = Color.Lerp(tint, new Color(0.22f, 0.1f, 0.45f), _slow);
            tint = Color.Lerp(tint, new Color(0.4f, 0.3f, 0.05f), _shield);
            tint = Color.Lerp(tint, new Color(0.4f, 0f, 0f), p2);
            _vig.color.value = tint;
            _vig.intensity.value = VignetteIntensity + 0.06f * Speed01 + 0.1f * _slow + 0.06f * _shield + 0.15f * _dead + 0.2f * p2;

            float baseCa = _low ? 0f : BaseAberration;
            _ca.intensity.value = Mathf.Clamp01(baseCa + 0.08f * Speed01 * (_low ? 0f : 1f) + 0.2f * _fly + 0.9f * p2);
            _lens.intensity.value = -0.06f * _fly - 0.28f * p2;
        }

        static float Ease(float v, float target, float dt, float rate) => Mathf.Lerp(v, target, 1f - Mathf.Exp(-rate * dt));

        void OnDestroy()
        {
            if (_p != null) Destroy(_p);
        }
    }
}
