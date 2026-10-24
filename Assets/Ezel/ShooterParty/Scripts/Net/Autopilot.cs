using System;
using System.Linq;
using UnityEngine;

namespace ShooterParty
{
    // A client that plays by itself, for end-to-end tests and soak runs:
    //   Game -batchmode -nographics -autopilot -join wss://host/g/1 -ticket T -name Ann [-dropAt 25] [-leaveAt Loading]
    //   Game -batchmode -nographics -autopilot -local 192.168.1.5 -name Bea
    public class AutopilotArgs
    {
        public string join, ticket, local, name = "Auto";
        public string[] playlist = { "knockback_arena", "knockback_arena" };
        public float dropAt = -1f;                  // seconds into the first round: drop the connection (rejoin test)
        public SessionPhase? leaveAt;               // quit as soon as this phase starts

        public static bool TryParse(string[] argv, out AutopilotArgs a)
        {
            a = null;
            if (argv == null || !argv.Contains("-autopilot")) return false;
            string Get(string k) { int i = Array.IndexOf(argv, k); return i >= 0 && i + 1 < argv.Length ? argv[i + 1] : null; }
            var r = new AutopilotArgs { join = Get("-join"), ticket = Get("-ticket"), local = Get("-local"), name = Get("-name") ?? "Auto" };
            if (Get("-playlist") is string pl) r.playlist = pl.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (float.TryParse(Get("-dropAt"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) r.dropAt = d;
            if (Enum.TryParse<SessionPhase>(Get("-leaveAt"), out var leave)) r.leaveAt = leave;
            if (r.join == null && r.local == null) return false;
            a = r;
            return true;
        }
    }

    public class Autopilot : MonoBehaviour
    {
        AutopilotArgs args;
        SessionPhase last = SessionPhase.Lobby;
        bool sawFinal, dropped, quitting;
        float playingSince = -1f, nextStart;
        NetworkCritter driven;

        public void Begin(AutopilotArgs a)
        {
            args = a;
            Application.targetFrameRate = 60;
            var net = PartyNet.Instance;
            net.Disconnected += reason => { Log("disconnected: " + reason); if (!PartyNetRejoining()) Quit(sawFinal ? 0 : 3); };
            bool ok = a.local != null ? net.JoinLocal(a.local, a.name) : net.JoinOnline(a.join, a.ticket, a.name);
            Log("joining " + (a.local ?? a.join) + " as " + a.name + ": " + ok);
            if (!ok) Quit(4);
        }

        // Task 17 replaces this with the real rejoin flow.
        static bool PartyNetRejoining() => false;

        void Update()
        {
            var s = SessionManager.Instance;
            if (quitting || !s || !s.IsSpawned) return;
            var phase = s.Phase.Value;
            if (phase != last)
            {
                Log("phase " + phase);
                if (phase == SessionPhase.Final) sawFinal = true;
                if (args.leaveAt == phase) { Log("leaving on purpose at " + phase); s.RequestLeave(); Quit(0); return; }
                if (phase == SessionPhase.Lobby && sawFinal) { Log("session over"); s.RequestLeave(); Quit(0); return; }
                last = phase;
            }
            if (!s.TryGetLocalPlayer(out var me)) return;
            if (phase == SessionPhase.Lobby) Lobby(s, me);
            if (phase == SessionPhase.Playing) Play();
            else playingSince = -1f;
        }

        void Lobby(SessionManager s, PlayerState me)
        {
            if (!me.leader) { if (!me.ready) s.RequestReady(true); return; }
            if (s.PlaylistIds.Count == 0) { s.RequestPlaylist("Autopilot", args.playlist); return; }
            int humans = 0; bool allReady = true;
            for (int i = 0; i < s.Players.Count; i++)
            {
                var p = s.Players[i];
                if (p.isBot || !p.connected) continue;
                humans++;
                if (!p.leader && !p.ready) allReady = false;
            }
            if (humans >= s.MinPlayers.Value && allReady && Time.unscaledTime > nextStart)
            {
                nextStart = Time.unscaledTime + 2f;
                s.RequestStart();
            }
        }

        void Play()
        {
            if (playingSince < 0f) playingSince = Time.unscaledTime;
            var mine = NetworkCritter.All.FirstOrDefault(c => c && c.IsOwner && !c.IsBot.Value && !c.Eliminated.Value);
            if (mine && mine != driven && MiniGameController.Current)
            {
                driven = mine;
                var input = mine.GetComponent<LocalCritterInput>();
                if (input) Destroy(input);
                MiniGameController.Current.AttachAutopilot(mine);
                Log("driving my critter, slot " + mine.Slot.Value);
            }
            if (args.dropAt >= 0f && !dropped && Time.unscaledTime - playingSince >= args.dropAt)
            {
                dropped = true;
                Log("dropping the connection on purpose");
                PartyNet.Instance.Bridge?.DropAll();
            }
        }

        void Quit(int code)
        {
            if (quitting) return;
            quitting = true;
            Log("exit " + code);
            Application.Quit(code);
        }

        static void Log(string msg) => Debug.Log("[Autopilot] " + msg);
    }
}
