using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // The room before (and between) sessions: the party code (or host address), one card per seat hung under its
    // critter on SP_Lobby's pedestals, the playlist panel, the leader's options and Start / everyone else's Ready.
    // It only reads replicated state (SessionManager) and sends requests; the server decides.
    public class LobbyView : SessionView
    {
        [System.Serializable]
        public class SlotCard
        {
            public RectTransform root;
            public Image portrait, colourBar, pingDot;
            public TMP_Text nameLabel, pingLabel, statusLabel;
            public GameObject leaderTag, botTag, readyMark, youTag, emptyLabel, body;
            public Button kick, colour;
        }

        public struct Controls { public bool start, ready, editPlaylist, kick; }

        public static readonly Color Good = new Color32(0x5B, 0xE3, 0x7A, 0xFF), Fair = new Color32(0xFF, 0xC2, 0x3D, 0xFF), Poor = new Color32(0xFF, 0x4F, 0x2E, 0xFF);

        public TMP_Text codeLabel, codeText, codeHint;
        public Button copyButton;
        public SlotCard[] slots = new SlotCard[4];
        public RectTransform slotArea;
        public PlaylistBuilder builder;
        public GameObject replaceRow;
        public Toggle replaceLeavers;
        public Button startButton, readyButton, leaveButton;
        public TMP_Text readyLabel, statusText;
        public Sprite[] neutralFaces = new Sprite[4], happyFaces = new Sprite[4];

        SessionManager bound;
        string copyText;

        public override bool ShowsIn(SessionPhase phase) => phase == SessionPhase.Lobby;

        // What the local seat may do. The server checks the same rules on every request.
        public static Controls ControlsFor(PlayerState me, PartyMode mode) => new Controls
        {
            start = me.leader,
            ready = !me.leader,
            editPlaylist = me.leader,
            kick = me.leader && mode != PartyMode.Practice,
        };

        public static Color PingColour(int ms) => ms < 120 ? Good : ms < 220 ? Fair : Poor;

        protected override void Awake()
        {
            base.Awake();
            startButton.onClick.AddListener(PressStart);
            readyButton.onClick.AddListener(() => { if (Me(out var me)) SessionManager.Instance.RequestReady(!me.ready); });
            leaveButton.onClick.AddListener(PauseMenu.LeaveRoom);
            copyButton.onClick.AddListener(() =>
            {
                if (string.IsNullOrEmpty(copyText)) return;
                GUIUtility.systemCopyBuffer = copyText;
                UiRoot.Toast("Copied " + copyText);
            });
            replaceLeavers.onValueChanged.AddListener(on => { if (SessionManager.Instance) SessionManager.Instance.RequestReplaceLeavers(on); });
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i;
                slots[i].kick.onClick.AddListener(() => AskKick(slot));
                slots[i].colour.onClick.AddListener(() => { if (SessionManager.Instance) SessionManager.Instance.RequestCycleColour(); });
            }
        }

        public void PressStart() { if (SessionManager.Instance) SessionManager.Instance.RequestStart(); }

        protected override void OnShown()
        {
            Sfx.Music("MusicLobby");
            if (builder.IsEditing) builder.Cancel();
            ShowCode();
        }

        static bool Me(out PlayerState me)
        {
            me = default;
            var s = SessionManager.Instance;
            return s && s.IsSpawned && s.TryGetLocalPlayer(out me);
        }

        void AskKick(int slot)
        {
            var s = SessionManager.Instance;
            if (!s || !s.TryGetPlayer(slot, out var p)) return;
            UiRoot.Instance.dialog.Ask($"Remove {p.name}?", "They leave the room.", "REMOVE", () => s.RequestKick(slot), "CANCEL", null);
        }

        void ShowCode()
        {
            var net = PartyNet.Instance;
            var s = SessionManager.Instance;
            var mode = net ? net.Mode : PartyMode.Practice;
            copyText = null;
            switch (mode)
            {
                case PartyMode.OnlineClient:
                    copyText = s ? s.RoomCode.Value.ToString() : "";
                    Code("PARTY CODE", copyText, 84f, "Share it with your friends · 3 to 4 players");
                    break;
                case PartyMode.LocalHost:
                    var ips = PartyNet.LanAddresses();
                    copyText = ips.Count > 0 ? ips[0] : null;
                    Code("HOST ADDRESS", copyText ?? "No network", 52f,
                        ips.Count > 1 ? "Also: " + string.Join(", ", ips.GetRange(1, Mathf.Min(2, ips.Count - 1))) : "Friends on the same Wi-Fi join with this");
                    break;
                case PartyMode.LocalClient:
                    Code("LOCAL ROOM", net.Address, 52f, "Playing on the same Wi-Fi");
                    break;
                default:
                    Code("PRACTICE", "You + 3 bots", 52f, "Warm up, then play with friends");
                    break;
            }
            copyButton.gameObject.SetActive(!string.IsNullOrEmpty(copyText));
        }

        void Code(string label, string text, float size, string hint)
        {
            codeLabel.text = label;
            codeText.text = text;
            codeText.fontSize = size;
            codeText.characterSpacing = label == "PARTY CODE" ? 12f : 0f;   // spaced letters read out loud more easily
            codeHint.text = hint;
        }

        protected override void Update()
        {
            base.Update();
            var s = SessionManager.Instance;
            if (s != bound)
            {
                if (bound) bound.Message -= UiRoot.Toast;
                bound = s;
                if (bound) bound.Message += UiRoot.Toast;   // refused requests ("Everyone must be ready")
            }
            if (!IsShown || !Me(out var me)) return;
            var net = PartyNet.Instance;
            var mode = net ? net.Mode : PartyMode.Practice;
            var c = ControlsFor(me, mode);
            builder.CanEdit = c.editPlaylist;
            if (mode == PartyMode.OnlineClient && s.RoomCode.Value != copyText) ShowCode();   // the code replicates with the spawn, but be sure

            for (int slot = 0; slot < slots.Length; slot++) Bind(slots[slot], slot, s, me, c);
            PlaceCards();

            replaceRow.SetActive(mode != PartyMode.Practice);
            replaceLeavers.interactable = c.editPlaylist;
            replaceLeavers.SetIsOnWithoutNotify(s.ReplaceLeavers.Value);

            startButton.gameObject.SetActive(c.start);
            readyButton.gameObject.SetActive(c.ready);
            var blocker = SessionFlow.StartBlocker(s.Seats(), s.MinPlayers.Value, s.PlaylistIds.Count);
            startButton.interactable = blocker == null;
            readyLabel.text = me.ready ? "NOT READY" : "READY!";
            statusText.text = c.start ? blocker ?? "Everyone's in. Start when you like!"
                : me.ready ? "Waiting for the leader to start" : "Press Ready when you are";
        }

        void Bind(SlotCard card, int slot, SessionManager s, PlayerState me, Controls c)
        {
            bool taken = s.TryGetPlayer(slot, out var p);
            card.body.SetActive(taken);
            card.emptyLabel.SetActive(!taken);
            if (!taken) return;
            bool mine = !p.isBot && p.clientId == me.clientId;
            int colour = Mathf.Clamp(p.colour, 0, 3);
            card.nameLabel.text = p.name.ToString();
            card.colourBar.color = CritterLook.Colours[colour];
            card.portrait.sprite = p.ready || p.leader ? happyFaces[colour] : neutralFaces[colour];
            card.leaderTag.SetActive(p.leader);
            card.botTag.SetActive(p.isBot);
            card.youTag.SetActive(mine);
            card.readyMark.SetActive(p.ready);
            card.statusLabel.text = !p.connected ? "reconnecting…" : p.leader ? "leader" : p.ready ? "ready" : "not ready";
            bool showPing = !p.isBot && p.connected && PartyNet.Instance && PartyNet.Instance.Mode != PartyMode.Practice;
            card.pingDot.gameObject.SetActive(showPing);
            card.pingLabel.gameObject.SetActive(showPing);
            if (showPing)
            {
                card.pingDot.color = PingColour(p.rttMs);
                card.pingLabel.text = p.rttMs + " ms";
            }
            card.kick.gameObject.SetActive(c.kick && !mine && !p.isBot);
            card.colour.gameObject.SetActive(mine);
        }

        // Hang each card under its critter: SP_Lobby's camera decides where the pedestals land on screen.
        void PlaceCards()
        {
            var stage = LobbyStage.Instance;
            var cam = Camera.main;
            if (!stage || !cam) return;
            var canvas = GetComponentInParent<Canvas>();
            var uiCam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                var screen = cam.WorldToScreenPoint(stage.TagPoint(slot));
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(slotArea, screen, uiCam, out var local))
                    slots[slot].root.anchoredPosition = local;
            }
        }
    }
}
