using System.Collections.Generic;
using UnityEngine;

namespace DachshundDash
{
    /// <summary>A handful of real spot lights lent to the lamps nearest the view (cheap on WebGL / mobile); every
    /// other lamp keeps only its fake shaft and glow pool.</summary>
    public class LightRig : MonoBehaviour
    {
        const int Count = 4;
        readonly Light[] _lights = new Light[Count];

        void Awake()
        {
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("lamp_light_" + i);
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = new Color(1f, 0.72f, 0.4f);
                l.intensity = 9f;
                l.range = 7f;
                l.spotAngle = 95f;
                l.innerSpotAngle = 40f;
                l.shadows = LightShadows.None;
                go.transform.rotation = Quaternion.Euler(90, 0, 0);
                l.enabled = false;
                _lights[i] = l;
            }
        }

        public void Assign(List<Level.Lamp> lamps, float viewX)
        {
            int used = 0;
            // lamps sorted by spawn order = by x; take the first ones from just behind the view onwards
            foreach (var lamp in lamps)
            {
                if (used >= Count) break;
                if (lamp.X < viewX - 6f || lamp.X > viewX + 26f) continue;
                var l = _lights[used++];
                l.enabled = true;
                l.transform.position = lamp.transform.position + lamp.HeadLocal + new Vector3(0, 0, -0.4f);
            }
            for (int i = used; i < Count; i++) _lights[i].enabled = false;
        }
    }

    public static class PowerUpInfo
    {
        public static string IconName(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Fly: return "icon_fly";
                case PowerUpType.Slow: return "icon_slow";
                case PowerUpType.Invincible: return "icon_invincible";
                default: return "icon_doublejump";
            }
        }

        public static Color Color(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Fly: return new Color(0.55f, 0.85f, 1f);
                case PowerUpType.Slow: return new Color(0.6f, 0.5f, 1f);
                case PowerUpType.Invincible: return new Color(1f, 0.8f, 0.25f);
                default: return new Color(0.5f, 1f, 0.6f);
            }
        }

        public static string Label(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Fly: return "Fly";
                case PowerUpType.Slow: return "Slow";
                case PowerUpType.Invincible: return "Shield";
                default: return "Double jump";
            }
        }

        public static string Key(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Fly: return "W";
                case PowerUpType.Slow: return "A";
                case PowerUpType.Invincible: return "S";
                default: return "D";
            }
        }
    }
}
