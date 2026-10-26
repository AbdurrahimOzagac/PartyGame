using NUnit.Framework;
using UnityEngine;

namespace ShooterParty.Tests
{
    public class SpeedCheckTests
    {
        const float Cap = 9f * 1.3f + 2f;   // dashSpeed 9

        // Feeds owner samples at 30 Hz moving at `speed` along +X for `seconds`, starting at owner time t0.
        static (int logs, int kicks) Run(SpeedCheck c, float speed, float seconds, double t0 = 0, bool knockback = false)
        {
            int logs = 0, kicks = 0;
            for (int i = 0; i <= Mathf.RoundToInt(seconds * 30f); i++)
            {
                double t = t0 + i / 30.0;
                var v = c.Feed(t, new Vector3((float)(t * speed), 0f, 0f), knockback);
                if (v == SpeedVerdict.Log) logs++;
                if (v == SpeedVerdict.Kick) kicks++;
            }
            return (logs, kicks);
        }

        [Test]
        public void NonFinitePositionIsKicked()
        {
            var c = new SpeedCheck(Cap);
            Assert.AreEqual(SpeedVerdict.Ok, c.Feed(0, Vector3.zero, false));
            Assert.AreEqual(SpeedVerdict.Kick, c.Feed(1.0 / 30, new Vector3(float.NaN, 0f, 0f), false), "no honest client sends NaN");
            Assert.AreEqual(SpeedVerdict.Kick, new SpeedCheck(Cap).Feed(0, new Vector3(0f, float.PositiveInfinity, 0f), false));
        }

        [Test]
        public void FortyMetresPerSecondForHalfASecondLogsOnceNoKick()
        {
            var (logs, kicks) = Run(new SpeedCheck(Cap), 40f, 0.5f);
            Assert.AreEqual(1, logs); Assert.AreEqual(0, kicks);
        }

        [Test]
        public void SustainedThreePointFiveTimesCapKicks()
        {
            var (_, kicks) = Run(new SpeedCheck(Cap), Cap * 3.5f, 2.1f);
            Assert.GreaterOrEqual(kicks, 1);
        }

        [Test]
        public void ShortBurstAboveThreeTimesCapDoesNotKick()
        {
            var (_, kicks) = Run(new SpeedCheck(Cap), Cap * 3.5f, 1.5f);
            Assert.AreEqual(0, kicks);
        }

        [Test]
        public void DashSpeedIsOk()
        {
            var (logs, kicks) = Run(new SpeedCheck(Cap), 9f, 3f);
            Assert.AreEqual(0, logs); Assert.AreEqual(0, kicks);
        }

        [Test]
        public void KnockbackWindowIsOk()
        {
            var (logs, kicks) = Run(new SpeedCheck(Cap), 60f, 3f, knockback: true);
            Assert.AreEqual(0, logs); Assert.AreEqual(0, kicks);
        }

        [Test]
        public void StalledSamplesArrivingAtOnceAreOk()
        {
            // a TCP stall: 0.5 s of samples at 5 m/s arrive together, but they still carry the owner's timestamps
            var c = new SpeedCheck(Cap);
            Run(c, 5f, 1f);
            for (int i = 1; i <= 15; i++)
            {
                double t = 1.0 + i / 30.0;
                Assert.AreEqual(SpeedVerdict.Ok, c.Feed(t, new Vector3((float)(t * 5f), 0f, 0f), false));
            }
        }

        [Test]
        public void VerticalFallSpeedIsIgnored()
        {
            var c = new SpeedCheck(Cap);
            for (int i = 0; i < 60; i++)
                Assert.AreEqual(SpeedVerdict.Ok, c.Feed(i / 30.0, new Vector3(0f, -i * 2f, 0f), false));
        }

        [Test]
        public void RepeatedOrStaleTimestampsAreIgnored()
        {
            var c = new SpeedCheck(Cap);
            c.Feed(1.0, Vector3.zero, false);
            Assert.AreEqual(SpeedVerdict.Ok, c.Feed(1.0, new Vector3(100f, 0f, 0f), false));
            Assert.AreEqual(SpeedVerdict.Ok, c.Feed(0.5, new Vector3(200f, 0f, 0f), false));
        }
    }
}
