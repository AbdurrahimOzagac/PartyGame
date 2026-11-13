using ShooterParty;
using UnityEngine;

namespace KnockbackArena
{
    // Knockback Arena's spit: push the victim along the blob's path and keep only massKeptPerHit of its CURRENT mass,
    // with no floor. Every hit pushes further than the last (after 10 hits a critter keeps ~6 % of its mass and the
    // same spit sends it ~18x as far), so the game is about dodging.
    [CreateAssetMenu(menuName = "ShooterParty/Shot Effects/Knockback", fileName = "ShotEffect_Knockback")]
    public class KnockbackShotEffect : ShotEffect
    {
        public float impulse = 3.5f;
        [Range(0.01f, 1f)] public float massKeptPerHit = 0.75f;   // 0.75 = each hit takes 25 % of the mass it has now

        public override void ApplyServer(in ShotHit hit, ShotContext ctx)
        {
            var victim = ctx.Critter(hit.victimSlot);
            if (!victim) return;
            var dir = hit.velocity; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : victim.transform.forward;
            // Knock removes a fraction of the start mass; this is that fraction for "a share of the current mass"
            float lossOfStart = (1f - massKeptPerHit) * victim.Mass.Value / Mathf.Max(0.01f, victim.StartMass);
            ctx.Knock(victim, dir * impulse + Vector3.up * impulse * 0.25f, lossOfStart, 0f);
        }
    }
}
