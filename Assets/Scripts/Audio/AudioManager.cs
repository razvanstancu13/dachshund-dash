using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DachshundDash
{
    public enum Sfx { Mole, Cat, Crow, Death, Bone, PowerUp, Whoosh, Bump, Click, Crash, Caw }

    /// <summary>Music with crossfade, a quiet ambience bed, a small pool of one-shot sources, ducking and pitch.
    /// Clips come from Resources/Audio; a missing clip is simply silent.</summary>
    public class AudioManager : MonoBehaviour
    {
        const float MusicVolume = 0.55f, AmbienceVolume = 0.25f;

        readonly AudioSource[] _music = new AudioSource[2];
        int _activeMusic;
        AudioSource _ambience;
        readonly Dictionary<string, AudioSource> _loops = new Dictionary<string, AudioSource>();
        readonly List<AudioSource> _sfx = new List<AudioSource>();
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        float _duck = 1f, _musicPitch = 1f;
        Coroutine _fade;

        void Awake()
        {
            for (int i = 0; i < 2; i++)
            {
                _music[i] = gameObject.AddComponent<AudioSource>();
                _music[i].loop = true; _music[i].playOnAwake = false; _music[i].volume = 0;
            }
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.loop = true; _ambience.playOnAwake = false; _ambience.volume = AmbienceVolume;
            for (int i = 0; i < 10; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _sfx.Add(s);
            }
        }

        AudioClip Clip(string name)
        {
            if (!_clips.TryGetValue(name, out var c))
            {
                c = Resources.Load<AudioClip>("Audio/" + name);
                _clips[name] = c;
            }
            return c;
        }

        public void PlayMusic(string name, float fade = 1.2f)
        {
            var clip = Clip(name);
            var cur = _music[_activeMusic];
            if (cur.clip == clip && cur.isPlaying) return;
            _activeMusic = 1 - _activeMusic;
            var next = _music[_activeMusic];
            next.clip = clip;
            next.volume = 0;
            if (clip != null) next.Play();
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Crossfade(cur, next, fade));
            if (!_ambience.isPlaying && Clip("ambience") != null) { _ambience.clip = Clip("ambience"); _ambience.Play(); }
        }

        IEnumerator Crossfade(AudioSource from, AudioSource to, float t)
        {
            float a = 0, startFrom = from.volume;
            while (a < 1)
            {
                a += Time.unscaledDeltaTime / Mathf.Max(0.01f, t);
                from.volume = Mathf.Lerp(startFrom, 0, a);
                to.volume = Mathf.Lerp(0, MusicVolume, a) * _duck;
                yield return null;
            }
            from.Stop();
            _fade = null;
        }

        public void Play(Sfx s, float volume = 1f, float pitch = 1f)
        {
            var clip = Clip(SfxName(s));
            if (clip == null) return;
            AudioSource src = null;
            foreach (var x in _sfx) if (!x.isPlaying) { src = x; break; }
            if (src == null) src = _sfx[0];
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.Play();
        }

        static string SfxName(Sfx s)
        {
            switch (s)
            {
                case Sfx.Mole: return "sfx_mole";
                case Sfx.Cat: return "sfx_cat";
                case Sfx.Crow: return "sfx_crow";
                case Sfx.Death: return "sfx_death";
                case Sfx.Bone: return "sfx_bone";
                case Sfx.PowerUp: return "sfx_powerup";
                case Sfx.Whoosh: return "sfx_whoosh";
                case Sfx.Bump: return "sfx_bump";
                case Sfx.Crash: return "sfx_crash";
                case Sfx.Caw: return "sfx_caw";
                default: return "sfx_click";
            }
        }

        /// <summary>Looping effects by name (the slide drag, the jet): starts / stops each, keeps it playing while on.</summary>
        public void SetLoop(string name, bool on, float volume)
        {
            if (!_loops.TryGetValue(name, out var src))
            {
                if (!on) return;
                var clip = Clip(name);
                if (clip == null) return;
                src = gameObject.AddComponent<AudioSource>();
                src.loop = true; src.playOnAwake = false; src.clip = clip;
                _loops[name] = src;
            }
            if (!on) { if (src.isPlaying) src.Stop(); return; }
            src.volume = volume;
            if (!src.isPlaying) src.Play();
        }

        /// <summary>Music volume multiplier (e.g. 0.3 under the death sound).</summary>
        public void Duck(float k)
        {
            _duck = k;
            if (_fade == null) _music[_activeMusic].volume = MusicVolume * k;
        }

        public void SetMusicPitch(float p)
        {
            _musicPitch = p;
            foreach (var m in _music) m.pitch = p;
        }

        public float MusicPitch => _musicPitch;
    }
}
