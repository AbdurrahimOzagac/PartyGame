using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // "Who are you, and where to?" — one dialog for every way in. Fields keep their text when a join fails.
    public class NameCodeDialog : UiScreen
    {
        public enum Mode { Practice, Create, Join, LocalHost, LocalJoin }

        public TMP_Text title, hint, error;
        public TMP_InputField nameField, codeField, addressField;
        public GameObject codeRow, addressRow, botsRow;
        public Toggle bots;
        public Button confirm, cancel;
        public TMP_Text confirmLabel;
        public Mode Current { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            confirm.onClick.AddListener(Confirm);
            cancel.onClick.AddListener(() => { Sfx.Ui("UiBack"); Hide(); });
            nameField.characterLimit = NameRules.MaxLength;
            codeField.characterLimit = 7;
            codeField.onValueChanged.AddListener(v => { var up = v.ToUpperInvariant(); if (up != v) codeField.SetTextWithoutNotify(up); });
            nameField.onSubmit.AddListener(_ => Confirm());
            codeField.onSubmit.AddListener(_ => Confirm());
            addressField.onSubmit.AddListener(_ => Confirm());
        }

        public void Open(Mode m)
        {
            Current = m;
            title.text = m switch
            {
                Mode.Practice => "Practice", Mode.Create => "Create a room", Mode.Join => "Join a room",
                Mode.LocalHost => "Host on this Wi-Fi", _ => "Join on this Wi-Fi",
            };
            hint.text = m switch
            {
                Mode.Practice => "You against three bots, offline.",
                Mode.Create => "You get a party code to share. 3 to 4 players.",
                Mode.Join => "Type the 5-letter code your friend got.",
                Mode.LocalHost => "Friends on the same Wi-Fi or hotspot join with your address.",
                _ => "Type the address shown on the host's lobby screen.",
            };
            confirmLabel.text = m switch { Mode.Create => "CREATE", Mode.Join => "JOIN", Mode.LocalHost => "HOST", Mode.LocalJoin => "JOIN", _ => "PLAY" };
            codeRow.SetActive(m == Mode.Join);
            addressRow.SetActive(m == Mode.LocalJoin);
            botsRow.SetActive(m == Mode.Create || m == Mode.LocalHost);
            if (string.IsNullOrEmpty(nameField.text)) nameField.text = GameSettings.LastName;
            error.text = "";
            Show();
            (m == Mode.Join && !string.IsNullOrEmpty(nameField.text) ? codeField : nameField).ActivateInputField();
        }

        public void Reopen(string message = null)
        {
            Open(Current);
            if (message != null) error.text = message;
        }

        public void Confirm()
        {
            if (!NameRules.TryClean(nameField.text, out var name, out var err)) { Fail(err); return; }
            nameField.text = name;
            GameSettings.LastName = name;
            var net = PartyNet.Ensure();
            switch (Current)
            {
                case Mode.Practice:
                    if (!net.StartPractice(name)) { Fail("Couldn't start practice"); return; }
                    break;
                case Mode.Create:
                    JoinFlow.Ensure().CreateRoom(name, bots.isOn);
                    UiRoot.Instance.progress.WatchOnline();
                    break;
                case Mode.Join:
                    if (RoomServiceClient.NormaliseCode(codeField.text) == null) { Fail(RoomServiceClient.BadCode); return; }
                    JoinFlow.Ensure().JoinRoom(codeField.text, name);
                    UiRoot.Instance.progress.WatchOnline();
                    break;
                case Mode.LocalHost:
                    if (!net.StartLocalHost(name)) { Fail("Couldn't host — is another game using port " + PartyProtocol.LocalPort + "?"); return; }
                    break;
                case Mode.LocalJoin:
                    var addr = (addressField.text ?? "").Trim();
                    if (addr.Length == 0) { Fail("Type the host's address"); return; }
                    if (!net.JoinLocal(addr, name)) { Fail("That address doesn't look right"); return; }
                    UiRoot.Instance.progress.WatchLocal(addr);
                    break;
            }
            Sfx.Ui("UiClick");
            Hide();
        }

        void Fail(string message)
        {
            error.text = message;
            Sfx.Ui("UiBack");
        }
    }
}
