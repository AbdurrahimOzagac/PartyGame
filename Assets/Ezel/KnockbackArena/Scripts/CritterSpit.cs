using System.Collections;
using UnityEngine;

namespace KnockbackArena
{
    // Fires a SpitBlob. The blob leaves on a timer (Spit frame 4), not an animation event: events can
    // double-fire in crossfades and stop when the Animator is culled.
    public class CritterSpit : MonoBehaviour
    {
        public SpitBlob blobPrefab;
        public Material blobMaterial;         // the shooter's goo material; its colour also tints the FX
        public ParticleSystem popFx;          // little goo burst at the mouth on release
        public float speed = 12f;
        public float cooldown = 10f / 30f;    // re-spit allowed from Spit frame 10
        public float knockback = 3.5f;        // ~1.2 m slide per hit on PM_Critter; playground value, tune per mode

        CritterAnimator anim;
        CritterMotor motor;
        Collider[] own;
        float readyAt;

        public bool Ready => Time.time >= readyAt;

        void Awake()
        {
            anim = GetComponentInChildren<CritterAnimator>();
            motor = GetComponent<CritterMotor>();
            own = GetComponentsInChildren<Collider>();
        }

        public bool TryFire(Vector3 aimDir)
        {
            aimDir.y = 0f;
            if (!Ready || !blobPrefab || aimDir.sqrMagnitude < 1e-4f) return false;
            aimDir.Normalize();
            readyAt = Time.time + cooldown;
            if (motor) motor.FaceTowards(aimDir, true);
            if (anim) anim.PlaySpit();
            StartCoroutine(Release(aimDir));
            return true;
        }

        IEnumerator Release(Vector3 dir)
        {
            yield return new WaitForSeconds(CritterAnimator.SpitReleaseTime);
            var muzzle = anim ? anim.Muzzle : null;
            var pos = muzzle ? muzzle.position + dir * 0.08f : transform.position + Vector3.up * 0.4f + dir * 0.6f;
            var blob = Instantiate(blobPrefab, pos, Quaternion.LookRotation(dir));
            blob.Launch(gameObject, dir * speed + Vector3.up * 0.8f, knockback, blobMaterial, own);
            Fx.Play(popFx, pos, Quaternion.LookRotation(dir), Fx.ColourOf(blobMaterial));
            Sfx.Play("Spit", pos);
        }
    }
}
