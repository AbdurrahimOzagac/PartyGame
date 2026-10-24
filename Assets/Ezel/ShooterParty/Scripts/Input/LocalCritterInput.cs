using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterParty
{
    // Keyboard/mouse + gamepad driver for one critter in the asset playground scene.
    [RequireComponent(typeof(CritterMotor), typeof(CritterSpit))]
    public class LocalCritterInput : MonoBehaviour
    {
        public OverShoulderCamera cam;

        CritterMotor motor;
        CritterSpit spit;
        CritterAnimator anim;

        void Awake()
        {
            motor = GetComponent<CritterMotor>();
            spit = GetComponent<CritterSpit>();
            anim = GetComponentInChildren<CritterAnimator>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            Vector2 m = Vector2.zero;
            if (kb != null)
                m += new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0), (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
            if (pad != null) m += pad.leftStick.ReadValue();
            var fwd = cam ? cam.FlatForward : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            motor.Move(fwd * m.y + right * m.x);

            bool fire = (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
                        || (pad != null && pad.rightTrigger.wasPressedThisFrame);
            if (fire) spit.TryFire(fwd);
            if ((kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame)) motor.Jump();
            if ((kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)) || (pad != null && pad.buttonEast.wasPressedThisFrame)) motor.Dash();
            if (anim && ((kb != null && kb.tKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame))) anim.PlayTaunt();
            if (anim && kb != null && kb.vKey.wasPressedThisFrame) anim.PlayWin();
            if (anim && kb != null && kb.bKey.wasPressedThisFrame) anim.PlayLose();
        }

        public bool showHelp = true;

        void OnDisable() { if (motor) motor.Move(Vector3.zero); }

        void OnGUI()
        {
            if (!showHelp) return;
            GUI.Label(new Rect(16, 12, 1000, 24),
                "Click to capture mouse | WASD move | Mouse aim | Click spit | Shift dash | Space jump | T taunt | V win | B lose | Esc release");
        }
    }
}
