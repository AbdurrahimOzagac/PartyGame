using ShooterParty;
using UnityEngine;

namespace IceLoop
{
    // Ice Loop's bot brain: follows the road on its own lane a few metres ahead, eases off before bends (ice does not
    // grip), pulls back to the middle when it nears the edge, dashes on the straights and spits at a rival ahead.
    // Runs only where the bot is simulated (the server) and only while the round is live.
    [RequireComponent(typeof(CritterMotor), typeof(CritterSpit))]
    public class IceBotAI : MonoBehaviour
    {
        public Vector2 spitDelay = new Vector2(1.2f, 2.6f);
        public float aimError = 5f;
        public float gripAccel = 7f;   // sideways m/s^2 a bot dares to use; the critter's ice acceleration is 9

        CritterMotor motor;
        CritterSpit spit;
        float lane, spitAt, dashAt, laneChangeAt;

        void Awake()
        {
            motor = GetComponent<CritterMotor>();
            spit = GetComponent<CritterSpit>();
            lane = Random.Range(-2f, 2f);
            spitAt = Time.time + Random.Range(2f, 4f);
            dashAt = Time.time + Random.Range(1f, 3f);
        }

        void OnDisable() { if (motor) motor.Move(Vector3.zero); }

        void Update()
        {
            var game = MiniGameController.Current;
            var track = IceTrack.Instance;
            if (!track || (game && game.IsSpawned && !game.Live.Value)) { motor.Move(Vector3.zero); return; }
            var pos = transform.position;
            int here = track.Nearest(pos);
            float speed = motor.HorizontalVelocity.magnitude;
            int ahead = here + Mathf.RoundToInt(Mathf.Lerp(5f, 9f, speed / 9f));
            if (Time.time > laneChangeAt) { lane = Random.Range(-track.halfWidth * 0.45f, track.halfWidth * 0.45f); laneChangeAt = Time.time + Random.Range(3f, 6f); }

            var aim = track.Lane(ahead, lane) - pos; aim.y = 0f;
            var dir = aim.normalized;

            // bends: the sharpest turn over the next ~18 m sets a top speed the ice can still hold (v = sqrt(grip / curvature))
            float turn = Mathf.Max(Vector3.Angle(track.Forward(here), track.Forward(here + 9)), Vector3.Angle(track.Forward(here + 4), track.Forward(here + 13)));
            float curvature = Mathf.Max(turn * Mathf.Deg2Rad / 18f, 1e-3f);
            float throttle = Mathf.Clamp(Mathf.Sqrt(gripAccel / curvature) / motor.moveSpeed, 0.45f, 1f);
            // the edge: hug the middle when the road's rim is close or the bot is sliding sideways
            float side = Vector3.Dot(pos - track.Point(here), track.Right(here));
            float rim = Mathf.InverseLerp(track.halfWidth - 2.2f, track.halfWidth - 0.6f, Mathf.Abs(side));
            if (rim > 0f) { dir = Vector3.Lerp(dir, -track.Right(here) * Mathf.Sign(side), rim).normalized; throttle = Mathf.Min(throttle, 0.6f); }

            motor.Move(dir * throttle);

            if (turn < 10f && rim <= 0f && Time.time > dashAt && motor.Dash()) dashAt = Time.time + Random.Range(2.5f, 5f);
            if (Time.time > spitAt) TrySpit(track, here);
        }

        void TrySpit(IceTrack track, int here)
        {
            var fwd = track.Forward(here);
            foreach (var m in FindObjectsByType<CritterMotor>(FindObjectsSortMode.None))
            {
                if (m == motor) continue;
                var nc = m.GetComponent<NetworkCritter>();
                if (nc && nc.IsSpawned && nc.Eliminated.Value) continue;
                var to = m.transform.position - transform.position; to.y = 0f;
                float d = to.magnitude;
                if (d < 1.5f || d > 10f || Vector3.Angle(fwd, to) > 40f) continue;
                var shot = Quaternion.Euler(0f, Random.Range(-aimError, aimError), 0f) * (to / d);
                if (spit.TryFire(shot)) spitAt = Time.time + Random.Range(spitDelay.x, spitDelay.y);
                return;
            }
        }
    }
}
