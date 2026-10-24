using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnockbackArena.UI
{
    // Screen HUD for Knockback Arena (U2, U4-U7 + dash/jump): built by Knockback Arena/Rebuild into
    // UI/HUD_KnockbackArena.prefab. Game code feeds it; it never decides rules. Timings from the UI brief.
    public class KnockbackHud : MonoBehaviour
    {
        [System.Serializable]
        public class ResultRow
        {
            public RectTransform root;
            public Image badge, portrait, crown;
            public TMP_Text place, playerName, plus, total;
            public GameObject comeback;
        }

        public struct Player
        {
            public CritterMotor motor; public string name; public Color colour; public Sprite[] faces;
        }

        public struct Result
        {
            public int player, place, plus, total; public bool comeback;
        }

        [Header("Cards")] public HudCard[] cards;
        [Header("Aim")] public RectTransform crosshair; public Image crosshairImage, cooldownRing, cooldownTrack;
        [Header("Dash + jump")] public Image[] dashPips, dashFills; public CanvasGroup jumpSlot; public Image jumpIcon; public TMP_Text jumpHint;
        [Header("Countdown")] public TMP_Text countdown;
        [Header("OUT")] public RectTransform banner; public Image bannerBg; public TMP_Text bannerTitle, bannerPlace;
        public RectTransform toast; public Image toastPortrait; public TMP_Text toastText;
        [Header("Results")] public RectTransform results; public ResultRow[] rows; public TMP_Text resultsHint;

        public static readonly Color Ink = new Color32(0x16, 0x1A, 0x3A, 0xFF);
        static readonly string[] Places = { "1st", "2nd", "3rd", "4th" };

        readonly List<Player> players = new List<Player>();
        CritterMotor localMotor; CritterSpit localSpit;
        int lastDash = -1; float readyPopT = -1f, hitT = -1f, cooldownStart; bool wasReady = true;
        Color hitColour;

        void Awake()
        {
            countdown.alpha = 0f;
            banner.gameObject.SetActive(false); toast.gameObject.SetActive(false); results.gameObject.SetActive(false);
        }

        public void Bind(IList<Player> list, int localIndex)
        {
            players.Clear(); players.AddRange(list);
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].gameObject.SetActive(i < list.Count);
                if (i < list.Count) cards[i].Bind(list[i].motor, list[i].name, list[i].colour, list[i].faces, i == localIndex);
            }
            if (localIndex >= 0 && localIndex < list.Count)
            {
                localMotor = list[localIndex].motor;
                localSpit = localMotor ? localMotor.GetComponent<CritterSpit>() : null;
            }
        }

        public void SetScores(IList<int> totals, int leader)
        {
            for (int i = 0; i < players.Count && i < cards.Length; i++)
            {
                cards[i].SetScore(totals[i]);
                cards[i].SetCrown(i == leader);
            }
        }

        public void HitConfirm(Color target) { hitT = 0f; hitColour = target; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            // cooldown ring: only while cooling, radial from the top, then a 120 ms pop when ready
            if (localSpit)
            {
                bool ready = localSpit.Ready;
                if (!ready && wasReady) cooldownStart = Time.time;
                if (ready && !wasReady) readyPopT = 0f;
                wasReady = ready;
                float k = ready ? 1f : Mathf.Clamp01((Time.time - cooldownStart) / Mathf.Max(0.01f, localSpit.cooldown));
                cooldownRing.fillAmount = k;
                bool show = !ready || readyPopT >= 0f;
                cooldownRing.enabled = cooldownTrack.enabled = show;
                float s = 1f;
                if (readyPopT >= 0f) { readyPopT += dt; s = readyPopT < 0.06f ? Mathf.Lerp(1f, 1.2f, readyPopT / 0.06f) : Mathf.Lerp(1.2f, 1f, (readyPopT - 0.06f) / 0.06f); if (readyPopT > 0.12f) readyPopT = -1f; }
                cooldownRing.rectTransform.localScale = Vector3.one * s;
            }
            // hit confirm: crosshair flashes the target's colour and spreads for 100 ms
            if (hitT >= 0f)
            {
                hitT += dt;
                float k = Mathf.Clamp01(hitT / 0.1f);
                crosshair.localScale = Vector3.one * Mathf.Lerp(1.33f, 1f, k);
                crosshairImage.color = Color.Lerp(hitColour, Color.white, k);
                if (k >= 1f) hitT = -1f;
            }
            if (localMotor)
            {
                // dash pips: full = white, spent = dim ink, the recharging one fills radially in the player colour
                int c = Mathf.Max(0, localMotor.dashCharges);
                for (int i = 0; i < dashPips.Length; i++)
                {
                    bool full = i < c, charging = i == c && c < localMotor.maxDashCharges;
                    dashPips[i].color = full ? Color.white : new Color(Ink.r, Ink.g, Ink.b, 0.4f);
                    dashFills[i].enabled = charging;
                    if (charging) dashFills[i].fillAmount = localMotor.DashRechargeProgress;
                }
                if (lastDash >= 0 && c > lastDash && c <= dashPips.Length) StartCoroutine(Punch(dashPips[c - 1].rectTransform, 1.3f, 0.14f));
                if (lastDash >= 0 && c > lastDash) Sfx.Ui("DashReady");
                lastDash = c;
                // jump slot: dashed/dim when empty, bobs when a charge is held
                bool held = localMotor.jumpCharges != 0;
                jumpSlot.alpha = held ? 1f : 0.35f;
                jumpIcon.enabled = held; jumpHint.enabled = held;
                jumpIcon.rectTransform.anchoredPosition = new Vector2(0f, held ? 3f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.4f) : 0f);
            }
        }

        IEnumerator Punch(RectTransform rt, float peak, float time)
        {
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                rt.localScale = Vector3.one * Mathf.Lerp(peak, 1f, Ease.OutQuad(t / time));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ countdown (U5)
        public IEnumerator Countdown(System.Action onGo)
        {
            countdown.color = Color.white;
            for (int n = 3; n >= 1; n--)
            {
                countdown.text = n.ToString();
                Sfx.Ui("Beep");
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
                {
                    float s, a, r;
                    if (t < 0.22f) { float k = t / 0.22f; s = Mathf.Lerp(2.2f, 1f, Ease.OutBack(k)); a = k; r = Mathf.Lerp(-8f, 0f, k); }
                    else if (t < 0.8f) { s = 1f; a = 1f; r = 0f; }
                    else { float k = Ease.InQuad((t - 0.8f) / 0.2f); s = Mathf.Lerp(1f, 0.6f, k); a = 1f - k; r = 0f; }
                    countdown.rectTransform.localScale = Vector3.one * s;
                    countdown.rectTransform.localRotation = Quaternion.Euler(0f, 0f, r);
                    countdown.alpha = a;
                    yield return null;
                }
            }
            countdown.text = "GO!";
            countdown.color = new Color32(0xD4, 0xF2, 0x1C, 0xFF);
            Sfx.Ui("Go");
            onGo?.Invoke();
            for (float t = 0f; t < 1.05f; t += Time.unscaledDeltaTime)
            {
                float s, a = 1f;
                if (t < 0.35f) s = t < 0.2f ? Mathf.Lerp(0f, 1.25f, Ease.OutQuad(t / 0.2f)) * 1.15f : Mathf.Lerp(1.25f, 1f, (t - 0.2f) / 0.15f) * 1.15f;
                else if (t < 0.8f) s = 1.15f;
                else { float k = (t - 0.8f) / 0.25f; s = Mathf.Lerp(1.15f, 2f, k); a = 1f - k; }
                countdown.rectTransform.localScale = Vector3.one * s;
                countdown.rectTransform.localRotation = Quaternion.identity;
                countdown.alpha = a;
                yield return null;
            }
            countdown.alpha = 0f;
        }

        // ------------------------------------------------------------------ OUT (U6)
        public void ShowOut(int player, int place, int remaining, bool isLocal)
        {
            if (player >= 0 && player < cards.Length) cards[player].SetOut();
            StartCoroutine(isLocal ? Banner(player, place) : Toast(player, remaining));
        }

        IEnumerator Banner(int player, int place)
        {
            var p = players[player];
            bannerBg.color = p.colour;
            var light = p.colour.r * 0.299f + p.colour.g * 0.587f + p.colour.b * 0.114f > 0.6f;   // ink text on Chartreuse/Teal
            bannerTitle.color = bannerPlace.color = light ? Ink : Color.white;
            bannerTitle.text = "OUT!";
            bannerPlace.text = Places[Mathf.Clamp(place - 1, 0, 3)];
            banner.gameObject.SetActive(true);
            Sfx.Ui("KO");
            float w = 2400f;
            for (float t = 0f; t < 0.28f; t += Time.unscaledDeltaTime) { banner.anchoredPosition = new Vector2(Mathf.Lerp(-w, 0f, Ease.OutBack(t / 0.28f)), 0f); yield return null; }
            banner.anchoredPosition = Vector2.zero;
            yield return new WaitForSecondsRealtime(1.4f);
            for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime) { banner.anchoredPosition = new Vector2(Mathf.Lerp(0f, w, Ease.InQuad(t / 0.22f)), 0f); yield return null; }
            banner.gameObject.SetActive(false);
        }

        IEnumerator Toast(int player, int remaining)
        {
            var p = players[player];
            toastPortrait.sprite = p.faces != null && p.faces.Length > 3 ? p.faces[3] : null;
            toastText.text = $"<b>{p.name}</b> is OUT!  <alpha=#99>· {remaining} left";
            toast.gameObject.SetActive(true);
            var cg = toast.GetComponent<CanvasGroup>();
            Sfx.Ui("KO");
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime) { toast.anchoredPosition = new Vector2(0f, Mathf.Lerp(-130f, -190f, Ease.OutBack(t / 0.2f))); cg.alpha = t / 0.2f; yield return null; }
            cg.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.6f);
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime) { cg.alpha = 1f - t / 0.2f; yield return null; }
            toast.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ results (U7)
        public void ShowResults(IList<Result> order)   // order[0] = 1st
        {
            SetPlayControls(false);
            results.gameObject.SetActive(true);
            StartCoroutine(RevealResults(order));
        }

        public void HideResults() { results.gameObject.SetActive(false); SetPlayControls(true); }

        void SetPlayControls(bool on)
        {
            crosshair.gameObject.SetActive(on);
            jumpSlot.gameObject.SetActive(on);
            dashPips[0].transform.parent.gameObject.SetActive(on);
        }

        IEnumerator RevealResults(IList<Result> order)
        {
            foreach (var r in rows) r.root.gameObject.SetActive(false);
            resultsHint.alpha = 0f;
            for (int i = order.Count - 1; i >= 0; i--)   // 4th -> 1st, 150 ms apart
            {
                var res = order[i]; var row = rows[i]; var p = players[res.player];
                row.place.text = Places[Mathf.Clamp(res.place - 1, 0, 3)];
                row.badge.color = i == 0 ? new Color32(0xFF, 0xD2, 0x3F, 0xFF) : Color.Lerp(p.colour, Color.white, 0.15f);
                row.portrait.sprite = p.faces != null && p.faces.Length > 1 ? p.faces[i == 0 ? 1 : i == order.Count - 1 ? 3 : 0] : null;
                row.playerName.text = p.name;
                row.plus.text = "+" + res.plus;
                row.comeback.SetActive(res.comeback);
                row.crown.enabled = false;
                row.root.gameObject.SetActive(true);
                StartCoroutine(SlideIn(row.root));
                StartCoroutine(CountUp(row.total, res.total - res.plus - (res.comeback ? 1 : 0), res.total));
                Sfx.Ui("UiClick");
                yield return new WaitForSecondsRealtime(0.15f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            rows[0].crown.enabled = true;            // the crown lands last, with the fanfare
            StartCoroutine(Punch(rows[0].crown.rectTransform, 1.6f, 0.3f));
            Sfx.Ui("Fanfare");
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { resultsHint.alpha = t / 0.4f; yield return null; }
        }

        IEnumerator SlideIn(RectTransform rt)
        {
            var end = rt.anchoredPosition; end.x = 0f;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { rt.anchoredPosition = new Vector2(Mathf.Lerp(700f, 0f, Ease.OutBack(t / 0.25f)), end.y); yield return null; }
            rt.anchoredPosition = end;
        }

        static IEnumerator CountUp(TMP_Text text, int from, int to)
        {
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { text.text = Mathf.RoundToInt(Mathf.Lerp(from, to, Ease.OutQuad(t / 0.6f))).ToString(); yield return null; }
            text.text = to.ToString();
        }
    }
}
