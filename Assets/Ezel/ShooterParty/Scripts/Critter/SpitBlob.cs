using UnityEngine;

namespace ShooterParty
{
    // Physical spit projectile: a glossy candy-goo ball in the shooter's colour. SpitBlob.fbx blend shapes:
    // "Stretch" (convex egg along travel, +Z) and "Squash" (pancake). Timings follow the VFX brief:
    // squirts out (0.5 -> 1.15 -> 1.0 scale in 120 ms), jiggles at 7 Hz, is absorbed into a critter it hits
    // (squash, then sinks in and shrinks over 180 ms), splats on the ice and leaves a decal, pops on timeout.
    [RequireComponent(typeof(Rigidbody))]
    public class SpitBlob : MonoBehaviour
    {
        public float gravity = 3f;            // floaty arc; Rigidbody.useGravity is off
        public float lifetime = 2.5f;
        public Transform visual;              // scaled for pop/wobble; the collider stays constant
        public ParticleSystem impactFx;       // V2: goo droplets, white star, streaks, ice chips
        public ParticleSystem timeoutFx;      // little sparkle pop when it runs out of time
        public ParticleSystem trail;          // child: droplet chain + sparkles (world space)
        public TrailRenderer ribbon;          // child: soft 0.15 s ribbon

        enum State { Flying, Absorb, Splat, Pop }

        Rigidbody rb;
        SkinnedMeshRenderer smr;
        int stretch = -1, squash = -1;
        GameObject owner;
        Color colour = Color.white;
        float knockback, bornAt, endAt;
        Vector3 lastVelocity, sinkDir;
        State state;

        public bool Splatted => state != State.Flying;
        public Color Colour => colour;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            smr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (!visual && smr) visual = smr.transform.parent ? smr.transform.parent : smr.transform;
            if (smr && smr.sharedMesh)
            {
                stretch = smr.sharedMesh.GetBlendShapeIndex("Stretch");
                squash = smr.sharedMesh.GetBlendShapeIndex("Squash");
            }
        }

