using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // Before each round: the mini-game's name, tagline, how to play and controls (all from its MiniGameDefinition),
    // "ROUND x OF n", and a bar that runs out with the intro; then the HUD's 3-2-1 takes over.
    public class IntroCard : SessionView
    {
        public TMP_Text title, tagline, round, controls;
        public TMP_Text[] lines = new TMP_Text[3];
        public Image icon, progress;

        double startedAt, endsAt;

        public override bool ShowsIn(SessionPhase phase) => phase == SessionPhase.Intro;

        protected override void OnShown()
        {
            var s = SessionManager.Instance;
            if (!s) return;
            Sfx.Music("MusicGameplay");
            Sfx.Ui("Whoosh");
            var def = s.CurrentGame;
            title.text = def ? def.displayName : "";
            tagline.text = def ? def.tagline : "";
            controls.text = def ? def.controlsHint : "";
            for (int i = 0; i < lines.Length; i++)
            {
                bool on = def && i < def.howToPlay.Length && !string.IsNullOrEmpty(def.howToPlay[i]);
                lines[i].transform.parent.gameObject.SetActive(on);
                lines[i].text = on ? def.howToPlay[i] : "";
            }
            icon.enabled = def && def.icon;
            if (icon.enabled) icon.sprite = def.icon;
            round.text = $"ROUND {s.RoundIndex.Value + 1} OF {s.PlaylistIds.Count}";
            startedAt = s.NetworkManager.ServerTime.Time;
            endsAt = s.PhaseEndsAt.Value;
        }

        protected override void Update()
        {
            base.Update();
            var s = SessionManager.Instance;
            if (!IsShown || !s || !s.IsSpawned || endsAt <= startedAt) return;
            progress.fillAmount = 1f - Mathf.Clamp01((float)((s.NetworkManager.ServerTime.Time - startedAt) / (endsAt - startedAt)));
        }
    }
}
