#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using ShooterParty;
using ShooterParty.Tests;
using UnityEngine;
using UnityEngine.TestTools;

namespace KnockbackArena.Tests
{
    public class KnockbackShotTests
    {
        [UnityTearDown] public IEnumerator TearDown() => NetTestRig.Teardown();

        [UnityTest]
        public IEnumerator BotShotKnocksHostAndLowersMass()
        {
            NetTestRig.StartNetworked();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f); floor.transform.localScale = new Vector3(30f, 1f, 30f);
            ShotSystem.Instance.Effect = ScriptableObject.CreateInstance<KnockbackShotEffect>();
            var host = NetTestRig.Spawn(0, bot: false, pos: new Vector3(0f, 0.02f, 1.5f));
            var bot = NetTestRig.Spawn(1, bot: true, pos: new Vector3(0f, 0.02f, -1.5f));
            yield return new WaitForSeconds(0.2f);
            float start = host.Mass.Value;
            ShotSystem.Instance.ServerFire(bot, bot.transform.position + Vector3.up * 0.45f + Vector3.forward * 0.6f, Vector3.forward, 0);
            float best = 0f;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                var v = host.GetComponent<Rigidbody>().linearVelocity; v.y = 0f;
                best = Mathf.Max(best, v.magnitude);
                yield return null;
            }
            Assert.Greater(best, 1f, "the hit pushes the host critter");
            Assert.AreEqual(0.9f, host.Mass.Value / start, 0.001f, "10 % mass lost");
            Assert.AreEqual(host.Mass.Value, host.GetComponent<Rigidbody>().mass, 1e-4f);
            Object.Destroy(floor);
        }
    }
}
#endif
