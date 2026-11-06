using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // After each round: the round's ranking, revealed 4th -> 1st, each row's points flying into its session total.
    // Everything comes from the seats (lastRank, lastPoints, total), so every client shows the same table.
    public class RoundResultsView : SessionView
    {
        [System.Serializable]
        public class Row
        {
            public RectTransform root;
            public Image badge, portrait, crown;
            public TMP_Text place, playerName, plus, total;
        }

        public TMP_Text title, subtitle;
        public Row[] rows = new Row[4];
        public Sprite[] faces = new Sprite[16];   // colour * 4 + (int)HudCard.Face

        static readonly string[] Places = { "1st", "2nd", "3rd", "4th" };
        static readonly Color Gold = new Color32(0xFF, 0xD2, 0x3F, 0xFF);

        public override bool ShowsIn(SessionPhase phase) => phase == SessionPhase.RoundResults;

        protected override void OnShown()
        {
            var s = SessionManager.Instance;
            if (!s) return;
            var game = s.CurrentGame;
            title.text = $"ROUND {s.RoundIndex.Value + 1} RESULTS";
            subtitle.text = game ? game.displayName : "";
            StopAllCoroutines();
            StartCoroutine(Reveal(s.SeatsByRoundRank()));
        }

        IEnumerator Reveal(List<PlayerState> order)
        {
            foreach (var r in rows) { r.root.gameObject.SetActive(false); r.crown.enabled = false; }
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = Mathf.Min(order.Count, rows.Length) - 1; i >= 0; i--)   // 4th -> 1st, 150 ms apart
            {
                var p = order[i]; var row = rows[i];
                int colour = Mathf.Clamp(p.colour, 0, 3), rank = p.lastRank > 0 ? p.lastRank : order.Count;
                row.place.text = Places[Mathf.Clamp(rank - 1, 0, 3)];
                row.badge.color = rank == 1 ? Gold : Color.Lerp(CritterLook.Colours[colour], Color.white, 0.15f);
                var face = rank == 1 ? HudCard.Face.Happy : i == order.Count - 1 ? HudCard.Face.Sad : HudCard.Face.Neutral;
                row.portrait.sprite = faces[colour * 4 + (int)face];
                row.playerName.text = p.name.ToString();
                row.plus.text = "+" + p.lastPoints;
                row.root.gameObject.SetActive(true);
                StartCoroutine(SlideIn(row.root));
                StartCoroutine(CountUp(row.total, p.total - p.lastPoints, p.total));
                Sfx.Ui("UiClick");
                yield return new WaitForSecondsRealtime(0.15f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            for (int i = 0; i < order.Count && i < rows.Length; i++)
            {
                if (order[i].lastRank != 1) continue;   // the crown lands on every round winner (ties share), with the fanfare
                rows[i].crown.enabled = true;
                StartCoroutine(Punch(rows[i].crown.rectTransform, 1.6f, 0.3f));
            }
            Sfx.Ui("Fanfare");
        }

        IEnumerator SlideIn(RectTransform rt)
        {
            var y = rt.anchoredPosition.y;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { rt.anchoredPosition = new Vector2(Mathf.Lerp(700f, 0f, Ease.OutBack(t / 0.25f)), y); yield return null; }
            rt.anchoredPosition = new Vector2(0f, y);
        }

        static IEnumerator CountUp(TMP_Text text, int from, int to)
        {
            text.text = from.ToString();
            yield return new WaitForSecondsRealtime(0.35f);
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { text.text = Mathf.RoundToInt(Mathf.Lerp(from, to, Ease.OutQuad(t / 0.6f))).ToString(); yield return null; }
            text.text = to.ToString();
        }

        static IEnumerator Punch(RectTransform rt, float peak, float time)
        {
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime) { rt.localScale = Vector3.one * Mathf.Lerp(peak, 1f, Ease.OutQuad(t / time)); yield return null; }
            rt.localScale = Vector3.one;
        }
    }
}
