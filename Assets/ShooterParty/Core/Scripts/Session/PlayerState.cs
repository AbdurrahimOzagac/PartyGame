using System;
using Unity.Collections;
using Unity.Netcode;

namespace ShooterParty
{
    public enum SessionPhase : byte { Lobby, Loading, Intro, Countdown, Playing, RoundResults, Standings, Final }

    // One seat in the room, replicated to everyone (SessionManager.Players). Scores belong to the seat (keyed by
    // the player's pid), so a player who drops and rejoins keeps them.
    public struct PlayerState : INetworkSerializable, IEquatable<PlayerState>
    {
        public byte slot, colour;
        public FixedString32Bytes pid;
        public FixedString64Bytes name;
        public ulong clientId;
        public bool isBot, connected, ready, leader, eligible;
        public int total, roundScore, wins, lastRank, lastPoints;   // lastPoints: what the last round added to total
        public double joinedAt, disconnectedAt;
        public ushort rttMs;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref slot); s.SerializeValue(ref colour);
            s.SerializeValue(ref pid); s.SerializeValue(ref name); s.SerializeValue(ref clientId);
            s.SerializeValue(ref isBot); s.SerializeValue(ref connected); s.SerializeValue(ref ready);
            s.SerializeValue(ref leader); s.SerializeValue(ref eligible);
            s.SerializeValue(ref total); s.SerializeValue(ref roundScore); s.SerializeValue(ref wins); s.SerializeValue(ref lastRank); s.SerializeValue(ref lastPoints);
            s.SerializeValue(ref joinedAt); s.SerializeValue(ref disconnectedAt); s.SerializeValue(ref rttMs);
        }

        public bool Equals(PlayerState o) =>
            slot == o.slot && colour == o.colour && pid == o.pid && name == o.name && clientId == o.clientId &&
            isBot == o.isBot && connected == o.connected && ready == o.ready && leader == o.leader && eligible == o.eligible &&
            total == o.total && roundScore == o.roundScore && wins == o.wins && lastRank == o.lastRank && lastPoints == o.lastPoints &&
            joinedAt == o.joinedAt && disconnectedAt == o.disconnectedAt && rttMs == o.rttMs;

        public override bool Equals(object obj) => obj is PlayerState o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(slot, pid, total, ready, connected);
        public bool IsHuman => !isBot;
    }
}
