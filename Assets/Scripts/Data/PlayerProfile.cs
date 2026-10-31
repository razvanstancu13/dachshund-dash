using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DachshundDash
{
    public interface IKeyValueStore
    {
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void Save();
    }

    public class PlayerPrefsStore : IKeyValueStore
    {
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    public class MemoryStore : IKeyValueStore
    {
        readonly Dictionary<string, string> _d = new Dictionary<string, string>();
        public string GetString(string key, string fallback) => _d.TryGetValue(key, out var v) ? v : fallback;
        public void SetString(string key, string value) => _d[key] = value;
        public void Save() { }
    }

    /// <summary>The player's name, asked once and kept on the device.</summary>
    public class PlayerProfile
    {
        public const int MaxNameLength = 12;
        const string Key = "dd_player_name";
        readonly IKeyValueStore _store;

        public PlayerProfile(IKeyValueStore store) { _store = store; }

        public bool HasName => !string.IsNullOrEmpty(Name);
        public string Name => _store.GetString(Key, "");

        public bool TrySetName(string raw)
        {
            var n = Sanitize(raw);
            if (n.Length == 0) return false;
            _store.SetString(Key, n);
            _store.Save();
            return true;
        }

        /// <summary>Letters, digits and single spaces, trimmed, at most 12 characters.</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder();
            foreach (var c in raw.Trim())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' && sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                if (sb.Length >= MaxNameLength) break;
            }
            return sb.ToString().Trim();
        }
    }
}
