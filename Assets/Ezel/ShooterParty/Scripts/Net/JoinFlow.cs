using System;
using System.Collections;
using UnityEngine;

namespace ShooterParty
{
    // Online join, one step at a time, for the join screen:
    //   Contacting (room service) -> Joining (game server) -> InLobby  |  Failed(message)
    // InLobby means "in the room" (any phase). If the connection drops there, or the very first connection to the
    // game server fails (seen through Cloudflare), it retries on its own with a fresh ticket:
    //   Joining | InLobby -> Rejoining (room service rejoin with the token, backoff 1, 2, 4, 8 s, up to 60 s) -> InLobby | Failed
    // The code, name and rejoin token live in memory only (nothing is saved).
    public class JoinFlow : MonoBehaviour
    {
        public enum Step { Idle, Contacting, Joining, InLobby, Rejoining, Failed }

        public const float ConnectTimeout = 20f, RejoinWindow = 60f;
        public const string CouldNotRejoin = "Couldn't rejoin the room";
        public static JoinFlow Instance { get; private set; }

        public Step State { get; private set; }
        public string Error { get; private set; }
        public string Code { get; private set; }
        public string PlayerName { get; private set; }
        public string RejoinToken { get; private set; }
        public event Action<Step> Changed;
        public int RejoinAttempt { get; private set; }
        public bool EverInRoom { get; private set; }   // false while a first join is still being retried
        public float NextAttemptAt { get; private set; }   // unscaled time of the next try, for the progress card

        RoomServiceClient client;
        Coroutine running;
        bool lostWhileRejoining;

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
            EverInRoom = false;
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
                if (s && s.IsSpawned && s.TryGetLocalPlayer(out _)) { EverInRoom = true; Set(Step.InLobby); yield break; }
                if (State != Step.Joining) yield break;
                yield return null;
            }
            PartyNet.Instance.Leave();
            running = StartCoroutine(Rejoin());   // a fresh ticket and another try, as after a drop
        }

        void OnDisconnected(string reason)
        {
            if (State == Step.Rejoining) { lostWhileRejoining = true; return; }
            if ((State == Step.InLobby || State == Step.Joining) && reason == PartyNet.ConnectionLost && !string.IsNullOrEmpty(RejoinToken) && client != null)
            {
                if (running != null) StopCoroutine(running);
                running = StartCoroutine(Rejoin());
                return;
            }
            if (State != Step.Joining && State != Step.InLobby) return;
            Error = reason;
            Set(Step.Failed);
        }

        // The seat and points wait on the server for 60 s; the token proves it is ours (never the name).
        IEnumerator Rejoin()
        {
            Set(Step.Rejoining);
            float deadline = Time.unscaledTime + RejoinWindow, wait = 1f;
            RejoinAttempt = 0;
            while (Time.unscaledTime + wait < deadline)
            {
                NextAttemptAt = Time.unscaledTime + wait;
                yield return new WaitForSecondsRealtime(wait);
                wait = Mathf.Min(wait * 2f, 8f);
                RejoinAttempt++;
                RoomJoin got = default; JoinError err = default; bool ok = false;
                yield return client.Rejoin(Code, RejoinToken, r => { ok = true; got = r; }, e => err = e);
                if (!ok)
                {
                    if (err.status == 403 || err.status == 404 || err.status == 410) break;   // the room or the seat is gone
                    continue;                                                                 // server busy or unreachable: try again
                }
                RejoinToken = got.rejoinToken ?? RejoinToken;
                lostWhileRejoining = false;
                if (!PartyNet.Instance.JoinOnline(got.wsUrl, got.ticket, PlayerName)) continue;
                float until = Time.unscaledTime + ConnectTimeout;
                while (Time.unscaledTime < until && !lostWhileRejoining)
                {
                    var s = SessionManager.Instance;
                    if (s && s.IsSpawned && s.TryGetLocalPlayer(out _)) { running = null; EverInRoom = true; Set(Step.InLobby); yield break; }
                    yield return null;
                }
                PartyNet.Instance.Leave();
            }
            running = null;
            Error = EverInRoom ? CouldNotRejoin : "Couldn't reach the game server — try again";
            Set(Step.Failed);
        }

        void Set(Step s)
        {
            State = s;
            Changed?.Invoke(s);
        }
    }
}
