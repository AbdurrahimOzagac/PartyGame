#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterParty.Tests
{
    public class NetworkCritterTests
    {
        [UnityTearDown] public IEnumerator TearDown() => NetTestRig.Teardown();

        [UnityTest]
        public IEnumerator HostCritterIsSimulatedBotIsServerSimulated()
        {
            NetTestRig.StartPractice(NetTestRig.Load(NetTestRig.CritterPrefab));
            var human = NetTestRig.Spawn(0, bot: false, pos: new Vector3(-2f, 0.2f, 0f));
            var bot = NetTestRig.Spawn(1, bot: true, pos: new Vector3(2f, 0.2f, 0f));
            yield return null;
            Assert.IsTrue(human.Simulated); Assert.IsTrue(bot.Simulated);
            Assert.IsTrue(human.GetComponent<CritterMotor>().enabled);
            Assert.IsTrue(bot.GetComponent<CritterMotor>().enabled);
            Assert.IsNotNull(human.GetComponent<LocalCritterInput>(), "the local human gets keyboard/mouse input");
            Assert.IsNull(bot.GetComponent<LocalCritterInput>(), "bots never get local input");
            Assert.AreEqual(0, human.Slot.Value); Assert.AreEqual(1, bot.Slot.Value);
            Assert.IsTrue(bot.IsBot.Value);
            CollectionAssert.Contains(NetworkCritter.All, human);
            CollectionAssert.Contains(NetworkCritter.All, bot);
        }

        [UnityTest]
        public IEnumerator RemoteCopyHasMotorDisabled()
        {
            NetTestRig.StartPractice(NetTestRig.Load(NetTestRig.CritterPrefab));
            var remote = NetTestRig.Spawn(2, bot: false, owner: 9999, pos: new Vector3(0f, 0.2f, 3f));
            yield return null;
            Assert.IsFalse(remote.IsOwner);
            Assert.IsFalse(remote.Simulated);
            Assert.IsFalse(remote.GetComponent<CritterMotor>().enabled);
            Assert.IsTrue(remote.GetComponent<Rigidbody>().isKinematic, "non-authority copies are kinematic");
            Assert.IsNull(remote.GetComponent<LocalCritterInput>());
        }

        [UnityTest]
        public IEnumerator CrittersDoNotCollide()
        {
            NetTestRig.StartPractice(NetTestRig.Load(NetTestRig.CritterPrefab));
            var a = NetTestRig.Spawn(0, bot: true, pos: new Vector3(0f, 0.2f, 0f));
            var b = NetTestRig.Spawn(1, bot: true, pos: new Vector3(3f, 0.2f, 0f));
            yield return null;
            Assert.IsTrue(Physics.GetIgnoreCollision(a.GetComponent<Collider>(), b.GetComponent<Collider>()));
        }

        [UnityTest]
        public IEnumerator MassVariableDrivesMotorMass()
        {
            NetTestRig.StartPractice(NetTestRig.Load(NetTestRig.CritterPrefab));
            var c = NetTestRig.Spawn(0, bot: true);
            yield return null;
            c.Mass.Value = 0.8f;
            yield return null;
            Assert.AreEqual(0.8f, c.GetComponent<Rigidbody>().mass, 1e-4f);
        }

        [UnityTest]
        public IEnumerator OfflinePrefabStillMoves()
        {
            var go = Object.Instantiate(NetTestRig.Load(NetTestRig.OfflineCritterPrefab), new Vector3(0f, 0.05f, -30f), Quaternion.identity);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, -30f); floor.transform.localScale = new Vector3(20f, 1f, 20f);
            var motor = go.GetComponent<CritterMotor>();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(motor.enabled);
            Assert.IsFalse(go.GetComponent<Rigidbody>().isKinematic, "offline critters stay dynamic");
            var start = go.transform.position;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { motor.Move(Vector3.forward); yield return null; }
            Assert.Greater(Vector3.Distance(start, go.transform.position), 0.5f);
            Object.Destroy(go); Object.Destroy(floor);
        }
    }
}
#endif
