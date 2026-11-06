using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShooterParty;
using Unity.Netcode;
using UnityEngine;

namespace IceLoop
{
    // Ice Loop on the ShooterParty framework: race the icy loop for the whole round (definition.durationSeconds).
    //   cars    bumper cars: contact between two cars is decided here and delivered as a knockback to both owners
    //   score   +1 for every checkpoint gate passed, in order. The gates repeat every lap, so each new lap scores again
    //   out     nobody. Falling in the water puts you back at the last gate you passed (the start grid before the first)
    //   end     the timer
    //   ranking most checkpoints; a tie goes to whoever got there first
    public class IceLoopGame : MiniGameController
    {
        public float bumpRadius = 2.1f, bumpCooldown = 0.5f;   // two cars touch when their centres are closer than this
        public ParticleSystem fallPoof;

        IceTrack track;
        readonly Dictionary<int, int> passed = new Dictionary<int, int>();       // checkpoints passed (== score)
        readonly Dictionary<int, float> reachedAt = new Dictionary<int, float>();
        readonly Dictionary<int, int> lastGate = new Dictionary<int, int>();     // gate index of the last checkpoint, -1 = grid
        readonly Dictionary<int, float> lastSide = new Dictionary<int, float>();   // signed distance past the next gate, last tick
        readonly Dictionary<int, float> bumpedAt = new Dictionary<int, float>();   // per pair of seats
        readonly Dictionary<int, int> falls = new Dictionary<int, int>();
        readonly Dictionary<int, float> fellAt = new Dictionary<int, float>();   // the teleport reaches a remote owner a round trip later

        public int Falls(int slot) => falls.TryGetValue(slot, out var f) ? f : 0;

        protected override void OnBotSpawned(NetworkCritter critter) => critter.gameObject.AddComponent<IceBotAI>();

        protected override void OnSetup(IReadOnlyList<int> slots)
        {
            track = IceTrack.Instance ? IceTrack.Instance : FindAnyObjectByType<IceTrack>();
            passed.Clear(); reachedAt.Clear(); lastGate.Clear(); falls.Clear(); fellAt.Clear(); lastSide.Clear(); bumpedAt.Clear();
            foreach (var s in slots) { passed[s] = 0; lastGate[s] = -1; }
            // the core spawns critters facing the origin; line them up on the start grid facing down the road
            if (track) foreach (var s in slots) RespawnAt(s, track.GridPose(s));
        }

        protected override void OnServerTick(float dt)
        {
            if (!track) return;
            foreach (var slot in AliveSlots.ToList())
            {
                if (fellAt.TryGetValue(slot, out var t) && Time.time - t < 1f) continue;   // still being put back
                var p = CritterOf(slot).transform.position;
                if (track.Fell(p)) { Fall(slot, p); continue; }
                int next = lastGate[slot] < 0 ? 1 : (lastGate[slot] + 1) % track.GateCount;   // the grid is behind gate 0: the first gate to pass is 1
                float side = track.GateSide(p, next, out bool inDoor);
                // a point only when the car crosses the gate's plane forwards, inside the arch (a sparse update cannot skip it)
                if (lastSide.TryGetValue(slot, out var before) && before < 0f && side >= 0f && side < 6f && inDoor)
                {
                    lastGate[slot] = next;
                    passed[slot]++;
                    reachedAt[slot] = Time.time;
                    lastSide.Remove(slot);
                    AddScore(slot, 1);
                }
                else lastSide[slot] = side;
            }
            Bumps();
        }

        // Bumper cars. Physical collisions between critters are off in the core (one authority per body), so contact is
        // decided here from the server's view of the positions and sent to both owners as an equal and opposite shove.
        void Bumps()
        {
            var live = AliveSlots.Where(s => !(fellAt.TryGetValue(s, out var t) && Time.time - t < 1f)).OrderBy(s => s).ToList();
            for (int i = 0; i < live.Count; i++)
                for (int j = i + 1; j < live.Count; j++)
                {
                    NetworkCritter a = CritterOf(live[i]), b = CritterOf(live[j]);
                    var d = b.transform.position - a.transform.position;
                    float dy = d.y; d.y = 0f;
                    float dist = d.magnitude;
                    if (dist > bumpRadius || dist < 0.01f || Mathf.Abs(dy) > 1f) continue;
                    int pair = live[i] * 4 + live[j];
                    if (bumpedAt.TryGetValue(pair, out var last) && Time.time - last < bumpCooldown) continue;
                    var n = d / dist;
                    float closing = Mathf.Max(0f, Vector3.Dot(a.Motor.HorizontalVelocity - b.Motor.HorizontalVelocity, n));
                    float push = Mathf.Min(3f + closing * 0.8f, 9f);   // m/s each: a rear-ender speeds the front car up and slows itself
                    bumpedAt[pair] = Time.time;
                    a.ServerKnock(-1 - pair, -n * push, a.Mass.Value);
                    b.ServerKnock(-1 - pair, n * push, b.Mass.Value);
                    BumpRpc((a.transform.position + b.transform.position) * 0.5f, live[i], live[j]);
                }
        }

        void Fall(int slot, Vector3 where)
        {
            falls[slot] = Falls(slot) + 1;
            fellAt[slot] = Time.time;
            lastSide.Remove(slot);
            FellRpc(slot, where);
            RespawnAt(slot, lastGate[slot] < 0 ? track.GridPose(slot) : track.RespawnPose(lastGate[slot], slot));
        }

        protected override IReadOnlyList<RankEntry> RankPlayers()
        {
            float When(int s) => reachedAt.TryGetValue(s, out var t) ? t : float.MaxValue;
            var order = Slots.OrderByDescending(ScoreOf).ThenBy(When).ToList();
            return RankBy(order, (a, b) => ScoreOf(a) == ScoreOf(b) && ScoreOf(a) == 0);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void FellRpc(int slot, Vector3 position)
        {
            var c = NetworkCritter.All.FirstOrDefault(n => n && n.Slot.Value == slot);
            var colour = c ? CritterLook.Colours[Mathf.Clamp(c.ColourIndex.Value, 0, 3)] : Color.white;
            var splash = new Vector3(position.x, IceTrack.Instance ? IceTrack.Instance.waterY : -2f, position.z);
            Fx.Play(fallPoof, splash, Quaternion.identity, colour);
            StartCoroutine(Later(0.15f, () => Sfx.Play("Splash", splash)));
            if (c && c.IsOwner && !c.IsBot.Value) CameraShake.Report(c, 0.5f);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void BumpRpc(Vector3 position, int slotA, int slotB)
        {
            Sfx.Play("Hit", position);
            foreach (var c in NetworkCritter.All)
                if (c && c.IsOwner && !c.IsBot.Value && (c.Slot.Value == slotA || c.Slot.Value == slotB)) CameraShake.Report(c, 0.35f);
        }

        static IEnumerator Later(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }
    }
}
