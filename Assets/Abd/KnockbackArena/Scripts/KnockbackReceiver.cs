using System;
using UnityEngine;

namespace Abd.KnockbackArena
{
    // Mermi bir oyuncuya/hedefe değince çağrılır: savurur, kütleyi azaltır, kontrolü kısa süre kilitler.
    [RequireComponent(typeof(Rigidbody))]
    public class KnockbackReceiver : MonoBehaviour
    {
        [SerializeField] KnockbackArenaSettings settings;
        [Tooltip("Kontrol kilidi bittikten sonra da savrulmayı söndür (hareket script'i olmayan hedefler için).")]
        [SerializeField] bool dragWhenUnlocked;

        Rigidbody rb;
        float lockedUntil;

        public bool IsControlLocked => Time.time < lockedUntil;
        public float MassRatio => rb.mass / settings.initialMass;
        public event Action<KnockbackReceiver> Hit;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = settings.initialMass;
        }

        public void ApplyHit(Vector3 bulletDirection)
        {
            bulletDirection.y = 0f;
            if (bulletDirection.sqrMagnitude < 1e-6f) return;
            bulletDirection.Normalize();

            // Önce mevcut kütleyle savur, sonra kütleyi azalt. VelocityChange kullanıyoruz ki
            // sonradan değişen kütle bu vuruşun etkisini değiştirmesin.
            rb.AddForce(bulletDirection * (settings.knockbackImpulse / rb.mass), ForceMode.VelocityChange);
            rb.mass *= settings.massMultiplier;

            lockedUntil = Time.time + settings.controlLockTime;
            Hit?.Invoke(this);
        }

        void FixedUpdate()
        {
            if (!dragWhenUnlocked && !IsControlLocked) return;

            Vector3 v = rb.linearVelocity;
            float k = Mathf.Exp(-settings.knockbackDrag * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(v.x * k, v.y, v.z * k);
        }
    }
}
