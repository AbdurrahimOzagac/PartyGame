using UnityEngine;

namespace IceLoop
{
    // The loop's centre line, baked into the scene by IceLoopSetup. The server reads it for checkpoints and falls,
    // the bots read it to steer. Index 0 is the start line; the lap runs 0 -> Count -> 0 (counter-clockwise).
    public class IceTrack : MonoBehaviour
    {
        public static IceTrack Instance { get; private set; }

        public Vector3[] centre = new Vector3[0];   // one point every ~2 m, closed loop
        public float halfWidth = 4f;
        public int gateStride = 10;                  // a checkpoint gate every gateStride points (~20 m)
        public float waterY = -2f;                   // the lake's surface; a critter below it is in the water

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public int Count => centre.Length;
        public int GateCount => Mathf.Max(1, Count / gateStride);
        int Wrap(int i) => ((i % Count) + Count) % Count;

        public Vector3 Point(int i) => centre[Wrap(i)];
        public Vector3 Forward(int i) { var t = Point(i + 1) - Point(i - 1); t.y = 0f; return t.normalized; }
        public Vector3 Right(int i) { var f = Forward(i); return new Vector3(f.z, 0f, -f.x); }
        public Vector3 Lane(int i, float offset) => Point(i) + Right(i) * offset;

        public Pose GatePose(int gate)
        {
            int i = Wrap(gate * gateStride);
            return new Pose(Point(i), Quaternion.LookRotation(Forward(i)));
        }

        // Where a critter stands after a fall: just past the gate, spread across the road by seat.
        public Pose RespawnPose(int gate, int slot)
        {
            int i = Wrap(gate * gateStride);
            var pos = Point(i) + Forward(i) * 1.5f + Right(i) * ((slot - 1.5f) * 1.6f) + Vector3.up * 0.35f;
            return new Pose(pos, Quaternion.LookRotation(Forward(i)));
        }

        // Start grid: two columns behind the line, seat 0 in front.
        public Pose GridPose(int slot)
        {
            int i = Wrap(-(2 + 2 * (slot / 2)));
            var pos = Point(i) + Right(i) * ((slot % 2 == 0 ? -1f : 1f) * 1.4f) + Vector3.up * 0.05f;
            return new Pose(pos, Quaternion.LookRotation(Forward(i)));
        }

        public int Nearest(Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < centre.Length; i++)
            {
                var d = centre[i] - p; d.y = 0f;
                float s = d.sqrMagnitude;
                if (s < bd) { bd = s; best = i; }
            }
            return best;
        }

        // Signed distance of p past gate `gate`'s plane (negative = before it). inDoor: within the arch's width and height.
        public float GateSide(Vector3 p, int gate, out bool inDoor)
        {
            int i = Wrap(gate * gateStride);
            var d = p - Point(i);
            inDoor = Mathf.Abs(Vector3.Dot(d, Right(i))) < halfWidth + 1.5f && Mathf.Abs(d.y) < 3f;
            return Vector3.Dot(d, Forward(i));
        }

        // In the lake. Detected at the water line so the splash and the respawn match what the player sees.
        public bool Fell(Vector3 p) => p.y < waterY - 0.5f;

        void OnDrawGizmos()
        {
            for (int i = 0; i < centre.Length; i++)
            {
                Gizmos.color = i % gateStride == 0 ? Color.yellow : Color.cyan;
                Gizmos.DrawLine(centre[i], centre[(i + 1) % centre.Length]);
            }
        }
    }

    // The control points of the lap and the Catmull-Rom resampling. Shared by the scene builder and the tests.
    // Vector3(x, height, z) in metres. A 400 m counter-clockwise loop: the start straight along +x through the origin, a long left
    // sweeper, the hill (up to 7.5 m, 12 % at the steepest), a tight left-hander, an S-bend, a hairpin and a chicane
    // back onto the straight. The tightest bend has a 4.8 m radius, so an 8 m wide road never folds over itself.
    public static class IceTrackLayout
    {
        public static readonly Vector3[] Controls =
        {
            new Vector3(-30f, 0f, -8f), new Vector3(-14f, 0f, 0f), new Vector3(8f, 0f, 0f), new Vector3(34f, 0.0f, 1f),
            new Vector3(54f, 0.5f, 14f), new Vector3(64f, 2.5f, 36f), new Vector3(54f, 5.5f, 58f), new Vector3(30f, 7.5f, 68f),
            new Vector3(12f, 6.5f, 58f), new Vector3(14f, 4f, 38f), new Vector3(-2f, 2.5f, 26f), new Vector3(-24f, 1.5f, 28f),
            new Vector3(-46f, 0.5f, 42f), new Vector3(-68f, 0f, 34f), new Vector3(-78f, 0f, 10f), new Vector3(-66f, 0f, -18f),
            new Vector3(-46f, 0f, -22f),
        };

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) =>
            0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);

        // Evenly spaced points along the closed spline (x, y = height, z), starting at the start line (nearest the origin).
        public static Vector3[] Resample(float spacing = 2f)
        {
            int n = Controls.Length;
            var dense = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < n; i++)
                for (int k = 0; k < 40; k++)
                    dense.Add(CatmullRom(Controls[(i + n - 1) % n], Controls[i], Controls[(i + 1) % n], Controls[(i + 2) % n], k / 40f));
            float total = 0f;
            for (int i = 0; i < dense.Count; i++) total += Vector3.Distance(dense[i], dense[(i + 1) % dense.Count]);
            int count = Mathf.RoundToInt(total / spacing / 10f) * 10;   // a multiple of 10: whole gates
            var pts = new Vector3[count];
            float step = total / count, walked = 0f, next = 0f;
            int c = 0;
            for (int i = 0; i < dense.Count && c < count; i++)
            {
                var a = dense[i]; var b = dense[(i + 1) % dense.Count];
                float seg = Vector3.Distance(a, b);
                while (c < count && next <= walked + seg + 1e-4f)
                {
                    pts[c++] = Vector3.Lerp(a, b, seg > 1e-6f ? (next - walked) / seg : 0f);
                    next += step;
                }
                walked += seg;
            }
            int start = 0; float bd = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var p = pts[i];
                float d = new Vector2(p.x, p.z).sqrMagnitude;
                if (d < bd) { bd = d; start = i; }
            }
            var rotated = new Vector3[count];
            for (int i = 0; i < count; i++) rotated[i] = pts[(start + i) % count];
            return rotated;
        }
    }
}
