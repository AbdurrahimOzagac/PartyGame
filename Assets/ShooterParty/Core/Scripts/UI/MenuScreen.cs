using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // Main menu: Play Online (create / join), Local Play (host / join by address), Practice, Settings, Quit.
    public class MenuScreen : UiScreen
    {
        public Button create, join, localHost, localJoin, practice, settingsButton, quit;
        public TMP_Text version;

        protected override void Awake()
        {
            base.Awake();
            create.onClick.AddListener(() => Open(NameCodeDialog.Mode.Create));
            join.onClick.AddListener(() => Open(NameCodeDialog.Mode.Join));
            localHost.onClick.AddListener(() => Open(NameCodeDialog.Mode.LocalHost));
            localJoin.onClick.AddListener(() => Open(NameCodeDialog.Mode.LocalJoin));
            practice.onClick.AddListener(() => Open(NameCodeDialog.Mode.Practice));
            settingsButton.onClick.AddListener(() => { Sfx.Ui("UiClick"); UiRoot.Instance.settings.Show(); });
            quit.onClick.AddListener(() =>
            {
                Sfx.Ui("UiBack");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
            if (version) version.text = "v" + Application.version + " · protocol " + PartyProtocol.Version;
        }

        protected override void OnShown() => Sfx.Music("MusicLobby");

        static void Open(NameCodeDialog.Mode m) { Sfx.Ui("UiClick"); UiRoot.Instance.nameDialog.Open(m); }
    }
}
