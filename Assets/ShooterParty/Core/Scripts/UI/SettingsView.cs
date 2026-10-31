using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // Sound, controls and display. Changes apply at once and stay on this computer.
    public class SettingsView : UiScreen
    {
        public Slider master, music, effects, sensitivity;
        public Toggle invertY, vsync, netStats;
        public Button displayPrev, displayNext, resPrev, resNext, back;
        public TMP_Text displayValue, resValue;
        Resolution[] resolutions;

        protected override void Awake()
        {
            base.Awake();
            master.onValueChanged.AddListener(v => GameSettings.Master = v);
            music.onValueChanged.AddListener(v => GameSettings.Music = v);
            effects.onValueChanged.AddListener(v => GameSettings.Effects = v);
            sensitivity.onValueChanged.AddListener(v => GameSettings.Sensitivity = v);
            invertY.onValueChanged.AddListener(v => GameSettings.InvertY = v);
            vsync.onValueChanged.AddListener(v => GameSettings.Vsync = v);
            netStats.onValueChanged.AddListener(v => GameSettings.ShowNetStats = v);
            displayPrev.onClick.AddListener(ToggleDisplay);
            displayNext.onClick.AddListener(ToggleDisplay);
            resPrev.onClick.AddListener(() => StepResolution(-1));
            resNext.onClick.AddListener(() => StepResolution(1));
            back.onClick.AddListener(() => { Sfx.Ui("UiBack"); Hide(); });
        }

        protected override void OnShown()
        {
            master.SetValueWithoutNotify(GameSettings.Master);
            music.SetValueWithoutNotify(GameSettings.Music);
            effects.SetValueWithoutNotify(GameSettings.Effects);
            sensitivity.SetValueWithoutNotify(GameSettings.Sensitivity);
            invertY.SetIsOnWithoutNotify(GameSettings.InvertY);
            vsync.SetIsOnWithoutNotify(GameSettings.Vsync);
            netStats.SetIsOnWithoutNotify(GameSettings.ShowNetStats);
            resolutions = Screen.resolutions;
            Refresh();
        }

        void ToggleDisplay() { Sfx.Ui("UiClick"); GameSettings.Fullscreen = !GameSettings.Fullscreen; Refresh(); }

        void StepResolution(int d)
        {
            if (resolutions == null || resolutions.Length == 0) return;
            Sfx.Ui("UiClick");
            int i = GameSettings.ResolutionIndex;
            if (i < 0) i = Array.FindIndex(resolutions, r => r.width == Screen.width && r.height == Screen.height);
            GameSettings.ResolutionIndex = Mathf.Clamp((i < 0 ? resolutions.Length - 1 : i) + d, 0, resolutions.Length - 1);
            Refresh();
        }

        void Refresh()
        {
            displayValue.text = GameSettings.Fullscreen ? "Fullscreen" : "Windowed";
            int i = GameSettings.ResolutionIndex;
            resValue.text = resolutions != null && i >= 0 && i < resolutions.Length ? $"{resolutions[i].width} × {resolutions[i].height}" : $"{Screen.width} × {Screen.height}";
        }
    }
}
