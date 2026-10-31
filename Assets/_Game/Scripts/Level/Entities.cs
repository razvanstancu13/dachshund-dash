using UnityEngine;

namespace DachshundDash
{
    public struct Box
    {
        public Vector3 Min, Max;
        public Box(Vector3 min, Vector3 max) { Min = min; Max = max; }

        public static Box FromBottom(Vector3 bottomCentre, float halfX, float height, float halfZ) =>
            new Box(bottomCentre + new Vector3(-halfX, 0, -halfZ), bottomCentre + new Vector3(halfX, height, halfZ));

        public bool Overlaps(Box o) =>
            Min.x < o.Max.x && Max.x > o.Min.x && Min.y < o.Max.y && Max.y > o.Min.y && Min.z < o.Max.z && Max.z > o.Min.z;
    }

    /// <summary>Anything the level spawns and recycles.</summary>
    public abstract class Entity : MonoBehaviour
    {
        [HideInInspector] public int Lane;
        [HideInInspector] public string PoolKey;
        [HideInInspector] public Level Level;
        public float X => transform.position.x;

        public virtual void OnSpawn() { }
        public virtual void Tick(Player dog, float dt) { }
    }

    /// <summary>An obstacle or platform with a gameplay box (relative to its pivot = bottom centre).</summary>
    public class Solid : Entity
    {
        public float HalfX = 0.5f, Bottom, Top = 1f, HalfZ = 0.6f;
        public bool Walkable = true;
        public bool Knocked;
        Vector3 _knockVel, _knockSpin;
        Quaternion _rest;

        public Box Box => new Box(transform.position + new Vector3(-HalfX, Bottom, -HalfZ),
                                  transform.position + new Vector3(HalfX, Top, HalfZ));
        public float TopY => transform.position.y + Top;

        public override void OnSpawn()
        {
            Knocked = false;
            _rest = transform.rotation;
        }

        /// <summary>Invincible dog smashes through: the prop flies off and stops counting.</summary>
        public void Knock()
        {
            if (Knocked) return;
            Knocked = true;
            _knockVel = new Vector3(Random.Range(4f, 7f), Random.Range(5f, 8f), Random.Range(-1f, 3f));
            _knockSpin = Random.insideUnitSphere * 540f;
            Fx.Puff(transform.position + Vector3.up * (Top * 0.5f), 1.2f);
        }

        public override void Tick(Player dog, float dt)
        {
            if (!Knocked) return;
            _knockVel += Vector3.down * 25f * dt;
            transform.position += _knockVel * dt;
            transform.rotation = Quaternion.Euler(_knockSpin * dt) * transform.rotation;
            if (transform.position.y < -10f) gameObject.SetActive(false);
        }

        void OnDisable() { if (Knocked) transform.rotation = _rest; }
    }

    /// <summary>A bone: spins, collected on touch.</summary>
    public class BonePickup : Entity
    {
        public bool Taken;
        public float Radius = 0.45f;
        Transform _model;
        float _phase;

        public override void OnSpawn()
        {
            Taken = false;
            if (_model == null && transform.childCount > 0) _model = transform.GetChild(0);
            _phase = X * 0.7f;
        }

        public override void Tick(Player dog, float dt)
        {
            if (Taken) return;
            if (_model) _model.localRotation = Quaternion.Euler(0, (Time.time * 160f + _phase * 50f) % 360f, 20f);
        }
    }

    /// <summary>A power-up bubble with its icon.</summary>
    public class PowerUpPickup : Entity
    {
        public PowerUpType Type;
        public bool Taken;
        public float Radius = 0.4f;
        float _baseY;

        public override void OnSpawn()
        {
            Taken = false;
            _baseY = transform.position.y;
        }

        public override void Tick(Player dog, float dt)
        {
            var p = transform.position;
            p.y = _baseY + Mathf.Sin(Time.time * 3f) * 0.12f;
            transform.position = p;
        }
    }
}
