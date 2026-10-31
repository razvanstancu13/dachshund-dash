using UnityEngine;

namespace DachshundDash
{
    /// <summary>Game-wide services, created once before the first scene loads.</summary>
    public static class Services
    {
        public static PlayerProfile Profile { get; private set; }
        public static HighscoreService Highscores { get; private set; }
        public static AudioManager Audio { get; private set; }

        /// <summary>Set by the game scene for the highscore screen: rank of the score just added (-1 = none).</summary>
        public static int LastRank = -1;
        public static bool ShowHighscoresOnMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            var store = new PlayerPrefsStore();
            Profile = new PlayerProfile(store);
            Highscores = new HighscoreService(store);
            var go = new GameObject("AudioManager");
            Object.DontDestroyOnLoad(go);
            Audio = go.AddComponent<AudioManager>();
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
