using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // The in-round HUD (countdown and play), the same for every mini-game: a card per seat, timer and game name,
    // crosshair + spit cooldown, dash pips, jump slot, 3-2-1-GO, OUT! banner and toasts, the spectate label.
    // It only reads SessionManager, the round's MiniGameController and the NetworkCritters; it never decides rules.
    // A mini-game adds its own widget under the cards (MiniGameDefinition.hudWidget, e.g. Knockback's mass line).
    public class PartyHud : SessionView
    {
        [Header("Cards")] public HudCard[] cards; public RectTransform widgetSlot;
        [Header("Top")] public TMP_Text timer, gameName;
        [Header("Aim")] public RectTransform crosshair; public Image crosshairImage, cooldownRing, cooldownTrack;
        [Header("Dash + jump")] public Image[] dashPips, dashFills; public CanvasGroup jumpSlot; public Image jumpIcon; public TMP_Text jumpHint;
        [Header("Countdown")] public TMP_Text countdown;
        [Header("OUT")] public RectTransform banner; public Image bannerBg; public TMP_Text bannerTitle, bannerPlace;
        public RectTransform toast; public Image toastPortrait; public TMP_Text toastText;
        public TMP_Text spectateLabel;
        public Sprite[] faces = new Sprite[16];   // colour * 4 + (int)HudCard.Face

        public static readonly Color Ink = new Color32(0x16, 0x1A, 0x3A, 0xFF);
        static readonly Color Go = new Color32(0xD4, 0xF2, 0x1C, 0xFF);
        static readonly string[] Places = { "1st", "2nd", "3rd", "4th" };

        SessionManager session;
        ShotSystem boundShots;
        NetworkCritter local;
        CritterSpit localSpit;
        readonly HashSet<NetworkCritter> outSeen = new HashSet<NetworkCritter>();
        readonly string[] bound = new string[4];
        readonly List<PlayerState> seats = new List<PlayerState>(4);
        string widgetFor;
        GameObject widget;
        SessionPhase lastPhase;
        int lastDash = -1, shownCount;
        float readyPopT = -1f, hitT = -1f, cooldownStart, countT = -1f, goT = -1f, countdownTotal;
        bool wasReady = true;
        Color hitColour;

        public override bool ShowsIn(SessionPhase p) => p == SessionPhase.Countdown || p == SessionPhase.Playing;

        protected override void Awake()
        {
            base.Awake();
            countdown.alpha = 0f;
            banner.gameObject.SetActive(false);
            toast.gameObject.SetActive(false);
            spectateLabel.transform.parent.gameObject.SetActive(false);
            foreach (var c in cards) c.gameObject.SetActive(false);
        }

        protected override void OnShown() => BindSession(SessionManager.Instance);

        public void BindSession(SessionManager s)
        {
            session = s;
            local = null; localSpit = null;
            outSeen.Clear();
            for (int i = 0; i < bound.Length; i++) bound[i] = null;
            lastDash = -1;
        }

        public HudCard CardFor(int slot)
        {
            foreach (var c in cards) if (c.gameObject.activeSelf && c.Slot == slot) return c;
            return null;
        }

        public Sprite[] FacesFor(int colour)
        {
            var f = new Sprite[4];
            for (int i = 0; i < 4; i++) f[i] = faces[Mathf.Clamp(colour, 0, 3) * 4 + i];
            return f;
        }

        // Card i shows this seat; the session binding calls it, and tools and tests can too.
        public void BindSeat(int card, int slot, string playerName, int colour, bool isLocal)
        {
            cards[card].gameObject.SetActive(true);
            cards[card].Bind(slot, playerName, CritterLook.Colours[Mathf.Clamp(colour, 0, 3)], FacesFor(colour), isLocal);
        }

        public void HitConfirm(Color target) { hitT = 0f; hitColour = target; }

        protected override void Update()
        {
            base.Update();
            float dt = Time.unscaledDeltaTime;
            var shots = ShotSystem.Instance;
            if (shots != boundShots)
            {
                if (boundShots) boundShots.HitConfirmed -= OnHitConfirmed;
                boundShots = shots;
                if (shots) shots.HitConfirmed += OnHitConfirmed;
            }
            UpdateWidget();
            if (session && session.IsSpawned) UpdateSession();
            UpdateAim(dt);
            UpdateCountdown(dt);
        }

        // ------------------------------------------------------------------ seats, timer, OUT
        void UpdateSession()
        {
            var phase = session.Phase.Value;
            seats.Clear();
            for (int i = 0; i < session.Players.Count; i++) { var p = session.Players[i]; if (p.isBot || p.connected || p.total > 0) seats.Add(p); }
            seats.Sort((a, b) => a.slot.CompareTo(b.slot));
            ulong me = session.NetworkManager.LocalClientId;
            bool practice = PartyNet.Instance && PartyNet.Instance.Mode == PartyMode.Practice;
            for (int i = 0; i < cards.Length; i++)
            {
                if (i >= seats.Count) { cards[i].gameObject.SetActive(false); bound[i] = null; continue; }
                var p = seats[i];
                bool mine = !p.isBot && p.clientId == me;
                var key = $"{p.slot}|{p.name}|{p.colour}|{mine}";
                if (bound[i] != key) { BindSeat(i, p.slot, p.name.ToString(), p.colour, mine); bound[i] = key; }
                var card = cards[i];
                card.SetScore(p.roundScore);
                card.SetCrown(p.slot == session.CrownSlot.Value);
                card.SetConnection(p.isBot || practice || !p.connected ? -1 : p.rttMs);
            }

            var game = session.CurrentGame;
            gameName.text = game ? game.displayName.ToUpperInvariant() : "";
            var ctl = MiniGameController.Current;
            if (phase == SessionPhase.Playing && ctl && ctl.IsSpawned && ctl.EndsAt.Value > 0)
            {
                double left = System.Math.Max(0, ctl.EndsAt.Value - session.NetworkManager.ServerTime.Time);
                int secs = Mathf.CeilToInt((float)left);
                timer.text = $"{secs / 60}:{secs % 60:00}";
                timer.color = secs <= 10 ? Color.Lerp(HudCard.Pink, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : Color.white;
            }
            else timer.text = phase == SessionPhase.Countdown && game ? $"{(int)game.durationSeconds / 60}:{(int)game.durationSeconds % 60:00}" : "";

            if (!local)
            {
                foreach (var c in NetworkCritter.All)
                    if (c && !c.IsBot.Value && c.HumanClientId.Value == me) { local = c; localSpit = c.GetComponent<CritterSpit>(); break; }
            }
            int alive = 0;
            foreach (var c in NetworkCritter.All) if (c && !c.Eliminated.Value) alive++;
            foreach (var c in NetworkCritter.All)
            {
                if (!c) continue;
                var card = CardFor(c.Slot.Value);
                if (card) card.SetOut(c.Eliminated.Value);
                if (!c.Eliminated.Value || !outSeen.Add(c)) continue;
                if (phase != SessionPhase.Playing) continue;
                if (c == local) StartCoroutine(Banner(c.ColourIndex.Value, alive + 1));
                else if (session.TryGetPlayer(c.Slot.Value, out var p)) StartCoroutine(Toast(p.name.ToString(), p.colour, alive));
            }
            bool spectating = local && local.Eliminated.Value;
            spectateLabel.transform.parent.gameObject.SetActive(spectating);   // the pill with its label
            if (spectating)
            {
                var cam = OverShoulderCamera.Main;
                var watched = cam && cam.target ? cam.target.GetComponent<NetworkCritter>() : null;
                spectateLabel.text = watched && watched != local && session.TryGetPlayer(watched.Slot.Value, out var w)
                    ? $"YOU'RE OUT  <alpha=#AA>· watching {w.name}" : "YOU'RE OUT";
            }
            if (lastPhase == SessionPhase.Countdown && phase == SessionPhase.Playing) { goT = 0f; Sfx.Ui("Go"); }
            if (phase == SessionPhase.Countdown)
            {
                double left = session.PhaseEndsAt.Value - session.NetworkManager.ServerTime.Time;
                if (lastPhase != SessionPhase.Countdown) { countdownTotal = Mathf.Max(0.1f, (float)left); shownCount = 0; }
                int n = Mathf.Clamp(Mathf.CeilToInt(3f * (float)left / countdownTotal), 1, 3);
                if (n != shownCount) { shownCount = n; countT = 0f; Sfx.Ui("Beep"); }
            }
            lastPhase = phase;
        }

        void OnHitConfirmed(NetworkCritter victim) => HitConfirm(CritterLook.Colours[Mathf.Clamp(victim.ColourIndex.Value, 0, 3)]);

        void UpdateWidget()
        {
            var game = session && session.IsSpawned ? session.CurrentGame : null;
            var id = game ? game.id : null;
            if (id == widgetFor) return;
            widgetFor = id;
            if (widget) Destroy(widget);
            widget = game && game.hudWidget ? Instantiate(game.hudWidget, widgetSlot) : null;
        }

        // ------------------------------------------------------------------ aim, dash, jump (the local critter)
        void UpdateAim(float dt)
        {
            if (localSpit)
            {
                // cooldown ring: only while cooling, radial from the top, then a 120 ms pop when ready
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
            bool playing = local && !local.Eliminated.Value;
            crosshair.gameObject.SetActive(playing);
            jumpSlot.gameObject.SetActive(playing);
            dashPips[0].transform.parent.gameObject.SetActive(playing);
            var motor = local ? local.Motor : null;
            if (!playing || !motor) return;
            // dash pips: full = white, spent = dim ink, the recharging one fills radially
            int c = Mathf.Max(0, motor.dashCharges);
            for (int i = 0; i < dashPips.Length; i++)
            {
                bool full = i < c, charging = i == c && c < motor.maxDashCharges;
                dashPips[i].color = full ? Color.white : new Color(Ink.r, Ink.g, Ink.b, 0.4f);
                dashFills[i].enabled = charging;
                if (charging) dashFills[i].fillAmount = motor.DashRechargeProgress;
            }
            if (lastDash >= 0 && c > lastDash && c <= dashPips.Length) StartCoroutine(Punch(dashPips[c - 1].rectTransform, 1.3f, 0.14f));
            if (lastDash >= 0 && c > lastDash) Sfx.Ui("DashReady");
            lastDash = c;
            // jump slot: dim when empty, bobs when a charge is held
            bool held = motor.jumpCharges != 0;
            jumpSlot.alpha = held ? 1f : 0.35f;
            jumpIcon.enabled = held; jumpHint.enabled = held;
            jumpIcon.rectTransform.anchoredPosition = new Vector2(0f, held ? 3f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.4f) : 0f);
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

        // ------------------------------------------------------------------ 3-2-1-GO (U5), paced by the server's countdown
        void UpdateCountdown(float dt)
        {
            if (goT >= 0f)
            {
                goT += dt;
                float t = goT, s, a = 1f;
                if (t < 0.35f) s = t < 0.2f ? Mathf.Lerp(0f, 1.25f, Ease.OutQuad(t / 0.2f)) * 1.15f : Mathf.Lerp(1.25f, 1f, (t - 0.2f) / 0.15f) * 1.15f;
                else if (t < 0.8f) s = 1.15f;
                else { float k = Mathf.Clamp01((t - 0.8f) / 0.25f); s = Mathf.Lerp(1.15f, 2f, k); a = 1f - k; }
                countdown.text = "GO!";
                countdown.color = Go;
                countdown.rectTransform.localScale = Vector3.one * s;
                countdown.rectTransform.localRotation = Quaternion.identity;
                countdown.alpha = a;
                if (t > 1.05f) { goT = -1f; countdown.alpha = 0f; }
                return;
            }
            if (countT < 0f) return;
            countT += dt;
            float step = Mathf.Max(0.1f, countdownTotal / 3f), u = countT / step, sc, al, r;
            if (u < 0.22f) { float k = u / 0.22f; sc = Mathf.Lerp(2.2f, 1f, Ease.OutBack(k)); al = k; r = Mathf.Lerp(-8f, 0f, k); }
            else if (u < 0.8f) { sc = 1f; al = 1f; r = 0f; }
            else { float k = Ease.InQuad(Mathf.Clamp01((u - 0.8f) / 0.2f)); sc = Mathf.Lerp(1f, 0.6f, k); al = 1f - k; r = 0f; }
            countdown.text = shownCount.ToString();
            countdown.color = Color.white;
            countdown.rectTransform.localScale = Vector3.one * sc;
            countdown.rectTransform.localRotation = Quaternion.Euler(0f, 0f, r);
            countdown.alpha = al;
            if (u >= 1f) countT = -1f;
        }

        // ------------------------------------------------------------------ OUT (U6)
        IEnumerator Banner(int colour, int place)
        {
            var c = CritterLook.Colours[Mathf.Clamp(colour, 0, 3)];
            bannerBg.color = c;
            var light = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f > 0.6f;   // ink text on Chartreuse/Teal
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

        IEnumerator Toast(string playerName, int colour, int remaining)
        {
            toastPortrait.sprite = faces[Mathf.Clamp(colour, 0, 3) * 4 + (int)HudCard.Face.Sad];
            toastText.text = $"<b>{playerName}</b> is OUT!  <alpha=#99>· {remaining} left";
            toast.gameObject.SetActive(true);
            var cg = toast.GetComponent<CanvasGroup>();
            Sfx.Ui("KO");
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime) { toast.anchoredPosition = new Vector2(0f, Mathf.Lerp(-130f, -190f, Ease.OutBack(t / 0.2f))); cg.alpha = t / 0.2f; yield return null; }
            cg.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.6f);
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime) { cg.alpha = 1f - t / 0.2f; yield return null; }
            toast.gameObject.SetActive(false);
        }
    }
}
