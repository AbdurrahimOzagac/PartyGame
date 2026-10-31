using NUnit.Framework;

namespace ShooterParty.Tests
{
    public class FloodGuardTests
    {
        [Test]
        public void NormalPaceIsFine()
        {
            var g = new FloodGuard(20f, 40f);
            for (int i = 0; i < 200; i++) Assert.AreEqual(FloodGuard.Verdict.Ok, g.Take(i / 20.0));
        }

        [Test]
        public void BurstsAreDroppedNotKicked()
        {
            var g = new FloodGuard(20f, 40f);
            int drops = 0;
            for (int i = 0; i < 100; i++) { var v = g.Take(0); Assert.AreNotEqual(FloodGuard.Verdict.Kick, v); if (v == FloodGuard.Verdict.Drop) drops++; }
            Assert.AreEqual(60, drops, "40 burst allowed, the rest dropped");
        }

        [Test]
        public void SustainedFloodIsKicked()   // spec §3: about 10x the allowed rate for 5 s
        {
            var g = new FloodGuard(20f, 40f);
            var last = FloodGuard.Verdict.Ok;
            for (int i = 0; i < 200 * 6 && last != FloodGuard.Verdict.Kick; i++) last = g.Take(i / 200.0);   // 200 per second
            Assert.AreEqual(FloodGuard.Verdict.Kick, last);
        }

        [Test]
        public void LogsAtMostOnceASecondWithTheCount()
        {
            var g = new FloodGuard(1f, 1f);
            g.Take(0); g.Take(0); g.Take(0);
            Assert.IsTrue(g.LogDue(0, out int n)); Assert.AreEqual(2, n);
            g.Take(0.5);
            Assert.IsFalse(g.LogDue(0.5, out _));
            Assert.IsTrue(g.LogDue(1.1, out n)); Assert.AreEqual(1, n);
        }
    }
}
