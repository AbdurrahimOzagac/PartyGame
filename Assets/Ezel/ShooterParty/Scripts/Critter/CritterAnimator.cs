using UnityEngine;

namespace ShooterParty
{
    // Bridges gameplay to the CubeCritter Animator, and layers random blinks on top of whatever the clips do.
    // Clip timings come from the Blender source (projects/game_assets/knockback_arena/blender/v2/clips.py).
    [RequireComponent(typeof(Animator))]
    public class CritterAnimator : MonoBehaviour
    {
        public const float RunAuthoredSpeed = 2.6f;     // m/s the Run cycle was keyed at (planted feet don't slide)
        public const float SpitReleaseTime = 4f / 30f;  // the blob leaves the mouth on Spit frame 4
        public const float SpitLength = 22f / 30f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int RunRateId = Animator.StringToHash("RunRate");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int SpitId = Animator.StringToHash("Spit");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int WinId = Animator.StringToHash("Win");
        static readonly int LoseId = Animator.StringToHash("Lose");
        static readonly int TauntId = Animator.StringToHash("Taunt");

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        Animator anim;
        SkinnedMeshRenderer face;
        MaterialPropertyBlock flashBlock;
        int blink = -1, happy = -1, hurt = -1, actionLayer = -1, bodySlot = -1;
        float nextBlink, blinkT = -1f, actionWeight, actionUntil, flashT = -1f;
        float stopUntil, stopLen, jiggleT = -1f, hitStrength;

        public Transform Muzzle { get; private set; }

        void Awake()
        {
            anim = GetComponent<Animator>();
            face = GetComponentInChildren<SkinnedMeshRenderer>();
            if (face && face.sharedMesh)
            {
                blink = face.sharedMesh.GetBlendShapeIndex("Blink");
                happy = face.sharedMesh.GetBlendShapeIndex("EyesHappy");
                hurt = face.sharedMesh.GetBlendShapeIndex("EyesHurt");
                var mats = face.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] && mats[i].name.StartsWith("M_CC_Body")) bodySlot = i;
            }
            flashBlock = new MaterialPropertyBlock();
            actionLayer = anim.GetLayerIndex("Action");
            Muzzle = FindDeep(transform, "Muzzle");
            nextBlink = Time.time + Random.Range(1f, 4f);
        }

        public void SetLocomotion(float horizontalSpeed, bool grounded)
        {
            anim.SetFloat(SpeedId, horizontalSpeed);
            anim.SetFloat(RunRateId, Mathf.Clamp(horizontalSpeed / RunAuthoredSpeed, 0.85f, 1.35f));
            anim.SetBool(GroundedId, grounded);
        }

        public void PlaySpit() { anim.SetTrigger(SpitId); actionUntil = Time.time + SpitLength; }
        // strength 0..1 scales the hit-stop (60-90 ms) and the jiggle. Visual only: the Rigidbody keeps simulating,
        // so this is safe under networking (never Time.timeScale).
        public void PlayHit(float strength = 1f)
        {
            anim.SetTrigger(HitId);
            flashT = 0f;
            hitStrength = Mathf.Clamp01(strength);
            stopLen = Mathf.Lerp(0.06f, 0.09f, hitStrength);
            stopUntil = Time.time + stopLen;
        }
        public void PlayJump() => anim.SetTrigger(JumpId);
        public void PlayWin() => anim.SetTrigger(WinId);
        public void PlayLose() => anim.SetTrigger(LoseId);
        public void PlayTaunt() => anim.SetTrigger(TauntId);

        void Update()
        {
            if (stopUntil > 0f)
            {
                if (Time.time < stopUntil)
                {
                    // hit-stop: freeze the pose and shake sideways, large then shrinking
                    anim.speed = 0f;
                    float k = (stopUntil - Time.time) / stopLen;
                    transform.localPosition = Vector3.right * (0.04f * k * Mathf.Sin(Time.time * 110f));
                }
                else { anim.speed = 1f; transform.localPosition = Vector3.zero; stopUntil = 0f; jiggleT = 0f; }
            }
            if (jiggleT >= 0f)
            {
                // absorb jiggle: y 0.85 / xz 1.08 at the peak, damped, ~2.5 cycles in 300 ms (pivot is at the feet)
                jiggleT += Time.deltaTime;
                float e = Mathf.Exp(-jiggleT / 0.11f) * Mathf.Cos(jiggleT * Mathf.PI * 2f * 8.3f) * (0.5f + 0.5f * hitStrength);
                transform.localScale = new Vector3(1f + 0.08f * e, 1f - 0.15f * e, 1f + 0.08f * e);
                if (jiggleT > 0.4f) { transform.localScale = Vector3.one; jiggleT = -1f; }
            }
            if (flashT >= 0f && bodySlot >= 0)
            {
                // white flash on the body (body material has _EMISSION on, black by default): hold 40 ms, ease out 100 ms
                flashT += Time.deltaTime;
                float k = flashT < 0.04f ? 1f : Mathf.Clamp01(1f - (flashT - 0.04f) / 0.1f);
                face.GetPropertyBlock(flashBlock, bodySlot);
                flashBlock.SetColor(EmissionId, Color.white * (0.8f * k * k));
                face.SetPropertyBlock(flashBlock, bodySlot);
                if (k <= 0f) flashT = -1f;
            }
            // the Action layer (upper body + face) is only on while a spit plays, so it never freezes the run
            if (actionLayer < 0) return;
            actionWeight = Mathf.MoveTowards(actionWeight, Time.time < actionUntil ? 1f : 0f, Time.deltaTime / 0.06f);
            anim.SetLayerWeight(actionLayer, actionWeight);
        }

        void LateUpdate()
        {
            // runs after the Animator wrote this frame's blend shapes, so the blink adds on top of the clip
            if (blink < 0) return;
            if (blinkT < 0f && Time.time >= nextBlink) blinkT = 0f;
            if (blinkT < 0f) return;
            blinkT += Time.deltaTime;
            bool eyesBusy = (happy >= 0 && face.GetBlendShapeWeight(happy) > 30f) || (hurt >= 0 && face.GetBlendShapeWeight(hurt) > 30f);
            if (!eyesBusy)
            {
                float k = blinkT < 0.07f ? blinkT / 0.07f : 1f - Mathf.Clamp01((blinkT - 0.09f) / 0.11f); // fast close, slower open
                face.SetBlendShapeWeight(blink, Mathf.Max(face.GetBlendShapeWeight(blink), k * 100f));
            }
            if (blinkT >= 0.2f)
            {
                blinkT = -1f;
                nextBlink = Time.time + (Random.value < 0.2f ? 0.25f : Random.Range(2f, 6f)); // 20% double blinks
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r) return r;
            }
            return null;
        }
    }
}
