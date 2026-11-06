using NUnit.Framework;

namespace ShooterParty.Tests
{
    public class TokenBucketTests
    {
        [Test]
        public void BurstThenRefillAtRate()
        {
            var b = new TokenBucket(6f, 12f);
            for (int i = 0; i < 12; i++) Assert.IsTrue(b.Take(0), "burst " + i);
            Assert.IsFalse(b.Take(0), "13th in the same instant");
            Assert.IsTrue(b.Take(0.5)); Assert.IsTrue(b.Take(0.5)); Assert.IsTrue(b.Take(0.5));
            Assert.IsFalse(b.Take(0.5), "only 3 tokens refill in 0.5 s at 6/s");
        }

        [Test]
        public void NeverExceedsBurst()
        {
            var b = new TokenBucket(6f, 4f);
            b.Take(0);
            for (int i = 0; i < 4; i++) Assert.IsTrue(b.Take(100));
            Assert.IsFalse(b.Take(100));
        }
    }
}
