using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ShooterParty
{
    // Owns the NetworkManager + UnityTransport and starts the game in one of its modes:
    //   Practice      host on 127.0.0.1 (UDP, ephemeral port), bots fill the room
    //   LocalHost     host on UDP 0.0.0.0:7777 for the same Wi-Fi / hotspot; others JoinLocal
    //   OnlineClient  WebSocket to the Pi via Cloudflare (wss -> WssBridge), ticket in the join payload
    //   DedicatedServer  headless Pi server: plain ws on 127.0.0.1:port/path behind cloudflared
    // Lives in SP_Boot (DontDestroyOnLoad); tests create it with Ensure().
    [DefaultExecutionOrder(-1000)]
    public class PartyNet : MonoBehaviour
    {
        public static PartyNet Instance { get; private set; }

        public NetworkPrefabsList prefabs;            // every networked prefab
        public NetworkObject[] serverSingletons;      // spawned once per server start (ShotSystem, SessionManager)

        public PartyMode Mode { get; private set; }
        public NetworkManager Net { get; private set; }
        public UnityTransport Transport { get; private set; }
        public WssBridge Bridge { get; private set; }
        public ServerArgs Server { get; private set; }
        public string LastDenyReason { get; private set; }
        public string Address { get; private set; }                    // LocalClient: the host address we joined

        public event Action ServerStarted;
        public event Action<string> Disconnected;                      // local client lost the server (reason)
        public event Action<ulong, ApprovedPlayer> PlayerLeft;         // server: a client went away
        public Func<ulong, ApprovedPlayer, string> Admission;          // server: null admits, else the reason

        readonly Dictionary<ulong, ApprovedPlayer> players = new Dictionary<ulong, ApprovedPlayer>();
        readonly HashSet<string> usedTickets = new HashSet<string>();
        bool configured, leaving;

        public IReadOnlyDictionary<ulong, ApprovedPlayer> Players => players;
        public bool IsServer => Net && Net.IsServer;
        static readonly string[] Argv = Environment.GetCommandLineArgs();
        static bool ForceBridge => Argv.Contains("-bridge");           // dev/test: route ws:// through the bridge too
        static int NetDelayMs { get { int i = Array.IndexOf(Argv, "-netDelay"); return i >= 0 && i + 1 < Argv.Length && int.TryParse(Argv[i + 1], out var d) ? d : 0; } }

        public static PartyNet Ensure() => Instance ? Instance : new GameObject("PartyNet").AddComponent<PartyNet>();

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Transport = gameObject.AddComponent<UnityTransport>();
            Net = gameObject.AddComponent<NetworkManager>();
            Net.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = Transport, TickRate = 30, ConnectionApproval = true, EnableSceneManagement = true,
            };
            Net.ConnectionApprovalCallback = Approve;
            Net.OnClientDisconnectCallback += OnClientDisconnect;
            Net.OnServerStarted += OnServerStarted;
        }

        void OnApplicationQuit() => leaving = true;

        void OnDestroy()
        {
            if (Instance != this) return;
            Leave();
            Instance = null;
        }

        void Configure()
        {
            if (configured) return;
            configured = true;
            if (prefabs) Net.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs);
        }

        // ------------------------------------------------------------------ start modes
        public bool StartPractice(string name)
        {
            Begin(PartyMode.Practice, name, null, ws: false);
            Transport.SetConnectionData("127.0.0.1", 0, "127.0.0.1");
            return Net.StartHost() || Fail();
        }

        public bool StartLocalHost(string name)
        {
            Begin(PartyMode.LocalHost, name, null, ws: false);
            Transport.SetConnectionData("0.0.0.0", PartyProtocol.LocalPort, "0.0.0.0");
            return Net.StartHost() || Fail();
        }

        public bool JoinLocal(string address, string name)
        {
            Begin(PartyMode.LocalClient, name, null, ws: false);
            var parts = (address ?? "").Trim().Split(':');
            ushort port = parts.Length > 1 && ushort.TryParse(parts[1], out var p) ? p : (ushort)PartyProtocol.LocalPort;
            Transport.SetConnectionData(parts[0], port);
            Address = port == PartyProtocol.LocalPort ? parts[0] : parts[0] + ":" + port;
            return Net.StartClient() || Fail();
        }

        public bool JoinOnline(string wsUrl, string ticket, string name)
        {
            Begin(PartyMode.OnlineClient, name, ticket, ws: true);
            var uri = new Uri(wsUrl);
            bool secure = uri.Scheme == "wss";
            int port = uri.Port > 0 ? uri.Port : secure ? 443 : 80;
            if (secure || ForceBridge)
            {
                Bridge = WssBridge.Start(uri.Host, port, tls: secure, delayMs: NetDelayMs);
                Transport.SetConnectionData("127.0.0.1", (ushort)Bridge.LocalPort);
            }
            else Transport.SetConnectionData(uri.Host, (ushort)port);
            Transport.ConnectionData.WebSocketPath = uri.AbsolutePath;
            return Net.StartClient() || Fail();
        }

        public bool StartDedicatedServer(ServerArgs args)
        {
            Server = args;
            Begin(PartyMode.DedicatedServer, null, null, ws: true);
            Transport.SetConnectionData("127.0.0.1", (ushort)args.port, "127.0.0.1");
            Transport.ConnectionData.WebSocketPath = args.wsPath;
            return Net.StartServer() || Fail();
        }

        // This machine's IPv4 addresses on live, non-loopback interfaces: what Local players type to join a host.
        public static List<string> LanAddresses()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254."))
                    .Select(a => a.Address.ToString()).Distinct().ToList();
            }
            catch (Exception e) { Debug.LogWarning("[PartyNet] no LAN addresses: " + e.Message); return new List<string>(); }
        }

        // Netcode's own disconnect texts are for developers; players see "Connection lost" instead.
        public static string FriendlyReason(string reason) =>
            string.IsNullOrWhiteSpace(reason) || reason.StartsWith("[Disconnect Event]") ? "Connection lost" : reason;

        // We chose to leave: no "connection lost" afterwards.
        public void Leave()
        {
            leaving = true;
            if (Net && Net.IsListening) Net.Shutdown();
            Bridge?.Dispose();
            Bridge = null;
            players.Clear();
            Mode = PartyMode.Offline;
        }

        void Begin(PartyMode mode, string name, string ticket, bool ws)
        {
            if (Net.IsListening) Leave();
            leaving = false;
            Configure();
            Mode = mode;
            LastDenyReason = null;
            players.Clear();
            usedTickets.Clear();
            Transport.UseWebSockets = ws;
            Transport.UseEncryption = false;   // online TLS is done by WssBridge / Cloudflare
            Net.NetworkConfig.ConnectionData = new JoinPayload { v = PartyProtocol.Version, name = name, ticket = ticket }.ToBytes();
        }

        bool Fail()
        {
            Debug.LogWarning($"[PartyNet] failed to start {Mode}");
            Leave();
            return false;
        }

        // ------------------------------------------------------------------ callbacks
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var server = Mode == PartyMode.DedicatedServer ? Server : null;
            bool ok = ConnectionGate.Approve(request.Payload, server, k => !usedTickets.Add(k), now, out var p, out var reason);
            if (ok && request.ClientNetworkId == NetworkManager.ServerClientId) p.leader = true;   // the host leads Local and Practice
            if (ok && Admission != null) { reason = Admission(request.ClientNetworkId, p); ok = reason == null; }
            response.Approved = ok;
            response.Reason = reason;
            response.CreatePlayerObject = false;
            if (ok) players[request.ClientNetworkId] = p;
        }

        void OnServerStarted()
        {
            if (serverSingletons != null)
                foreach (var prefab in serverSingletons)
                    if (prefab) Instantiate(prefab).Spawn(destroyWithScene: false);
            ServerStarted?.Invoke();
        }

        void OnClientDisconnect(ulong clientId)
        {
            if (Net.IsServer)
            {
                if (players.TryGetValue(clientId, out var p)) { PlayerLeft?.Invoke(clientId, p); players.Remove(clientId); }
                return;
            }
            if (clientId != Net.LocalClientId && clientId != NetworkManager.ServerClientId) return;
            if (leaving) return;
            LastDenyReason = FriendlyReason(Net.DisconnectReason);
            Bridge?.Dispose();
            Bridge = null;
            Mode = PartyMode.Offline;
            Disconnected?.Invoke(LastDenyReason);
        }
    }
}
