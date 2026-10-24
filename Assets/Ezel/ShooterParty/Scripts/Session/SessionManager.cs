using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterParty
{
    // The room: who is in it, the playlist, the phase, the scores. The server runs SessionFlow (the rules) and
    // mirrors it into these network variables; clients read them for the lobby, HUD and results, and ask for
    // changes with the Request* methods (validated on the server against the sender).
    // Spawned once per server start by PartyNet (serverSingletons), kept across scene loads.
    public class SessionManager : NetworkBehaviour, ISessionHost
    {
        public const string LobbyScene = "SP_Lobby";
        public static SessionManager Instance { get; private set; }

        public MiniGameCatalog catalog;

        public NetworkList<PlayerState> Players { get; private set; }
        public NetworkList<FixedString32Bytes> PlaylistIds { get; private set; }
        public readonly NetworkVariable<SessionPhase> Phase = new NetworkVariable<SessionPhase>();
        public readonly NetworkVariable<double> PhaseEndsAt = new NetworkVariable<double>(-1);   // server time; -1 = no timer
        public readonly NetworkVariable<int> RoundIndex = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> CrownSlot = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Epoch = new NetworkVariable<int>();
        public readonly NetworkVariable<FixedString64Bytes> PlaylistName = new NetworkVariable<FixedString64Bytes>();
        public readonly NetworkVariable<FixedString32Bytes> CurrentGameId = new NetworkVariable<FixedString32Bytes>();
        public readonly NetworkVariable<FixedString32Bytes> RoomCode = new NetworkVariable<FixedString32Bytes>();
        public readonly NetworkVariable<bool> ReplaceLeavers = new NetworkVariable<bool>();
        public readonly NetworkVariable<int> MinPlayers = new NetworkVariable<int>(3);

        public event Action<string> Message;          // this client: a refused request's reason ("Everyone must be ready")
        public SessionFlow ServerFlow => flow;          // server only (tests, server runtime)

        SessionFlow flow;
        MiniGameController controller;
        readonly Dictionary<ulong, TokenBucket> buckets = new Dictionary<ulong, TokenBucket>();
        readonly HashSet<string> cleanLeaves = new HashSet<string>();
        float nextRtt;
        bool mirrorQueued;
        string pendingScene;   // asked for while another scene event was still running

        void Awake()
        {
            Players = new NetworkList<PlayerState>();
            PlaylistIds = new NetworkList<FixedString32Bytes>();
        }

        public MiniGameDefinition CurrentGame => catalog ? catalog.Find(CurrentGameId.Value.ToString()) : null;

        public bool TryGetPlayer(int slot, out PlayerState p)
        {
            for (int i = 0; i < Players.Count; i++) if (Players[i].slot == slot) { p = Players[i]; return true; }
            p = default;
            return false;
        }

        public bool TryGetLocalPlayer(out PlayerState p)
        {
            ulong me = NetworkManager.LocalClientId;
            for (int i = 0; i < Players.Count; i++) if (!Players[i].isBot && Players[i].clientId == me) { p = Players[i]; return true; }
            p = default;
            return false;
        }

        // ------------------------------------------------------------------ server setup
        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;
            var net = PartyNet.Instance;
            var mode = net ? net.Mode : PartyMode.Practice;
            var args = net ? net.Server : null;
            flow = new SessionFlow(new SessionFlow.Options
            {
                minPlayers = mode == PartyMode.Practice ? 1 : args?.minPlayers ?? 3,
                maxPlayers = 4,
                replaceLeavers = args?.replaceLeavers ?? false,
                fillWithBots = mode == PartyMode.Practice,
            }, id => catalog ? catalog.Find(id) : null) { LobbyScene = LobbyScene };
            flow.Changed += () => mirrorQueued = true;
            flow.PhaseChanged += ph => Debug.Log($"[Session] phase {ph} (round {flow.RoundIndex + 1}/{flow.Playlist.Count})");
            flow.LoadScene += Load;
            flow.SetupRound += SetupRound;
            flow.StartRound += StartRound;
            flow.PlayerOutMidRound += slot => { if (controller) controller.ServerPlayerLeft(slot); };
            RoomCode.Value = args != null ? args.room : "";
            MiniGameController.Host = this;
            if (net)
            {
                net.Admission = (id, p) => flow.CanJoin(p.pid);
                net.PlayerLeft += OnPlayerLeft;
            }
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            if (net) foreach (var kv in net.Players) if (NetworkManager.ConnectedClientsIds.Contains(kv.Key)) Join(kv.Key, kv.Value);
            Mirror();
            if (SceneManager.GetActiveScene().name != LobbyScene) Load(LobbyScene);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (!IsServer) return;
            if (MiniGameController.Host == (ISessionHost)this) MiniGameController.Host = null;
            if (NetworkManager)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                if (NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            }
            var net = PartyNet.Instance;
            if (net) { net.PlayerLeft -= OnPlayerLeft; net.Admission = null; }
        }

        double Now => NetworkManager.ServerTime.Time;

        void OnClientConnected(ulong clientId)
        {
            var net = PartyNet.Instance;
            if (net && net.Players.TryGetValue(clientId, out var p)) Join(clientId, p);
        }

        void Join(ulong clientId, ApprovedPlayer p)
        {
            if (flow.AddPlayer(p.pid, p.name, false, clientId, Now, out var error) < 0)
                NetworkManager.DisconnectClient(clientId, error);
        }

        void OnPlayerLeft(ulong clientId, ApprovedPlayer p)
        {
            if (cleanLeaves.Remove(p.pid)) flow.Remove(p.pid, true, Now);
            else flow.Disconnected(p.pid, Now);
            buckets.Remove(clientId);
        }

        void Load(string scene)
        {
            if (controller && controller.IsSpawned) controller.NetworkObject.Despawn(true);
            controller = null;
            foreach (var c in NetworkCritter.All.ToList()) if (c && c.IsSpawned) c.NetworkObject.Despawn(true);   // never carried across loads
            var status = NetworkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
            Debug.Log($"[Session] loading {scene}: {status}");
            if (status == SceneEventProgressStatus.SceneEventInProgress) pendingScene = scene;   // retried when it finishes
            else if (status != SceneEventProgressStatus.Started) Debug.LogError($"[Session] could not load {scene}: {status}");
        }

        void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (timedOut != null && timedOut.Count > 0) Debug.LogWarning($"[Session] {timedOut.Count} client(s) timed out loading {sceneName}");
            Debug.Log($"[Session] loaded {sceneName} on {completed?.Count ?? 0} client(s)");
            if (pendingScene != null) { var next = pendingScene; pendingScene = null; Load(next); return; }
            flow.SceneLoaded(sceneName, Now);
        }

        void SetupRound()
        {
            var def = flow.CurrentGame;
            Epoch.Value++;
            CrownSlot.Value = -1;
            CurrentGameId.Value = def.id;
            var go = Instantiate(def.controllerPrefab);
            controller = go.GetComponent<MiniGameController>();
            controller.Definition = def;
            go.Spawn(destroyWithScene: true);
            controller.ServerSetupRound();
        }

        void StartRound()
        {
            if (controller) controller.ServerStartRound();
            mirrorQueued = true;
        }

        void Update()
        {
            if (!IsServer || flow == null) return;
            flow.Tick(Now);
            if (Time.unscaledTime >= nextRtt)
            {
                nextRtt = Time.unscaledTime + 1f;
                var transport = NetworkManager.NetworkConfig.NetworkTransport;
                foreach (var p in flow.Players.Where(p => !p.isBot && p.connected && p.clientId != NetworkManager.ServerClientId).ToList())
                    flow.SetRtt(p.pid.ToString(), (int)transport.GetCurrentRtt(p.clientId));
            }
            if (mirrorQueued) Mirror();
        }

        // Copy the flow into the replicated variables (only what changed is sent).
        void Mirror()
        {
            mirrorQueued = false;
            var ps = flow.Players;
            while (Players.Count > ps.Count) Players.RemoveAt(Players.Count - 1);
            for (int i = 0; i < ps.Count; i++)
            {
                if (i >= Players.Count) Players.Add(ps[i]);
                else if (!Players[i].Equals(ps[i])) Players[i] = ps[i];
            }
            Phase.Value = flow.Phase;
            double ends = flow.Phase == SessionPhase.Playing && controller ? controller.EndsAt.Value : flow.PhaseEndsAt;
            PhaseEndsAt.Value = double.IsInfinity(ends) ? -1 : ends;
            RoundIndex.Value = flow.RoundIndex;
            PlaylistName.Value = flow.PlaylistName ?? "";
            if (PlaylistIds.Count != flow.Playlist.Count || Enumerable.Range(0, PlaylistIds.Count).Any(i => PlaylistIds[i].ToString() != flow.Playlist[i]))
            {
                PlaylistIds.Clear();
                foreach (var id in flow.Playlist) PlaylistIds.Add(id);
            }
            ReplaceLeavers.Value = flow.ReplaceLeavers;
            MinPlayers.Value = flow.MinPlayers;
        }

        // ------------------------------------------------------------------ ISessionHost (mini-game controller)
        public IReadOnlyList<SlotInfo> RoundSlots => flow.RoundSlots();
        int ISessionHost.Epoch => Epoch.Value;
        public float TimeScale => SessionFlow.TimeScaleForTests;

        public void ReportScores(IReadOnlyList<(int slot, int score)> scores, int crownSlot)
        {
            flow.SetRoundScores(scores);
            CrownSlot.Value = crownSlot;
        }

        public void ReportRound(IReadOnlyList<RankEntry> ranking)
        {
            CrownSlot.Value = -1;
            flow.RoundDone(ranking, Now);
        }

        // ------------------------------------------------------------------ requests from players
        public void RequestReady(bool ready) => ReadyRpc(ready);
        public void RequestCycleColour() => CycleColourRpc();
        public void RequestPlaylist(string name, IEnumerable<string> ids) => PlaylistRpc(name ?? "", string.Join(",", ids ?? Enumerable.Empty<string>()));
        public void RequestReplaceLeavers(bool on) => ReplaceLeaversRpc(on);
        public void RequestKick(int slot) => KickRpc(slot);
        public void RequestStart() => StartRpc();
        public void RequestLeave() => LeaveRpc();

        bool Allowed(RpcParams p, out string pid)
        {
            pid = null;
            ulong sender = p.Receive.SenderClientId;
            if (!buckets.TryGetValue(sender, out var b)) buckets[sender] = b = new TokenBucket(20f, 40f);
            if (!b.Take(Time.unscaledTimeAsDouble)) { Debug.LogWarning($"[Session] request flood from client {sender}"); return false; }
            var net = PartyNet.Instance;
            if (!net || !net.Players.TryGetValue(sender, out var approved)) return false;
            pid = approved.pid;
            return true;
        }

        [Rpc(SendTo.Server)]
        void ReadyRpc(bool ready, RpcParams p = default) { if (Allowed(p, out var pid)) flow.SetReady(pid, ready); }

        [Rpc(SendTo.Server)]
        void CycleColourRpc(RpcParams p = default) { if (Allowed(p, out var pid) && flow.Phase == SessionPhase.Lobby) flow.CycleColour(pid); }

        [Rpc(SendTo.Server)]
        void PlaylistRpc(string name, string csv, RpcParams p = default)
        {
            if (!Allowed(p, out var pid)) return;
            if (name.Length > 64 || csv.Length > 400) { Reply(p, "That playlist is too long"); return; }
            var ids = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!flow.SetPlaylist(pid, name, ids, out var error)) Reply(p, error);
        }

        [Rpc(SendTo.Server)]
        void ReplaceLeaversRpc(bool on, RpcParams p = default) { if (Allowed(p, out var pid)) flow.SetReplaceLeavers(pid, on); }

        [Rpc(SendTo.Server)]
        void KickRpc(int slot, RpcParams p = default)
        {
            if (!Allowed(p, out var pid)) return;
            var kicked = flow.Kick(pid, slot);
            if (kicked == null) return;
            var net = PartyNet.Instance;
            foreach (var kv in net.Players.Where(kv => kv.Value.pid == kicked).ToList())
                NetworkManager.DisconnectClient(kv.Key, "You were removed by the leader");
        }

        [Rpc(SendTo.Server)]
        void StartRpc(RpcParams p = default)
        {
            if (!Allowed(p, out var pid)) return;
            var error = flow.TryStart(pid, Now);
            if (error != null) Reply(p, error);
        }

        [Rpc(SendTo.Server)]
        void LeaveRpc(RpcParams p = default)
        {
            if (!Allowed(p, out var pid)) return;
            cleanLeaves.Add(pid);
            if (p.Receive.SenderClientId == NetworkManager.ServerClientId) flow.Remove(pid, true, Now);
        }

        void Reply(RpcParams p, string message)
        {
            Debug.Log($"[Session] refused client {p.Receive.SenderClientId}: {message}");
            MessageRpc(message, RpcTarget.Single(p.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        void MessageRpc(string message, RpcParams p = default) => Message?.Invoke(message);
    }
}
