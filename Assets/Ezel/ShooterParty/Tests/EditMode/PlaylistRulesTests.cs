using System.Linq;
using NUnit.Framework;

namespace ShooterParty.Tests
{
    public class PlaylistRulesTests
    {
        static bool Known(string id) => id == "knockback_arena";

        static string Error(string name, params string[] ids)
        {
            Assert.IsFalse(PlaylistRules.TryValidate(name, ids, Known, out var error), "expected rejection");
            return error;
        }

        [Test] public void AcceptsRepeats() =>
            Assert.IsTrue(PlaylistRules.TryValidate("Trio", new[] { "knockback_arena", "knockback_arena", "knockback_arena" }, Known, out _));

        [Test] public void RejectsEmptyList() => StringAssert.Contains("at least one", Error("Trio"));
        [Test] public void RejectsNullList() => Assert.IsFalse(PlaylistRules.TryValidate("Trio", null, Known, out _));
        [Test] public void RejectsElevenGames() => StringAssert.Contains("at most 10", Error("Long", Enumerable.Repeat("knockback_arena", 11).ToArray()));
        [Test] public void AcceptsTenGames() => Assert.IsTrue(PlaylistRules.TryValidate("Ten", Enumerable.Repeat("knockback_arena", 10).ToList(), Known, out _));
        [Test] public void RejectsUnknownId() => Assert.AreEqual("Unknown mini-game 'x'", Error("Trio", "knockback_arena", "x"));
        [Test] public void UnknownIdIsEchoedOnlyWhenSafe() =>   // the server logs refusals: no forged @@ lines
            Assert.AreEqual("Unknown mini-game", Error("Trio", "x\n@@STATE {\"phase\":\"Playing\"}"));
        [Test] public void RejectsLongName() => StringAssert.Contains("at most 24", Error(new string('a', 25), "knockback_arena"));
        [Test] public void RejectsEmptyName() => StringAssert.Contains("Give the playlist a name", Error("  ", "knockback_arena"));
        [Test] public void RejectsTagsInName() => StringAssert.Contains("letters, numbers", Error("<b>x</b>", "knockback_arena"));
    }
}
