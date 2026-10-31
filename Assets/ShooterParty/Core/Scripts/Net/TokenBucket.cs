namespace ShooterParty
{
    // Per-client rate limit for RPCs: `rate` tokens per second, up to `burst` saved. Burst covers TCP stalls that
    // deliver a few seconds of honest traffic at once.
    public sealed class TokenBucket
    {
        readonly float rate, burst;
        float tokens;
        double last = double.NaN;

        public TokenBucket(float ratePerSecond, float burst) { rate = ratePerSecond; this.burst = burst; tokens = burst; }

        public bool Take(double now, float cost = 1f)
        {
            if (!double.IsNaN(last) && now > last) tokens = System.Math.Min(burst, tokens + (float)(now - last) * rate);
            last = now;
            if (tokens < cost) return false;
            tokens -= cost;
            return true;
        }
    }
}
