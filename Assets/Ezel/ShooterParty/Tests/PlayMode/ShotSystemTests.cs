#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterParty.Tests
{
    public class ShotSystemTests
    {
        // Records server hits instead of applying a mini-game's rules.
        class RecordingEffect : ShotEffect
        {
            public readonly List<ShotHit> hits = new List<ShotHit>();
            public override void ApplyServer(in ShotHit hit, ShotContext ctx) => hits.Add(hit);
        }

        [UnityTearDown] public IEnumerator TearDown() => NetTestRig.Teardown();

        static GameObject Floor()
        {
            var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
            f.transform.position = new Vector3(0f, -0.5f, 0f); f.transform.localScale = new Vector3(30f, 1f, 30f);
            return f;
        }

        [UnityTest]
        public IEnumerator ServerShotHitsTheCritterInFront()
        {
            NetTestRig.StartNetworked();
            var floor = Floor();
            var effect = ScriptableObject.CreateInstance<RecordingEffect>();
            ShotSystem.Instance.Effect = effect;
            var a = NetTestRig.Spawn(0, bot: true, pos: new Vector3(0f, 0.02f, -1.5f));
            var b = NetTestRig.Spawn(1, bot: true, pos: new Vector3(0f, 0.02f, 1.5f));
            yield return new WaitForSeconds(0.2f);
            ShotSystem.Instance.ServerFire(a, a.transform.position + Vector3.up * 0.45f + Vector3.forward * 0.6f, Vector3.forward, 0);
            for (float t = 0f; t < 1.5f && effect.hits.Count == 0; t += Time.deltaTime) yield return null;
            Assert.AreEqual(1, effect.hits.Count, "one hit");
            Assert.AreEqual(0, effect.hits[0].shooterSlot);
            Assert.AreEqual(1, effect.hits[0].victimSlot);
            Object.Destroy(floor);
        }

        [UnityTest]
        public IEnumerator CooldownEnforcedOnServer()
        {
            NetTestRig.StartNetworked();
            var a = NetTestRig.Spawn(0, bot: true, pos: new Vector3(0f, 0.02f, 0f));
            yield return null;
            int before = ShotSystem.Instance.ServerShots;
            for (int i = 0; i < 10; i++) a.FireRpc(a.transform.position + Vector3.up * 0.45f, Vector3.forward, NetworkManager.Singleton.LocalTime.Time);
            yield return null; yield return null;
            Assert.LessOrEqual(ShotSystem.Instance.ServerShots - before, 2);
            Assert.GreaterOrEqual(ShotSystem.Instance.ServerShots - before, 1);
        }

        [UnityTest]
        public IEnumerator FireRpcFromNonOwnerRejected()
        {
            NetTestRig.StartNetworked();
            var remote = NetTestRig.Spawn(2, bot: false, owner: 9999, pos: new Vector3(0f, 0.02f, 0f));
            yield return null;
            int before = ShotSystem.Instance.ServerShots;
            LogAssert.ignoreFailingMessages = true;   // NGO logs the refused invoke
            try { remote.FireRpc(remote.transform.position, Vector3.forward, NetworkManager.Singleton.LocalTime.Time); } catch (System.Exception) { }
            yield return null; yield return null;
            LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(before, ShotSystem.Instance.ServerShots);
        }

        [UnityTest]
        public IEnumerator CosmeticBlobDecidesNothing()
        {
            var floor = Floor();
            var critter = Object.Instantiate(NetTestRig.Load(NetTestRig.OfflineCritterPrefab), new Vector3(0f, 0.02f, 2f), Quaternion.identity);
            yield return new WaitForSeconds(0.3f);
            var spit = critter.GetComponent<CritterSpit>();
            var blob = Object.Instantiate(spit.blobPrefab, new Vector3(0f, 0.5f, 0f), Quaternion.identity);
            blob.Cosmetic = true;
            blob.Launch(null, Vector3.forward * 12f, 0f, spit.blobMaterial, null);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;
            var v = critter.GetComponent<Rigidbody>().linearVelocity; v.y = 0f;
            Assert.Less(v.magnitude, 0.05f, "a cosmetic blob must not push anyone");
            Object.Destroy(critter); Object.Destroy(floor);
        }
    }
}
#endif
