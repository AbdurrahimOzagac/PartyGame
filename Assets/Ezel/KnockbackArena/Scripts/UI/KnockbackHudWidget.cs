using System.Collections.Generic;
using ShooterParty;
using ShooterParty.UI;
using UnityEngine;

namespace KnockbackArena.UI
{
    // Knockback Arena's piece of the shared HUD (MiniGameDefinition.hudWidget): each card's stat line is that
    // critter's mass ("65%", white >= 70 %, amber 40-70 %, pulsing pink below), the meter drains with it, and a
    // hit punches the card. A mass ring floats over every other critter. Mass comes from NetworkCritter.Mass.
    public class KnockbackHudWidget : MonoBehaviour
    {
        public MassRing ringPrefab;

        PartyHud hud;
        readonly Dictionary<NetworkCritter, float> shown = new Dictionary<NetworkCritter, float>(), last = new Dictionary<NetworkCritter, float>(),
            speed = new Dictionary<NetworkCritter, float>();
        readonly Dictionary<NetworkCritter, MassRing> rings = new Dictionary<NetworkCritter, MassRing>();

        public static Color MassColour(float m, float time)
        {
            if (m >= 0.7f) return Color.white;
            if (m >= 0.4f) return HudCard.Amber;
            return Color.Lerp(HudCard.Pink, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 3f)));
        }

        void Awake() => hud = GetComponentInParent<PartyHud>(true);

        void OnDestroy()
        {
            foreach (var r in rings.Values) if (r) Destroy(r.gameObject);
        }

        void Update()
        {
            if (!hud) return;
            float dt = Time.unscaledDeltaTime;
            if (shown.Count > NetworkCritter.All.Count)   // last round's critters are gone (their rings went with them)
            {
                foreach (var dead in new List<NetworkCritter>(shown.Keys))
                    if (!dead) { shown.Remove(dead); last.Remove(dead); speed.Remove(dead); rings.Remove(dead); }
            }
            foreach (var c in NetworkCritter.All)
            {
                if (!c || !c.IsSpawned) continue;
                float m = c.MassFraction;
                if (!shown.TryGetValue(c, out var s)) { s = m; last[c] = m; speed[c] = 0f; }
                if (Mathf.Abs(m - last[c]) > 0.001f)
                {
                    speed[c] = Mathf.Abs(s - m) / 0.25f;   // the number rolls to the new value over 250 ms
                    var hit = m < last[c] ? hud.CardFor(c.Slot.Value) : null;
                    if (hit) hit.PunchStat("-" + Mathf.Max(1, Mathf.RoundToInt((last[c] - m) * 100f)));
                }
                last[c] = m;
                s = Mathf.MoveTowards(s, m, speed[c] * dt);
                shown[c] = s;
                var card = hud.CardFor(c.Slot.Value);
                if (card)
                {
                    card.SetStatLine(Mathf.Round(s * 100f).ToString("0") + "%", MassColour(m, Time.unscaledTime));
                    card.SetStatMeter(m);
                }
                if (ringPrefab && !rings.ContainsKey(c) && !(c.IsOwner && !c.IsBot.Value))   // your own mass is on your card
                {
                    var ring = Instantiate(ringPrefab);
                    ring.Bind(c, CritterLook.Colours[Mathf.Clamp(c.ColourIndex.Value, 0, 3)]);
                    rings[c] = ring;
                }
            }
        }
    }
}
