using System.Collections.Generic;

namespace ShooterParty
{
    // One participant of the round about to be played.
    public struct SlotInfo
    {
        public int slot, colourIndex;
        public ulong ownerClientId;   // NetworkManager.ServerClientId for bots and the host's own critter
        public bool isBot;
        public PlayerModifiers mods;
    }

    // What a mini-game controller needs from the session that runs it. SessionManager implements it;
    // tests use a small fake.
    public interface ISessionHost
    {
        IReadOnlyList<SlotInfo> RoundSlots { get; }
        int Epoch { get; }            // changes every round; late events from an older round are ignored
        float TimeScale { get; }      // 1 in play; tests shorten rounds
        void ReportScores(IReadOnlyList<(int slot, int score)> scores, int crownSlot);
        void ReportRound(IReadOnlyList<RankEntry> ranking);
    }
}
