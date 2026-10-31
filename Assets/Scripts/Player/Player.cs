using System;
using UnityEngine;

namespace DachshundDash
{
    /// <summary>The dachshund: a kinematic runner on three lanes (no physics forces, fully deterministic).</summary>
    public class Player : MonoBehaviour
    {
        public event Action<int> BonesChanged;
        public event Action<PowerUpType> PowerUpCollected;
        public event Action Died;

        public Level Level;
        public InputRouter Input;
        public readonly PowerUps PowerUps = new PowerUps();

        GameConfig _cfg;
        ClipPlayer _clips;
        Transform _model;
        GameObject _aura;
        float _pitch;
        float _fxT;

        public int Bones { get; private set; }
        public float Speed { get; private set; }
        public float X => transform.position.x;
        public bool Dead { get; private set; }
        public bool Running { get; private set; }
        public bool Flying => PowerUps.Active(PowerUpType.Fly);
        public bool Grounded => _grounded;
        public bool Crouching => _crouch;
        public int TargetLane => _lane;
        /// <summary>Run speed multiplier (the Backspace cheat halves it).</summary>
        public float SpeedScale = 1f;
        /// <summary>Test aid (auto-capture, play-mode tests): obstacles and enemies are knocked away, no glow.</summary>
        public bool GodMode;
        /// <summary>Prop showcase: obstacles and enemies are passed through untouched (bones and power-ups still count).</summary>
        public bool NoCollide;

        /// <summary>Puts the dog in a lane before the run starts.</summary>
        public void SetLane(int lane)
        {
            _lane = Mathf.Clamp(lane, 0, 2);
            _z = _cfg.LaneZ(_lane);
            var p = transform.position; p.z = _z; transform.position = p;
        }

        int _lane = 1;
        float _z, _y, _vy, _runTime, _airTime, _jumpBufferT, _laneBufferT, _bumpT;
        int _laneBufferDir;
        bool _grounded = true, _crouch, _usedDoubleJump, _slowFall, _wasFlying;

        public void Init(Level level, InputRouter input)
        {
            _cfg = GameConfig.I;
            Level = level;
            Input = input;
            var art = Art.Spawn("dog", _cfg.dogHeight, new Color(0.12f, 0.09f, 0.08f), new Vector3(_cfg.dogLength, _cfg.dogHeight, 0.3f), transform);
            _model = art.transform;
            _clips = ClipPlayer.Attach(art, "dog");
            _clips?.Play("Idle", 0);
            _z = _cfg.LaneZ(_lane);
            transform.position = new Vector3(0, 0, _z);

            // invincibility: a soft green glow behind the dog (additive billboard)
            _aura = MeshUtil.Make("aura", Resources.GetBuiltinResource<Mesh>("Quad.fbx"), new Material(Art.Mat("Halo")), transform);
            _aura.transform.localPosition = new Vector3(0, 0.35f, 0);
            _aura.AddComponent<FaceCamera>().Push = 0.3f;
            var am = _aura.GetComponent<Renderer>().sharedMaterial;
            var green = new Color(0.35f, 1f, 0.45f, 0.6f);
            am.color = green;
            if (am.HasProperty("_BaseColor")) am.SetColor("_BaseColor", green);
            _aura.SetActive(false);
            input.Jump += OnJump;
            input.LaneLeft += () => RequestLane(-1);
            input.LaneRight += () => RequestLane(+1);

            input.SlowCheat += () => SpeedScale = SpeedScale < 1f ? 1f : 0.5f;
            input.Cheat += t =>
            {
                if (!Running || Dead) return;
                PowerUps.Collect(t, _cfg);                                // same as picking one up
                OnActivated(t);
            };
        }

        public void StartRunning() { Running = true; _runTime = 0; }

        public bool InLane(int lane) => Mathf.Abs(_z - _cfg.LaneZ(lane)) < 0.85f;

        int NearestLane => Mathf.Clamp(Mathf.RoundToInt(1 - _z / _cfg.laneSpacing), 0, 2);

