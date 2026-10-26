#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShooterParty.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterParty.Tests
{
    // The in-session views in a real Practice room: intro card, HUD cards and crown, final podium. Everything they
    // show comes from SessionManager's replicated state, so the same views run on every client.
    public class HudTests
    {
        UiRoot ui;
        SessionManager s;

        [UnitySetUp]
        public IEnumerator EnterPracticeLobby()
        {
            yield return NetTestRig.BootToPracticeLobby();
            ui = UiRoot.Instance;
            s = SessionManager.Instance;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SessionFlow.TimeScaleForTests = 1f;
            yield return NetTestRig.Teardown();
        }

        static IEnumerator WaitFor(System.Func<bool> done, float timeout, string what)
        {
            for (float t = 0f; t < timeout && !done(); t += Time.unscaledDeltaTime) yield return null;
            Assert.IsTrue(done(), "timed out waiting for " + what);
        }

        IEnumerator StartPlaylist(int games)
        {
            s.RequestPlaylist("Test", Enumerable.Repeat("knockback_arena", games).ToArray());
            yield return WaitFor(() => s.PlaylistIds.Count == games && s.PlaylistName.Value == "Test", 3f, "the playlist");
            s.RequestStart();
        }

        [UnityTest]
        public IEnumerator IntroShowsHowToPlayAndRoundXofN()
        {
            yield return StartPlaylist(2);
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Intro, 15f, "the intro");
            yield return null; yield return null;
            var def = s.CurrentGame;
            var intro = ui.intro;
            Assert.IsTrue(intro.IsShown, "the intro card shows in the Intro phase");
            Assert.AreEqual(def.displayName, intro.title.text);
            Assert.AreEqual(def.tagline, intro.tagline.text);
            Assert.AreEqual(def.howToPlay[0], intro.lines[0].text);
            Assert.AreEqual(def.controlsHint, intro.controls.text);
            Assert.AreEqual("ROUND 1 OF 2", intro.round.text);
            Assert.IsFalse(ui.hud.IsShown, "the HUD waits for the countdown");
        }

        [UnityTest]
        public IEnumerator CardsShowScoreAndCrownIcon()
        {
            SessionFlow.TimeScaleForTests = 0.25f;
            yield return StartPlaylist(1);
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Playing, 20f, "the round");
            int slot = s.Players[2].slot;
            s.ReportScores(new[] { (slot, 5) }, slot);
            yield return null; yield return null;
            Assert.IsTrue(ui.hud.IsShown);
            var card = ui.hud.CardFor(slot);
            Assert.IsNotNull(card, "a card per seat");
            Assert.AreEqual("5", card.scoreText.text);
            Assert.IsTrue(card.crown.enabled, "the top scorer's card wears the crown icon");
            Assert.AreEqual(1, ui.hud.cards.Count(c => c.gameObject.activeSelf && c.crown.enabled), "one crown");
            Assert.AreEqual(4, ui.hud.cards.Count(c => c.gameObject.activeSelf), "four seats, four cards");
        }

        [UnityTest]
        public IEnumerator CrownAttachesToLeaderCritter()
        {
            SessionFlow.TimeScaleForTests = 0.25f;
            yield return StartPlaylist(1);
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Playing, 20f, "the round");
            int slot = s.Players[1].slot;
            s.ReportScores(new[] { (slot, 2) }, slot);
            yield return null; yield return null;
            var critter = NetworkCritter.All.First(c => c.Slot.Value == slot);
            var crown = Object.FindFirstObjectByType<LeaderCrown>();
            Assert.IsNotNull(crown, "the crown is in the scene");
            Assert.IsTrue(crown.gameObject.activeInHierarchy && crown.transform.IsChildOf(critter.transform), "it rides the leader's head");
            critter.Eliminated.Value = true;
            yield return null; yield return null;
            Assert.IsFalse(crown.gameObject.activeInHierarchy, "an eliminated leader drops the crown");
            Assert.IsTrue(ui.hud.CardFor(slot).crown.enabled, "the card keeps the icon");
        }

        // The freeze before GO is client-side input gating; the server must refuse shots too, or a modified client
        // pushes frozen players off the ice during the intro and countdown.
        [UnityTest]
        public IEnumerator ShotsBeforeTheRoundIsLiveAreRefused()
        {
            yield return StartPlaylist(1);
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Intro && NetworkCritter.All.Count == 4, 15f, "the intro");
            var mine = NetworkCritter.All.First(c => !c.IsBot.Value && c.IsOwner);
            int before = ShotSystem.Instance.ServerShots;
            mine.FireRpc(mine.transform.position + Vector3.up * 0.45f, mine.transform.forward, s.NetworkManager.LocalTime.Time);
            yield return null; yield return null;
            Assert.AreEqual(before, ShotSystem.Instance.ServerShots, "no shots during the intro");
            yield return WaitFor(() => MiniGameController.Current && MiniGameController.Current.Live.Value, 15f, "GO");
            before = ShotSystem.Instance.ServerShots;
            mine.FireRpc(mine.transform.position + Vector3.up * 0.45f, mine.transform.forward, s.NetworkManager.LocalTime.Time);
            yield return null; yield return null;
            Assert.AreEqual(before + 1, ShotSystem.Instance.ServerShots, "and they work once the round is live");
        }

        [UnityTest]
        public IEnumerator FinalShowsChampionAndBackToLobby()
        {
            SessionFlow.TimeScaleForTests = 0.1f;
            yield return StartPlaylist(1);
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Standings, 60f, "the standings");
            SessionFlow.TimeScaleForTests = 0.5f;   // a 6 s final: long enough for SP_Lobby to load under it
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Final, 10f, "the final");
            yield return WaitFor(() => LobbyStage.Instance && LobbyStage.Instance.CrownedSlot >= 0, 5f, "the podium");
            var champs = s.ServerFlow.Champions();
            var names = s.ServerFlow.Players.Where(p => champs.Contains(p.slot)).Select(p => p.name.ToString()).ToList();
            Assert.IsTrue(ui.final.IsShown, "the final view shows");
            foreach (var n in names) StringAssert.Contains(n, ui.final.championName.text);
            var stage = LobbyStage.Instance;
            Assert.IsNotNull(stage);
            Assert.AreEqual(champs[0], stage.CrownedSlot, "the champion wears the crown on the podium");
            foreach (var p in s.ServerFlow.Players)
                Assert.IsTrue(stage.CritterAt(p.slot).gameObject.activeInHierarchy, p.name + " stands on the podium");
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Lobby, 10f, "back to the lobby");
            yield return null; yield return null;
            Assert.IsTrue(ui.lobby.IsShown, "the lobby comes back");
            Assert.AreEqual(-1, stage.CrownedSlot, "the podium clears");
        }
    }
}
#endif
