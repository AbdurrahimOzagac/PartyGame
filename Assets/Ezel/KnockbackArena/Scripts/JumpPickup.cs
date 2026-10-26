using ShooterParty;
using Unity.Netcode;
using UnityEngine;

namespace KnockbackArena
{
    // Jump power-up (pitch: jumping is strong, so it is a pickup). Floats, spins and breathes; a critter that
    // touches it gets a jump charge, then it respawns after a while. Timings from the VFX brief.
    // Online the server decides who got it (availability is a NetworkVariable) and every machine animates the
    // change; offline (playground, tests) it works on its own as before.
    [RequireComponent(typeof(SphereCollider))]
    public class JumpPickup : NetworkBehaviour
    {
        public Transform visual;
        public ParticleSystem collectFx;
        public int charges = 1;
        public float respawnTime = 8f;
        public float bobHeight = 0.12f, bobHz = 0.8f, spinDps = 90f;

        public event System.Action<CritterMotor> Collected;
        public bool Available => IsSpawned ? available.Value : collectT < 0f && hiddenUntil < 0f;

        readonly NetworkVariable<bool> available = new NetworkVariable<bool>(true);
        Vector3 basePos;
        float collectT = -1f, hiddenUntil = -1f, popT = -1f, phase, serverRespawnAt = -1f;

        void Awake()
        {
            GetComponent<SphereCollider>().isTrigger = true;
            if (visual) basePos = visual.localPosition;
            phase = Random.value * 10f;
        }

        public override void OnNetworkSpawn()
        {
            available.OnValueChanged += OnAvailable;
            if (!available.Value && visual) visual.gameObject.SetActive(false);
        }

        public override void OnNetworkDespawn() => available.OnValueChanged -= OnAvailable;

        void OnAvailable(bool was, bool now)
        {
            if (!now) PlayCollect();
            else { hiddenUntil = 0.001f; }   // reappear on the next frame with the pop
        }

        void PlayCollect()
        {
            Fx.Play(collectFx, visual ? visual.position : transform.position, Quaternion.identity, Color.white);
            Sfx.Play("Pickup", transform.position);
            collectT = 0f;
        }

        void Update()
        {
            if (IsSpawned && IsServer && !available.Value && serverRespawnAt > 0f && Time.time >= serverRespawnAt)
            {
                serverRespawnAt = -1f;
                available.Value = true;
            }
            if (!visual) return;
            float t = Time.time + phase;
            if (collectT >= 0f)
            {
                // 1 -> 1.3 in 60 ms, then -> 0 in 100 ms (ease in)
                collectT += Time.deltaTime;
                float s = collectT < 0.06f ? Mathf.Lerp(1f, 1.3f, collectT / 0.06f) : Mathf.Lerp(1.3f, 0f, Mathf.Pow(Mathf.Clamp01((collectT - 0.06f) / 0.1f), 2f));
                visual.localScale = Vector3.one * s;
                if (collectT > 0.16f)
                {
                    visual.gameObject.SetActive(false);
                    collectT = -1f;
                    hiddenUntil = IsSpawned ? float.MaxValue : Time.time + respawnTime;   // online: wait for the server
                }
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
            var m = c.GetComponentInParent<CritterMotor>();
            if (!m) return;
            if (IsSpawned)
            {
                if (!IsServer || !available.Value) return;
                var nc = m.GetComponent<NetworkCritter>();
                if (!nc || !nc.IsSpawned || nc.Eliminated.Value || nc.Config.Value.jump != CritterConfig.JumpMode.Pickup) return;
                available.Value = false;
                serverRespawnAt = Time.time + respawnTime;
                nc.ServerAddJumpCharge(charges);
                Collected?.Invoke(m);
                return;
            }
            if (!Available) return;
            m.AddJumpCharge(charges);
            PlayCollect();
            Collected?.Invoke(m);
        }
    }
}
