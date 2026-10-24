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

        // Hold-to-fire. A button already down when play took the mouse (the click that closed a menu) waits for a release.
        public static bool HoldToFire(ref bool armed, bool captured, bool held)
        {
            if (!captured) armed = false;
            else if (!held) armed = true;
            return armed && held;
        }
    }
}
