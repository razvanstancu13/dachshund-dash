using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DachshundDash
{
    /// <summary>Plays the model's own animation clips by name with a short crossfade (Playables, no controller asset).
    /// Clip names are matched by suffix, so "Armature|Run" answers to "Run".</summary>
    public class ClipPlayer : MonoBehaviour
    {
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        AnimationClipPlayable _current, _previous;
        readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>();
        float _fade, _fadeTime;
        float _speed = 1f;
        public string Current { get; private set; }

        public static ClipPlayer Attach(GameObject root, string modelName)
        {
            var clips = Resources.LoadAll<AnimationClip>("Models/" + modelName);
            if (clips.Length == 0 || root.transform.childCount == 0) return null;
            // the clips' curve paths start at the imported model's root (the art wrapper's first child)
            var model = root.transform.GetChild(0).gameObject;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var p = root.AddComponent<ClipPlayer>();
            p.Init(animator, clips);
            return p;
        }

        public void Init(Animator animator, IEnumerable<AnimationClip> clips)
        {
            foreach (var c in clips)
            {
                if (c.name.StartsWith("__preview__")) continue;
                var n = c.name;
                int bar = n.LastIndexOf('|');
                if (bar >= 0) n = n.Substring(bar + 1);
                _clips[n] = c;
            }
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            _graph = PlayableGraph.Create(name + "_clips");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            var output = AnimationPlayableOutput.Create(_graph, "anim", animator);
            output.SetSourcePlayable(_mixer);
            _graph.Play();
        }

        public bool Has(string clip) => _clips.ContainsKey(clip);

        public float Length(string clip) => _clips.TryGetValue(clip, out var c) ? c.length : 0f;

        public float Speed
        {
            get => _speed;
            set { _speed = value; if (_current.IsValid()) _current.SetSpeed(value); }
        }

        public void Play(string clip, float fade = 0.12f, bool restart = false)
        {
            if (!_graph.IsValid() || !_clips.TryGetValue(clip, out var c)) return;
            if (clip == Current && !restart) return;
            if (_previous.IsValid())
            {
                _graph.Disconnect(_mixer, 1);
                _previous.Destroy();
            }
            if (_current.IsValid())
            {
                _graph.Disconnect(_mixer, 0);
                _previous = _current;
                _graph.Connect(_previous, 0, _mixer, 1);
            }
            _current = AnimationClipPlayable.Create(_graph, c);
            _current.SetSpeed(_speed);
            _graph.Connect(_current, 0, _mixer, 0);
            Current = clip;
            _fadeTime = fade;
            _fade = fade > 0 && _previous.IsValid() ? 0 : 1;
            Apply();
        }

        /// <summary>Jumps the current clip to a normalised time.</summary>
        public void SetNormalized(float t)
        {
            if (_current.IsValid() && _clips.TryGetValue(Current, out var c)) _current.SetTime(t * c.length);
        }

        /// <summary>Normalised time of the current clip (0..1, not wrapped for one-shot clips).</summary>
        public float Normalized => _current.IsValid() && _clips.TryGetValue(Current, out var c) && c.length > 0
            ? (float)(_current.GetTime() / c.length) : 0f;

        void Update()
        {
            if (_fade < 1)
            {
                _fade = Mathf.Min(1, _fade + Time.deltaTime / Mathf.Max(0.001f, _fadeTime));
                Apply();
            }
        }

        void Apply()
        {
            _mixer.SetInputWeight(0, _fade);
            _mixer.SetInputWeight(1, _previous.IsValid() ? 1 - _fade : 0);
        }

        void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
