using UnityEngine;

namespace Abd.KnockbackArena
{
    // WASD ile hızlı ivmeli sabit hız. Mouse yatay hareketi karakteri döndürür.
    [RequireComponent(typeof(Rigidbody), typeof(PlayerInputReader), typeof(KnockbackReceiver))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] KnockbackArenaSettings settings;

        Rigidbody rb;
        PlayerInputReader input;
        KnockbackReceiver receiver;
        float yaw;

        public float Yaw => yaw;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<PlayerInputReader>();
            receiver = GetComponent<KnockbackReceiver>();
            yaw = transform.eulerAngles.y;
            // Yaw'ı kodla veriyoruz, fizik sadece X/Z devrilmesini engellemek için kısıtlı.
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        void Update()
        {
            yaw += input.LookDelta.x * settings.mouseSensitivity;
        }

        void FixedUpdate()
        {
            rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
            rb.angularVelocity = Vector3.zero;

            // Savrulma sırasında hareket hızı savrulmayı silmesin.
            if (receiver.IsControlLocked) return;
            if (!settings.airControl && !IsGrounded()) return;

            Vector2 m = input.Move;
            Vector3 wish = Quaternion.Euler(0f, yaw, 0f) * new Vector3(m.x, 0f, m.y) * settings.moveSpeed;

            Vector3 v = rb.linearVelocity;
            Vector3 horizontal = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), wish,
                settings.moveAcceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
        }

        bool IsGrounded()
        {
            // Küpün tabanına doğru kalın bir kutu atar (kenarda dururken de zemini bulsun).
            return Physics.BoxCast(rb.position, new Vector3(0.45f, 0.05f, 0.45f), Vector3.down,
                out _, Quaternion.identity, 0.6f, ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
