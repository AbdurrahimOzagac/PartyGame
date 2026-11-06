namespace ShooterParty
{
    // Per-client RPC budget on the server: a token bucket paces normal use; going past it drops the request, and
    // a client that keeps flooding (about 10x the rate for 5 s, spec §3) is kicked. Logging is the caller's, at most
    // once a second with the count, so a flood never becomes a log flood.
    public sealed class FloodGuard
    {
        public enum Verdict { Ok, Drop, Kick }
        public const double KickWindowSeconds = 5.0;

        readonly TokenBucket bucket;
        readonly int kickAfter;
        double windowStart = double.NegativeInfinity, lastLog = double.NegativeInfinity;
        int dropsInWindow, dropsSinceLog;

        public FloodGuard(float ratePerSecond, float burst)
        {
            bucket = new TokenBucket(ratePerSecond, burst);
            kickAfter = (int)(ratePerSecond * 9f * KickWindowSeconds);   // 1x is allowed, 9x more is dropped
        }

        public Verdict Take(double now)
        {
            if (bucket.Take(now)) return Verdict.Ok;
            if (now - windowStart > KickWindowSeconds) { windowStart = now; dropsInWindow = 0; }
            dropsInWindow++; dropsSinceLog++;
            return dropsInWindow > kickAfter ? Verdict.Kick : Verdict.Drop;
        }

        public bool LogDue(double now, out int dropped)
        {
            dropped = dropsSinceLog;
            if (dropped == 0 || now - lastLog < 1.0) return false;
            lastLog = now; dropsSinceLog = 0;
            return true;
        }
    }
}
