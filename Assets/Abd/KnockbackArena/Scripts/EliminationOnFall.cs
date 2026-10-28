using System;
using UnityEngine;

namespace Abd.KnockbackArena
{
    // Belirlenen Y'nin altına düşen oyuncu elenir: kontrolü kapanır, olay fırlatılır, kısa süre sonra silinir.
    public class EliminationOnFall : MonoBehaviour
    {
        [SerializeField] KnockbackArenaSettings settings;
        [SerializeField] float removeDelay = 2f;

        public bool IsEliminated { get; private set; }
        public event Action<EliminationOnFall> Eliminated;

        void Update()
        {
            if (!IsEliminated && transform.position.y < settings.killY)
                Eliminate();
        }

        void Eliminate()
        {
            IsEliminated = true;

            PlayerMotor motor = GetComponent<PlayerMotor>();
            if (motor != null) motor.enabled = false;
            PlayerShooter shooter = GetComponent<PlayerShooter>();
            if (shooter != null) shooter.enabled = false;

            Eliminated?.Invoke(this);
            Destroy(gameObject, removeDelay);
        }
    }
}
