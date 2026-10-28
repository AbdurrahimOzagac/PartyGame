using UnityEngine;

namespace Abd.KnockbackArena
{
    // Gerçek bir obje ama fizik motoruna karışmaz: her karede önündeki yolu SphereCast ile tarar.
    // Böylece hızlı mermi hiçbir şeyin içinden geçmez ve vurduğu şeyi kendi kendine itmez.
    public class Bullet : MonoBehaviour
    {
        Transform owner;
        Vector3 direction;
        float speed;
        float radius;
        float lifeLeft;

        public void Initialize(Transform owner, Vector3 direction, KnockbackArenaSettings settings)
        {
            this.owner = owner;
            this.direction = direction.normalized;
            speed = settings.bulletSpeed;
            radius = settings.bulletRadius;
            lifeLeft = settings.bulletLifetime;
        }

        void Update()
        {
            float step = speed * Time.deltaTime;

            if (TryFindHit(step, out RaycastHit hit))
            {
                KnockbackReceiver receiver = hit.collider.GetComponentInParent<KnockbackReceiver>();
                if (receiver != null) receiver.ApplyHit(direction);
                Destroy(gameObject);
                return;
            }

            transform.position += direction * step;

            lifeLeft -= Time.deltaTime;
            if (lifeLeft <= 0f) Destroy(gameObject);
        }

        bool TryFindHit(float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            float best = float.MaxValue;

            RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, direction, distance,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in hits)
            {
                // Atan kişiye etki yok.
                if (owner != null && h.collider.transform.IsChildOf(owner)) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    nearest = h;
                    found = true;
                }
            }
            return found;
        }
    }
}
