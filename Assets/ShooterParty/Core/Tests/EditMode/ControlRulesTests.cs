using NUnit.Framework;
using UnityEngine;

namespace ShooterParty.Tests
{
    public class ControlRulesTests
    {
        static void Near(Vector3 expected, Vector3 actual, string msg = null) =>
            Assert.Less(Vector3.Distance(expected, actual), 1e-3f, $"{msg} expected {expected} got {actual}");

        [Test]
        public void MouseIsCapturedOnlyWhilePlayingWithNoMenuAndFocus()
        {
            Assert.IsTrue(ControlRules.CaptureMouse(SessionPhase.Countdown, false, true));
            Assert.IsTrue(ControlRules.CaptureMouse(SessionPhase.Playing, false, true));
            Assert.IsFalse(ControlRules.CaptureMouse(SessionPhase.Playing, true, true), "a modal (pause, settings, dialog) frees it");
            Assert.IsFalse(ControlRules.CaptureMouse(SessionPhase.Playing, false, false), "losing focus frees it");
            Assert.IsFalse(ControlRules.CaptureMouse(null, false, true), "menu (not in a room)");
            foreach (var p in new[] { SessionPhase.Lobby, SessionPhase.Loading, SessionPhase.Intro, SessionPhase.RoundResults, SessionPhase.Standings, SessionPhase.Final })
                Assert.IsFalse(ControlRules.CaptureMouse(p, false, true), p.ToString());
        }

        [Test]
        public void MoveFollowsTheCameraNotTheCrittersFacing()
        {
            Near(Vector3.forward, ControlRules.CameraRelative(new Vector2(0f, 1f), Vector3.forward), "W, camera looking +Z");
            Near(Vector3.right, ControlRules.CameraRelative(new Vector2(0f, 1f), Vector3.right), "W, camera looking +X");
            Near(Vector3.back, ControlRules.CameraRelative(new Vector2(1f, 0f), Vector3.right), "D, camera looking +X");
            Near(new Vector3(1f, 0f, 1f).normalized, ControlRules.CameraRelative(new Vector2(1f, 1f), Vector3.forward), "W+D is not faster than W");
            Near(new Vector3(0f, 0f, 0.5f), ControlRules.CameraRelative(new Vector2(0f, 0.5f), Vector3.forward), "half stick, half speed");
        }

        [Test]
        public void AimGoesFromTheMouthToTheCrosshairPoint()
        {
            var mouth = new Vector3(0f, 0.45f, 0f);
            Near(new Vector3(0.6f, 0f, 10f).normalized, ControlRules.AimDirection(mouth, new Vector3(0.6f, 3f, 10f), Vector3.forward),
                 "corrects the over-shoulder offset, ignores height");
        }

        [Test]
        public void AimAtSomethingTooCloseOrBehindStillGoesForward()
        {
            var mouth = new Vector3(0f, 0.45f, 0f);
            var atFeet = ControlRules.AimDirection(mouth, new Vector3(0.6f, 0f, 0.3f), Vector3.forward);
            Assert.Greater(atFeet.z, 0.95f, "the floor right at the feet: converge further out, not sideways");
            var behind = ControlRules.AimDirection(mouth, new Vector3(0.6f, 1f, -2f), Vector3.forward);
            Assert.Greater(behind.z, 0.95f, "a wall between camera and critter never makes it spit backwards");
            Near(Vector3.forward, ControlRules.AimDirection(mouth, mouth, Vector3.forward), "degenerate point");
            Assert.AreEqual(0f, atFeet.y);
            Assert.AreEqual(1f, behind.magnitude, 1e-4f);
        }

        [Test]
        public void HoldToFireKeepsFiringButNotOnAButtonHeldWhenPlayTookTheMouse()
        {
            var g = new ControlRules.FireGate();
            Assert.IsFalse(ControlRules.FireIntent(ref g, false, true, true, 0f), "menu open: never fires");
            Assert.IsFalse(ControlRules.FireIntent(ref g, true, true, true, 0.1f), "the click that closed the menu (capture frame)");
            Assert.IsFalse(ControlRules.FireIntent(ref g, true, true, false, 0.2f), "... is still down");
            Assert.IsFalse(ControlRules.FireIntent(ref g, true, false, false, 0.3f), "released");
            Assert.IsTrue(ControlRules.FireIntent(ref g, true, true, true, 1.0f), "a fresh press fires");
            Assert.IsTrue(ControlRules.FireIntent(ref g, true, true, false, 1.5f), "holding keeps firing");
            Assert.IsFalse(ControlRules.FireIntent(ref g, false, true, false, 1.6f), "pause opens");
            Assert.IsFalse(ControlRules.FireIntent(ref g, true, true, false, 1.7f), "back in play, still held: wait for a release");
        }

        [Test]
        public void ATapIsNeverLostEvenInsideOneFrameOrDuringTheCooldown()
        {
            var g = new ControlRules.FireGate();
            ControlRules.FireIntent(ref g, true, false, false, 0f);
            Assert.IsTrue(ControlRules.FireIntent(ref g, true, false, true, 1.0f), "pressed and released within one frame still fires");
            Assert.IsTrue(ControlRules.FireIntent(ref g, true, false, false, 1.0f + ControlRules.FireBuffer * 0.9f),
                "a tap during the cooldown is remembered until the spit is ready");
            Assert.IsFalse(ControlRules.FireIntent(ref g, true, false, false, 1.0f + ControlRules.FireBuffer * 1.1f), "but not forever");
            Assert.Greater(ControlRules.FireBuffer, 10f / 30f, "a tap right after a shot outlives the 10-frame cooldown");

            ControlRules.FireIntent(ref g, true, false, true, 3.0f);
            ControlRules.FireIntent(ref g, true, false, true, 3.05f);   // mashing re-arms the one remembered tap, never a queue
            g.bufferedUntil = 0f;                                      // what LocalCritterInput does when the shot leaves
            Assert.IsFalse(ControlRules.FireIntent(ref g, true, false, false, 3.1f), "one shot per remembered tap");
        }
    }
}
