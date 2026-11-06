using ShooterParty;
using UnityEngine;

namespace IceLoop
{
    // The race car under a critter (part of the critter prefab, so it follows the seat's colour): paints the body,
    // rolls the wheels with the speed, leans into a slide, trails ice spray off the rear wheels, and on the local
    // player's own car widens the camera with speed.
    public class IceCar : MonoBehaviour
    {
        public Renderer[] tinted = new Renderer[0];
        public Transform[] wheels = new Transform[0];   // pivots: spin about their local x
        public TrailRenderer[] trails = new TrailRenderer[0];
        public float wheelRadius = 0.3f, baseFov = 65f, fastFov = 76f;

        NetworkCritter critter;
        CritterMotor motor;
        int shown = -1;
        MaterialPropertyBlock block;
        Vector3 lastPos;
        float lean;

        void Awake() { critter = GetComponentInParent<NetworkCritter>(); motor = GetComponentInParent<CritterMotor>(); block = new MaterialPropertyBlock(); }
        void OnEnable() { lastPos = transform.position; foreach (var t in trails) if (t) t.Clear(); }

        void Update()
        {
            Tint();
            if (!motor) return;
            var v = motor.HorizontalVelocity;
            float speed = v.magnitude, fwd = Vector3.Dot(v, transform.forward), side = Vector3.Dot(v, transform.right);
            foreach (var w in wheels) if (w) w.Rotate(fwd / wheelRadius * Mathf.Rad2Deg * Time.deltaTime, 0f, 0f, Space.Self);

            lean = Mathf.Lerp(lean, Mathf.Clamp(-side * 1.6f, -9f, 9f), 1f - Mathf.Exp(-8f * Time.deltaTime));
            transform.localRotation = Quaternion.Euler(0f, 0f, lean);

            // a respawn is a jump of the whole car: do not draw a streak across the lake
            if ((transform.position - lastPos).sqrMagnitude > 16f) foreach (var t in trails) if (t) t.Clear();
            lastPos = transform.position;
            bool spray = speed > 4f && motor.Grounded;
            foreach (var t in trails) if (t) t.emitting = spray;

            if (critter && critter.IsOwner && !critter.IsBot.Value && Camera.main)
            {
                var cam = Camera.main;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(baseFov, fastFov, Mathf.InverseLerp(6f, 15f, speed)), 1f - Mathf.Exp(-3f * Time.deltaTime));
            }
        }

        void Tint()
        {
            if (!critter || !critter.IsSpawned || shown == critter.ColourIndex.Value) return;
            shown = critter.ColourIndex.Value;
            var colour = CritterLook.Colours[Mathf.Clamp(shown, 0, 3)];
            foreach (var r in tinted)
            {
                if (!r) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", colour);
                r.SetPropertyBlock(block);
            }
        }
    }
}
