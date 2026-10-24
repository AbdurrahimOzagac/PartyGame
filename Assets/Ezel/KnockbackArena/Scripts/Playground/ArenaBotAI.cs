using UnityEngine;

namespace KnockbackArena.Playground
{
    // Local stand-in opponent for the arena demo (not networked, not the game's AI): picks the nearest rival,
    // keeps a spitting distance while strafing, stays off the edge, dashes back to safety when pushed out,
    // and sometimes detours for a jump pickup.
    [RequireComponent(typeof(CritterMotor), typeof(CritterSpit))]
    public class ArenaBotAI : MonoBehaviour
    {
        public float arenaHalf = 8f, edgeMargin = 2.2f;
        public Vector2 spitDelay = new Vector2(0.7f, 1.6f);
        public float aimError = 6f;

        CritterMotor motor;
        CritterSpit spit;
        Transform target;
        float retargetAt, spitAt, strafe, strafeFlipAt;

        void Awake()
        {
            motor = GetComponent<CritterMotor>();
            spit = GetComponent<CritterSpit>();
            strafe = Random.value < 0.5f ? -1f : 1f;
            spitAt = Time.time + Random.Range(1f, 2f);
        }

        void OnDisable() { if (motor) motor.Move(Vector3.zero); }

        void Update()
        {
            if (Time.time > retargetAt) { target = Nearest(); retargetAt = Time.time + 1.5f; }
            var pos = transform.position; pos.y = 0f;
            var move = Vector3.zero;
            if (target)
            {
                var to = target.position - transform.position; to.y = 0f;
                float d = to.magnitude;
                var dir = d > 0.01f ? to / d : transform.forward;
                if (Time.time > strafeFlipAt) { strafe = -strafe; strafeFlipAt = Time.time + Random.Range(1.2f, 2.8f); }
                move = dir * (d > 7f ? 1f : d < 4.5f ? -0.6f : 0f) + Vector3.Cross(Vector3.up, dir) * strafe * 0.55f;
                if (Time.time > spitAt && d < 11f)
                {
                    var aim = Quaternion.Euler(0f, Random.Range(-aimError, aimError), 0f) * dir;
                    if (spit.TryFire(aim)) spitAt = Time.time + Random.Range(spitDelay.x, spitDelay.y);
                }
            }
            var pickup = NearestPickup(6f);
            if (pickup && motor.jumpCharges == 0) { var tp = pickup.transform.position - transform.position; tp.y = 0f; move = Vector3.Lerp(move, tp.normalized, 0.7f); }
            // edge: steer home, and dash back when a knockback is carrying us out
            float edge = Mathf.Max(Mathf.Abs(pos.x), Mathf.Abs(pos.z));
            if (edge > arenaHalf - edgeMargin)
            {
                var home = -pos.normalized;
                move = Vector3.Lerp(move, home, Mathf.InverseLerp(arenaHalf - edgeMargin, arenaHalf - 0.5f, edge));
                if (Vector3.Dot(motor.HorizontalVelocity, -home) > 2.5f && !motor.Stunned) { motor.Move(home); motor.Dash(); }
            }
            motor.Move(Vector3.ClampMagnitude(move, 1f) * 0.85f);
            if (target && move.sqrMagnitude < 0.04f) motor.FaceTowards(target.position - transform.position, false);
        }

        Transform Nearest()
        {
            Transform best = null; float bd = float.MaxValue;
            foreach (var m in FindObjectsByType<CritterMotor>(FindObjectsSortMode.None))
            {
                if (m == motor || !m.isActiveAndEnabled) continue;
                float d = (m.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = m.transform; }
            }
            return best;
        }

        JumpPickup NearestPickup(float range)
        {
            JumpPickup best = null; float bd = range * range;
            foreach (var p in FindObjectsByType<JumpPickup>(FindObjectsSortMode.None))
            {
                if (!p.Available) continue;
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }
    }
}
