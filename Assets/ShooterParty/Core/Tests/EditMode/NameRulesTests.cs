using NUnit.Framework;

namespace ShooterParty.Tests
{
    public class NameRulesTests
    {
        static string Clean(string raw)
        {
            Assert.IsTrue(NameRules.TryClean(raw, out var clean, out var error), error);
            return clean;
        }

        static string Error(string raw)
        {
            Assert.IsFalse(NameRules.TryClean(raw, out _, out var error), "expected '" + raw + "' to be rejected");
            return error;
        }

        [Test] public void TrimsAndKeepsPlainNames() => Assert.AreEqual("Mochi", Clean("  Mochi  "));
        [Test] public void CollapsesInnerSpaces() => Assert.AreEqual("Big Mochi", Clean("Big    Mochi"));
        [Test] public void AllowsUnderscoreDigitsAndDash() => Assert.AreEqual("Zeynep_2-x", Clean("Zeynep_2-x"));
        [Test] public void AllowsTurkishLetters() => Assert.AreEqual("Çağla", Clean("Çağla"));
        [Test] public void NormalisesToNfc() => Assert.AreEqual("Çağla", Clean("Çağla"));

        [Test] public void RejectsRichTextTags() => StringAssert.Contains("letters, numbers, spaces, _ and - only", Error("<size=400>x"));
        [Test] public void RejectsZeroWidth() => StringAssert.Contains("letters, numbers", Error("a​b"));
        [Test] public void RejectsBidiOverride() => StringAssert.Contains("letters, numbers", Error("ab‮cd"));
        [Test] public void RejectsTooShort() => StringAssert.Contains("at least 2", Error("a"));
        [Test] public void RejectsEmpty() => StringAssert.Contains("at least 2", Error("   "));
        [Test] public void RejectsNull() => StringAssert.Contains("at least 2", Error(null));
        [Test] public void RejectsTooLong() => StringAssert.Contains("at most 16", Error(new string('a', 17)));
        [Test] public void AcceptsSixteen() => Assert.AreEqual(new string('a', 16), Clean(new string('a', 16)));

        [Test] public void DuplicateGetsSuffix()
        {
            Assert.AreEqual("Mochi", NameRules.Unique("Mochi", new[] { "Zesty" }));
            Assert.AreEqual("Mochi 2", NameRules.Unique("Mochi", new[] { "Mochi" }));
            Assert.AreEqual("Mochi 3", NameRules.Unique("Mochi", new[] { "Mochi", "Mochi 2" }));
        }

        [Test] public void DuplicateCheckIgnoresCase() => Assert.AreEqual("mochi 2", NameRules.Unique("mochi", new[] { "MOCHI" }));

        [Test] public void SuffixKeepsSixteenCharLimit()
        {
            var full = "ABCDEFGHIJKLMNOP";
            Assert.AreEqual("ABCDEFGHIJKLMN 2", NameRules.Unique(full, new[] { full }));
        }
    }
}
