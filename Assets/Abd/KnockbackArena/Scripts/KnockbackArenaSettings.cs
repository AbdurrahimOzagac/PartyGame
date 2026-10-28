using UnityEngine;

namespace Abd.KnockbackArena
{
    // Tüm dengeleme değerleri tek yerde. Oyun sırasında Inspector'dan değiştirilebilir.
    [CreateAssetMenu(menuName = "Abd/Knockback Arena/Settings", fileName = "KnockbackArenaSettings")]
    public class KnockbackArenaSettings : ScriptableObject
    {
        [Header("Movement")]
        public float moveSpeed = 8f;
        [Tooltip("Hedef hıza ulaşma ivmesi (m/s²). Yüksek = anında hareket hissi.")]
        public float moveAcceleration = 80f;
        [Tooltip("Kapalıysa havadayken (platformdan çıkınca) WASD etkisiz.")]
        public bool airControl = false;
        [Tooltip("Mouse hassasiyeti (piksel başına derece).")]
        public float mouseSensitivity = 0.12f;

        [Header("Shooting")]
        [Tooltip("Saniyedeki atış sayısı.")]
        public float fireRate = 4f;
        public float bulletSpeed = 25f;
        public float bulletLifetime = 3f;
        public float bulletRadius = 0.15f;

        [Header("Knockback")]
        [Tooltip("Mermi başına impulse. Hız değişimi = impulse / kütle.")]
        public float knockbackImpulse = 12f;
        [Tooltip("Her vuruşta kütle bununla çarpılır.")]
        [Range(0.01f, 1f)] public float massMultiplier = 0.9f;
        public float initialMass = 1f;
        [Tooltip("Vurulunca WASD'nin devre dışı kaldığı süre (sn).")]
        public float controlLockTime = 0.25f;
        [Tooltip("Kontrol kilidi sırasında savrulmanın sönme hızı.")]
        public float knockbackDrag = 1.5f;

        [Header("Camera")]
        public float cameraDistance = 6f;
        public float cameraPivotHeight = 1.5f;
        public float cameraStartPitch = 20f;
        public float cameraMinPitch = -10f;
        public float cameraMaxPitch = 60f;

        [Header("Arena")]
        [Tooltip("Bu Y'nin altına düşen oyuncu elenir.")]
        public float killY = -8f;
    }
}
