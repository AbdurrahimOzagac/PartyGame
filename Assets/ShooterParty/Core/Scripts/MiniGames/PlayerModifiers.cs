using System;
using Unity.Netcode;

namespace ShooterParty
{
    // Per-player multipliers applied to a critter before each round (all 1 = no change). This is where underdog
    // advantages and dynamic difficulty plug in later: ScoreRules.ModifiersFor decides them from the standings.
    [Serializable]
    public struct PlayerModifiers : INetworkSerializable, IEquatable<PlayerModifiers>
    {
        public float moveSpeed, mass, cooldown, knockbackTaken;

        public static readonly PlayerModifiers One = new PlayerModifiers { moveSpeed = 1f, mass = 1f, cooldown = 1f, knockbackTaken = 1f };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref moveSpeed); s.SerializeValue(ref mass); s.SerializeValue(ref cooldown); s.SerializeValue(ref knockbackTaken);
        }

        public bool Equals(PlayerModifiers o) => moveSpeed == o.moveSpeed && mass == o.mass && cooldown == o.cooldown && knockbackTaken == o.knockbackTaken;
        public override bool Equals(object obj) => obj is PlayerModifiers o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(moveSpeed, mass, cooldown, knockbackTaken);
        public override string ToString() => $"speed x{moveSpeed} mass x{mass} cooldown x{cooldown} knockback x{knockbackTaken}";
    }
}
