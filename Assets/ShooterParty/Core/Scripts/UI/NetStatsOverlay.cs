using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterParty.UI
{
    // F3 (or Settings > Show network stats): RTT, jitter, server tick and WebSocket kbps while in a room.
    // The choice is a device setting (sp.netstats), so it stays on between sessions.
    public class NetStatsOverlay : UiScreen
    {
        public TMP_Text text;

        NetStats stats;
        float nextText;

        public void Toggle() => GameSettings.ShowNetStats = !GameSettings.ShowNetStats;

        public override void Show()
        {
            base.Show();
            var g = GetComponent<CanvasGroup>();
            g.blocksRaycasts = false; g.interactable = false;   // a readout: clicks go through to the game and menus
        }

        static string Num(float v, string unit, string format = "0") => v < 0f ? "—" : v.ToString(format) + unit;

        protected override void Update()
        {
            base.Update();
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame) Toggle();
            if (!stats) stats = NetStats.Ensure();
            bool want = GameSettings.ShowNetStats && stats.Live;
            if (want && !IsShown) Show(); else if (!want && IsShown) Hide();
            if (!IsShown || Time.unscaledTime < nextText) return;
            nextText = Time.unscaledTime + 0.25f;
            var dot = ColorUtility.ToHtmlStringRGB(LobbyView.PingColour(stats.RttMs));
            text.text = $"<color=#{dot}>RTT {stats.RttMs} ms</color>  <alpha=#99>·<alpha=#FF>  JITTER {stats.JitterMs} ms  <alpha=#99>·<alpha=#FF>  " +
                        $"TICK {Num(stats.ServerTickMs, " ms", "0.0")}  <alpha=#99>·<alpha=#FF>  UP {Num(stats.UpKbps, " kbps")}   DOWN {Num(stats.DownKbps, " kbps")}";
        }
    }
}
