using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterParty
{
    // Minimal over-the-shoulder orbit camera for the asset playground (not the game's final camera).
    public class OverShoulderCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 3.8f, height = 1.35f, shoulder = 0.6f;
        public float mouseSensitivity = 0.12f, stickSensitivity = 160f;
        public float minPitch = -15f, maxPitch = 55f;
        public bool allowCursorLock = true;      // off while menus need the mouse

        float yaw, pitch = 14f;

        public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        void Start() { if (target) yaw = target.eulerAngles.y; }

        public void SetTarget(Transform t, bool snapYaw = true) { target = t; if (t && snapYaw) yaw = t.eulerAngles.y; }

        void LateUpdate()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (!allowCursorLock) Cursor.lockState = CursorLockMode.None;
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue() * mouseSensitivity;
                yaw += d.x; pitch -= d.y;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var s = pad.rightStick.ReadValue() * stickSensitivity * Time.deltaTime;
                yaw += s.x; pitch -= s.y;
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            if (!target) return;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target.position + Vector3.up * height + rot * new Vector3(shoulder, 0f, -distance), rot);
        }
    }
}
