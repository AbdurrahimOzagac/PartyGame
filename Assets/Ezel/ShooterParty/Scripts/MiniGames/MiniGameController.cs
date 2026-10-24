using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace ShooterParty
{
    // Base class for a mini-game's rules. Subclass it, put it on a NetworkObject prefab and point the
    // MiniGameDefinition's controllerPrefab at it. The session spawns it when the round's scene has loaded.
    //
    // The core does the rest for every mini-game:
    //   ServerSetupRound  spawns one critter per slot at the scene's spawn points (frozen) + pickups, OnSetup
    //   ServerStartRound  after the countdown: Live = true, timer starts, OnRoundStart
    //   every frame       OnServerTick while live; the timer ends the round at durationSeconds
    //   EndRound          RankPlayers -> critters despawned -> results to the session (points, standings)
    // Scores you set drive the crown (top scorer of THIS game). All hooks run on the server only.
    public abstract class MiniGameController : NetworkBehaviour
    {
        public static ISessionHost Host { get; set; }
        public static MiniGameController Current { get; private set; }

        [SerializeField] MiniGameDefinition definition;
        public MiniGameDefinition Definition { get => definition; set => definition = value; }

        public readonly NetworkVariable<bool> Live = new NetworkVariable<bool>();          // inputs and bots move only while true
        public readonly NetworkVariable<double> EndsAt = new NetworkVariable<double>();    // server time the round times out

        readonly Dictionary<int, NetworkCritter> critters = new Dictionary<int, NetworkCritter>();
        readonly Dictionary<int, int> scores = new Dictionary<int, int>();
        readonly List<NetworkObject> extras = new List<NetworkObject>();
        int crown = -1;
        bool ended;

        protected MiniGameScene Scene { get; private set; }
        public Transform[] SpawnPoints => Scene ? Scene.spawnPoints : Array.Empty<Transform>();
        public IEnumerable<int> Slots => critters.Keys;
        public IEnumerable<int> AliveSlots => critters.Where(kv => kv.Value && !kv.Value.Eliminated.Value).Select(kv => kv.Key);
        public bool Ended => ended;

        // ------------------------------------------------------------------ hooks (server)
        protected virtual void OnSetup(IReadOnlyList<int> slots) { }
        protected virtual void OnRoundStart() { }
        protected virtual void OnServerTick(float dt) { }
        protected virtual void OnHit(in ShotHit hit) { }
        protected virtual void OnBotSpawned(NetworkCritter critter) { }          // attach your bot brain here
        protected virtual void OnEliminatedClient(int slot, Vector3 position) { } // every machine: your fall FX
        protected virtual IReadOnlyList<RankEntry> RankPlayers() =>
            RankBy(critters.Keys.OrderByDescending(ScoreOf).ToList(), (a, b) => ScoreOf(a) == ScoreOf(b));

        // ------------------------------------------------------------------ helpers
        public NetworkCritter CritterOf(int slot) => critters.TryGetValue(slot, out var c) ? c : null;
        public int ScoreOf(int slot) => scores.TryGetValue(slot, out var s) ? s : 0;
        public bool IsAlive(int slot) => critters.TryGetValue(slot, out var c) && c && !c.Eliminated.Value;

        protected void AddScore(int slot, int n) => SetScore(slot, ScoreOf(slot) + n);

        protected void SetScore(int slot, int value)
        {
            if (!critters.ContainsKey(slot)) return;
            scores[slot] = value;
            ReportScores();
        }

        // Out of the round: the critter disappears for everyone and its player spectates (NetworkCritter).
        protected void Eliminate(int slot)
        {
            var c = CritterOf(slot);
            if (!c || c.Eliminated.Value) return;
            c.Eliminated.Value = true;
            EliminatedRpc(slot, c.transform.position);
        }

        protected void RespawnAt(int slot, Pose pose)
        {
            var c = CritterOf(slot);
            if (c) c.ServerTeleport(pose.position, pose.rotation);
        }

        // Competition ranking ("1, 2, 2, 4") over slots already in order, best first.
        protected static List<RankEntry> RankBy(IList<int> ordered, Func<int, int, bool> tied)
        {
            var list = new List<RankEntry>();
            for (int i = 0; i < ordered.Count; i++)
            {
                int rank = i > 0 && tied(ordered[i - 1], ordered[i]) ? list[i - 1].rank : i + 1;
                list.Add(new RankEntry { slot = ordered[i], rank = rank });
            }
            return list;
        }

        // ------------------------------------------------------------------ lifecycle
        public override void OnNetworkSpawn()
        {
            Current = this;
            Scene = FindAnyObjectByType<MiniGameScene>();
            if (ShotSystem.Instance)
            {
                ShotSystem.Instance.Effect = definition ? definition.shotEffect : null;
                if (IsServer) ShotSystem.Instance.ServerHit += NotifyHit;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Current == this) Current = null;
            if (ShotSystem.Instance)
            {
                ShotSystem.Instance.ServerHit -= NotifyHit;
                if (definition && ShotSystem.Instance.Effect == definition.shotEffect) ShotSystem.Instance.Effect = null;
            }
        }

        public void ServerSetupRound()
        {
            if (!IsServer || Host == null) return;
            if (!Scene) Scene = FindAnyObjectByType<MiniGameScene>();
            if (ShotSystem.Instance) ShotSystem.Instance.Epoch = Host.Epoch;
            var slots = Host.RoundSlots;
            var spawns = SpawnPoints;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                var pos = spawns.Length > 0 && spawns[i % spawns.Length] ? spawns[i % spawns.Length].position : new Vector3(i * 2f, 0.05f, 0f);
                var toCentre = new Vector3(-pos.x, 0f, -pos.z);
                var go = Instantiate(definition.critterPrefab, pos, toCentre.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCentre) : Quaternion.identity);
                var nc = go.GetComponent<NetworkCritter>();
                nc.ServerSetup(s.slot, s.colourIndex, s.isBot, definition.critter, s.mods, Host.Epoch, s.ownerClientId);
                if (s.ownerClientId == NetworkManager.ServerClientId) go.Spawn(destroyWithScene: true);
                else go.SpawnWithOwnership(s.ownerClientId, destroyWithScene: true);
                critters[s.slot] = nc;
                scores[s.slot] = 0;
                if (s.isBot) OnBotSpawned(nc);
            }
            if (Scene && Scene.pickupPrefab)
                foreach (var spot in Scene.pickupSpots)
                    if (spot)
                    {
                        var p = Instantiate(Scene.pickupPrefab, spot.position, spot.rotation);
                        p.Spawn(destroyWithScene: true);
                        extras.Add(p);
                    }
            OnSetup(slots.Select(s => s.slot).ToList());
            ReportScores();
        }

        public void ServerStartRound()
        {
            if (!IsServer || Live.Value || ended) return;
            EndsAt.Value = NetworkManager.ServerTime.Time + definition.durationSeconds * (Host?.TimeScale ?? 1f);
            Live.Value = true;
            OnRoundStart();
        }

        // A player left mid-round: it counts as a fall at that moment (spec §5).
        public void ServerPlayerLeft(int slot)
        {
            if (IsServer && Live.Value && IsAlive(slot)) Eliminate(slot);
        }

        public void NotifyHit(ShotHit hit) { if (IsServer && Live.Value) OnHit(hit); }

        protected void EndRound()
        {
            if (!IsServer || ended) return;
            ended = true;
            Live.Value = false;
            var ranking = RankPlayers();
            foreach (var c in critters.Values) if (c && c.IsSpawned) c.NetworkObject.Despawn(true);
            foreach (var o in extras) if (o && o.IsSpawned) o.Despawn(true);
            critters.Clear();
            extras.Clear();
            Host?.ReportRound(ranking);
        }

        void Update()
        {
            if (!IsSpawned || !IsServer || !Live.Value || ended) return;
            OnServerTick(Time.deltaTime);
            if (!ended && NetworkManager.ServerTime.Time >= EndsAt.Value) EndRound();
        }

        void ReportScores()
        {
            var list = scores.Select(kv => (kv.Key, kv.Value)).ToList();
            crown = ScoreRules.NextCrown(crown, list);
            Host?.ReportScores(list, crown);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void EliminatedRpc(int slot, Vector3 position) => OnEliminatedClient(slot, position);
    }
}
