using UnityEngine;

namespace Abd.KnockbackArena
{
    // Kütle azaldıkça küpün rengi soluklaşır.
    [RequireComponent(typeof(KnockbackReceiver))]
    public class MassColorFeedback : MonoBehaviour
    {
        [SerializeField] Renderer target;
        [SerializeField] Color paleColor = new Color(0.92f, 0.92f, 0.92f);

        KnockbackReceiver receiver;
        Material material;
        Color baseColor;

        void Awake()
        {
            receiver = GetComponent<KnockbackReceiver>();
            if (target == null) target = GetComponent<Renderer>();
            material = target.material; // oyuncuya özel kopya
            baseColor = material.color;
            receiver.Hit += OnHit;
        }

        void OnDestroy()
        {
            receiver.Hit -= OnHit;
            if (material != null) Destroy(material);
        }

        void OnHit(KnockbackReceiver r)
        {
            material.color = Color.Lerp(baseColor, paleColor, Mathf.Clamp01(1f - r.MassRatio));
        }
    }
}
