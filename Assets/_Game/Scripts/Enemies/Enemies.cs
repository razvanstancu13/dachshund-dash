using UnityEngine;

namespace DachshundDash
{
    /// <summary>Enemies report a danger box while they can hurt and turn to face the dog. Moles and cats act in their
    /// own lane; crows pick the dog's lane when they dive.</summary>
    public abstract class Enemy : Entity
    {
        [HideInInspector] public Transform Model;     // the art root (pivot bottom centre, faces +X unturned)
        [HideInInspector] public ClipPlayer Clips;
        public float Surface;                         // ground / platform top under the enemy
        public bool Knocked;
        Vector3 _knockVel, _knockSpin;
        Quaternion _facing;

        protected GameConfig Cfg => GameConfig.I;

        public abstract bool Dangerous(out Box box);

        public override void OnSpawn()
        {
            Knocked = false;
            _facing = Quaternion.Euler(0, 180, 0);    // towards the dog's side (-X) until the first turn
            if (Model) Model.localRotation = _facing;
        }

        /// <summary>The art's turn towards a point on the ground, eased (about 1.5 turns a second).</summary>
        protected Quaternion TurnTowards(Vector3 target, float dt)
        {
            var d = target - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 1e-4f)
            {
                // the art faces +X: yaw a maps +X to (cos a, 0, -sin a)
                var want = Quaternion.Euler(0, Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg, 0);
                _facing = Quaternion.RotateTowards(_facing, want, 540f * dt);
            }
            return _facing;
        }

        protected Quaternion FaceDog(Player dog, float dt) => TurnTowards(dog.transform.position, dt);

        public void Knock()
        {
            if (Knocked) return;
            Knocked = true;
            _knockVel = new Vector3(Random.Range(5f, 8f), Random.Range(5f, 8f), Random.Range(0f, 3f));
            _knockSpin = Random.insideUnitSphere * 720f;
            Fx.Puff(transform.position + Vector3.up * 0.4f, 1f);
        }

        protected bool TickKnocked(float dt)
        {
            if (!Knocked) return false;
            _knockVel += Vector3.down * 25f * dt;
            transform.position += _knockVel * dt;
            transform.rotation = Quaternion.Euler(_knockSpin * dt) * transform.rotation;
            if (transform.position.y < -10f) gameObject.SetActive(false);
            return true;
        }

        protected virtual void OnDisable() { transform.rotation = Quaternion.identity; }

