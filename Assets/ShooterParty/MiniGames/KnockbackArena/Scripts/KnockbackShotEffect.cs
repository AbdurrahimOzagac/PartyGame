using ShooterParty;
using UnityEngine;

namespace KnockbackArena
{
    // Knockback Arena's spit: push the victim along the blob's path and take 10 % of its starting mass, so every
    // later hit pushes it further (pitch rule). Values match the original single-player tuning.
    [CreateAssetMenu(menuName = "ShooterParty/Shot Effects/Knockback", fileName = "ShotEffect_Knockback")]
    public class KnockbackShotEffect : ShotEffect
    {
        public float impulse = 3.5f;
        public float massLossPerHit = 0.10f;
        public float minMass = 0.35f;

        public override void ApplyServer(in ShotHit hit, ShotContext ctx)
        {
            var victim = ctx.Critter(hit.victimSlot);
            if (!victim) return;
            var dir = hit.velocity; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : victim.transform.forward;
            ctx.Knock(victim, dir * impulse + Vector3.up * impulse * 0.25f, massLossPerHit, minMass);
        }
    }
}
