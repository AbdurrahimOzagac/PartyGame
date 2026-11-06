using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // A message box with one or two buttons.
    public class Dialog : UiScreen
    {
        public TMP_Text title, body;
        public Button primary, secondary;
        public TMP_Text primaryLabel, secondaryLabel;

        public void Ask(string heading, string text, string ok, Action onOk, string other = null, Action onOther = null)
        {
            title.text = heading; body.text = text; primaryLabel.text = ok;
            primary.onClick.RemoveAllListeners();
            primary.onClick.AddListener(() => { Sfx.Ui("UiClick"); Hide(); onOk?.Invoke(); });
            secondary.gameObject.SetActive(other != null);
            secondary.onClick.RemoveAllListeners();
            if (other != null)
            {
                secondaryLabel.text = other;
                secondary.onClick.AddListener(() => { Sfx.Ui("UiBack"); Hide(); onOther?.Invoke(); });
            }
            Show();
        }
    }
}
