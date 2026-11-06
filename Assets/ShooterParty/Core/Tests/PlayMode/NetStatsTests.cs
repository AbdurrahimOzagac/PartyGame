#if UNITY_EDITOR
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShooterParty.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterParty.Tests
{
    public class NetStatsTests
    {
        bool saved;

        [UnitySetUp]
        public IEnumerator EnterPracticeLobby()
        {
            saved = GameSettings.ShowNetStats;
            yield return NetTestRig.BootToPracticeLobby();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSettings.ShowNetStats = saved;
            yield return NetTestRig.Teardown();
        }

        [UnityTest]
        public IEnumerator OverlayShowsRttInPractice()
        {
            var overlay = UiRoot.Instance.netStats;
            GameSettings.ShowNetStats = false;
            yield return null;
            Assert.IsFalse(overlay.IsShown, "hidden unless switched on");
            overlay.Toggle();   // what F3 does
            Assert.IsTrue(GameSettings.ShowNetStats, "F3 persists the choice");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(overlay.IsShown);
            var m = Regex.Match(overlay.text.text, @"RTT\s+(\d+)");
            Assert.IsTrue(m.Success, "RTT line in: " + overlay.text.text);
            Assert.GreaterOrEqual(int.Parse(m.Groups[1].Value), 0);
            StringAssert.Contains("—", overlay.text.text, "no bridge on UDP: the kbps read as a dash");
            StringAssert.Contains("TICK", overlay.text.text);
        }
    }
}
#endif
