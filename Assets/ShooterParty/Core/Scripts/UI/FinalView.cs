using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // The session's end, over SP_Lobby's podium (LobbyStage puts the critters on it and crowns the champion):
    // the champion (ties share), the other places, and the way back. The room returns to its lobby on its own
    // when the Final timer runs out, ready flags cleared; Leave goes back to the menu.
    public class FinalView : SessionView
    {
        public TMP_Text championLabel, championName, championDetail, backLabel;
        public TMP_Text[] places = new TMP_Text[3];
        public Button leaveButton;

        public override bool ShowsIn(SessionPhase phase) => phase == SessionPhase.Final;

        protected override void Awake()
        {
            base.Awake();
            leaveButton.onClick.AddListener(PauseMenu.LeaveRoom);
        }

        protected override void OnShown()
        {
            var s = SessionManager.Instance;
            if (!s) return;
            Sfx.Music("MusicLobby");
            Sfx.Ui("Fanfare");
            var champs = s.ChampionSlots();
            var order = s.SeatsByTotal();
            var winners = order.Where(p => champs.Contains(p.slot)).ToList();
            championLabel.text = winners.Count > 1 ? "CHAMPIONS" : "CHAMPION";
            championName.text = winners.Count == 0 ? "Nobody this time" : string.Join(" & ", winners.Select(p => p.name.ToString()));
            var top = winners.FirstOrDefault();
            championDetail.text = winners.Count == 0 ? "" : $"{top.total} points · {top.wins} round win{(top.wins == 1 ? "" : "s")}";
            var rest = order.Where(p => !champs.Contains(p.slot)).ToList();
            for (int i = 0; i < places.Length; i++)
            {
                bool on = i < rest.Count;
                places[i].transform.parent.gameObject.SetActive(on);   // the pill with its line
                if (on) places[i].text = $"<alpha=#99>{winners.Count + i + 1}.<alpha=#FF>  {rest[i].name}  <alpha=#99>{rest[i].total}";
            }
        }

        protected override void Update()
        {
            base.Update();
            var s = SessionManager.Instance;
            if (!IsShown || !s || !s.IsSpawned) return;
            double left = s.PhaseEndsAt.Value - s.NetworkManager.ServerTime.Time;
            backLabel.text = left > 0 ? $"Back to the lobby in {Mathf.CeilToInt((float)left)}" : "Back to the lobby…";
        }
    }
}
