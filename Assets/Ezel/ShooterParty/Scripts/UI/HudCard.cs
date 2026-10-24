using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // One seat's card in the HUD's top strip: portrait, name, game score, crown icon, connection dot, and a stat
    // line + meter that the mini-game's HUD widget owns (Knockback Arena: "65%" mass). A widget calls PunchStat
    // when its stat drops, which punches, shakes and floats the loss (UI brief A1).
    public class HudCard : MonoBehaviour
    {
        public enum Face { Neutral, Happy, Hurt, Sad }

        public Image portrait, portraitBg, band, barFill, barGhost, crown, outline, connection;
        public TMP_Text nameText, statText, scoreText, deltaText;
        public RectTransform statGroup;
        public CanvasGroup group;

        public static readonly Color Amber = new Color(1f, 0.761f, 0.2f), Pink = new Color(1f, 0.239f, 0.545f);

        public int Slot { get; private set; } = -1;

        Sprite[] faces;
        float meter = 1f, ghost = 1f, ghostHold, punchT = -1f, deltaT = -1f, faceUntil;
        Face face, restFace = Face.Neutral;
        Color statColour = Color.white;

        public void Bind(int slot, string playerName, Color colour, Sprite[] portraits, bool local)
        {
            Slot = slot;
            faces = portraits;
            nameText.text = playerName;
            band.color = colour;
            portraitBg.color = Color.Lerp(colour, Color.white, 0.25f);
            outline.enabled = local;                         // the local card gets a white stroke and a little scale
            transform.localScale = Vector3.one * (local ? 1.06f : 1f);
            meter = ghost = 1f;
            restFace = Face.Neutral;
            if (group) group.alpha = 1f;
            SetFace(Face.Neutral);
            deltaText.alpha = 0f;
            SetStatLine("", Color.white);
            SetCrown(false);
            SetScore(0);
        }

        public void SetScore(int s) => scoreText.text = s.ToString();
        public void SetCrown(bool on) => crown.enabled = on;

        // Green < 120 ms, amber < 220 ms, red otherwise; a negative rtt hides the dot (bots, practice).
        public void SetConnection(int rttMs)
        {
            connection.enabled = rttMs >= 0;
            if (rttMs >= 0) connection.color = LobbyView.PingColour(rttMs);
        }

        public void SetStatLine(string text, Color colour)
        {
            statText.text = text;
            statColour = colour;
            statGroup.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        // 0..1 bar under the stat line; the white ghost holds 300 ms then drains to it.
        public void SetStatMeter(float fill)
        {
            if (fill < meter - 0.001f) { ghost = Mathf.Max(ghost, meter); ghostHold = Time.unscaledTime + 0.3f; }
            meter = fill;
        }

        public void PunchStat(string delta)
        {
            punchT = 0f; deltaT = 0f;
            deltaText.text = delta;
            SetFace(Face.Hurt); faceUntil = Time.unscaledTime + 0.6f;
        }

        public void SetOut(bool isOut)
        {
            restFace = isOut ? Face.Sad : Face.Neutral;
            if (face != Face.Hurt) SetFace(restFace);
            if (group) group.alpha = isOut ? 0.55f : 1f;
        }

        public void SetWinner() { restFace = Face.Happy; SetFace(Face.Happy); }

        void SetFace(Face f)
        {
            face = f;
            if (faces != null && (int)f < faces.Length && faces[(int)f]) portrait.sprite = faces[(int)f];
        }

        void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            if (now > ghostHold) ghost = Mathf.MoveTowards(ghost, meter, dt / 0.25f);
            if (face == Face.Hurt && now > faceUntil) SetFace(restFace);
            barFill.fillAmount = meter;
            barGhost.fillAmount = ghost;
            var c = statColour;
            if (punchT >= 0f && punchT < 0.06f) c = Color.white;   // 60 ms white flash
            statText.color = c;
            barFill.color = c;

            // punch 1 -> 1.35 in 60 ms, back over 180 ms; shake x = 8 e^(-t/0.06) sin(2pi 30 t) for 200 ms
            if (punchT >= 0f)
            {
                punchT += dt;
                float s = punchT < 0.06f ? Mathf.Lerp(1f, 1.35f, punchT / 0.06f) : Mathf.Lerp(1.35f, 1f, Ease.OutBack((punchT - 0.06f) / 0.18f));
                statText.rectTransform.localScale = Vector3.one * s;
                statGroup.anchoredPosition = new Vector2(punchT < 0.2f ? 8f * Mathf.Exp(-punchT / 0.06f) * Mathf.Sin(2f * Mathf.PI * 30f * punchT) : 0f, statGroup.anchoredPosition.y);
                if (punchT > 0.24f) { punchT = -1f; statText.rectTransform.localScale = Vector3.one; }
            }
            if (deltaT >= 0f)
            {
                deltaT += dt;
                float k = deltaT / 0.6f;
                deltaText.rectTransform.anchoredPosition = new Vector2(deltaText.rectTransform.anchoredPosition.x, 22f + 40f * Ease.OutCubic(k));
                deltaText.alpha = 1f - Ease.InQuad(k);
                if (k >= 1f) deltaT = -1f;
            }
        }
    }
}
