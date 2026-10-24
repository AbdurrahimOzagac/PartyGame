using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KnockbackArena.UI
{
    // Lobby (U8): HOST / JOIN by IP on the left, four player slots on the right, READY + START at the bottom.
    // View only: networking code (NGO) calls SetSlot / SetHosting and listens to the events.
    public class LobbyPanel : MonoBehaviour
    {
        public enum SlotState { Empty, Joined, Ready }

        [System.Serializable]
        public class Slot
        {
            public Image card, portrait, pill, check;
            public TMP_Text playerName, status;
        }

        public Slot[] slots;
        public Button hostButton, joinButton, readyButton, startButton;
        public TMP_InputField ipField, portField;
        public TMP_Text hostInfo, readyLabel;

        public UnityEvent onHost = new UnityEvent(), onJoin = new UnityEvent(), onReady = new UnityEvent(), onStart = new UnityEvent();

        static readonly Color Ink = new Color32(0x16, 0x1A, 0x3A, 0xFF);

        void Awake()
        {
            hostButton.onClick.AddListener(() => { Sfx.Ui("UiClick"); onHost.Invoke(); });
            joinButton.onClick.AddListener(() => { Sfx.Ui("UiClick"); onJoin.Invoke(); });
            readyButton.onClick.AddListener(() => { Sfx.Ui("UiClick"); onReady.Invoke(); });
            startButton.onClick.AddListener(() => { Sfx.Ui("UiClick"); onStart.Invoke(); });
        }

        public void SetSlot(int i, SlotState state, string playerName = "", Color colour = default, Sprite face = null)
        {
            var s = slots[i];
            bool filled = state != SlotState.Empty;
            s.portrait.enabled = filled && face;
            s.portrait.sprite = face;
            s.card.color = filled ? Color.Lerp(colour, Color.white, 0.2f) : new Color(1f, 1f, 1f, 0.08f);
            s.playerName.text = filled ? playerName : "";
            s.status.text = state == SlotState.Ready ? "READY" : state == SlotState.Joined ? "Not ready" : "EMPTY";
            s.status.color = state == SlotState.Ready ? Ink : Color.white;
            s.pill.color = state == SlotState.Ready ? new Color32(0xD4, 0xF2, 0x1C, 0xFF) : new Color(1f, 1f, 1f, state == SlotState.Joined ? 0.25f : 0.1f);
            s.check.enabled = state == SlotState.Ready;   // never colour alone: the tick says it too
        }

        public void SetHosting(string lanAddress) { hostInfo.text = string.IsNullOrEmpty(lanAddress) ? "" : "Hosting on <b>" + lanAddress + "</b>"; }

        public void SetCanStart(bool can, bool isHost) { startButton.gameObject.SetActive(isHost); startButton.interactable = can; }

        public void SetReady(bool ready) { readyLabel.text = ready ? "READY!" : "READY?"; }
    }
}
