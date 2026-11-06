using System.Collections.Generic;
using NUnit.Framework;

namespace ShooterParty.Tests
{
    public class ScoreRulesTests
    {
        static readonly int[] Table = { 4, 3, 2, 1 };

        static List<RankEntry> Ranks(params int[] rankPerSlot)
        {
            var list = new List<RankEntry>();
            for (int s = 0; s < rankPerSlot.Length; s++) list.Add(new RankEntry { slot = s, rank = rankPerSlot[s] });
            return list;
        }

        [Test] public void FourDistinctPlacesGetTheTable() => CollectionAssert.AreEqual(new[] { 4, 3, 2, 1 }, ScoreRules.Award(Ranks(1, 2, 3, 4), Table, 4));
        [Test] public void TiedRanksShareTheHigherPoints() => CollectionAssert.AreEqual(new[] { 4, 3, 3, 1 }, ScoreRules.Award(Ranks(1, 2, 2, 4), Table, 4));
        [Test] public void ThreePlayersCountFromTheTop() => CollectionAssert.AreEqual(new[] { 4, 3, 2 }, ScoreRules.Award(Ranks(1, 2, 3), Table, 3));

        [Test] public void SlotMissingFromRankingGetsZero()
        {
            var r = new List<RankEntry> { new RankEntry { slot = 0, rank = 1 }, new RankEntry { slot = 2, rank = 2 } };
            CollectionAssert.AreEqual(new[] { 4, 0, 3, 0 }, ScoreRules.Award(r, Table, 4));
        }

        static SlotTotals T(int slot, int total, int wins = 0, int lastRank = 4, bool eligible = true) =>
            new SlotTotals { slot = slot, total = total, wins = wins, lastRoundRank = lastRank, eligible = eligible };

        [Test] public void ChampionIsUniqueMax() => CollectionAssert.AreEqual(new[] { 1 }, ScoreRules.Champions(new[] { T(0, 5), T(1, 9), T(2, 7) }));
        [Test] public void TotalTieGoesToMoreWins() => CollectionAssert.AreEqual(new[] { 2 }, ScoreRules.Champions(new[] { T(0, 9, wins: 1), T(1, 4), T(2, 9, wins: 2) }));
        [Test] public void ThenToBetterLastRound() => CollectionAssert.AreEqual(new[] { 0 }, ScoreRules.Champions(new[] { T(0, 9, 1, lastRank: 1), T(1, 9, 1, lastRank: 2) }));
        [Test] public void OtherwiseShared() => CollectionAssert.AreEqual(new[] { 0, 1 }, ScoreRules.Champions(new[] { T(0, 9, 1, 2), T(1, 9, 1, 2), T(2, 3) }));
        [Test] public void IneligibleNeverChampion() => CollectionAssert.AreEqual(new[] { 1 }, ScoreRules.Champions(new[] { T(0, 12, eligible: false), T(1, 9) }));

        static List<(int slot, int score)> S(params int[] scores)
        {
            var l = new List<(int, int)>();
            for (int i = 0; i < scores.Length; i++) l.Add((i, scores[i]));
            return l;
        }

        [Test] public void NoCrownWhileAllZero() => Assert.AreEqual(-1, ScoreRules.NextCrown(-1, S(0, 0, 0)));
        [Test] public void CrownGoesToTopScorer() => Assert.AreEqual(1, ScoreRules.NextCrown(-1, S(1, 2, 0)));
        [Test] public void TieKeepsCurrentWearer() => Assert.AreEqual(0, ScoreRules.NextCrown(0, S(2, 2, 1)));
        [Test] public void CrownMovesWhenOvertaken() => Assert.AreEqual(1, ScoreRules.NextCrown(0, S(0, 1)));
        [Test] public void TieWithoutWearerPicksFirst() => Assert.AreEqual(1, ScoreRules.NextCrown(-1, S(0, 3, 3)));

        [Test] public void ModifiersDefaultToOne()
        {
            var m = ScoreRules.ModifiersFor(0, new[] { T(0, 0), T(1, 10) });
            Assert.AreEqual(PlayerModifiers.One, m);
            Assert.AreEqual(1f, m.moveSpeed); Assert.AreEqual(1f, m.mass); Assert.AreEqual(1f, m.cooldown); Assert.AreEqual(1f, m.knockbackTaken);
        }
    }
}
