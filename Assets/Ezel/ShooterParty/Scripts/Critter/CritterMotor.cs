using UnityEngine;

namespace ShooterParty
{
    // Rigidbody locomotion for a critter: in-place clips, so this is what actually moves it.
    // Inputs come from a player (LocalCritterInput) or an AI; hits arrive through Knockback().
    // Pitch rules wired here: every hit lowers mass (not size), Shift dash with charges, jump as a pickup charge.
    [RequireComponent(typeof(Rigidbody))]
    public class CritterMotor : MonoBehaviour
    {
        public float moveSpeed = 3.5f;       // 1.35x the authored run speed: the fastest Animator rate without foot slide
        public float acceleration = 28f;
        public float airControl = 0.35f;
        public float turnSpeed = 900f;
        public float jumpSpeed = 5.5f;
        public float hitStun = 0.45f;
        public float respawnBelowY = -8f;    // playground only; a match removes fallen critters itself (set -Infinity)

        [Header("Mass (every hit lowers it; the same shot then pushes further)")]
        public float massLossPerHit = 0f;    // 0 = off (playground); the arena mode sets it
        public float minMass = 0.35f;

        [Header("Charges (-1 = unlimited)")]
        public int jumpCharges = -1;         // the pitch makes jump a pickup: JumpPickup adds charges
        public int dashCharges = 3, maxDashCharges = 3;
        public float dashRecharge = 1.6f, dashSpeed = 9f, dashTime = 0.16f;

        public event System.Action<float> Landed;     // impact speed (m/s, positive)
        public event System.Action Dashed, Jumped;
        public event System.Action<Vector3> Knocked;  // impulse

        Rigidbody rb;
        CritterAnimator anim;
        Vector3 moveInput, faceDir, spawnPos, dashDir;
        Quaternion spawnRot;
        float stunUntil, dashUntil, rechargeAt, fallSpeed;
        bool jumpQueued, wasGrounded = true;

        public bool Grounded { get; private set; }
        public bool Stunned => Time.time < stunUntil;
        public bool Dashing => Time.time < dashUntil;
        public Vector3 DashDirection => dashDir;
        public float StartMass { get; private set; }
        public float MassFraction => StartMass > 0f ? rb.mass / StartMass : 1f;
        public float DashRechargeProgress => dashCharges >= maxDashCharges ? 1f : 1f - Mathf.Clamp01((rechargeAt - Time.time) / dashRecharge);
        public Vector3 HorizontalVelocity { get { var v = rb.linearVelocity; v.y = 0f; return v; } }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            anim = GetComponentInChildren<CritterAnimator>();
            StartMass = rb.mass;
            spawnPos = transform.position;
            spawnRot = transform.rotation;
            faceDir = transform.forward;
        }

        public void Move(Vector3 worldDir)
        {
            worldDir.y = 0f;
            moveInput = Vector3.ClampMagnitude(worldDir, 1f);
            if (moveInput.sqrMagnitude > 0.01f) faceDir = moveInput.normalized;
        }

        public void Jump() { if (Grounded && !Stunned && jumpCharges != 0) jumpQueued = true; }
        public void AddJumpCharge(int n = 1) { if (jumpCharges >= 0) jumpCharges += n; }

        public bool Dash()
        {
            if (Stunned || Dashing || dashCharges == 0) return false;
            if (dashCharges > 0) { if (dashCharges == maxDashCharges) rechargeAt = Time.time + dashRecharge; dashCharges--; }
            dashDir = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : faceDir;
            dashUntil = Time.time + dashTime;
            Dashed?.Invoke();
            return true;
        }

        public void FaceTowards(Vector3 dir, bool instant)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            faceDir = dir.normalized;
            if (instant) rb.MoveRotation(Quaternion.LookRotation(faceDir));
        }

        public void Knockback(Vector3 impulse)
        {
            rb.AddForce(impulse / rb.mass, ForceMode.VelocityChange);   // this hit uses the mass before it lands
            if (massLossPerHit > 0f) rb.mass = Mathf.Max(minMass * StartMass, rb.mass - massLossPerHit * StartMass);
            stunUntil = Time.time + hitStun;
            dashUntil = 0f;
            if (anim) anim.PlayHit(Mathf.Clamp01(impulse.magnitude / 5f));
            Knocked?.Invoke(impulse);
        }

        void FixedUpdate()
        {
            // the sphere starts inside our own collider, so the cast ignores it and only finds the ground
            Grounded = Physics.SphereCast(transform.position + Vector3.up * 0.3f, 0.25f, Vector3.down, out _, 0.17f,
                                          ~0, QueryTriggerInteraction.Ignore);
            var v = rb.linearVelocity;
            if (!Grounded) fallSpeed = Mathf.Max(fallSpeed, -v.y);
            if (Grounded && !wasGrounded) { Landed?.Invoke(fallSpeed); fallSpeed = 0f; }
            wasGrounded = Grounded;
            var h = new Vector3(v.x, 0f, v.z);
            if (Dashing) rb.linearVelocity = new Vector3(dashDir.x * dashSpeed, v.y, dashDir.z * dashSpeed);
            else if (!Stunned)
            {
                float a = acceleration * (Grounded ? 1f : airControl) * Time.fixedDeltaTime;
                h = Vector3.MoveTowards(h, moveInput * moveSpeed, a);
                if (jumpQueued)
                {
                    v.y = jumpSpeed * Mathf.Lerp(1.25f, 1f, MassFraction);   // a lighter cube jumps higher (pitch)
                    if (jumpCharges > 0) jumpCharges--;
                    if (anim) anim.PlayJump();
                    Jumped?.Invoke();
                }
                rb.linearVelocity = new Vector3(h.x, v.y, h.z);
                var target = Quaternion.LookRotation(faceDir);
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, target, turnSpeed * Time.fixedDeltaTime));
            }
            jumpQueued = false;
            if (dashCharges >= 0 && dashCharges < maxDashCharges && Time.time >= rechargeAt)
            {
                dashCharges++;
                rechargeAt = Time.time + dashRecharge;
            }
            if (transform.position.y < respawnBelowY) Respawn();
        }

        void Update()
        {
            if (anim) anim.SetLocomotion(HorizontalVelocity.magnitude, Grounded);
        }

        public void Respawn()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = spawnPos + Vector3.up * 2f;
            rb.rotation = spawnRot;
            rb.mass = StartMass;
            faceDir = spawnRot * Vector3.forward;
            stunUntil = 0f;
        }
    }
}