        float BodyHeight => (_crouch ? _cfg.dogCrouchHeight : _cfg.dogHeight) * _cfg.hitboxScale;

        public Box HitBox => Box.FromBottom(new Vector3(X, _y, _z), _cfg.dogLength * 0.5f * _cfg.hitboxScale, BodyHeight, 0.22f);

        // ------------------------------------------------------------ input

        void OnJump()
        {
            if (!Running || Dead) return;
            if (TryJump()) return;
            _jumpBufferT = _cfg.jumpBuffer;
        }

        bool TryJump()
        {
            if (Flying) return false;
            if (_grounded || _airTime < _cfg.coyoteTime)
            {
                _vy = _cfg.JumpVelocity;
                _grounded = false;
                _airTime = _cfg.coyoteTime;
                _usedDoubleJump = false;
                Services.Audio.Play(Sfx.Whoosh, 0.5f, 1.1f);
                Fx.Dust(new Vector3(X, _y, _z), 4);
                PlayJumpClip();
                return true;
            }
            if (PowerUps.Active(PowerUpType.DoubleJump) && !_usedDoubleJump)
            {
                _vy = _cfg.JumpVelocity;
                _usedDoubleJump = true;
                Services.Audio.Play(Sfx.Whoosh, 0.6f, 1.3f);
                Fx.Feathers(new Vector3(X, _y + 0.2f, _z), 8);
                PlayJumpClip();
                return true;
            }
            return false;
        }

        /// <summary>In the air the dog stretches out: front legs forward, hind legs back (the Glide clip); the body
        /// tilts with the vertical speed in Animate (front up rising, back up falling).</summary>
        string AirClip => _clips != null && _clips.Has("Glide") ? "Glide" : "Jump";

        void PlayJumpClip()
        {
            if (_clips == null) return;
            _clips.Speed = 1f;
            _clips.Play(AirClip, 0.06f, true);
        }

        void RequestLane(int dir)
        {
            if (!Running || Dead) return;
            if (!TrySwitch(dir)) { _laneBufferDir = dir; _laneBufferT = _cfg.inputBuffer; }
        }

        bool TrySwitch(int dir)
        {
            int target = _lane + dir;
            if (target < 0 || target > 2) return true;          // edge: ignore, nothing to buffer
            if (Level.LaneBlocked(target, HitBox))
            {
                if (_bumpT <= 0)
                {
                    _bumpT = 0.25f;
                    Services.Audio.Play(Sfx.Bump, 0.5f);
                }
                return false;
            }
            _lane = target;
            Services.Audio.Play(Sfx.Whoosh, 0.25f, 1.5f);
            return true;
        }

        // ------------------------------------------------------------ simulation

