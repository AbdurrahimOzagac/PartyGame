using UnityEngine;

namespace ShooterParty
{
    // Movement feel for one critter: speed lines + snow spray while knocked back fast (V4), snow puff on landing
    // (V5), dash burst, footsteps. Listens to CritterMotor events; purely local presentation.
    [RequireComponent(typeof(CritterMotor))]
    public class CritterFx : MonoBehaviour
    {
        public ParticleSystem speedLines;          // child instance of VFX_SpeedLines (emits by distance)
        public ParticleSystem landPuff, dashBurst; // prefabs
        public float speedLineThreshold = 6f;

        CritterMotor motor;
        ParticleSystem[] lineSystems;
        Color tint = Color.white;
        float stepT;

        void Awake()
        {
            motor = GetComponent<CritterMotor>();
            var spit = GetComponent<CritterSpit>();
            if (spit) tint = Fx.ColourOf(spit.blobMaterial);
            if (speedLines) lineSystems = speedLines.GetComponentsInChildren<ParticleSystem>();
            motor.Landed += OnLanded;
            motor.Dashed += OnDashed;
            motor.Knocked += OnKnocked;
            motor.Jumped += () => Sfx.Play("Jump", transform.position);
        }

        void Update()
        {
            float v = motor.HorizontalVelocity.magnitude;
            if (lineSystems != null)
            {
                bool fast = v > speedLineThreshold || motor.Dashing;
                foreach (var ps in lineSystems)
                {
                    var e = ps.emission;
                    e.rateOverDistanceMultiplier = fast ? (ps == speedLines ? 6f : 4f) : 0f;
                }
            }
            // two footsteps per Run cycle (0.4 s at the authored 2.6 m/s), following the Animator's RunRate
            if (motor.Grounded && v > 0.6f && !motor.Dashing && !motor.Stunned)
            {
                stepT -= Time.deltaTime * Mathf.Clamp(v / CritterAnimator.RunAuthoredSpeed, 0.85f, 1.35f);
                if (stepT <= 0f) { Sfx.Play("Step", transform.position, 0.7f); stepT = 0.2f; }
            }
            else stepT = 0.05f;
        }

        void OnLanded(float speed)
        {
            if (speed < 2f) return;
            float k = Mathf.InverseLerp(2f, 9f, speed);
            var fx = Fx.Play(landPuff, transform.position, Quaternion.identity, tint);
            if (fx) fx.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.2f, k);
            Sfx.Play("Land", transform.position, Mathf.Lerp(0.35f, 1f, k));
            if (speed > 6f) CameraShake.Report(this, 0.12f);
        }

        void OnDashed()
        {
            var back = -motor.DashDirection;
            Fx.Play(dashBurst, transform.position, back.sqrMagnitude > 0.01f ? Quaternion.LookRotation(back) : transform.rotation, tint);
            Sfx.Play("Whoosh", transform.position, 0.7f, 1.25f);
        }

        void OnKnocked(Vector3 impulse)
        {
            if (impulse.magnitude > 3f) Sfx.Play("Whoosh", transform.position, 0.45f);
        }
    }
}
