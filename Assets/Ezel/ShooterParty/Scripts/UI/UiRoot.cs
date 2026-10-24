using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterParty.UI
{
    // The one UI canvas (SP_Boot, kept across scenes). It shows the menu while offline and, once in a room, the
    // view that matches the session phase. Modals (dialogs, settings, pause) sit on top. It also decides when the
    // game may capture the mouse: only during play with no menu open.
    public class UiRoot : MonoBehaviour
    {
        public static UiRoot Instance { get; private set; }

        public MenuScreen menu;
        public LobbyView lobby;
        public NameCodeDialog nameDialog;
        public JoinProgressView progress;
        public Dialog dialog;
        public Toasts toasts;
        public SettingsView settings;
        public PauseMenu pause;
        public UiScreen loadingCurtain;
        public TMPro.TMP_Text loadingLabel;

        public string menuScene = "SP_Menu";
        readonly List<SessionView> views = new List<SessionView>();
        bool wasInRoom;

        public static void Toast(string text) { if (Instance && Instance.toasts) Instance.toasts.Show(text); }

        void Awake()
        {
            Instance = this;
            views.AddRange(GetComponentsInChildren<SessionView>(true));
        }

        void Start() => GameSettings.Apply();

        public bool AnyModal => nameDialog.IsShown || progress.IsShown || dialog.IsShown || settings.IsShown || pause.IsShown;

        public SessionPhase? Phase
        {
            get
            {
                var net = PartyNet.Instance;
                var s = SessionManager.Instance;
                if (!net || net.Mode == PartyMode.Offline || !s || !s.IsSpawned || !s.TryGetLocalPlayer(out _)) return null;
                return s.Phase.Value;
            }
        }

        void Update()
        {
            var phase = Phase;
            if (phase == null && wasInRoom && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != menuScene
                && Application.CanStreamedLevelBeLoaded(menuScene))
                UnityEngine.SceneManagement.SceneManager.LoadScene(menuScene);   // left a room: back to the menu's backdrop
            wasInRoom = phase != null;
            if (phase == null)
            {
                if (!nameDialog.IsShown && !progress.IsShown) menu.Show(); else menu.Hide();
                loadingCurtain.Hide();
                foreach (var v in views) v.Hide();
                if (pause.IsShown) pause.Hide();
            }
            else
            {
                menu.Hide();
                if (nameDialog.IsShown) nameDialog.Hide();
                var p = phase.Value;
                if (p == SessionPhase.Loading)
                {
                    var game = SessionManager.Instance.CurrentGameForIndex(SessionManager.Instance.RoundIndex.Value);
                    if (loadingLabel) loadingLabel.text = game ? "Loading " + game.displayName + "…" : "Loading…";
                    loadingCurtain.Show();
                }
                else loadingCurtain.Hide();
                foreach (var v in views) { if (v.ShowsIn(p)) v.Show(); else v.Hide(); }
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame && !dialog.IsShown && !settings.IsShown)
                {
                    if (pause.IsShown) pause.Hide(); else pause.Show();
                }
            }
            bool playing = phase == SessionPhase.Countdown || phase == SessionPhase.Playing || phase == SessionPhase.Intro;
            var cam = OverShoulderCamera.Main;
            if (cam) cam.allowCursorLock = playing && !AnyModal;
        }
    }

    // A view that belongs to some session phases (lobby, intro card, HUD, results...). UiRoot shows and hides it.
    public abstract class SessionView : UiScreen
    {
        public abstract bool ShowsIn(SessionPhase phase);
    }
}