        public void Tick(float dt, float unscaledDt)
        {
            PowerUps.Tick(unscaledDt);
            Services.Audio.SetLoop("sfx_jet", Flying && Running && !Dead, 0.45f);      // jet engine while flying
            if (Dead) { Animate(); return; }
            if (!Running) { Animate(); return; }

            _runTime += dt;
            // a short walk-in after GO, then the speed curve
            float walkIn = Mathf.Clamp01(_runTime / 0.8f);
            Speed = Mathf.Lerp(2.5f, _cfg.SpeedAt(_runTime), walkIn * walkIn) * SpeedScale;

            var pos = transform.position;
            pos.x += Speed * dt;

            // lanes
            float tz = _cfg.LaneZ(_lane);
            _z = Mathf.MoveTowards(_z, tz, _cfg.laneSpacing / _cfg.laneSwitchTime * dt);
            if (_laneBufferT > 0)
            {
                _laneBufferT -= dt;
                if (TrySwitch(_laneBufferDir)) _laneBufferT = 0;
            }
            if (_bumpT > 0) _bumpT -= dt;

            // vertical
            float prevY = _y;
            bool held = Input.CrouchHeld;
            if (Flying)
            {
                _wasFlying = true;
                _y = Mathf.MoveTowards(_y, _cfg.flyAltitude, 5f * dt);
                _vy = 0; _grounded = false; _crouch = false;
                // jet flame out of the back, trailing behind
                for (int i = 0; i < 4; i++)
                    Fx.Jet(new Vector3(X - _cfg.dogLength * 0.5f, _y + 0.22f, _z), new Vector3(Speed - 7f, UnityEngine.Random.Range(-0.4f, 0.4f), 0));
            }
            else
            {
                if (_wasFlying) { _wasFlying = false; _slowFall = true; }
                float g = _cfg.gravity;
                if (_slowFall) g *= 0.12f;
                else if (!_grounded && held && _vy < 6f) g *= _cfg.fastFallMultiplier;
                if (!_grounded)
                {
                    _vy -= g * dt;
                    if (_slowFall) _vy = Mathf.Max(_vy, -2.2f);
                    _y += _vy * dt;
                    _airTime += dt;
                }
                float ground = Level.SurfaceAt(NearestLane, pos.x, Mathf.Max(prevY, _y) + 0.02f, _cfg.dogLength * 0.3f);
                if (_grounded)
                {
                    if (ground < _y - 0.02f) { _grounded = false; _airTime = 0; _vy = 0; }   // ran off a platform
                    else _y = ground;
                }
                else if (_y <= ground && _vy <= 0)
                {
                    _y = ground; _vy = 0; _grounded = true; _slowFall = false;
                    Fx.Dust(new Vector3(X, _y, _z), 6);
                    Services.Audio.Play(Sfx.Bump, 0.2f, 1.4f);
                    if (_jumpBufferT > 0) { _jumpBufferT = 0; TryJump(); }
                }
                if (_jumpBufferT > 0) _jumpBufferT -= dt;
                _crouch = held && _grounded;
            }
            if (_grounded) _airTime = 0;

            pos.y = _y; pos.z = _z;
            transform.position = pos;

            if (_grounded && UnityEngine.Random.value < Speed * dt * 0.8f) Fx.Dust(new Vector3(X - 0.4f, _y, _z), 1);

            Collide(prevY);
            Animate();
            UpdateAura();
            PowerUpFx(dt);
            SlideFx();
        }

        void Collide(float prevY)
        {
            var box = HitBox;
            bool inv = PowerUps.Active(PowerUpType.Invincible) || PowerUps.Grace > 0 || GodMode;
            foreach (var s in Level.Solids)
            {
                if (NoCollide) break;
                if (s.Knocked || !InLane(s.Lane)) continue;
                var sb = s.Box;
                if (!box.Overlaps(sb)) continue;
                if (s.Walkable && prevY >= sb.Max.y - 0.12f) continue;      // standing on it
                if (inv) { s.Knock(); Services.Audio.Play(Sfx.Death, 0.35f, 1.6f); continue; }
                Die(true); return;
            }
            foreach (var e in Level.Enemies)
            {
                if (Flying || NoCollide) break;                            // gliding above everything
                if (!e.Dangerous(out var eb) || !box.Overlaps(eb)) continue;
                if (inv) { e.Knock(); Services.Audio.Play(Sfx.Death, 0.35f, 1.6f); continue; }
                Die(false); return;
            }
            var centre = new Vector3(X, _y + BodyHeight * 0.5f, _z);
            foreach (var b in Level.Bones)
            {
                if (b.Taken) continue;
                var bp = b.transform.position + Vector3.up * 0.1f;
                if (Mathf.Abs(bp.z - _z) > 0.7f) continue;
                if (Mathf.Abs(bp.x - X) < _cfg.dogLength * 0.5f + b.Radius * 0.5f && bp.y > _y - b.Radius && bp.y < _y + BodyHeight + b.Radius)
                {
                    b.Taken = true;
                    b.gameObject.SetActive(false);
                    Bones++;
                    Fx.Sparkle(bp);
                    Services.Audio.Play(Sfx.Bone, 0.7f, 1f + Mathf.Min(0.3f, (Bones % 10) * 0.03f));
                    BonesChanged?.Invoke(Bones);
                }
            }
            foreach (var p in Level.PowerUps)
            {
                if (p.Taken) continue;
                var pp = p.transform.position;
                if ((pp - centre).sqrMagnitude < (p.Radius + 0.45f) * (p.Radius + 0.45f))
                {
                    p.Taken = true;
                    p.gameObject.SetActive(false);
                    PowerUps.Collect(p.Type, _cfg);                       // active at once, replaces the running one
                    Fx.Burst(pp, PowerUpInfo.Color(p.Type));
                    OnActivated(p.Type);
                    PowerUpCollected?.Invoke(p.Type);
                }
            }
        }

