using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ShooterParty
{
    // One room's headless game server. The room service starts it and reads these stdout lines:
    //   @@READY                       listening
    //   @@STATE {"phase":..,"humans":n,"connected":n,"pids":[..]}   on change
    //   @@STATS {"tickMs":..,"tickMsMax":..,"players":n,"rttAvgMs":..}   every 10 s
    //   @@RESULT {"totals":[..],"rankings":[[..],..]}   when a session reaches its final
    //   @@END                         about to exit
    // It exits when a finished session's players all left on purpose, when nobody has been connected for 60 s,
    // after 30 min idle in the lobby, or after 2 h.
    public class ServerRuntime : MonoBehaviour
    {
        public const float EmptySeconds = 60f, LobbyIdleSeconds = 30f * 60f, MaxSeconds = 2f * 3600f;

        string lastState;
        float emptySince, lobbySince, nextStats, tickSum, tickMax;
        int ticks;
        bool ended, reportedFinal, sessionOver;

        public void Begin(ServerArgs args)
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Fx.Enabled = false;   // nothing renders here
            var argv = Environment.GetCommandLineArgs();
            int ts = Array.IndexOf(argv, "-timeScale");
            if (ts >= 0 && ts + 1 < argv.Length && float.TryParse(argv[ts + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale))
                SessionFlow.TimeScaleForTests = Mathf.Clamp(scale, 0.05f, 1f);
            var net = PartyNet.Instance;
            net.ServerStarted += () => Emit("@@READY");
            if (!net.StartDedicatedServer(args)) { Emit("@@END"); Application.Quit(5); return; }
            emptySince = lobbySince = Time.unscaledTime;
            nextStats = Time.unscaledTime + 10f;
        }

        void Update()
        {
            if (ended) return;
            float dt = Time.unscaledDeltaTime * 1000f;
            tickSum += dt; tickMax = Mathf.Max(tickMax, dt); ticks++;
            var s = SessionManager.Instance;
            if (!s || !s.IsSpawned) return;
            var flow = s.ServerFlow;
            int humans = flow.Players.Count(p => !p.isBot && p.eligible);
            int connected = flow.Players.Count(p => !p.isBot && p.connected);
            var state = $"{{\"phase\":\"{flow.Phase}\",\"humans\":{humans},\"connected\":{connected},\"pids\":[{string.Join(",", flow.Players.Where(p => !p.isBot).Select(p => "\"" + p.pid + "\""))}]}}";
            if (state != lastState) { lastState = state; Emit("@@STATE " + state); }

            if (flow.Phase == SessionPhase.Final && !reportedFinal) { reportedFinal = true; sessionOver = true; Emit("@@RESULT " + Result(flow)); }
            if (flow.Phase != SessionPhase.Final) reportedFinal = false;

            if (connected > 0) emptySince = Time.unscaledTime;
            if (flow.Phase != SessionPhase.Lobby) lobbySince = Time.unscaledTime;
            if (Time.unscaledTime >= nextStats)
            {
                nextStats = Time.unscaledTime + 10f;
                var rtts = flow.Players.Where(p => !p.isBot && p.connected).Select(p => (int)p.rttMs).ToList();
                var inv = System.Globalization.CultureInfo.InvariantCulture;   // JSON numbers, whatever the server locale
                float avgMs = ticks > 0 ? tickSum / ticks : 0f;
                string avg = avgMs.ToString("F1", inv), max = tickMax.ToString("F1", inv);
                if (SessionManager.Instance && SessionManager.Instance.IsSpawned) SessionManager.Instance.ServerTickMs.Value = avgMs;   // players' F3 overlay
                string rtt = (rtts.Count > 0 ? rtts.Average() : 0.0).ToString("F0", inv);
                Emit("@@STATS {\"tickMs\":" + avg + ",\"tickMsMax\":" + max + ",\"players\":" + connected + ",\"rttAvgMs\":" + rtt + "}");
                tickSum = tickMax = 0f; ticks = 0;
            }
            string why = ExitReason(sessionOver, humans, connected, s.NetworkManager.ServerTime.Time - flow.LastDropAt,
                Time.unscaledTime - emptySince, Time.unscaledTime - lobbySince, Time.realtimeSinceStartup);
            if (why != null) End(why);
        }

        // A finished session that everyone left on purpose frees the slot at once. After a drop, wait: they are rejoining
        // (the empty rule still applies after EmptySeconds).
        public static string ExitReason(bool sessionOver, int humans, int connected, double sinceLastDrop, float emptyFor, float lobbyIdleFor, float uptime) =>
            sessionOver && humans == 0 && connected == 0 && sinceLastDrop > SessionFlow.RejoinGraceSeconds ? "session over, everyone left"
            : emptyFor > EmptySeconds ? "empty"
            : lobbyIdleFor > LobbyIdleSeconds ? "idle in lobby"
            : uptime > MaxSeconds ? "time limit" : null;

        static string Result(SessionFlow flow)
        {
            var totals = string.Join(",", flow.Players.OrderBy(p => p.slot).Select(p => $"{{\"slot\":{p.slot},\"pid\":\"{p.pid}\",\"bot\":{(p.isBot ? "true" : "false")},\"total\":{p.total}}}"));
            var rankings = string.Join(",", flow.Rankings.Select(r => "[" + string.Join(",", r.Select(e => $"{{\"slot\":{e.slot},\"rank\":{e.rank}}}")) + "]"));
            return $"{{\"totals\":[{totals}],\"rankings\":[{rankings}]}}";
        }

        void End(string why)
        {
            ended = true;
            Debug.Log("[ServerRuntime] stopping: " + why);
            Emit("@@END");
            Application.Quit(0);
        }

        // Protocol lines go to stdout only (the room service reads them); the Unity log gets a prefixed copy, so
        // even with -logFile - no line is ever duplicated.
        static void Emit(string line)
        {
            Console.Out.WriteLine(line);
            Console.Out.Flush();
            Debug.Log("[ServerRuntime] " + line);
        }
    }
}
