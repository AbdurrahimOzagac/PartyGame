using ShooterParty;
using ShooterParty.UI;
using UnityEngine;

namespace IceLoop.UI
{
    // Ice Loop's piece of the shared HUD: every card's stat line is "LAP 2 · 17" (lap, checkpoints passed) and the
    // meter fills with the progress through the current lap. Reads the in-game score the session mirrors.
    public class IceLoopHudWidget : MonoBehaviour
    {
        PartyHud hud;
        IceTrack track;

        void Awake() => hud = GetComponentInParent<PartyHud>(true);

        void Update()
        {
            var session = SessionManager.Instance;
            if (!hud || !session) return;
            if (!track) track = FindAnyObjectByType<IceTrack>();
            if (!track) return;
            int gates = track.GateCount;
            for (int i = 0; i < session.Players.Count; i++)
            {
                var p = session.Players[i];
                var card = hud.CardFor(p.slot);
                if (!card) continue;
                card.SetStatLine("LAP " + (p.roundScore / gates + 1) + " · " + p.roundScore, Color.white);
                card.SetStatMeter((p.roundScore % gates) / (float)gates);
            }
        }
    }
}
