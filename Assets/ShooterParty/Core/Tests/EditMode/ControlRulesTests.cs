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
            bool armed = false;
            Assert.IsFalse(ControlRules.HoldToFire(ref armed, false, true), "menu open: never fires");
            Assert.IsFalse(ControlRules.HoldToFire(ref armed, true, true), "the click that closed the menu is still down");
            Assert.IsFalse(ControlRules.HoldToFire(ref armed, true, false), "released");
            Assert.IsTrue(ControlRules.HoldToFire(ref armed, true, true), "a fresh press fires");
            Assert.IsTrue(ControlRules.HoldToFire(ref armed, true, true), "holding keeps firing");
            Assert.IsFalse(ControlRules.HoldToFire(ref armed, false, true), "pause opens");
            Assert.IsFalse(ControlRules.HoldToFire(ref armed, true, true), "back in play, still held: wait for a release");
        }
    }
}
