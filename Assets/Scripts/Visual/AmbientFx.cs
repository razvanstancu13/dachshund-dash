using UnityEngine;

namespace DachshundDash
{
    /// <summary>Night air around the run: a few motes drifting in the lamp light (street: warm dust, alley: cool ash),
    /// fireflies blinking over the backyards. World-space particles spawned in a box around the view, so the dog
    /// runs past them and they give the scene depth. The firefly glow is bright enough to catch the bloom.</summary>
    public class AmbientFx : MonoBehaviour
    {
        struct Look
        {
            public Color Color; public float Glow, SizeMin, SizeMax, Rate, Fall; public bool Blink;
        }

        static readonly Look[] Looks =
        {
            new Look { Color = new Color(1f, 0.85f, 0.6f, 0.45f), Glow = 1.2f, SizeMin = 0.025f, SizeMax = 0.06f, Rate = 1f, Fall = -0.05f },   // street
            new Look { Color = new Color(0.75f, 0.82f, 1f, 0.4f), Glow = 1.1f, SizeMin = 0.03f, SizeMax = 0.07f, Rate = 1.2f, Fall = -0.18f },  // alley
            new Look { Color = new Color(0.8f, 1f, 0.35f, 1f), Glow = 3.2f, SizeMin = 0.05f, SizeMax = 0.09f, Rate = 0.8f, Fall = 0.03f, Blink = true },  // backyard
        };

        ParticleSystem _ps;
        Material _mat;
        Gradient _steady, _blink;
        int _biome = -1;
        float _glow = 1f, _glowTarget = 1f;
        bool _behind;
        float _rateScale;

        public static AmbientFx Create()
        {
            var go = new GameObject("AmbientFx");
            var fx = go.AddComponent<AmbientFx>();
            fx.Build();
            return fx;
        }

        void Build()
        {
            _rateScale = PostFx.LowTier ? 0.5f : 1f;
            _ps = gameObject.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5.5f);
            main.startSpeed = 0f;
            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            var noise = _ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            _steady = new Gradient();
            _steady.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            _blink = new Gradient();
            _blink.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                           new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(0.1f, 0.3f), new GradientAlphaKey(0.9f, 0.45f),
                                   new GradientAlphaKey(0.05f, 0.62f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0, 1) });
            var col = _ps.colorOverLifetime;
            col.enabled = true;

            _mat = Fx.Mat(true, MeshUtil.SoftDot());
            GetComponent<ParticleSystemRenderer>().sharedMaterial = _mat;
            SetView(false);
            SetBiome(0);
            _ps.Play();
        }

        /// <summary>Side view: a box around the dog's stretch of street; behind view: a long box down the run.</summary>
        public void SetView(bool behind)
        {
            _behind = behind;
            var shape = _ps.shape;
            shape.scale = behind ? new Vector3(40f, 3f, 9f) : new Vector3(16f, 3f, 9f);
            shape.position = behind ? new Vector3(20f, 0, 0) : new Vector3(1f, 0, 1.5f);   // side: a little behind the lanes too
            ApplyRate();
        }

        void SetBiome(int b)
        {
            if (b == _biome) return;
            _biome = b;
            var look = Looks[b];
            var main = _ps.main;
            main.startColor = look.Color;
            main.startSize = new ParticleSystem.MinMaxCurve(look.SizeMin, look.SizeMax);
            var vel = _ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            vel.y = new ParticleSystem.MinMaxCurve(look.Fall - 0.05f, look.Fall + 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            var col = _ps.colorOverLifetime;
            col.color = look.Blink ? _blink : _steady;
            _glowTarget = look.Glow;
            ApplyRate();
        }

        void ApplyRate()
        {
            if (_biome < 0) return;
            var em = _ps.emission;
            em.rateOverTime = Looks[_biome].Rate * _rateScale * (_behind ? 45f : 24f);
        }

        /// <summary>Called every frame with the view position and the biome under it.</summary>
        public void Follow(float camX, float camZ, Biome biome)
        {
            SetBiome((int)biome);
            transform.position = new Vector3(camX, 1.6f, camZ);
            _glow = Mathf.MoveTowards(_glow, _glowTarget, Time.deltaTime * 2f);
            _mat.SetColor("_BaseColor", new Color(_glow, _glow, _glow, 1f));
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