        protected float Ahead(Player dog) => X - dog.X;
    }

    /// <summary>Hidden under a faint patch of turned soil; bursts up just before the dog reaches it and bites once it
    /// is out, so the player sees it (and a hit shows the mole, not just a sudden death).</summary>
    public class Mole : Enemy
    {
        enum S { Hidden, Up, Down }
        S _s;
        float _t, _nextDirt;
        public Transform Hill;                    // the ground decal (it shivers when the mole is about to come up)
        const float HiddenY = -0.45f, UpTime = 1.0f, PopAhead = 1.8f, BiteFrom = 0.12f, BiteTo = 0.75f;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (Model) Model.localScale = Vector3.one;
            _s = S.Hidden; _t = 0;
            if (Model) Model.localPosition = new Vector3(0, HiddenY, 0);
            if (Hill) Hill.localScale = Vector3.one;
            Clips?.Play("Idle", 0);
        }

        public override bool Dangerous(out Box box)
        {
            box = Box.FromBottom(new Vector3(X, Surface, Cfg.LaneZ(Lane)), 0.35f, Cfg.moleReach, 0.45f);
            return _s == S.Up && _t > BiteFrom && _t < BiteTo && !Knocked;
        }

        public override void Tick(Player dog, float dt)
        {
            if (TickKnocked(dt)) return;
            _t += dt;
            float ahead = Ahead(dog);
            bool sameLane = dog.InLane(Lane);
            var facing = FaceDog(dog, dt);
            if (Model) Model.localRotation = facing;
            switch (_s)
            {
                case S.Hidden:
                    if (sameLane && ahead > 0 && ahead < Cfg.moleTelegraphDistance)
                    {
                        float k = 1 + 0.08f * Mathf.Sin(Time.time * 40f);
                        if (Hill) Hill.localScale = new Vector3(k, 1, k);
                        if (Time.time > _nextDirt)
                        {
                            Fx.Dirt(transform.position + Vector3.up * 0.05f, 3);
                            _nextDirt = Time.time + 0.3f;
                        }
                    }
                    if (sameLane && ahead < PopAhead && ahead > -0.5f)
                    {
                        _s = S.Up; _t = 0;
                        Clips?.Play("Attack", 0.05f, true);
                        Services.Audio.Play(Sfx.Mole, 0.9f, Random.Range(0.95f, 1.1f));
                        Fx.Dirt(transform.position + Vector3.up * 0.2f, 18);
                        if (Hill) Hill.localScale = Vector3.one;
                    }
                    break;
                case S.Up:
                    if (Model)
                    {
                        Model.localPosition = new Vector3(0, Mathf.Lerp(HiddenY, 0.05f, Mathf.Clamp01(_t / BiteFrom)), 0);
                        if (Clips == null)
                        {
                            // no rig: squash and stretch on the way out, then two snapping nods
                            float s = Mathf.Sin(Mathf.Clamp01(_t / 0.25f) * Mathf.PI);
                            Model.localScale = new Vector3(1 - 0.15f * s, 1 + 0.3f * s, 1 - 0.15f * s);
                            float nod = _t > 0.15f ? Mathf.Sin((_t - 0.15f) * 28f) * 18f * Mathf.Clamp01(1 - (_t - 0.15f) / 0.5f) : 0;
                            Model.localRotation = facing * Quaternion.Euler(0, 0, nod);
                        }
                    }
                    if (_t > UpTime) { _s = S.Down; _t = 0; }
                    break;
                case S.Down:
                    if (Model)
                    {
                        Model.localPosition = new Vector3(0, Mathf.Lerp(0.05f, HiddenY, Mathf.Clamp01(_t / 0.3f)), 0);
                        Model.localScale = Vector3.one;
                    }
                    if (_t > 0.3f) { _s = S.Hidden; Clips?.Play("Idle", 0.1f); }
                    break;
            }
        }
    }

    /// <summary>Sits facing the dog; when the dog gets close in its lane it winds up and charges like an arrow.</summary>
    public class Cat : Enemy
    {
        enum S { Sit, Windup, Charge }
        S _s;
        float _t, _travel;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _s = S.Sit; _t = 0; _travel = 0;
            Clips?.Play(Clips.Has("Sit") ? "Sit" : "Idle", 0);
        }

        public override bool Dangerous(out Box box)
        {
            box = Box.FromBottom(new Vector3(X, transform.position.y, Cfg.LaneZ(Lane)), 0.35f, 0.38f, 0.4f);
            return !Knocked;
        }

        public float TriggerDistance(float dogSpeed) =>
            Mathf.Min((dogSpeed + Cfg.catSpeed) * Cfg.catReactionTime, Cfg.maxTriggerDistance);

        public override void Tick(Player dog, float dt)
        {
            if (TickKnocked(dt)) return;
            _t += dt;
            float ahead = Ahead(dog);
            // watching the dog while it sits and winds up; charging, it looks where it runs (once the dog has jumped
            // over it, turning round would make it run backwards)
            if (Model) Model.localRotation = _s == S.Charge ? TurnTowards(transform.position + Vector3.left, dt) : FaceDog(dog, dt);
            switch (_s)
            {
                case S.Sit:
                    if (dog.InLane(Lane) && ahead > 0 && ahead < TriggerDistance(dog.Speed))
                    {
                        _s = S.Windup; _t = 0;
                        if (Clips != null && Clips.Has("Attack")) Clips.Play("Attack", 0.05f, true);
                        Services.Audio.Play(Sfx.Cat, 0.9f, Random.Range(0.95f, 1.1f));
                    }
                    break;
                case S.Windup:
                    if (_t > Cfg.catWindup) { _s = S.Charge; _t = 0; Clips?.Play("Run", 0.08f); if (Clips) Clips.Speed = 1.4f; }
                    break;
                case S.Charge:
                    float step = Cfg.catSpeed * dt;
                    transform.position += Vector3.left * step;
                    _travel += step;
                    if (Random.value < 0.5f) Fx.Dust(transform.position, 1);
                    // stays on its platform / the ground: drop to the ground when it runs off a platform
                    float ground = Level.SurfaceAt(Lane, X, transform.position.y + 0.05f);
                    var p = transform.position; p.y = Mathf.MoveTowards(p.y, ground, 6f * dt); transform.position = p;
                    if (_travel > 40f) gameObject.SetActive(false);
                    break;
            }
        }

        protected override void OnDisable() { base.OnDisable(); if (Clips) Clips.Speed = 1f; }
    }

    /// <summary>Hovers at mid height above any lane. It caws when it spots the dog; when the dog comes close it dives
    /// at the lane the dog is in at that moment (locked: a quick lane change makes it miss), bottoming out above a
    /// crouching dog.</summary>
    public class Crow : Enemy
    {
        enum S { Hover, Dive }
        S _s;
        float _t, _T, _startY, _baseY, _startZ, _targetZ;
        bool _cawed;
        const float HalfH = 0.2f, SpotExtra = 7f;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _s = S.Hover; _t = Random.value * 3f;
            _cawed = false;
            _baseY = Surface + Cfg.crowHeight;
            var p = transform.position; p.y = _baseY; transform.position = p;
            Clips?.Play("Flap", 0);
            if (Clips) Clips.Speed = 1f;
        }

        public override bool Dangerous(out Box box)
        {
            var c = transform.position + Vector3.up * HalfH;
            box = new Box(new Vector3(X - 0.3f, c.y - HalfH, c.z - 0.4f), new Vector3(X + 0.3f, c.y + HalfH, c.z + 0.4f));
            return !Knocked;
        }

        float TriggerDistance(Player dog) =>
            Mathf.Min((dog.Speed + Cfg.crowSpeed) * Cfg.crowReactionTime + 1f, Cfg.maxTriggerDistance);

        public override void Tick(Player dog, float dt)
        {
            if (TickKnocked(dt)) return;
            _t += dt;
            float ahead = Ahead(dog);
            var p = transform.position;
            float pitch = 0f;
            switch (_s)
            {
                case S.Hover:
                    p.y = _baseY + Mathf.Sin(_t * 2.2f) * 0.1f;
                    if (!_cawed && ahead > 0 && ahead < TriggerDistance(dog) + SpotExtra)
                    {
                        _cawed = true;
                        Services.Audio.Play(Sfx.Caw, 0.8f, Random.Range(0.92f, 1.1f));
                    }
                    if (ahead > 0 && ahead < TriggerDistance(dog))
                    {
                        // lock on the dog's lane now; the dive gets there before it bottoms out
                        Lane = dog.TargetLane;
                        _startZ = p.z;
                        _targetZ = Cfg.LaneZ(Lane);
                        _s = S.Dive; _t = 0; _startY = p.y;
                        _T = Mathf.Max(0.35f, (ahead - 0.5f) / (dog.Speed + Cfg.crowSpeed));
                        Services.Audio.Play(Sfx.Crow, 0.85f, Random.Range(0.95f, 1.08f));
                        if (Clips) Clips.Speed = 1.8f;
                    }
                    break;
                case S.Dive:
                    p.x -= Cfg.crowSpeed * dt;
                    float bottom = Surface + Cfg.crowDiveBottom;
                    if (_t < _T) p.y = Mathf.Lerp(_startY, bottom, Mathf.Sin(Mathf.PI * 0.5f * (_t / _T)));
                    else p.y = bottom + (_t - _T) * (_t - _T) * 6f;
                    float u = Mathf.Clamp01(_t / (_T * 0.8f));
                    p.z = Mathf.Lerp(_startZ, _targetZ, u * u * (3 - 2 * u));
                    pitch = _t < _T ? 25f : -25f;
                    if (_t > _T + 2f) gameObject.SetActive(false);
                    break;
            }
            transform.position = p;
            // hovering it watches the dog; diving it looks where it flies (on past the dog after a miss)
            var facing = _s == S.Dive ? TurnTowards(p + Vector3.left + Vector3.forward * (_targetZ - p.z), dt) : FaceDog(dog, dt);
            if (Model) Model.localRotation = facing * Quaternion.Euler(0, 0, pitch);
        }
    }
}
