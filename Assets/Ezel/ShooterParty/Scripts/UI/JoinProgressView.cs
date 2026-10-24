using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // Step-by-step progress for joining, with Cancel; a failure shows its reason and goes back to the dialog.
    // Also the card for an automatic rejoin after a dropped connection (Leave goes back to the menu).
    public class JoinProgressView : UiScreen
    {
        public TMP_Text step, detail;
        public Button cancel;
        public TMP_Text cancelLabel;
        bool failed, rejoining;
        Coroutine local;

        protected override void Awake()
        {
            base.Awake();
            cancel.onClick.AddListener(Cancel);
        }

        public void WatchOnline()
        {
            var flow = JoinFlow.Ensure();
            flow.Changed -= OnStep;
            flow.Changed += OnStep;
            failed = false; rejoining = false;
            Show();
            OnStep(flow.State);
        }

        public void WatchRejoin()
        {
            WatchOnline();
            rejoining = true;
            OnStep(JoinFlow.Instance.State);
        }

        protected override void Update()
        {
            base.Update();
            var flow = JoinFlow.Instance;
            if (!IsShown || !rejoining || failed || !flow || flow.State != JoinFlow.Step.Rejoining) return;
            float wait = flow.NextAttemptAt - Time.unscaledTime;
            detail.text = wait > 0.5f ? $"Trying again in {Mathf.CeilToInt(wait)} s" + (flow.RejoinAttempt > 0 ? $"  ·  attempt {flow.RejoinAttempt + 1}" : "")
                                      : "Rejoining the room…";
        }

        public void WatchLocal(string address)
        {
            failed = false;
            Show();
            Set("Connecting…", "to " + address, "CANCEL");
            if (local != null) StopCoroutine(local);
            local = StartCoroutine(WaitLocal());
        }

        IEnumerator WaitLocal()
        {
            var net = PartyNet.Instance;
            string reason = null;
            void Lost(string r) => reason = r;
            net.Disconnected += Lost;
            float until = Time.unscaledTime + 15f;
            while (reason == null && Time.unscaledTime < until)
            {
                var s = SessionManager.Instance;
                if (s && s.IsSpawned && s.TryGetLocalPlayer(out _)) { net.Disconnected -= Lost; Hide(); yield break; }
                yield return null;
            }
            net.Disconnected -= Lost;
            net.Leave();
            ShowFailure(reason ?? "Couldn't reach that address — are you on the same Wi-Fi?");
        }

        void OnStep(JoinFlow.Step s)
        {
            if (!IsShown) return;
            var flow = JoinFlow.Instance;
            switch (s)
            {
                case JoinFlow.Step.Contacting: Set("Contacting server…", "partyshooter.ezellm.com", "CANCEL"); break;
                case JoinFlow.Step.Joining: Set("Joining room " + flow.Code + "…", "Getting the room ready", "CANCEL"); break;
                case JoinFlow.Step.InLobby: Hide(); break;
                case JoinFlow.Step.Rejoining: Set("Connection lost", "Rejoining the room…", "LEAVE"); break;
                case JoinFlow.Step.Failed: ShowFailure(flow.Error); break;
            }
        }

        void ShowFailure(string message)
        {
            failed = true;
            Set(rejoining ? JoinFlow.CouldNotRejoin : "Couldn't join", rejoining ? "The room may have ended." : message, "BACK");
            Sfx.Ui("UiBack");
        }

        void Set(string a, string b, string button)
        {
            step.text = a; detail.text = b; cancelLabel.text = button;
        }

        void Cancel()
        {
            Sfx.Ui("UiBack");
            if (local != null) { StopCoroutine(local); local = null; }
            if (!failed) { if (JoinFlow.Instance) JoinFlow.Instance.Cancel(); else PartyNet.Instance?.Leave(); }
            var message = failed ? detail.text : null;
            Hide();
            if (rejoining) { rejoining = false; return; }   // back to the menu, not the join dialog
            UiRoot.Instance.nameDialog.Reopen(message);
        }
    }
}
