using System.Collections.Generic;
using UnityEngine;

namespace DachshundDash
{
    /// <summary>One world-space particle system per effect, emitted on demand (no allocations per burst).</summary>
    public static class Fx
    {
        enum Kind { Puff, Dirt, Dust, Sparkle, Burst, Feather, Star, Wind, Jet, ArrowBack, ArrowUp }

        static Transform _root;
        static readonly Dictionary<Kind, ParticleSystem> Systems = new Dictionary<Kind, ParticleSystem>();
        static Texture2D _dot, _star, _arrowLeft, _arrowUp;

        static ParticleSystem Get(Kind k)
        {
            if (_root == null)
            {
                Systems.Clear();
                _root = new GameObject("Fx").transform;
            }
            if (Systems.TryGetValue(k, out var ps) && ps != null) return ps;
            ps = Create(k);
            Systems[k] = ps;
            return ps;
        }

        internal static Material Mat(bool additive, Texture tex)
        {
            var m = new Material(Art.Mat(additive ? "ParticleAdd" : "ParticleAlpha"));
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            return m;
        }

        static ParticleSystem Create(Kind k)
        {
            if (_dot == null) _dot = MeshUtil.SoftDot();
            if (_star == null) _star = MeshUtil.Star();
            if (_arrowLeft == null) _arrowLeft = MeshUtil.Arrow(true);
            if (_arrowUp == null) _arrowUp = MeshUtil.Arrow(false);
            var go = new GameObject("fx_" + k);
            go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.15f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = fade;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            var r = go.GetComponent<ParticleSystemRenderer>();
            Texture tex = _dot; bool add = false;
            switch (k)
            {
                case Kind.Puff:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                    main.startColor = new Color(0.85f, 0.85f, 0.95f, 0.8f);
                    sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.4f));
                    break;
                case Kind.Dirt:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.36f, 0.24f, 0.14f), new Color(0.5f, 0.34f, 0.2f));
                    main.gravityModifier = 1.6f;
                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    shape.rotation = new Vector3(-90, 0, 0);
                    break;
                case Kind.Dust:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                    main.startColor = new Color(0.7f, 0.7f, 0.8f, 0.45f);
                    sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 1.5f));
                    break;
                case Kind.Sparkle:
                    tex = _star; add = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
                    main.startColor = new Color(1f, 0.92f, 0.6f);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI);
                    sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1f, 1, 0f));
                    break;
                case Kind.Burst:
                    tex = _dot; add = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                    sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1f, 1, 0f));
                    break;
                case Kind.Feather:
                    var feather = Art.Sprite("icon_doublejump");
                    tex = feather != null ? (Texture)feather.texture : _dot;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                    main.startColor = feather != null ? new Color(0.25f, 0.25f, 0.3f) : new Color(0.1f, 0.1f, 0.12f);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    main.gravityModifier = 0.15f;
                    var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
                    break;
                case Kind.Star:
                    tex = _star; add = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.32f);
                    main.startColor = new Color(1f, 0.9f, 0.3f);
                    var rot2 = ps.rotationOverLifetime; rot2.enabled = true; rot2.z = new ParticleSystem.MinMaxCurve(4f);
                    break;
                case Kind.Jet:
                    // jet flame: hot yellow core going red, shrinking as it trails behind
                    add = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.42f);
                    shape.radius = 0.06f;
                    r.renderMode = ParticleSystemRenderMode.Stretch;
                    r.velocityScale = 0.06f;
                    r.lengthScale = 1.6f;
                    var jet = new Gradient();
                    jet.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0), new GradientColorKey(new Color(1f, 0.3f, 0.1f), 0.35f),
                                        new GradientColorKey(new Color(0.8f, 0.05f, 0.05f), 1) },
                                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0, 1) });
                    col.color = jet;
                    sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1f, 1, 0.2f));
                    break;
                case Kind.ArrowBack:
                case Kind.ArrowUp:
                    tex = k == Kind.ArrowBack ? _arrowLeft : _arrowUp; add = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.2f);
                    shape.radius = 0.05f;
                    var pulse = new Gradient();
                    pulse.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
                    col.color = pulse;
                    sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.1f));
                    break;
                case Kind.Wind:
                    add = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                    main.startColor = new Color(0.7f, 0.85f, 1f, 0.6f);
                    r.renderMode = ParticleSystemRenderMode.Stretch;
                    r.velocityScale = 0.08f;
                    r.lengthScale = 6f;
                    break;
            }
            r.sharedMaterial = Mat(add, tex);
            ps.Play();
            return ps;
        }

        static void Emit(Kind k, Vector3 pos, int count, Color? color = null, Vector3? velocity = null)
        {
            var ps = Get(k);
            var ep = new ParticleSystem.EmitParams { position = pos, applyShapeToPosition = true };
            if (color.HasValue) ep.startColor = color.Value;
            if (velocity.HasValue) ep.velocity = velocity.Value;
            ps.Emit(ep, count);
        }

        public static void Puff(Vector3 p, float scale = 1f) => Emit(Kind.Puff, p, Mathf.RoundToInt(10 * scale));
        public static void Dirt(Vector3 p, int n) => Emit(Kind.Dirt, p, n);
        public static void Dust(Vector3 p, int n) => Emit(Kind.Dust, p, n);
        public static void Sparkle(Vector3 p) => Emit(Kind.Sparkle, p, 8);
        public static void Burst(Vector3 p, Color c) => Emit(Kind.Burst, p, 24, c);
        public static void Feathers(Vector3 p, int n) => Emit(Kind.Feather, p, n);
        public static void Stars(Vector3 p) => Emit(Kind.Star, p, 7);
        public static void Wind(Vector3 p, Vector3 v) => Emit(Kind.Wind, p, 1, null, v);
        public static void Jet(Vector3 p, Vector3 v) => Emit(Kind.Jet, p, 1, null, v);
        public static void ArrowBack(Vector3 p, Vector3 v) => Emit(Kind.ArrowBack, p, 1, new Color(0.35f, 0.65f, 1f), v);
        public static void ArrowUp(Vector3 p, Vector3 v) => Emit(Kind.ArrowUp, p, 1, new Color(1f, 0.55f, 0.1f), v);
    }
}
