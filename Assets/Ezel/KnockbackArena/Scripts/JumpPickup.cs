using UnityEngine;
using ShooterParty;
using ShooterParty.UI;

namespace KnockbackArena
{
    // Jump power-up (pitch: jumping is strong, so it is a pickup). Floats, spins and breathes; a critter that
    // touches it gets a jump charge. Respawns after a while. Timings from the VFX brief.
    [RequireComponent(typeof(SphereCollider))]
    public class JumpPickup : MonoBehaviour
    {
        public Transform visual;
        public ParticleSystem collectFx;
        public int charges = 1;
        public float respawnTime = 8f;
        public float bobHeight = 0.12f, bobHz = 0.8f, spinDps = 90f;

        public event System.Action<CritterMotor> Collected;
        public bool Available => collectT < 0f && hiddenUntil < 0f;

        Vector3 basePos;
        float collectT = -1f, hiddenUntil = -1f, popT = -1f, phase;

        void Awake()
        {
            GetComponent<SphereCollider>().isTrigger = true;
            if (visual) basePos = visual.localPosition;
            phase = Random.value * 10f;
        }

        void Update()
        {
            if (!visual) return;
            float t = Time.time + phase;
            if (collectT >= 0f)
            {
                // 1 -> 1.3 in 60 ms, then -> 0 in 100 ms (ease in)
                collectT += Time.deltaTime;
                float s = collectT < 0.06f ? Mathf.Lerp(1f, 1.3f, collectT / 0.06f) : Mathf.Lerp(1.3f, 0f, Mathf.Pow(Mathf.Clamp01((collectT - 0.06f) / 0.1f), 2f));
                visual.localScale = Vector3.one * s;
                if (collectT > 0.16f) { visual.gameObject.SetActive(false); collectT = -1f; hiddenUntil = Time.time + respawnTime; }
                return;
            }
            if (hiddenUntil > 0f)
            {
                if (Time.time < hiddenUntil) return;
                hiddenUntil = -1f; popT = 0f;
                visual.gameObject.SetActive(true);
            }
            float pop = 1f;
            if (popT >= 0f) { popT += Time.deltaTime; pop = popT < 0.2f ? Mathf.Sin(popT / 0.2f * Mathf.PI * 0.75f) * 1.25f : 1f; if (popT > 0.2f) popT = -1f; }
            visual.localPosition = basePos + Vector3.up * (Mathf.Sin(t * bobHz * Mathf.PI * 2f) * bobHeight);
            visual.localRotation = Quaternion.Euler(0f, t * spinDps, 0f);
            visual.localScale = Vector3.one * (pop * (1f + 0.04f * Mathf.Sin(t * 1.6f * Mathf.PI * 2f)));
        }

        void OnTriggerEnter(Collider c)
        {
            if (!Available) return;
            var m = c.GetComponentInParent<CritterMotor>();
            if (!m) return;
            m.AddJumpCharge(charges);
            Fx.Play(collectFx, visual ? visual.position : transform.position, Quaternion.identity, Color.white);
            Sfx.Play("Pickup", transform.position);
            collectT = 0f;
            Collected?.Invoke(m);
        }
    }
}
