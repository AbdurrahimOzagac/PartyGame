using System;
using System.Collections;
using UnityEngine;

namespace ShooterParty
{
    // Online join, one step at a time, for the join screen:
    //   Contacting (room service) -> Joining (game server) -> InLobby  |  Failed(message)
    // The code, name and rejoin token live in memory only (nothing is saved).
    public class JoinFlow : MonoBehaviour
    {
        public enum Step { Idle, Contacting, Joining, InLobby, Failed }

        public const float ConnectTimeout = 20f;
        public static JoinFlow Instance { get; private set; }

        public Step State { get; private set; }
        public string Error { get; private set; }
        public string Code { get; private set; }
        public string PlayerName { get; private set; }
        public string RejoinToken { get; private set; }
        public event Action<Step> Changed;

        RoomServiceClient client;
        Coroutine running;

        public static JoinFlow Ensure()
        {
            var net = PartyNet.Ensure();
            return net.GetComponent<JoinFlow>() ?? net.gameObject.AddComponent<JoinFlow>();
        }

        void Awake()
        {
            Instance = this;
            PartyNet.Ensure().Disconnected += OnDisconnected;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (PartyNet.Instance) PartyNet.Instance.Disconnected -= OnDisconnected;
        }

        public void CreateRoom(string name, bool replaceLeavers, string roomServiceUrl = null) =>
            Begin(name, c => c.Create(name, replaceLeavers, Joined, Failed), roomServiceUrl);

        public void JoinRoom(string code, string name, string roomServiceUrl = null) =>
            Begin(name, c => c.Join(code, name, Joined, Failed), roomServiceUrl);

        public void Cancel()
        {
            if (running != null) StopCoroutine(running);
            running = null;
            if (PartyNet.Instance) PartyNet.Instance.Leave();
            Set(Step.Idle);
        }

        void Begin(string name, Func<RoomServiceClient, IEnumerator> request, string url)
        {
            if (running != null) StopCoroutine(running);
            client = new RoomServiceClient(url);
            PlayerName = name;
            Error = null;
            Set(Step.Contacting);
            running = StartCoroutine(request(client));
        }

        void Failed(JoinError e) { Error = e.message; Set(Step.Failed); }

        void Joined(RoomJoin r)
        {
            Code = r.code;
            RejoinToken = r.rejoinToken ?? RejoinToken;
            Set(Step.Joining);
            if (!PartyNet.Instance.JoinOnline(r.wsUrl, r.ticket, PlayerName)) { Error = "Couldn't connect to the game server"; Set(Step.Failed); return; }
            running = StartCoroutine(WaitForLobby());
        }

        IEnumerator WaitForLobby()
        {
            float until = Time.unscaledTime + ConnectTimeout;
            while (Time.unscaledTime < until)
            {
                var s = SessionManager.Instance;
                if (s && s.IsSpawned && s.TryGetLocalPlayer(out _)) { Set(Step.InLobby); yield break; }
                if (State != Step.Joining) yield break;
                yield return null;
            }
            PartyNet.Instance.Leave();
            Error = "Couldn't reach the game server — try again";
            Set(Step.Failed);
        }

        void OnDisconnected(string reason)
        {
            if (State != Step.Joining && State != Step.InLobby) return;
            Error = reason;
            Set(Step.Failed);
        }

        void Set(Step s)
        {
            State = s;
            Changed?.Invoke(s);
        }
    }
}
