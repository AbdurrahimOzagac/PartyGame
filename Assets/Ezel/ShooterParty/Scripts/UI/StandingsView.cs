using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // Between rounds: the session totals as a bar race. Rows start in the old order, the bars grow by the round's
    // points, then the rows swap into the new order; the session leader gets the crown. The next game is teased below.
    public class StandingsView : SessionView
    {
        [System.Serializable]
        public class Row
        {
            public RectTransform root, bar;
            public Image barFill, portrait, crown;
            public TMP_Text rank, playerName, total;
        }

        public TMP_Text title, nextLabel, nextGame, nextRound;
        public Row[] rows = new Row[4];
        public Sprite[] faces = new Sprite[16];
        public float rowSpacing = 112f, firstRowY = 170f, barWidth = 760f;

        public override bool ShowsIn(SessionPhase phase) => phase == SessionPhase.Standings;

        protected override void OnShown()
        {
            var s = SessionManager.Instance;
            if (!s) return;
            int round = s.RoundIndex.Value + 1, games = s.PlaylistIds.Count;
            title.text = $"STANDINGS  <alpha=#99>· after {round} of {games}";
            var next = s.CurrentGameForIndex(round);
            nextLabel.text = next ? "NEXT UP" : "AND NOW…";
            nextGame.text = next ? next.displayName : "The champion!";
            nextRound.text = next ? $"Round {round + 1} of {games}" : "";
            StopAllCoroutines();
            StartCoroutine(Race(s.SeatsByTotal(before: true), s.SeatsByTotal()));
        }

        Vector2 Slot(int i) => new Vector2(0f, firstRowY - rowSpacing * i);

        IEnumerator Race(List<PlayerState> before, List<PlayerState> after)
        {
            int max = 1;
            foreach (var p in after) max = Mathf.Max(max, p.total);
            int n = Mathf.Min(before.Count, rows.Length);
            var rowOf = new Dictionary<int, Row>();   // slot -> row
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].root.gameObject.SetActive(i < n);
                if (i >= n) continue;
                var p = before[i]; var r = rows[i];
                rowOf[p.slot] = r;
                int colour = Mathf.Clamp(p.colour, 0, 3);
                r.root.anchoredPosition = Slot(i);
                r.rank.text = (i + 1).ToString();
                r.playerName.text = p.name.ToString();
                r.portrait.sprite = faces[colour * 4];
                r.barFill.color = CritterLook.Colours[colour];
                r.crown.enabled = false;
                SetBar(r, p.total - p.lastPoints, max);
            }
            yield return new WaitForSecondsRealtime(0.35f);
            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)   // bars grow by the round's points
            {
                float k = Ease.OutCubic(t / 0.9f);
                for (int i = 0; i < n; i++) SetBar(rows[i], Mathf.Lerp(before[i].total - before[i].lastPoints, before[i].total, k), max);
                yield return null;
            }
            for (int i = 0; i < n; i++) SetBar(rows[i], before[i].total, max);
            Sfx.Ui("UiClick");
            var from = new Vector2[n];
            for (int i = 0; i < n; i++) from[i] = rows[i].root.anchoredPosition;
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)   // rows swap into the new order
            {
                float k = Ease.OutBack(t / 0.45f);
                for (int j = 0; j < n; j++)
                {
                    var r = rowOf[after[j].slot];
                    int i = System.Array.IndexOf(rows, r);
                    r.root.anchoredPosition = Vector2.LerpUnclamped(from[i], Slot(j), k);
                }
                yield return null;
            }
            for (int j = 0; j < n; j++)
            {
                var r = rowOf[after[j].slot];
                r.root.anchoredPosition = Slot(j);
                r.rank.text = (j + 1).ToString();
                bool leads = after[j].total == after[0].total && after[0].total > 0;
                r.crown.enabled = leads;
                if (leads) r.portrait.sprite = faces[Mathf.Clamp(after[j].colour, 0, 3) * 4 + (int)HudCard.Face.Happy];
            }
        }

        void SetBar(Row r, float total, int max)
        {
            r.bar.sizeDelta = new Vector2(Mathf.Max(24f, barWidth * total / max), r.bar.sizeDelta.y);
            r.total.text = Mathf.RoundToInt(total).ToString();
        }
    }
}
