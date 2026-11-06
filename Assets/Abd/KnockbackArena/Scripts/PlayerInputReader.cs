using UnityEngine;
using UnityEngine.InputSystem;

namespace Abd.KnockbackArena
{
    // Sadece girdiyi okur. Hareketi/ateşi uygulayan kod buradan okur, böylece
    // online aşamasında girdi ağdan gelen bir kaynakla değiştirilebilir.
    public class PlayerInputReader : MonoBehaviour
    {
        InputAction move;
        InputAction look;
        InputAction fire;

        public Vector2 Move => move.ReadValue<Vector2>();
        public Vector2 LookDelta => look.ReadValue<Vector2>();
        public bool FireHeld => fire.IsPressed() && Cursor.lockState == CursorLockMode.Locked;

        void OnEnable()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            fire = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");

            move.Enable();
            look.Enable();
            fire.Enable();

            SetCursorLocked(true);
        }

        void OnDisable()
        {
            move.Dispose();
            look.Dispose();
            fire.Dispose();
        }

        void Update()
        {
            // Esc imleci serbest bırakır, oyuna tıklayınca tekrar kilitlenir.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                     && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
