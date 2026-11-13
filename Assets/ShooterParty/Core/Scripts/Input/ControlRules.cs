using UnityEngine;

namespace ShooterParty
{
    // The control decisions as pure functions, so they are tested without devices (ControlRulesTests).
    public static class ControlRules
    {
        public const float MinAimAhead = 2f;   // a crosshair point closer than this (the floor at your feet) converges this far out

        // The game holds the mouse (locked, hidden) only while a round is played, no menu is open and the window has focus.
        public static bool CaptureMouse(SessionPhase? phase, bool modalOpen, bool focused) =>
            focused && !modalOpen && (phase == SessionPhase.Countdown || phase == SessionPhase.Playing);

        // WASD / left stick on the ground, relative to where the camera looks (not where the critter faces).
        public static Vector3 CameraRelative(Vector2 stick, Vector3 flatForward)
        {
            stick = Vector2.ClampMagnitude(stick, 1f);
            return flatForward * stick.y + Vector3.Cross(Vector3.up, flatForward) * stick.x;
        }

        // Flat spit direction from the mouth to the crosshair's point. The camera sits over the right shoulder, so
        // spitting along its forward would land beside the crosshair. Spit stays flat (ShotSystem drops the height).
        public static Vector3 AimDirection(Vector3 mouth, Vector3 aimPoint, Vector3 forward)
        {
            forward.y = 0f; forward.Normalize();
            var d = aimPoint - mouth; d.y = 0f;
            float ahead = Vector3.Dot(d, forward);
            if (ahead < MinAimAhead) d += forward * (MinAimAhead - ahead);   // too close or behind the mouth: never sideways or back
            return d.normalized;
        }

        // Fire intent for the local player: hold keeps firing every cooldown, and a tap is never lost. A press seen
        // this frame (even one released inside the same frame, which a held check misses) or during the cooldown is
        // remembered for FireBuffer and fires as soon as the spit is ready. Only one tap is remembered, so mashing
        // never queues a burst. A button already down when play took the mouse (the click that closed a menu) waits
        // for a release.
        public const float FireBuffer = 0.35f;   // a little longer than the 10-frame cooldown: a tap right after a shot still lands

        public struct FireGate
        {
            public bool armed, wasCaptured;
            public float bufferedUntil;
        }

        public static bool FireIntent(ref FireGate g, bool captured, bool held, bool pressedThisFrame, float now)
        {
            if (!captured) { g = default; return false; }
            bool settled = g.wasCaptured;          // the capture frame's click belongs to the menu
            g.wasCaptured = true;
            if (pressedThisFrame && settled) { g.armed = true; g.bufferedUntil = now + FireBuffer; }
            else if (!held) g.armed = true;
            return (g.armed && held) || now < g.bufferedUntil;
        }
    }
}
