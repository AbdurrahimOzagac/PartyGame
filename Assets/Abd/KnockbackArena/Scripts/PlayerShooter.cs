using UnityEngine;

namespace Abd.KnockbackArena
{
    // Sol tık basılı tutulunca atış hızı sınırıyla ateş eder.
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor))]
    public class PlayerShooter : MonoBehaviour
    {
        [SerializeField] KnockbackArenaSettings settings;
        [SerializeField] Bullet bulletPrefab;
        [SerializeField] Transform muzzle;

        PlayerInputReader input;
        PlayerMotor motor;
        float nextFireTime;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            motor = GetComponent<PlayerMotor>();
        }

        void Update()
        {
            if (!input.FireHeld || Time.time < nextFireTime) return;
            nextFireTime = Time.time + 1f / settings.fireRate;
            Fire();
        }

        void Fire()
        {
            // Kamera karakterin tam arkasında ve onunla aynı yöne baktığı için ekran ortası (crosshair)
            // yönünün yatay bileşeni karakterin yönüyle aynı. Mermi her zaman yatay gider.
            Vector3 direction = Quaternion.Euler(0f, motor.Yaw, 0f) * Vector3.forward;
            Bullet bullet = Instantiate(bulletPrefab, muzzle.position, Quaternion.LookRotation(direction));
            bullet.Initialize(transform, direction, settings);
        }
    }
}