        public void Launch(GameObject shooter, Vector3 velocity, float impulse, Material mat, Collider[] ignore)
        {
            owner = shooter;
            knockback = impulse;
            bornAt = Time.time;
            rb.linearVelocity = lastVelocity = velocity;
            transform.rotation = Quaternion.LookRotation(velocity);
            var col = GetComponent<Collider>();
            if (ignore != null && col)
                foreach (var c in ignore) if (c) Physics.IgnoreCollision(col, c);
            colour = Fx.ColourOf(mat);
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>()) if (mat) r.sharedMaterial = mat;
            if (trail) Fx.Tint(trail, colour);
            if (ribbon)
            {
                ribbon.Clear();
                var g = ribbon.colorGradient;
                var ck = g.colorKeys;
                for (int i = 0; i < ck.Length; i++) ck[i].color = Color.Lerp(colour, Color.white, 0.25f);
                g.SetKeys(ck, g.alphaKeys);
                ribbon.colorGradient = g;
            }
            Apply(0.5f, 86f, 0f);
        }

        void FixedUpdate()
        {
            if (state != State.Flying) return;
            rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
            lastVelocity = rb.linearVelocity;
        }

        void Update()
        {
            float now = Time.time;
            switch (state)
            {
                case State.Flying:
                {
                    float t = now - bornAt;
                    var v = rb.linearVelocity;
                    if (v.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(v);
                    // squirt: 0.5 -> 1.15 at 70 ms (ease out) -> 1.0 at 120 ms
                    float scale = t < 0.07f ? Mathf.Lerp(0.5f, 1.15f, 1f - (1f - t / 0.07f) * (1f - t / 0.07f))
                                : t < 0.12f ? Mathf.Lerp(1.15f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.07f) / 0.05f)) : 1f;
                    // stretch 1 + 0.25 * speed/12 (full key = 1.35 long), starting extra long as it leaves the mouth
                    float flightW = Mathf.Clamp01(v.magnitude / 12f) * 0.25f / 0.35f * 100f;
                    float w = Mathf.Lerp(86f, flightW, Mathf.Clamp01(t / 0.12f));
                    Apply(scale, w, 0.04f + 0.08f * Mathf.Exp(-t / 0.15f));
                    if (t > lifetime) Begin(State.Pop, 0.1f);
                    break;
                }
                case State.Absorb:
                {
                    // 0-60 ms flatten on the surface, 60-180 ms sink in and shrink away (sells the mass loss)
                    float k = 1f - (endAt - now) / 0.18f;
                    if (squash >= 0) smr.SetBlendShapeWeight(squash, Mathf.Clamp01(k / 0.33f) * 100f);
                    float s = k < 0.33f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.33f) / 0.67f);
                    if (visual) visual.localScale = Vector3.one * Mathf.Max(0f, s);
                    if (k > 0.33f) transform.position += sinkDir * (0.1f / 0.12f * Time.deltaTime);
                    break;
                }
                case State.Splat:
                {
                    float k = 1f - (endAt - now) / 0.18f;
                    if (squash >= 0) smr.SetBlendShapeWeight(squash, Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI * 0.5f) * 100f);
                    if (visual) visual.localScale = Vector3.one * Mathf.Lerp(1.15f, 0f, Mathf.Clamp01((k - 0.55f) / 0.45f));
                    break;
                }
                case State.Pop:
                {
                    float k = 1f - (endAt - now) / 0.1f;
                    if (visual) visual.localScale = Vector3.one * (k < 0.4f ? Mathf.Lerp(1f, 1.2f, k / 0.4f) : Mathf.Lerp(1.2f, 0f, (k - 0.4f) / 0.6f));
                    break;
                }
            }
            if (state != State.Flying && now >= endAt) Finish();
        }

        void Apply(float scale, float stretchW, float wobble)
        {
            if (stretch >= 0 && smr) smr.SetBlendShapeWeight(stretch, stretchW);
            if (!visual) return;
            // volume-preserving 7 Hz jiggle across the travel axis, layered on the pop scale
            float s = Mathf.Sin((Time.time - bornAt) * 7f * Mathf.PI * 2f) * wobble;
            visual.localScale = new Vector3(1f + s, 1f - s, 1f) * scale;
        }

        void Begin(State s, float duration)
        {
            state = s;
            endAt = Time.time + duration;
            rb.isKinematic = true;
            var col = GetComponent<Collider>();
            if (col) col.enabled = false;
            if (stretch >= 0 && smr) smr.SetBlendShapeWeight(stretch, 0f);
            // let the trail finish on its own instead of vanishing with the blob
            if (trail) { trail.transform.SetParent(null, true); trail.Stop(true, ParticleSystemStopBehavior.StopEmitting); var m = trail.main; m.stopAction = ParticleSystemStopAction.Destroy; }
            if (ribbon) { ribbon.transform.SetParent(null, true); ribbon.emitting = false; ribbon.autodestruct = true; }
            if (s == State.Pop)
            {
                Fx.Play(timeoutFx, transform.position, Quaternion.identity, colour);
                Sfx.Play("BlobPop", transform.position, 0.5f);
            }
        }

        void Finish()
        {
            if (state == State.Splat) SplatDecals.Add(transform.position, -transform.forward, colour);
            Destroy(gameObject);
        }

        void OnCollisionEnter(Collision c)
        {
            if (state != State.Flying) return;
            var contact = c.GetContact(0);
            var dir = lastVelocity; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : transform.forward;
            var motor = c.collider.GetComponentInParent<CritterMotor>();
            bool critter = motor && motor.gameObject != owner;
            if (critter)
            {
                motor.Knockback(dir * knockback + Vector3.up * knockback * 0.25f);
                CameraShake.Report(owner ? owner.transform : null, 0.08f);
                CameraShake.Report(motor, 0.45f);
                Sfx.Play("Hit", contact.point);
            }
            else Sfx.Play("Splat", contact.point, 0.8f);
            Fx.Play(impactFx, contact.point, Quaternion.LookRotation(contact.normal), colour);
            transform.SetPositionAndRotation(contact.point, Quaternion.LookRotation(-contact.normal));
            sinkDir = -contact.normal;
            if (critter) transform.SetParent(motor.transform, true);   // ride along while it sinks in
            Begin(critter ? State.Absorb : State.Splat, 0.18f);
        }
    }
}
