using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShooterParty;
using UnityEngine;

namespace KnockbackArena
{
    // Knockback Arena on the ShooterParty framework: spit each other off the floating ice.
    //   score   +1 per knockout, credited to the last critter that hit the victim within koCreditSeconds
    //   out     below outY, or hovering outside the ice footprint for campSeconds (anti-cheat)
    //   end     one critter left, or the timer (definition.durationSeconds)
    //   ranking by survival: alive first (KOs, then mass left), then fallers latest-first; same-tick falls share a rank
    public class KnockbackArenaGame : MiniGameController
    {
        public float outY = -2.5f;
        public float koCreditSeconds = 4f;
        public float arenaHalf = 8f, arenaCornerRadius = 4f, footprintMargin = 0.75f, campSeconds = 1f;
        public ParticleSystem fallPoof;

        readonly Dictionary<int, (int shooter, float time)> lastHit = new Dictionary<int, (int, float)>();
        readonly Dictionary<int, float> outsideSince = new Dictionary<int, float>();
        readonly Dictionary<int, int> fellAtStep = new Dictionary<int, int>();
        int step;

        protected override void OnBotSpawned(NetworkCritter critter) => critter.gameObject.AddComponent<ArenaBotAI>();

        protected override void OnHit(in ShotHit hit)
        {
            if (hit.victimSlot >= 0 && hit.shooterSlot != hit.victimSlot) lastHit[hit.victimSlot] = (hit.shooterSlot, Time.time);
        }

        protected override void OnServerTick(float dt)
        {
            step++;   // everything that falls during one tick shares a rank
            foreach (var slot in AliveSlots.ToList())
            {
                var p = CritterOf(slot).transform.position;
                bool fell = p.y < outY;
                if (!fell && FootprintDistance(p) > footprintMargin)
                {
                    if (!outsideSince.TryGetValue(slot, out var since)) outsideSince[slot] = Time.time;
                    else if (Time.time - since > campSeconds) fell = true;
                }
                else outsideSince.Remove(slot);
                if (fell) Fall(slot);
            }
            if (AliveSlots.Count() <= 1) EndRound();
        }

        void Fall(int slot)
        {
            if (lastHit.TryGetValue(slot, out var h) && h.shooter != slot && Time.time - h.time <= koCreditSeconds && Slots.Contains(h.shooter))
                AddScore(h.shooter, 1);
            fellAtStep[slot] = step;
            Eliminate(slot);
        }

        // Signed distance from the rounded-square ice top (half size arenaHalf, corner radius) in the XZ plane.
        float FootprintDistance(Vector3 p)
        {
            float r = arenaCornerRadius, h = arenaHalf - r;
            var q = new Vector2(Mathf.Abs(p.x) - h, Mathf.Abs(p.z) - h);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        protected override IReadOnlyList<RankEntry> RankPlayers()
        {
            float MassOf(int s) { var c = CritterOf(s); return c ? c.Mass.Value : 0f; }
            var alive = AliveSlots.OrderByDescending(ScoreOf).ThenByDescending(MassOf).ToList();
            var fallen = Slots.Where(s => !alive.Contains(s)).OrderByDescending(s => fellAtStep.TryGetValue(s, out var t) ? t : -1).ToList();
            var order = alive.Concat(fallen).ToList();
            return RankBy(order, (a, b) =>
            {
                bool aAlive = alive.Contains(a), bAlive = alive.Contains(b);
                if (aAlive != bAlive) return false;
                if (aAlive) return ScoreOf(a) == ScoreOf(b) && Mathf.Abs(MassOf(a) - MassOf(b)) < 1e-3f;
                return fellAtStep.TryGetValue(a, out var ta) && fellAtStep.TryGetValue(b, out var tb) && ta == tb;
            });
        }

        protected override void OnEliminatedClient(int slot, Vector3 position)
        {
            var c = CritterOf(slot) ?? NetworkCritter.All.FirstOrDefault(n => n && n.Slot.Value == slot);
            var colour = c ? CritterLook.Colours[Mathf.Clamp(c.ColourIndex.Value, 0, 3)] : Color.white;
            var splash = new Vector3(position.x, outY, position.z);
            Fx.Play(fallPoof, splash, Quaternion.identity, colour);
            StartCoroutine(Later(0.6f, () => Sfx.Play("Splash", splash)));
            if (c && c.IsOwner && !c.IsBot.Value) CameraShake.Report(c, 0.6f);
        }

        static IEnumerator Later(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }
    }
}
