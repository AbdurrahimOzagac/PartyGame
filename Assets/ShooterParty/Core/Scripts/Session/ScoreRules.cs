using System.Collections.Generic;
using System.Linq;

namespace ShooterParty
{
    public struct RankEntry { public int slot; public int rank; }   // rank is 1-based; tied players share a rank

    public struct SlotTotals
    {
        public int slot, total, wins, lastRoundRank;
        public bool eligible;   // false once the slot's human has left the session
    }

    // Every scoring decision lives here, so session rules (comeback points, underdog advantages) change in one place.
    public static class ScoreRules
    {
        // Round ranking -> points per slot. The table is read from the top, so 3 players get 4/3/2,
        // and tied ranks share the higher value. Seam: add comeback rules (e.g. last overall +1) here.
        public static int[] Award(IReadOnlyList<RankEntry> ranking, int[] table, int slotCount)
        {
            var points = new int[slotCount];
            foreach (var e in ranking)
                if (e.slot >= 0 && e.slot < slotCount && e.rank >= 1 && table.Length > 0)
                    points[e.slot] = table[System.Math.Min(e.rank, table.Length) - 1];
            return points;
        }

        // Highest total wins; ties go to more round wins, then the better last-round rank, otherwise shared.
        public static IReadOnlyList<int> Champions(IReadOnlyList<SlotTotals> totals)
        {
            var best = totals.Where(t => t.eligible)
                .OrderByDescending(t => t.total).ThenByDescending(t => t.wins).ThenBy(t => t.lastRoundRank).ToList();
            if (best.Count == 0) return new int[0];
            var top = best[0];
            return best.TakeWhile(t => t.total == top.total && t.wins == top.wins && t.lastRoundRank == top.lastRoundRank)
                       .Select(t => t.slot).OrderBy(s => s).ToList();
        }

        // The crown sits on the current game's top scorer; nobody wears it at 0, and a tie keeps the current wearer.
        public static int NextCrown(int currentWearer, IReadOnlyList<(int slot, int score)> scores)
        {
            int max = 0;
            foreach (var s in scores) if (s.score > max) max = s.score;
            if (max <= 0) return -1;
            foreach (var s in scores) if (s.slot == currentWearer && s.score == max) return currentWearer;
            foreach (var s in scores) if (s.score == max) return s.slot;
            return -1;
        }

        // Seam for underdog advantages / dynamic difficulty: decide each slot's multipliers from the standings.
        public static PlayerModifiers ModifiersFor(int slot, IReadOnlyList<SlotTotals> standings) => PlayerModifiers.One;
    }
}
