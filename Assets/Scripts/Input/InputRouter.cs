using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DachshundDash
{
    public enum PowerUpType { Fly = 0, Slow = 1, Invincible = 2, DoubleJump = 3 }

    /// <summary>One set of game actions for keyboard and on-screen buttons. Gameplay code only listens here.</summary>
    public class InputRouter : MonoBehaviour
    {
        public event Action Jump, LaneLeft, LaneRight, Pause;
        /// <summary>Tab or the view button: side view / behind the dog (works while paused or counting down too).</summary>
        public event Action ToggleView;
        /// <summary>Cheat keys: as if that power-up was just collected (Z invincible, X slow, C fly, V double jump).</summary>
        public event Action<PowerUpType> Cheat;
        /// <summary>Cheat key Backspace: half speed on / off.</summary>
        public event Action SlowCheat;

        bool _touchCrouch;
        public bool CrouchHeld => _touchCrouch || (Keyboard.current != null && Keyboard.current.downArrowKey.isPressed);
        public bool Enabled = true;

        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame) Pause?.Invoke();
            if (k.tabKey.wasPressedThisFrame) ToggleView?.Invoke();
            if (!Enabled) return;
            if (k.upArrowKey.wasPressedThisFrame) Jump?.Invoke();
            if (k.leftArrowKey.wasPressedThisFrame) LaneLeft?.Invoke();
            if (k.rightArrowKey.wasPressedThisFrame) LaneRight?.Invoke();
            if (k.zKey.wasPressedThisFrame) Cheat?.Invoke(PowerUpType.Invincible);
            if (k.xKey.wasPressedThisFrame) Cheat?.Invoke(PowerUpType.Slow);
            if (k.cKey.wasPressedThisFrame) Cheat?.Invoke(PowerUpType.Fly);
            if (k.vKey.wasPressedThisFrame) Cheat?.Invoke(PowerUpType.DoubleJump);
            if (k.backspaceKey.wasPressedThisFrame) SlowCheat?.Invoke();
        }

        // on-screen buttons
        public void PressJump() { if (Enabled) Jump?.Invoke(); }
        public void PressLeft() { if (Enabled) LaneLeft?.Invoke(); }
        public void PressRight() { if (Enabled) LaneRight?.Invoke(); }
        public void SetCrouch(bool held) { _touchCrouch = held && Enabled; }
        public void PressPause() => Pause?.Invoke();
        public void PressView() => ToggleView?.Invoke();
        public void PressCheat(PowerUpType t) { if (Enabled) Cheat?.Invoke(t); }

        public static bool TouchUI =>
            Application.isMobilePlatform || Touchscreen.current != null;
    }
}
