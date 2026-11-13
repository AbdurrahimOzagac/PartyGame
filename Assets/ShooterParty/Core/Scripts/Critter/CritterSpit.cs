using System.Collections;
using UnityEngine;

namespace ShooterParty
{
    // Fires a SpitBlob. The blob leaves on a timer (Spit frame 4), not an animation event: events can
    // double-fire in crossfades and stop when the Animator is culled.
    public class CritterSpit : MonoBehaviour
    {
        public SpitBlob blobPrefab;
        public Material blobMaterial;         // the shooter's goo material; its colour also tints the FX
        public ParticleSystem popFx;          // little goo burst at the mouth on release
        public float speed = 12f;
        public float gravity = 3f;            // floaty arc (CritterConfig.shotGravity); 0 = straight shot
        public float cooldown = 10f / 30f;    // re-spit allowed from Spit frame 10
        public float knockback = 3.5f;        // ~1.2 m slide per hit on PM_Critter; playground value, tune per mode
        public const float SpawnHeight = 0.45f;   // blob centre above the feet: clear of the floor (radius 0.15), mid-body on a target

        CritterAnimator anim;
        CritterMotor motor;
        NetworkCritter net;
        Collider[] own;
        float readyAt;

        public bool Ready => Time.time >= readyAt;

        void Awake()
        {
            anim = GetComponentInChildren<CritterAnimator>();
            motor = GetComponent<CritterMotor>();
            net = GetComponent<NetworkCritter>();
            own = GetComponentsInChildren<Collider>();
        }

        public bool TryFire(Vector3 aimDir)
        {
            aimDir.y = 0f;
            if (!Ready || !blobPrefab || aimDir.sqrMagnitude < 1e-4f) return false;
            if (net && net.IsSpawned && (!net.Simulated || net.Eliminated.Value)) return false;   // online: only the owner shoots
            aimDir.Normalize();
            readyAt = Time.time + cooldown;
            if (motor) motor.FaceTowards(aimDir, true);
            if (anim) anim.PlaySpit();
            // the server checks the cooldown on this timestamp: taken at the press (spaced exactly by the cooldown),
            // not at the release, whose frame-rounded delay could bring two shots under the server's 0.9 x cooldown
            double clientTime = net && net.IsSpawned ? net.NetworkManager.LocalTime.Time : double.NaN;
            StartCoroutine(Release(aimDir, clientTime));
            return true;
        }

        // Launch velocity: straight along dir when the mode has no gravity, else with a small lift for a floaty arc.
        public Vector3 LaunchVelocity(Vector3 dir) => dir.normalized * speed + (gravity > 0f ? Vector3.up * 0.8f : Vector3.zero);

        IEnumerator Release(Vector3 dir, double clientTime)
        {
            yield return new WaitForSeconds(CritterAnimator.SpitReleaseTime);
            var muzzle = anim ? anim.Muzzle : null;
            var mouth = muzzle ? muzzle.position : transform.position + dir * 0.6f;
            // horizontally at the snout, but always SpawnHeight above the feet: the spit pose (and a run lean) can dip
            // a long snout so low that a flat shot touches the floor and splats at once
            var pos = new Vector3(mouth.x, transform.position.y + SpawnHeight, mouth.z) + dir * 0.08f;
            Fx.Play(popFx, pos, Quaternion.LookRotation(dir), Fx.ColourOf(blobMaterial));
            Sfx.Play("Spit", pos);
            if (net && net.IsSpawned && ShotSystem.Instance) { ShotSystem.Instance.Fire(net, pos, dir, clientTime); yield break; }
            var blob = Instantiate(blobPrefab, pos, Quaternion.LookRotation(dir));
            blob.gravity = gravity;
            blob.Launch(gameObject, LaunchVelocity(dir), knockback, blobMaterial, own);
        }
    }
}
