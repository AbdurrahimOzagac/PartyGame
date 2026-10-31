using System;
using UnityEngine;

namespace ShooterParty
{
    // Player settings, stored on this device (PlayerPrefs). These are preferences, not saved game data.
    public static class GameSettings
    {
        public const string MasterKey = "sp.vol.master", MusicKey = "sp.vol.music", SfxKey = "sp.vol.sfx", SensKey = "sp.sens",
            InvertKey = "sp.invertY", DisplayKey = "sp.display", ResKey = "sp.res", VsyncKey = "sp.vsync", NetStatsKey = "sp.netstats",
            LastNameKey = "sp.lastName";

        public static event Action Changed;

        public static float Master { get => PlayerPrefs.GetFloat(MasterKey, 0.9f); set => SetF(MasterKey, Mathf.Clamp01(value)); }
        public static float Music { get => PlayerPrefs.GetFloat(MusicKey, 0.6f); set => SetF(MusicKey, Mathf.Clamp01(value)); }
        public static float Effects { get => PlayerPrefs.GetFloat(SfxKey, 1f); set => SetF(SfxKey, Mathf.Clamp01(value)); }
        public static float Sensitivity { get => PlayerPrefs.GetFloat(SensKey, 1f); set => SetF(SensKey, Mathf.Clamp(value, 0.2f, 3f)); }
        public static bool InvertY { get => PlayerPrefs.GetInt(InvertKey, 0) == 1; set => SetI(InvertKey, value ? 1 : 0); }
        public static bool Fullscreen { get => PlayerPrefs.GetInt(DisplayKey, 1) == 1; set => SetI(DisplayKey, value ? 1 : 0); }
        public static int ResolutionIndex { get => PlayerPrefs.GetInt(ResKey, -1); set => SetI(ResKey, value); }
        public static bool Vsync { get => PlayerPrefs.GetInt(VsyncKey, 1) == 1; set => SetI(VsyncKey, value ? 1 : 0); }
        public static bool ShowNetStats { get => PlayerPrefs.GetInt(NetStatsKey, 0) == 1; set => SetI(NetStatsKey, value ? 1 : 0); }
        public static string LastName { get => PlayerPrefs.GetString(LastNameKey, ""); set { PlayerPrefs.SetString(LastNameKey, value ?? ""); PlayerPrefs.Save(); } }

        static void SetF(string k, float v) { PlayerPrefs.SetFloat(k, v); PlayerPrefs.Save(); Apply(); }
        static void SetI(string k, int v) { PlayerPrefs.SetInt(k, v); PlayerPrefs.Save(); Apply(); }

        // Push the stored values into the engine; safe to call any time (and on a server, where it does little).
        public static void Apply()
        {
            AudioListener.volume = Master;
            if (SfxPlayer.Instance) SfxPlayer.Instance.SetVolumes(Effects, Music);
            if (!Application.isBatchMode)
            {
                QualitySettings.vSyncCount = Vsync ? 1 : 0;
                Application.targetFrameRate = Vsync ? -1 : 144;
                var res = Screen.resolutions;
                int i = ResolutionIndex;
                var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (i >= 0 && i < res.Length) Screen.SetResolution(res[i].width, res[i].height, mode);
                else if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
            }
            Changed?.Invoke();
        }
    }
}
