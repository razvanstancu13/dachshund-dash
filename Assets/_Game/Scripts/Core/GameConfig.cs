using UnityEngine;

namespace DachshundDash
{
    /// <summary>All gameplay tunables in one place. A GameConfig asset in Resources overrides these defaults.</summary>
    [CreateAssetMenu(menuName = "Dachshund Dash/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("World")]
        public float laneSpacing = 1.6f;
        public float slotLength = 2f;              // one pattern cell along X
        public float biomeLength = 400f;

        [Header("Run speed")]
        public float startSpeed = 4f;
        public float maxSpeed = 6.5f;
        public float speedTimeConstant = 90f;      // seconds; speed = max - (max - start) * e^(-t / tau)

        [Header("Dog")]
        public float dogLength = 1.1f;
        public float dogHeight = 0.62f;
        public float dogCrouchHeight = 0.36f;
        public float hitboxScale = 0.85f;
        public float laneSwitchTime = 0.16f;
        public float jumpHeight = 1.4f;
        public float gravity = 30f;
        public float fastFallMultiplier = 3f;
        public float coyoteTime = 0.08f;
        public float jumpBuffer = 0.12f;
        public float inputBuffer = 0.15f;

        [Header("Enemies")]
        public float moleReach = 0.6f;
        public float moleTelegraphDistance = 8f;
        public float catSpeed = 5f;
        public float catReactionTime = 1.0f;       // trigger distance = (dog + cat speed) * this
        public float maxTriggerDistance = 8f;        // cats and crows react only once on screen
        public float catWindup = 0.2f;
        public float crowHeight = 1.8f;
        public float crowDiveBottom = 0.45f;
        public float crowSpeed = 5f;
        public float crowReactionTime = 1.0f;

        [Header("Power-ups")]
        public float flyDuration = 6f;
        public float flyAltitude = 1.6f;
        public float slowDuration = 5f;
        public float slowFactor = 0.6f;
        public float invincibleDuration = 6f;
        public float doubleJumpDuration = 10f;
        public float powerUpSpacingMin = 250f;
        public float powerUpSpacingMax = 400f;

        [Header("Generation")]
        public float lookAhead = 90f;
        public float keepBehind = 25f;

        static GameConfig _instance;
        public static GameConfig I
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameConfig>("GameConfig");
                    if (_instance == null) _instance = CreateInstance<GameConfig>();
                }
                return _instance;
            }
        }

        public float JumpVelocity => Mathf.Sqrt(2f * gravity * jumpHeight);

        public float SpeedAt(float t) => maxSpeed - (maxSpeed - startSpeed) * Mathf.Exp(-t / speedTimeConstant);

        /// <summary>Lane 0 = dog's left (far from the camera), 1 = middle, 2 = dog's right (near).</summary>
        public float LaneZ(int lane) => (1 - lane) * laneSpacing;
    }
}