        void OnActivated(PowerUpType t)
        {
            Services.Audio.Play(Sfx.PowerUp, 0.7f, 1.3f);
            Fx.Burst(transform.position + Vector3.up * 0.3f, PowerUpInfo.Color(t));
            if (t == PowerUpType.Fly) { _grounded = false; _vy = 0; }
            if (t == PowerUpType.DoubleJump) Fx.Feathers(transform.position + Vector3.up * 0.3f, 10);
        }

        /// <summary>The run ended by running into an obstacle (false: caught by an enemy).</summary>
        public bool HitObstacle { get; private set; }

        void Die(bool obstacle)
        {
            if (Dead) return;
            Dead = true;
            HitObstacle = obstacle;
            Services.Audio.SetLoop("sfx_jet", false, 0f);
            if (obstacle) Services.Audio.Play(Sfx.Crash, 1f, UnityEngine.Random.Range(0.95f, 1.05f));
            Running = false;
            Services.Audio.Play(Sfx.Death, 1f);
            Fx.Stars(transform.position + Vector3.up * 0.6f);
            Fx.Puff(transform.position + Vector3.up * 0.3f, 0.8f);
            if (_clips != null) { _clips.Speed = 1f; _clips.Play(_clips.Has("Death") ? "Death" : "Hit", 0.05f, true); }
            _aura.SetActive(false);
            Services.Audio.SetLoop("sfx_slide", false, 0f);
            if (_model) _model.localRotation = Quaternion.identity;
            Died?.Invoke();
        }

        /// <summary>After the death clip: the dog slides back a little and lands on the ground.</summary>
        public void DeathSettle(float dt)
        {
            var p = transform.position;
            p.x -= 1.5f * dt;
            _y = Mathf.MoveTowards(_y, Level.SurfaceAt(NearestLane, p.x, _y + 0.01f), 4f * dt);
            p.y = _y;
            transform.position = p;
        }

        void Animate()
        {
            float pitch = 0f;
            if (_clips == null)
            {
                if (_model) _model.localScale = new Vector3(1, _crouch ? 0.6f : 1f, 1);
            }
            else if (Dead) { }
            else if (!Running) { _clips.Speed = 1f; _clips.Play("Idle"); }
            else if (Flying || !_grounded)
            {
                // jump and fall share the stretched-out pose; rising lifts the front, falling (after a jump, off a
                // platform, or at the end of Fly) lifts the back
                _clips.Speed = 1f;
                _clips.Play(AirClip, 0.12f);
                pitch = Flying ? 3f : _vy > 0 ? Mathf.Min(25f, _vy * 3f) : Mathf.Max(-30f, _vy * 5f);
            }
            else if (_crouch && _clips.Has("LieDown"))
            {
                // slide: lying flat, the settle part of the clip skipped and the pose held
                if (_clips.Current != "LieDown") { _clips.Play("LieDown", 0.08f); _clips.SetNormalized(0.32f); }
                _clips.Speed = 0.05f;
            }
            else if (_crouch && _clips.Has("CrouchRun")) { _clips.Play("CrouchRun", 0.08f); _clips.Speed = Speed / 8f; }
            else if (Speed < 3f) { _clips.Play("Walk", 0.15f); _clips.Speed = Speed / 2.5f; }
            else
            {
                _clips.Play("Run", 0.12f);
                _clips.Speed = Mathf.Lerp(0.9f, 1.4f, Mathf.InverseLerp(_cfg.startSpeed, _cfg.maxSpeed, Speed));
            }
            if (_model && !Dead)
            {
                _pitch = Mathf.MoveTowards(_pitch, pitch, 180f * Time.deltaTime);
                _model.localRotation = Quaternion.Euler(0, 0, _pitch);       // +Z = nose up (the dog faces +X)
            }
        }

