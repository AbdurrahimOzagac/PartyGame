#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using ShooterParty.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterParty.Tests
{
    // The real boot path: SP_Boot -> Bootstrap -> SP_Menu with the persistent UI root.
    public class MenuFlowTests
    {
        const string BootScene = "Assets/ShooterParty/Core/Scenes/SP_Boot.unity";
        float savedMusic;

        [UnitySetUp]
        public IEnumerator Boot()
        {
            savedMusic = GameSettings.Music;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(BootScene, new LoadSceneParameters(LoadSceneMode.Single));
            for (float t = 0f; t < 10f && !(UiRoot.Instance && SceneManager.GetActiveScene().name == "SP_Menu"); t += Time.unscaledDeltaTime) yield return null;
            Assert.IsNotNull(UiRoot.Instance, "UI root from SP_Boot");
            Assert.AreEqual("SP_Menu", SceneManager.GetActiveScene().name, "Bootstrap opens the menu scene");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSettings.Music = savedMusic;
            yield return NetTestRig.Teardown();
        }

        [UnityTest]
        public IEnumerator PracticeFromMenuReachesLobby()
        {
            var ui = UiRoot.Instance;
            yield return null;
            Assert.IsTrue(ui.menu.IsShown, "the menu shows while offline");
            ui.nameDialog.Open(NameCodeDialog.Mode.Practice);
            ui.nameDialog.nameField.text = "Tester";
            ui.nameDialog.Confirm();
            for (float t = 0f; t < 10f && !(SessionManager.Instance && SessionManager.Instance.Players.Count == 4); t += Time.unscaledDeltaTime) yield return null;
            Assert.IsNotNull(SessionManager.Instance);
            Assert.AreEqual(SessionPhase.Lobby, SessionManager.Instance.Phase.Value);
            Assert.AreEqual(4, SessionManager.Instance.Players.Count, "1 human + 3 bots");
            yield return null;
            Assert.IsFalse(ui.menu.IsShown, "the menu hides once in a room");
            Assert.IsFalse(ui.nameDialog.IsShown);
            Assert.AreEqual("Tester", GameSettings.LastName);
        }

        [UnityTest]
        public IEnumerator BadNameShowsErrorAndKeepsDialog()
        {
            var d = UiRoot.Instance.nameDialog;
            d.Open(NameCodeDialog.Mode.Create);
            d.nameField.text = "<b>hi</b>";
            d.Confirm();
            yield return null;
            Assert.IsTrue(d.IsShown, "the dialog stays open");
            Assert.AreEqual(NameRules.CharsetError, d.error.text);
            Assert.AreEqual(PartyMode.Offline, PartyNet.Instance.Mode, "nothing was started");
        }

        [UnityTest]
        public IEnumerator BadCodeShowsMessageAndKeepsText()
        {
            var d = UiRoot.Instance.nameDialog;
            d.Open(NameCodeDialog.Mode.Join);
            d.nameField.text = "Tester";
            d.codeField.text = "AE";
            d.Confirm();
            yield return null;
            Assert.IsTrue(d.IsShown);
            Assert.AreEqual(RoomServiceClient.BadCode, d.error.text);
            Assert.AreEqual("AE", d.codeField.text, "what the player typed stays");
        }

        [UnityTest]
        public IEnumerator SettingsPersist()
        {
            var s = UiRoot.Instance.settings;
            s.Show();
            yield return null;
            s.music.value = 0.3f;
            Assert.AreEqual(0.3f, GameSettings.Music, 1e-4f);
            Assert.AreEqual(0.3f, PlayerPrefs.GetFloat(GameSettings.MusicKey), 1e-4f, "stored on this device");
            Assert.AreEqual(0.3f, SfxPlayer.Instance.musicVolume, 1e-4f, "applied at once");
        }
    }
}
#endif
