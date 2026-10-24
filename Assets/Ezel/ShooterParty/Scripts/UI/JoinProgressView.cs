using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // Step-by-step progress for joining, with Cancel; a failure shows its reason and goes back to the dialog.
    public class JoinProgressView : UiScreen
    {
        public TMP_Text step, detail;
        public Button cancel;
        public TMP_Text cancelLabel;
        bool failed;
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
            failed = false;
            Show();
            OnStep(flow.State);
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
                case JoinFlow.Step.Failed: ShowFailure(flow.Error); break;
            }
        }

        void ShowFailure(string message)
        {
            failed = true;
            Set("Couldn't join", message, "BACK");
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
            UiRoot.Instance.nameDialog.Reopen(message);
        }
    }
}