        /// <summary>Slide: dust at the legs and a drag sound while the dog slides along the ground.</summary>
        void SlideFx()
        {
            bool sliding = Running && !Dead && _crouch && _grounded;
            Services.Audio.SetLoop("sfx_slide", sliding, 0.55f);
            if (!sliding) return;
            Fx.Dust(new Vector3(X + 0.3f, _y + 0.03f, _z - 0.15f), 1);
            Fx.Dust(new Vector3(X - 0.35f, _y + 0.03f, _z - 0.15f), 1);
        }
        /// <summary>Invincible: a pulsing green glow behind the dog; it blinks during the grace second.</summary>
        void UpdateAura()
        {
            bool inv = PowerUps.Active(PowerUpType.Invincible);
            bool on = inv || PowerUps.Grace > 0;
            if (on && !inv) on = Mathf.Repeat(Time.unscaledTime * 8f, 1f) < 0.5f;
            if (_aura.activeSelf != on) _aura.SetActive(on);
            if (on) _aura.transform.localScale = new Vector3(2.0f, 1.4f, 1f) * (1f + 0.06f * Mathf.Sin(Time.time * 9f));
        }

        /// <summary>Slow: blue arrows streaming backwards by the legs. Double jump: orange arrows rising by the legs.</summary>
        void PowerUpFx(float dt)
        {
            _fxT += dt;
            if (_fxT < 0.07f) return;
            _fxT = 0;
            var feet = new Vector3(X + UnityEngine.Random.Range(-0.35f, 0.35f), _y + UnityEngine.Random.Range(0.04f, 0.18f), _z - 0.3f);
            if (PowerUps.Active(PowerUpType.Slow)) Fx.ArrowBack(feet, new Vector3(Speed - 4f, 0, 0));
            if (PowerUps.Active(PowerUpType.DoubleJump) && UnityEngine.Random.value < 0.7f) Fx.ArrowUp(feet, new Vector3(Speed, 2.2f, 0));
        }
    }
    /// <summary>The active power-up (at most one): it starts when collected and a newly collected one replaces it.
    /// Timers run on unscaled time, so Slow does not slow itself.</summary>
    public class PowerUps
    {
        public event Action Changed;
        float _timer, _duration;
        public float Grace { get; private set; }
        /// <summary>The running power-up, or null.</summary>
        public PowerUpType? Current { get; private set; }

        public bool Active(PowerUpType t) => Current == t;
        public float Remaining01 => _duration > 0 ? _timer / _duration : 0;

        /// <summary>Starts t at full duration, cancelling the one running (losing invincibility leaves a grace second).</summary>
        public void Collect(PowerUpType t, GameConfig cfg)
        {
            if (Current == PowerUpType.Invincible && t != PowerUpType.Invincible) Grace = 1f;
            Current = t;
            _timer = _duration = t == PowerUpType.Fly ? cfg.flyDuration : t == PowerUpType.Slow ? cfg.slowDuration
                               : t == PowerUpType.Invincible ? cfg.invincibleDuration : cfg.doubleJumpDuration;
            Changed?.Invoke();
        }

        public void Tick(float dt)
        {
            if (Current != null)
            {
                _timer -= dt;
                if (_timer <= 0)
                {
                    if (Current == PowerUpType.Invincible) Grace = 1f;
                    Current = null;
                    _timer = 0;
                    Changed?.Invoke();
                }
            }
            if (Grace > 0) Grace = Mathf.Max(0, Grace - dt);
        }

        public void StopAll()
        {
            Current = null;
            _timer = 0;
            Grace = 0;
            Changed?.Invoke();
        }
    }
}