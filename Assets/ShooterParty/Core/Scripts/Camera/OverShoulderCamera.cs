using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterParty
{
    // Over-the-shoulder orbit camera. While allowCursorLock is on (UiRoot: play with no menu open, ControlRules) it holds
    // the mouse, locked and hidden, and turns with mouse / right stick at GameSettings sensitivity and invert-Y.
    public class OverShoulderCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 3.8f, height = 1.35f, shoulder = 0.6f;
        public float mouseSensitivity = 0.12f, stickSensitivity = 160f;
        public float minPitch = -15f, maxPitch = 55f;
        public bool allowCursorLock;             // set each frame by whoever owns the menus (UiRoot; the playground's own toggle)

        float yaw, pitch = 14f;

        public static OverShoulderCamera Main { get; private set; }   // the scene's gameplay camera, if any
        public bool Captured { get; private set; }   // the game holds the mouse: gameplay input may run
        public Ray AimRay { get; private set; }      // through the crosshair (screen centre), without the camera shake
        void OnEnable() => Main = this;

        void OnDisable()
        {
            if (Main == this) Main = null;
            if (Captured) { Captured = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }   // never leave a scene with it locked
        }

        public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        void Start() { if (target) yaw = target.eulerAngles.y; }

        public void SetTarget(Transform t, bool snapYaw = true) { target = t; if (t && snapYaw) yaw = t.eulerAngles.y; }

        void LateUpdate()
        {
            bool was = Captured;
            Captured = allowCursorLock;
            Cursor.lockState = Captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !Captured;
            if (Captured && was)   // skip the first locked frame: its delta is the cursor jumping to the centre
            {
                var look = Vector2.zero;
                var mouse = Mouse.current;
                if (mouse != null) look += mouse.delta.ReadValue() * mouseSensitivity;
                var pad = Gamepad.current;
                if (pad != null) look += pad.rightStick.ReadValue() * (stickSensitivity * Time.deltaTime);
                look *= GameSettings.Sensitivity;
                yaw += look.x; pitch -= GameSettings.InvertY ? -look.y : look.y;
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            if (!target) return;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var pos = target.position + Vector3.up * height + rot * new Vector3(shoulder, 0f, -distance);
            transform.SetPositionAndRotation(pos, rot);
            AimRay = new Ray(pos, rot * Vector3.forward);
        }
    }
}
