using ShooterParty;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KnockbackArena.Playground
{
    // Playground-only extras on top of LocalCritterInput (this scene has no menus or pause): the mouse is held from the
    // start, Esc frees it, a click takes it back; T / Y taunt, V win, B lose for checking the clips; and the key hint.
    [RequireComponent(typeof(LocalCritterInput))]
    public class PlaygroundControls : MonoBehaviour
    {
        LocalCritterInput input;
        CritterAnimator anim;

        void Awake()
        {
            input = GetComponent<LocalCritterInput>();
            anim = GetComponentInChildren<CritterAnimator>();
        }

        void Start() { if (input.cam) input.cam.allowCursorLock = true; }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            if (input.cam && kb != null && kb.escapeKey.wasPressedThisFrame) input.cam.allowCursorLock = false;
            else if (input.cam && mouse != null && mouse.leftButton.wasPressedThisFrame) input.cam.allowCursorLock = true;   // this click never spits
            if (!anim) return;
            if ((kb != null && kb.tKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) anim.PlayTaunt();
            if (kb != null && kb.vKey.wasPressedThisFrame) anim.PlayWin();
            if (kb != null && kb.bKey.wasPressedThisFrame) anim.PlayLose();
        }

        void OnGUI() => GUI.Label(new Rect(16, 12, 1100, 24),
            "WASD move | Mouse aim | Hold click spit | Shift dash | Space jump | T taunt | V win | B lose | Esc free the mouse, click to take it back");
    }
}
