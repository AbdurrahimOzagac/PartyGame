using System;
using Unity.Netcode;

namespace ShooterParty
{
    // How a mini-game wants the shared critter to behave (set on its MiniGameDefinition, synced to every copy).
    [Serializable]
    public struct CritterConfig : INetworkSerializable, IEquatable<CritterConfig>
    {
        public enum JumpMode : byte { Off, Pickup, On }

        public JumpMode jump;
        public int dashCharges;
        public float shotSpeed, shotGravity, shotCooldown;
        public float chargeTime;   // 0 = tap to shoot; > 0 reserved for charged shots (e.g. Blast Parkour)

        // Knockback Arena's values
        public static readonly CritterConfig Default = new CritterConfig
        {
            jump = JumpMode.Pickup, dashCharges = 3, shotSpeed = 12f, shotGravity = 3f, shotCooldown = 10f / 30f, chargeTime = 0f,
        };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref jump); s.SerializeValue(ref dashCharges);
            s.SerializeValue(ref shotSpeed); s.SerializeValue(ref shotGravity); s.SerializeValue(ref shotCooldown); s.SerializeValue(ref chargeTime);
        }

        public bool Equals(CritterConfig o) => jump == o.jump && dashCharges == o.dashCharges && shotSpeed == o.shotSpeed
                                               && shotGravity == o.shotGravity && shotCooldown == o.shotCooldown && chargeTime == o.chargeTime;
        public override bool Equals(object obj) => obj is CritterConfig o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(jump, dashCharges, shotSpeed, shotGravity, shotCooldown, chargeTime);
    }
}
