using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DachshundDash
{
    /// <summary>The intro cutscene (StreamingAssets/intro.mp4, made by tools/cutscene/make_cutscene.ps1): plays once per
    /// launch over the menu, letterboxed on black, with its sound (Resources/Audio/intro_sfx, same script); any key,
    /// click or tap skips it. Missing or broken video = skipped.</summary>
    public class IntroCutscene : MonoBehaviour
    {
        public static bool Played;

        Action _done;
        VideoPlayer _player;
        AudioSource _sound;
        RenderTexture _rt;
        float _started;
        bool _finished;

        /// <summary>Plays the intro unless it already played (or -nointro / -ddcapture is on the command line), then calls done.</summary>
        public static void PlayOnce(Action done)
        {
            var args = Environment.GetCommandLineArgs();
            if (Played || Array.IndexOf(args, "-nointro") >= 0 || Array.IndexOf(args, "-ddcapture") >= 0) { done(); return; }
            Played = true;
            var canvas = UIKit.Canvas("Intro", 100);
            canvas.gameObject.AddComponent<IntroCutscene>().Begin(done);
        }

        void Begin(Action done)
        {
            _done = done;
            var black = UIKit.Stretch("black", transform).gameObject.AddComponent<Image>();
            black.color = Color.black;                                      // also blocks clicks on the menu below
            var screen = UIKit.Stretch("video", transform);
            var raw = screen.gameObject.AddComponent<RawImage>();
            raw.color = Color.clear;                                        // shown once the first frame is ready
            raw.raycastTarget = false;
            var fit = screen.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1280f / 704f;

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.source = VideoSource.Url;
            _player.url = Path.Combine(Application.streamingAssetsPath, "intro.mp4");
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.audioOutputMode = VideoAudioOutputMode.None;          // the sound is a separate clip (more reliable on WebGL)
            _sound = gameObject.AddComponent<AudioSource>();
            _sound.playOnAwake = false;
            _sound.clip = Resources.Load<AudioClip>("Audio/intro_sfx");
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.prepareCompleted += p =>
            {
                _rt = new RenderTexture((int)p.width, (int)p.height, 0);
                p.targetTexture = _rt;
                raw.texture = _rt;
                raw.color = Color.white;
                fit.aspectRatio = (float)p.width / p.height;
                p.Play();
                if (_sound.clip != null) _sound.Play();
            };
            _player.loopPointReached += _ => Finish();
            _player.errorReceived += (_, msg) => { Debug.LogWarning("Intro video: " + msg); Finish(); };
            _player.Prepare();
            _started = Time.unscaledTime;
        }

        void Update()
        {
            if (_finished) return;
            if (Time.unscaledTime - _started < 0.3f) return;                // the launch click / key is not a skip
            bool skip = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                     || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                     || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
            // never stuck on black: no first frame after 5 s (e.g. a platform that cannot decode it) = skip
            if (skip || (!_player.isPrepared && Time.unscaledTime - _started > 5f)) Finish();
        }

        void Finish()
        {
            if (_finished) return;
            _finished = true;
            _player.Stop();
            _sound.Stop();
            if (_rt != null) _rt.Release();
            Destroy(gameObject);
            _done?.Invoke();
        }
    }
}
