using NUnit.Framework;

namespace DachshundDash.Tests
{
    public class HighscoreTests
    {
        [Test]
        public void FreshTableIsDasherTenToOne()
        {
            var hs = new HighscoreService(new MemoryStore());
            Assert.AreEqual(10, hs.Entries.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual("Dasher", hs.Entries[i].name);
                Assert.AreEqual(10 - i, hs.Entries[i].score);
            }
        }

        [Test]
        public void QualifiesOnlyWhenStrictlyAboveTenth()
        {
            var hs = new HighscoreService(new MemoryStore());
            Assert.IsFalse(hs.Qualifies(0));
            Assert.IsFalse(hs.Qualifies(1));      // ties the 10th
            Assert.IsTrue(hs.Qualifies(2));
        }

        [Test]
        public void SubmitInsertsSortedAndKeepsTen()
        {
            var store = new MemoryStore();
            var hs = new HighscoreService(store);
            Assert.AreEqual(0, hs.Submit("Rex", 50));
            Assert.AreEqual(3, hs.Submit("Bo", 9));   // below the existing 9: 50, 10, 9, Bo 9, 8 .. 3
            Assert.AreEqual(10, hs.Entries.Count);
            Assert.AreEqual("Rex", hs.Entries[0].name);
            Assert.AreEqual(3, hs.Entries[9].score);
            Assert.AreEqual(-1, hs.Submit("Low", 1));

            var reloaded = new HighscoreService(store);
            Assert.AreEqual(50, reloaded.Entries[0].score);
            Assert.AreEqual("Bo", reloaded.Entries[3].name);
        }
    }

    public class ProfileTests
    {
        [TestCase("  Razvan  ", "Razvan")]
        [TestCase("a  b", "a b")]
        [TestCase("<script>", "script")]
        [TestCase("ABCDEFGHIJKLMNOP", "ABCDEFGHIJKL")]
        [TestCase("   ", "")]
        public void Sanitize(string raw, string expected) => Assert.AreEqual(expected, PlayerProfile.Sanitize(raw));

        [Test]
        public void NameIsStored()
        {
            var store = new MemoryStore();
            var p = new PlayerProfile(store);
            Assert.IsFalse(p.HasName);
            Assert.IsFalse(p.TrySetName("!!!"));
            Assert.IsTrue(p.TrySetName("Dasher Two"));
            Assert.AreEqual("Dasher Two", new PlayerProfile(store).Name);
        }
    }

    public class PatternTests
    {
        [Test]
        public void EveryPatternIsFair()
        {
            var errors = new System.Collections.Generic.List<string>();
            foreach (var p in Patterns.All)
            {
                errors.AddRange(PatternValidator.Validate(p));
                errors.AddRange(PatternValidator.Validate(p.Mirrored()));
            }
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void EveryTierHasPatterns()
        {
            for (int t = 0; t <= 4; t++)
                Assert.IsTrue(Patterns.All.Exists(p => p.Tier == t), "tier " + t);
        }

        [Test]
        public void ValidatorCatchesAWall()
        {
            var p = new Pattern("wall", 0, "....X...", "....X...", "....X...");
            Assert.IsNotEmpty(PatternValidator.Validate(p));
        }

        [Test]
        public void ValidatorCatchesJumpThenCrouch()
        {
            var p = new Pattern("jc", 0, "..L.O...", "........", "........");
            Assert.IsNotEmpty(PatternValidator.Validate(p));
        }
    }

    public class ConfigTests
    {
        [Test]
        public void SpeedRisesToMax()
        {
            var c = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            Assert.AreEqual(c.startSpeed, c.SpeedAt(0), 1e-4);
            float prev = 0;
            for (float t = 0; t < 600; t += 10)
            {
                float s = c.SpeedAt(t);
                Assert.GreaterOrEqual(s, prev);
                Assert.LessOrEqual(s, c.maxSpeed + 1e-4);
                prev = s;
            }
        }

        [Test]
        public void JumpClearsTheTallestLowObstacleAndCrouchPassesUnderOverheads()
        {
            var c = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            Assert.Greater(c.jumpHeight, 0.85f + 0.2f);
            Assert.Less(c.dogCrouchHeight * c.hitboxScale, 0.42f);       // overhead clearance
            Assert.Greater(c.dogHeight * c.hitboxScale, 0.42f);
            Assert.Less(c.dogCrouchHeight * c.hitboxScale, c.crowDiveBottom);
            Assert.Greater(c.dogHeight * c.hitboxScale, c.crowDiveBottom);
        }

        [Test]
        public void LanesLeftIsFar()
        {
            var c = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            Assert.Greater(c.LaneZ(0), c.LaneZ(1));
            Assert.Greater(c.LaneZ(1), c.LaneZ(2));
        }
    }
}
