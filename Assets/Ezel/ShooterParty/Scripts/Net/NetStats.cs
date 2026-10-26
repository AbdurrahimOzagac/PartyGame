using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ShooterParty
{
    // Connection numbers for the F3 overlay, sampled twice a second while connected:
    //   RttMs        round trip to the server (UnityTransport), 0 on a host
    //   JitterMs     standard deviation of the last 20 RTT samples
    //   Up/DownKbps  WebSocket traffic through WssBridge; -1 on UDP (no bridge to count it)
    //   ServerTickMs the dedicated server's average frame (SessionManager.ServerTickMs, from @@STATS), or the host's own
    public class NetStats : MonoBehaviour
    {
        public const float SampleSeconds = 0.5f;
        public const int JitterSamples = 20;

        public int RttMs { get; private set; }
        public int JitterMs { get; private set; }
        public float UpKbps { get; private set; } = -1f;
        public float DownKbps { get; private set; } = -1f;
        public float ServerTickMs { get; private set; } = -1f;
        public bool Live { get; private set; }

        readonly Queue<int> rtts = new Queue<int>(JitterSamples);
        float nextSample, frameSum;
        int frames;
        long lastUp = -1, lastDown = -1;

        public static NetStats Ensure()
        {
            var net = PartyNet.Ensure();
            return net.GetComponent<NetStats>() ?? net.gameObject.AddComponent<NetStats>();
        }

        public static float StdDev(IReadOnlyCollection<int> xs)
        {
            if (xs.Count < 2) return 0f;
            double mean = 0, sq = 0;
            foreach (var x in xs) mean += x;
            mean /= xs.Count;
            foreach (var x in xs) sq += (x - mean) * (x - mean);
            return (float)System.Math.Sqrt(sq / xs.Count);
        }

        void Update()
        {
            frameSum += Time.unscaledDeltaTime; frames++;
            if (Time.unscaledTime < nextSample) return;
            float dt = SampleSeconds + Mathf.Max(0f, Time.unscaledTime - nextSample);
            nextSample = Time.unscaledTime + SampleSeconds;
            var net = PartyNet.Instance;
            Live = net && net.Net && net.Net.IsListening && net.Mode != PartyMode.Offline;
            if (!Live) { rtts.Clear(); lastUp = lastDown = -1; UpKbps = DownKbps = ServerTickMs = -1f; frameSum = 0f; frames = 0; return; }

            RttMs = net.Net.IsServer ? 0 : (int)net.Transport.GetCurrentRtt(NetworkManager.ServerClientId);
            if (rtts.Count == JitterSamples) rtts.Dequeue();
            rtts.Enqueue(RttMs);
            JitterMs = Mathf.RoundToInt(StdDev(rtts));

            var bridge = net.Bridge;
            if (bridge == null) { UpKbps = DownKbps = -1f; lastUp = lastDown = -1; }
            else
            {
                long up = bridge.BytesUp, down = bridge.BytesDown;
                if (lastUp >= 0) { UpKbps = (up - lastUp) * 8f / 1000f / dt; DownKbps = (down - lastDown) * 8f / 1000f / dt; }
                lastUp = up; lastDown = down;
            }

            var s = SessionManager.Instance;
            if (net.Net.IsServer) ServerTickMs = frames > 0 ? frameSum / frames * 1000f : -1f;   // a host is its own server
            else ServerTickMs = s && s.IsSpawned && s.ServerTickMs.Value > 0f ? s.ServerTickMs.Value : -1f;
            frameSum = 0f; frames = 0;
        }
    }
}
