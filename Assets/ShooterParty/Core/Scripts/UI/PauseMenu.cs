using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // Esc in a room. The game keeps running underneath (it's online).
    public class PauseMenu : UiScreen
    {
        public Button resume, settingsButton, leave;

        protected override void Awake()
        {
            base.Awake();
            resume.onClick.AddListener(() => { Sfx.Ui("UiClick"); Hide(); });
            settingsButton.onClick.AddListener(() => { Sfx.Ui("UiClick"); UiRoot.Instance.settings.Show(); });
            leave.onClick.AddListener(() =>
            {
                Sfx.Ui("UiClick");
                UiRoot.Instance.dialog.Ask("Leave the room?", "Your points stay on the board, but you can't come back to this session.",
                    "LEAVE", LeaveRoom, "STAY", null);
            });
        }

        public static void LeaveRoom()
        {
            var s = SessionManager.Instance;
            if (s && s.IsSpawned) s.RequestLeave();
            if (JoinFlow.Instance) JoinFlow.Instance.Cancel();
            else if (PartyNet.Instance) PartyNet.Instance.Leave();
            if (UiRoot.Instance) UiRoot.Instance.pause.Hide();
            UiRoot.Toast("You left the room");
        }
    }
}
