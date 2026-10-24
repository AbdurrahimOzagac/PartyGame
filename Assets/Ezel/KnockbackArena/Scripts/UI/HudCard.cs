using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnockbackArena.UI
{
    // One player's card in the top strip (U2): portrait, name, MASS (Smash-style % + bar with a drain trail),
    // score, crown. Mass is the read the whole mode hangs on, so a hit punches, shakes and floats the loss.
    // Colours show danger, not player: white >= 70 %, amber 40-70 %, pink below 40 % (pulsing). UI brief A1.
    public class HudCard : MonoBehaviour
    {
        public enum Face { Neutral, Happy, Hurt, Sad }

        public Image portrait, portraitBg, band, barFill, barGhost, crown, outline;
        public TMP_Text nameText, massText, scoreText, deltaText;
        public RectTransform massGroup;
        public CanvasGroup group;

        public static readonly Color Amber = new Color(1f, 0.761f, 0.2f), Pink = new Color(1f, 0.239f, 0.545f);

        CritterMotor motor;
        Sprite[] faces;
        float shown = 1f, target = 1f, ghost = 1f, ghostHold, punchT = -1f, deltaT = -1f, faceUntil, rollFrom;
        Face face, restFace = Face.Neutral;
        bool isOut;

        public CritterMotor Motor => motor;

        public void Bind(CritterMotor m, string playerName, Color colour, Sprite[] portraits, bool local)
        {
            motor = m; faces = portraits;
            nameText.text = playerName;
            band.color = colour;
            portraitBg.color = Color.Lerp(colour, Color.white, 0.25f);
            outline.enabled = local;                         // the local card gets a white stroke and a little scale
            transform.localScale = Vector3.one * (local ? 1.06f : 1f);
            shown = target = ghost = m ? m.MassFraction : 1f;
            isOut = false; restFace = Face.Neutral;
            if (group) group.alpha = 1f;
            SetFace(Face.Neutral);
            deltaText.alpha = 0f;
            Refresh();
        }

        public void SetScore(int s) => scoreText.text = s.ToString();
        public void SetCrown(bool on) => crown.enabled = on;

        public void SetOut()
        {
            isOut = true; restFace = Face.Sad; SetFace(Face.Sad);
            if (group) group.alpha = 0.55f;
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
            if (motor && !isOut)
            {
                float m = motor.MassFraction;
                if (m < target - 0.001f) OnLoss(target - m, now);
                target = m;
            }
            // number rolls to the new value over 250 ms; the bar snaps, the white ghost holds 300 ms then drains
            shown = Mathf.MoveTowards(shown, target, Mathf.Max(0.01f, Mathf.Abs(rollFrom - target)) * dt / 0.25f);
            if (now > ghostHold) ghost = Mathf.MoveTowards(ghost, target, dt / 0.25f);
            if (face == Face.Hurt && now > faceUntil) SetFace(restFace);
            Refresh();

            // punch 1 -> 1.35 in 60 ms, back over 180 ms; shake x = 8 e^(-t/0.06) sin(2pi 30 t) for 200 ms
            if (punchT >= 0f)
            {
                punchT += dt;
                float s = punchT < 0.06f ? Mathf.Lerp(1f, 1.35f, punchT / 0.06f) : Mathf.Lerp(1.35f, 1f, Ease.OutBack((punchT - 0.06f) / 0.18f));
                massText.rectTransform.localScale = Vector3.one * s;
                massGroup.anchoredPosition = new Vector2(punchT < 0.2f ? 8f * Mathf.Exp(-punchT / 0.06f) * Mathf.Sin(2f * Mathf.PI * 30f * punchT) : 0f, massGroup.anchoredPosition.y);
                if (punchT > 0.24f) { punchT = -1f; massText.rectTransform.localScale = Vector3.one; }
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

        void OnLoss(float amount, float now)
        {
            rollFrom = shown;
            ghost = Mathf.Max(ghost, target);
            ghostHold = now + 0.3f;
            punchT = 0f; deltaT = 0f;
            deltaText.text = "-" + Mathf.Max(1, Mathf.RoundToInt(amount * 100f));
            SetFace(Face.Hurt); faceUntil = now + 0.6f;
        }

        void Refresh()
        {
            float pct = Mathf.Round(shown * 100f);
            massText.text = pct.ToString("0") + "%";
            barFill.fillAmount = target;
            barGhost.fillAmount = ghost;
            Color c = target >= 0.7f ? Color.white : target >= 0.4f ? Amber : Pink;
            if (target < 0.4f) c = Color.Lerp(c, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 3f)));
            if (punchT >= 0f && punchT < 0.06f) c = Color.white;   // 60 ms white flash
            massText.color = c;
            barFill.color = c;
        }
    }
}
