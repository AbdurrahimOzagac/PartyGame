using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterParty
{
    // The local player's critter, keyboard/mouse and gamepad:
    //   WASD / left stick       move, relative to the camera
    //   mouse / right stick     look (OverShoulderCamera)
    //   hold left click / RT    spit at the crosshair, every cooldown; it faces the crosshair while held
    //   Shift / B / LB          dash (move direction, or facing when idle)
    //   Space / A               jump (needs a charge)
    // Runs only while the camera holds the mouse (play, no menu, focused) and the round is live (ControlRules).
    [RequireComponent(typeof(CritterMotor), typeof(CritterSpit))]
    public class LocalCritterInput : MonoBehaviour
    {
        public OverShoulderCamera cam;
        const float AimRange = 60f;

        CritterMotor motor;
        CritterSpit spit;
        CritterAnimator anim;
        bool fireArmed;
        readonly RaycastHit[] hits = new RaycastHit[16];

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
            bool captured = cam && cam.Captured;
            bool fire = ControlRules.HoldToFire(ref fireArmed, captured,
                (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightTrigger.isPressed));
            var game = MiniGameController.Current;   // frozen through the intro and countdown
            if (!captured || (game && game.IsSpawned && !game.Live.Value)) { motor.Move(Vector3.zero); return; }

            var m = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
            if (kb != null)
                m += new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0), (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
            motor.Move(ControlRules.CameraRelative(m, cam.FlatForward));
            if (fire)
            {
                var aim = Aim();
                motor.FaceTowards(aim, false);   // after Move: while firing it faces the crosshair, not its movement
                spit.TryFire(aim);
            }
            if ((kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame)) motor.Jump();
            if ((kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame))
                || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame))) motor.Dash();
        }

        // The first thing (not ourselves) under the crosshair, or a point far ahead; the spit leaves the mouth toward it.
        Vector3 Aim()
        {
            var ray = cam.AimRay;
            var point = ray.GetPoint(AimRange);
            float best = AimRange;
            int n = Physics.RaycastNonAlloc(ray, hits, AimRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (hits[i].distance < best && !hits[i].collider.transform.IsChildOf(transform)) { best = hits[i].distance; point = hits[i].point; }
            var mouth = anim && anim.Muzzle ? anim.Muzzle.position : transform.position + Vector3.up * 0.45f;
            return ControlRules.AimDirection(mouth, point, cam.FlatForward);
        }

        void OnDisable() { if (motor) motor.Move(Vector3.zero); }
    }
}
