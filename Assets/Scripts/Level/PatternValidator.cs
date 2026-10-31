using System.Collections.Generic;

namespace DachshundDash
{
    /// <summary>Fairness rules every pattern must pass (run by an edit-mode test over all patterns).</summary>
    public static class PatternValidator
    {
        public const int EntryColumns = 2;      // the first columns hold no blocker or enemy
        public const int MixedActionGap = 3;    // jump then crouch (or back) in one lane: at least 3 slots apart
        public const int SameActionGap = 2;
        public const int CatRunway = 3;         // clear cells in front of a cat for its charge

        public static List<string> Validate(Pattern p)
        {
            var errors = new List<string>();
            int n = p.Rows[0].Length;
            for (int l = 0; l < 3; l++)
                if (p.Rows[l].Length != n) errors.Add($"{p.Name}: row {l} length {p.Rows[l].Length} != {n}");
            if (errors.Count > 0) return errors;
            if (n < 6) errors.Add($"{p.Name}: shorter than 6 slots");

            for (int c = 0; c < n; c++)
            {
                bool open = false;
                for (int l = 0; l < 3; l++) open |= !Pattern.IsBlocker(p.At(l, c));
                if (!open) errors.Add($"{p.Name}: column {c} is blocked in every lane");
            }

            for (int c = 0; c < EntryColumns && c < n; c++)
                for (int l = 0; l < 3; l++)
                {
                    char ch = p.At(l, c);
                    if (ch == 'X' || ch == 'M' || ch == 'C' || ch == 'R' || ch == '^' || ch == '&')
                        errors.Add($"{p.Name}: '{ch}' in the entry columns (lane {l}, col {c})");
                }

            for (int start = 0; start < 3; start++)
                if (!Reachable(p, start)) errors.Add($"{p.Name}: no way through from lane {start}");

            for (int l = 0; l < 3; l++)
            {
                int lastCol = -100; char lastAct = '-';
                for (int c = 0; c < n; c++)
                {
                    char a = Pattern.Action(p.At(l, c));
                    if (a != 'J' && a != 'D') continue;
                    int gap = c - lastCol;
                    if (lastAct != '-' && a != lastAct && gap < MixedActionGap)
                        errors.Add($"{p.Name}: lane {l} needs '{lastAct}' then '{a}' only {gap} slot(s) apart");
                    if (a == lastAct && gap < SameActionGap)
                        errors.Add($"{p.Name}: lane {l} two '{a}' actions in adjacent slots (col {c})");
                    lastCol = c; lastAct = a;
                }
                for (int c = 0; c < n; c++)
                {
                    if (p.At(l, c) != 'C') continue;
                    for (int k = 1; k <= CatRunway && c - k >= 0; k++)
                    {
                        char ch = p.At(l, c - k);
                        if (ch == 'X' || ch == 'L' || ch == 'l' || ch == 'O' || ch == 'o' || Pattern.IsPlatform(ch))
                            errors.Add($"{p.Name}: cat at lane {l} col {c} has '{ch}' in its charge runway");
                    }
                }
            }
            return errors;
        }

        /// <summary>The dog can get from the start lane to the end changing at most one lane per slot; a change
        /// needs the target lane free of blockers in both slots.</summary>
        public static bool Reachable(Pattern p, int startLane)
        {
            int n = p.Length;
            var cur = new bool[3];
            if (Pattern.IsBlocker(p.At(startLane, 0))) return false;
            cur[startLane] = true;
            for (int c = 0; c < n - 1; c++)
            {
                var next = new bool[3];
                for (int l = 0; l < 3; l++)
                {
                    if (!cur[l]) continue;
                    if (!Pattern.IsBlocker(p.At(l, c + 1))) next[l] = true;
                    foreach (int t in new[] { l - 1, l + 1 })
                    {
                        if (t < 0 || t > 2) continue;
                        if (!Pattern.IsBlocker(p.At(t, c)) && !Pattern.IsBlocker(p.At(t, c + 1))) next[t] = true;
                    }
                }
                cur = next;
                if (!cur[0] && !cur[1] && !cur[2]) return false;
            }
            return true;
        }
    }
}
