using System.Collections.Generic;

namespace DachshundDash
{
    /// <summary>A hand-authored level piece: three rows (lane 0 = dog's left / far, 1 = middle, 2 = right / near),
    /// one character per 2 m slot.
    ///   .  empty            b  bone
    ///   L  low obstacle     l  low obstacle with a bone arc over it      (jump)
    ///   O  overhead         o  overhead with bones under it              (crouch)
    ///   X  blocker (a run of X = one wide blocker)                       (switch lane)
    ///   P  platform cell (a run of P = one platform)   p  platform cell with a bone on top
    ///   ^  platform cell with a mole on it             &  platform cell with a cat on it
    ///   M  mole   C  cat   R  crow
    /// </summary>
    public class Pattern
    {
        public readonly string Name;
        public readonly int Tier;
        public readonly string[] Rows;

        public Pattern(string name, int tier, string left, string middle, string right)
        {
            Name = name; Tier = tier; Rows = new[] { left, middle, right };
        }

        public int Length => Rows[0].Length;
        public char At(int lane, int col) => Rows[lane][col];

        public Pattern Mirrored() => new Pattern(Name + "_m", Tier, Rows[2], Rows[1], Rows[0]);

        public static bool IsPlatform(char c) => c == 'P' || c == 'p' || c == '^' || c == '&';
        public static bool IsBlocker(char c) => c == 'X';

        /// <summary>The action a cell asks for: 'J' jump, 'C' crouch, 'S' switch / none-possible, '-' nothing.</summary>
        public static char Action(char c)
        {
            switch (c)
            {
                case 'L': case 'l': case 'M': case 'C': return 'J';
                case 'O': case 'o': case 'R': return 'D';
                case 'X': return 'S';
                case 'P': case 'p': case '^': case '&': return 'P';
                default: return '-';
            }
        }

        /// <summary>Distance from which a tier may appear.</summary>
        public static float TierStart(int tier)
        {
            switch (tier)
            {
                case 0: return 0;
                case 1: return 80;
                case 2: return 160;
                case 3: return 260;
                default: return 450;
            }
        }
    }

    public static class Patterns
    {
        public static readonly List<Pattern> All = new List<Pattern>
        {
            // tier 0 - low obstacles, blockers, lots of bones
            new Pattern("intro_low", 0,
                "............",
                "..bbb.l.bbb.",
                "............"),
            new Pattern("intro_block", 0,
                "...bbbb.....",
                "..XX........",
                "........L..."),
            new Pattern("zigzag_bones", 0,
                "bb.......L..",
                "..bb..bb....",
                "....bb......"),
            new Pattern("three_crates", 0,
                "..L.....bbb.",
                ".......l....",
                "....L......."),
            new Pattern("cars_row", 0,
                "..XX....XX..",
                "bbbbbbbbbbbb",
                "......XX...."),
            new Pattern("hop_hop", 0,
                "............",
                "..l....l....",
                "....XX......"),
            new Pattern("clutter", 0,
                "..L....L....",
                ".....l....L.",
                "...L....l..."),
            new Pattern("crates_and_cars", 0,
                "..XX...L....",
                "....L....XX.",
                ".L....bbb.L."),
            new Pattern("slalom", 0,
                "..L..XX.....",
                ".......L..L.",
                "....L....XX."),
            new Pattern("porch", 0,
                "...bbb..L...",
                "..L....XX...",
                "......L...L."),

            // tier 1 - crouching, platforms, moles
            new Pattern("bench_line", 1,
                "....bbb.....",
                "...o....XX..",
                "......XX...."),
            new Pattern("platform_run", 1,
                "..PpppP.....",
                "........bb..",
                "..L.......L."),
            new Pattern("mole_field", 1,
                "....M...bb..",
                "bb......M...",
                "..M.....bb.."),
            new Pattern("crouch_or_switch", 1,
                "...O.....O..",
                "......o.....",
                "...XX......."),
            new Pattern("platform_mole", 1,
                "..PPP^PPP...",
                "....bbb.....",
                ".....XXX...."),
            new Pattern("hedge_maze", 1,
                "..XX....bb..",
                ".....XX.....",
                "..bb....XX.."),
            new Pattern("stairs", 1,
                "..L...PPpp..",
                "....PPpp....",
                "PPpp........"),

            // tier 2 - cats
            new Pattern("cat_alley", 2,
                "........C...",
                "..XX........",
                "....L.....C."),
            new Pattern("cat_on_crates", 2,
                "..PPP&P.....",
                "..........C.",
                "...bbbb.XX.."),
            new Pattern("cat_and_bench", 2,
                "...O....bb..",
                ".......C....",
                "..C...XX...."),
            new Pattern("mole_cat", 2,
                "..M.....M...",
                ".....C......",
                "bbbb...XX..."),

            // tier 3 - crows
            new Pattern("crow_low", 3,
                "......R.....",
                "..l.......l.",
                "....R...O..."),
            new Pattern("crow_gate", 3,
                "..XX...R....",
                "....R.....XX",
                "..bbb...R..."),
            new Pattern("crow_platform", 3,
                "..PPPPP.....",
                ".......R....",
                "..R....bbb.."),
            new Pattern("mixed_birds", 3,
                "...C....R...",
                "......M.....",
                "...R...XXX.."),

            // tier 4 - dense
            new Pattern("gauntlet", 4,
                "..L...O...L.",
                "..XX....XX..",
                "....R...C..."),
            new Pattern("weave", 4,
                "..XX...XX...",
                "....XX....XX",
                "..L...M...R."),
            new Pattern("rooftop", 4,
                "..PPPPPPPP..",
                "...M...C....",
                "..XX....XX.."),
            new Pattern("zoo", 4,
                "...R....C...",
                "..C...R.....",
                ".....M...O.."),
        };
    }
}
