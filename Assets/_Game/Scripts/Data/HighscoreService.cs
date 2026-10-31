using System;
using System.Collections.Generic;
using UnityEngine;

namespace DachshundDash
{
    [Serializable]
    public class HighscoreEntry
    {
        public string name;
        public int score;
        public HighscoreEntry(string name, int score) { this.name = name; this.score = score; }
    }

    /// <summary>Top 10 table. Pure logic; persistence goes through IKeyValueStore (PlayerPrefs in the game).</summary>
    public class HighscoreService
    {
        public const int Size = 10;
        public const string DefaultName = "Dasher";
        const string Key = "dd_highscores";

        [Serializable] class Table { public List<HighscoreEntry> entries = new List<HighscoreEntry>(); }

        readonly IKeyValueStore _store;
        Table _table;

        public HighscoreService(IKeyValueStore store)
        {
            _store = store;
            Load();
        }

        public IReadOnlyList<HighscoreEntry> Entries => _table.entries;

        /// <summary>Strictly greater than the 10th entry (ties do not push anyone out).</summary>
        public bool Qualifies(int score) => _table.entries.Count < Size || score > _table.entries[Size - 1].score;

        /// <summary>Inserts the score if it qualifies; returns its rank index (0-based) or -1.</summary>
        public int Submit(string name, int score)
        {
            if (!Qualifies(score)) return -1;
            int i = 0;
            while (i < _table.entries.Count && _table.entries[i].score >= score) i++;   // below equal scores
            _table.entries.Insert(i, new HighscoreEntry(name, score));
            if (_table.entries.Count > Size) _table.entries.RemoveRange(Size, _table.entries.Count - Size);
            Save();
            return i;
        }

        public static List<HighscoreEntry> DefaultTable()
        {
            var l = new List<HighscoreEntry>();
            for (int s = Size; s >= 1; s--) l.Add(new HighscoreEntry(DefaultName, s));
            return l;
        }

        void Load()
        {
            _table = null;
            var json = _store.GetString(Key, null);
            if (!string.IsNullOrEmpty(json))
            {
                try { _table = JsonUtility.FromJson<Table>(json); }
                catch (Exception) { _table = null; }
            }
            if (_table == null || _table.entries == null || _table.entries.Count == 0)
            {
                _table = new Table { entries = DefaultTable() };
                Save();
            }
            _table.entries.Sort((a, b) => b.score.CompareTo(a.score));
        }

        void Save()
        {
            _store.SetString(Key, JsonUtility.ToJson(_table));
            _store.Save();
        }
    }
}
