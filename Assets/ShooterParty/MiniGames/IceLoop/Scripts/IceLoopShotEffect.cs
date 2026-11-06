using ShooterParty;
using UnityEngine;

namespace IceLoop
{
    // Ice Loop's spit: a hard shove along the blob's path. On the ice the target keeps sliding, so a hit near the
    // edge sends it into the water. No mass loss: this is a race, not a brawl.
    [CreateAssetMenu(menuName = "ShooterParty/Shot Effects/Ice Loop", fileName = "ShotEffect_IceLoop")]
    public class IceLoopShotEffect : ShotEffect
    {
        public float impulse = 6f;

        public override void ApplyServer(in ShotHit hit, ShotContext ctx)
        {
            var victim = ctx.Critter(hit.victimSlot);
            if (!victim) return;
            var dir = hit.velocity; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : victim.transform.forward;
            ctx.Knock(victim, dir * impulse + Vector3.up * impulse * 0.1f, 0f);
        }
    }
}
