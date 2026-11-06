using UnityEngine;

namespace ShooterParty
{
    public enum SpeedVerdict { Ok, Log, Kick }

    // Server-side sanity check of an owner-authoritative critter, fed with the owner's own timestamps (network
    // ticks), so a TCP stall that delivers samples in a burst never looks like a teleport. v1 never corrects:
    // it logs (at most once a second) above the cap, and kicks only above 3x the cap sustained for 2 s.
    public sealed class SpeedCheck
    {
        public const double KickAfterSeconds = 2.0;
        readonly float cap;
        double lastT = double.NaN, abuseSince = -1, lastLog = double.NegativeInfinity;
        Vector3 lastP;

        public SpeedCheck(float horizontalCap) { cap = horizontalCap; }

        public SpeedVerdict Feed(double ownerTime, Vector3 pos, bool recentKnockback)
        {
            if (!float.IsFinite(pos.x) || !float.IsFinite(pos.y) || !float.IsFinite(pos.z) || double.IsNaN(ownerTime)) return SpeedVerdict.Kick;   // no honest client sends NaN
            if (double.IsNaN(lastT)) { lastT = ownerTime; lastP = pos; return SpeedVerdict.Ok; }
            if (ownerTime <= lastT) return SpeedVerdict.Ok;   // same tick or reordered
            var d = pos - lastP; d.y = 0f;                    // falls are fast and legitimate
            double dt = ownerTime - lastT;
            float v = (float)(d.magnitude / dt);
            lastT = ownerTime; lastP = pos;
            if (recentKnockback) { abuseSince = -1; return SpeedVerdict.Ok; }
            if (v > 3f * cap)
            {
                if (abuseSince < 0) abuseSince = ownerTime - dt;
                if (ownerTime - abuseSince >= KickAfterSeconds) return SpeedVerdict.Kick;
            }
            else abuseSince = -1;
            if (v > cap && ownerTime - lastLog >= 1.0) { lastLog = ownerTime; return SpeedVerdict.Log; }
            return SpeedVerdict.Ok;
        }
    }
}
