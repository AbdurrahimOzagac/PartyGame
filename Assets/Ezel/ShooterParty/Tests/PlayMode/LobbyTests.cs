#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterParty.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterParty.Tests
{
    // The lobby through its own view API, in a Practice room (1 human leader + 3 bots) reached from the real menu.
    public class LobbyTests
    {
        const string BootScene = "Assets/Ezel/ShooterParty/Scenes/SP_Boot.unity";
        LobbyView lobby;
        SessionManager session;

        [UnitySetUp]
        public IEnumerator EnterPracticeLobby()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(BootScene, new LoadSceneParameters(LoadSceneMode.Single));
            for (float t = 0f; t < 10f && !(UiRoot.Instance && SceneManager.GetActiveScene().name == "SP_Menu"); t += Time.unscaledDeltaTime) yield return null;
            var ui = UiRoot.Instance;
            ui.nameDialog.Open(NameCodeDialog.Mode.Practice);
            ui.nameDialog.nameField.text = "Tester";
            ui.nameDialog.Confirm();
            lobby = ui.lobby;
            for (float t = 0f; t < 10f && !(lobby.IsShown && SessionManager.Instance && SessionManager.Instance.Players.Count == 4
                && SceneManager.GetActiveScene().name == SessionManager.LobbyScene); t += Time.unscaledDeltaTime) yield return null;
            session = SessionManager.Instance;
            Assert.IsTrue(lobby.IsShown, "the lobby view shows in the Lobby phase");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown() { yield return NetTestRig.Teardown(); }

        [UnityTest]
        public IEnumerator RoomStartsWithTheQuickPreset()
        {
            yield return null;
            CollectionAssert.AreEqual(new[] { "Quick", "Classic", "Marathon" }, session.catalog.presets.Select(p => p.name).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 5, 8 }, session.catalog.presets.Select(p => p.games.Count).ToArray());
            Assert.AreEqual("Quick", session.PlaylistName.Value.ToString());
            Assert.AreEqual(3, session.PlaylistIds.Count);
        }

        [UnityTest]
        public IEnumerator LeaderBuildsPlaylistAndStarts()
        {
            Assert.IsTrue(lobby.startButton.gameObject.activeSelf, "the leader sees Start");
            Assert.IsFalse(lobby.readyButton.gameObject.activeSelf, "the leader has no Ready button");
            var b = lobby.builder;
            b.Open();
            b.Clear();
            b.Add("knockback_arena");
            b.Add("knockback_arena");
            b.SetName("Duo");
            Assert.IsTrue(b.Apply(), b.Error);
            for (float t = 0f; t < 5f && session.PlaylistName.Value != "Duo"; t += Time.unscaledDeltaTime) yield return null;
            Assert.AreEqual("Duo", session.PlaylistName.Value.ToString());
            Assert.AreEqual(2, session.PlaylistIds.Count);
            Assert.IsFalse(b.IsEditing, "applying closes the builder");

            var seen = new List<SessionPhase>();
            session.Phase.OnValueChanged += (_, now) => seen.Add(now);
            lobby.PressStart();
            for (float t = 0f; t < 5f && seen.Count == 0; t += Time.unscaledDeltaTime) yield return null;
            Assert.AreEqual(SessionPhase.Loading, seen.FirstOrDefault());
            Assert.AreEqual("Duo", session.PlaylistName.Value.ToString());
        }

        [UnityTest]
        public IEnumerator InvalidPlaylistNameRejectedWithMessage()
        {
            var b = lobby.builder;
            b.Open();
            b.SetName("<b>bad</b>");
            Assert.IsFalse(b.Apply());
            Assert.AreEqual(NameRules.CharsetError, b.Error);
            Assert.AreEqual(NameRules.CharsetError, b.errorLabel.text, "the error shows under the builder");
            Assert.IsTrue(b.IsEditing, "the builder stays open to fix it");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual("Quick", session.PlaylistName.Value.ToString(), "nothing reached the server");
        }

        [UnityTest]
        public IEnumerator SeatsShowNamesBotsAndCrittersInTheirColours()
        {
            yield return null;
            var stage = Object.FindFirstObjectByType<LobbyStage>();
            Assert.IsNotNull(stage, "SP_Lobby has the stage");
            for (int i = 0; i < session.Players.Count; i++)
            {
                var p = session.Players[i];
                var card = lobby.slots[p.slot];
                Assert.AreEqual(p.name.ToString(), card.nameLabel.text);
                Assert.AreEqual(p.isBot, card.botTag.activeSelf, "bot tag on " + p.name);
                Assert.AreEqual(p.leader, card.leaderTag.activeSelf, "leader tag on " + p.name);
                var critter = stage.CritterAt(p.slot);
                Assert.IsTrue(critter && critter.gameObject.activeInHierarchy, "a critter stands on pedestal " + p.slot);
                Assert.AreEqual(p.colour, critter.GetComponentInParent<CritterLook>().Current);
            }
            session.RequestReady(true);
            for (float t = 0f; t < 3f && !stage.ShownReady(0); t += Time.unscaledDeltaTime) yield return null;
            Assert.IsTrue(stage.ShownReady(0), "the stage saw the leader get ready");
            yield return null;   // the view may run before the session mirrors the seat in a given frame
            Assert.IsTrue(lobby.slots[0].readyMark.activeSelf);
        }
    }

    public class LobbyControlsTests
    {
        [Test]
        public void NonLeaderCannotSeeStart()
        {
            var member = new PlayerState { connected = true, leader = false };
            var c = LobbyView.ControlsFor(member, PartyMode.OnlineClient);
            Assert.IsFalse(c.start); Assert.IsTrue(c.ready); Assert.IsFalse(c.editPlaylist); Assert.IsFalse(c.kick);
            member.leader = true;
            c = LobbyView.ControlsFor(member, PartyMode.OnlineClient);
            Assert.IsTrue(c.start); Assert.IsFalse(c.ready); Assert.IsTrue(c.editPlaylist); Assert.IsTrue(c.kick);
            Assert.IsFalse(LobbyView.ControlsFor(member, PartyMode.Practice).kick, "nobody to kick in practice");
        }

        [Test]
        public void PingDotColours()
        {
            Assert.AreEqual(LobbyView.Good, LobbyView.PingColour(119));
            Assert.AreEqual(LobbyView.Fair, LobbyView.PingColour(120));
            Assert.AreEqual(LobbyView.Fair, LobbyView.PingColour(219));
            Assert.AreEqual(LobbyView.Poor, LobbyView.PingColour(220));
        }

        [Test]
        public void StartBlockerMatchesTheServerRules()
        {
            var lead = new PlayerState { pid = "a", connected = true, leader = true };
            var other = new PlayerState { pid = "b", connected = true };
            Assert.AreEqual("Need at least 3 players", SessionFlow.StartBlocker(new[] { lead, other }, 3, 1));
            Assert.AreEqual("Everyone must be ready", SessionFlow.StartBlocker(new[] { lead, other }, 2, 1));
            other.ready = true;
            Assert.AreEqual("Pick a playlist first", SessionFlow.StartBlocker(new[] { lead, other }, 2, 0));
            Assert.IsNull(SessionFlow.StartBlocker(new[] { lead, other }, 2, 1));
        }
    }
}
#endif
