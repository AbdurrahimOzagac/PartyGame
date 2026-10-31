using UnityEngine;

namespace ShooterParty
{
    // One server-decided contact between a shot and a critter.
    public struct ShotHit
    {
        public int shooterSlot, victimSlot;
        public Vector3 point, normal, velocity;   // velocity = the blob's at impact
    }

    // What the shared shot does in a given mini-game ("same shot, new effect"). Effects on OTHER critters run on the
    // server (ApplyServer). Effects on the shooter itself (recoil, blast-jumping off your own shot) run on the
    // shooter's machine, because a server round trip would make them feel late.
    public abstract class ShotEffect : ScriptableObject
    {
        public abstract void ApplyServer(in ShotHit hit, ShotContext ctx);
        public virtual void OnFireLocal(NetworkCritter shooter) { }
        public virtual void OnOwnImpactLocal(NetworkCritter shooter, Vector3 point) { }
    }

    // Server-side tools a ShotEffect uses to change the game.
    public sealed class ShotContext
    {
        readonly ShotSystem system;
        readonly int blobId;

        internal ShotContext(ShotSystem system, int blobId) { this.system = system; this.blobId = blobId; }

        public int Epoch => system.Epoch;
        public NetworkCritter Critter(int slot) => ShotSystem.CritterBySlot(slot);

        // Push `victim` by `impulse` (N·s) using the server's mass, then lower its mass by a fraction of its start
        // mass (never below minMassFraction). The victim's owner applies it; bots and the host's critter at once.
        public void Knock(NetworkCritter victim, Vector3 impulse, float massLossFraction, float minMassFraction = 0.35f)
        {
            if (!victim || !victim.IsSpawned || victim.Eliminated.Value) return;
            impulse *= victim.Modifiers.Value.knockbackTaken;
            float mass = Mathf.Max(0.01f, victim.Mass.Value);
            float newMass = Mathf.Max(minMassFraction * victim.StartMass, mass - massLossFraction * victim.StartMass);
            system.MarkKnocked(blobId);
            victim.ServerKnock(blobId, impulse / mass, newMass);
        }
    }
}
