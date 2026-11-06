#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShooterParty;
using ShooterParty.Tests;
using UnityEngine;
using UnityEngine.TestTools;

namespace KnockbackArena.Tests
{
    // The whole loop on one machine: Practice host + 3 bots, a two-game Knockback Arena playlist,
    // real scene loads, real bots fighting, timers at 1/10 speed.
    public class PracticeSessionTests
    {
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

        [UnityTest]
        public IEnumerator PracticeSessionRunsTwoRoundsToFinal()
        {
            SessionFlow.TimeScaleForTests = 0.1f;
            NetTestRig.StartSession();
            yield return WaitFor(() => SessionManager.Instance && SessionManager.Instance.Players.Count == 4, 10f, "lobby with 4 seats");
            var s = SessionManager.Instance;
            Assert.AreEqual(SessionPhase.Lobby, s.Phase.Value);
            Assert.AreEqual(3, Enumerable.Range(0, s.Players.Count).Count(i => s.Players[i].isBot));
            s.RequestPlaylist("Duo", new[] { "knockback_arena", "knockback_arena" });
            yield return WaitFor(() => s.PlaylistIds.Count == 2, 3f, "playlist");
            Assert.AreEqual("Duo", s.PlaylistName.Value.ToString());
            s.RequestStart();
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Intro, 15f, "first intro");
            Assert.IsNotNull(MiniGameController.Current, "the mini-game controller is spawned for the round");
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Final, 90f, "the final");
            var flow = s.ServerFlow;
            Assert.AreEqual(2, flow.Rankings.Count, "two rounds played");
            foreach (var p in flow.Players)
            {
                int expected = flow.Rankings.Sum(r => ScoreRules.Award(r, new[] { 4, 3, 2, 1 }, 4)[p.slot]);
                Assert.AreEqual(expected, p.total, p.name.ToString());
            }
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Lobby, 10f, "back to the lobby");
        }
    }
}
#endif
