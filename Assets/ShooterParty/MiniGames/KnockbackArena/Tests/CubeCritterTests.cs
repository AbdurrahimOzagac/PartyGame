#if UNITY_EDITOR   // play mode tests that load assets through the AssetDatabase
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using ShooterParty;
using ShooterParty.UI;

namespace KnockbackArena.Tests
{
    public class CubeCritterTests
    {
        const string Fbx = "Assets/ShooterParty/MiniGames/KnockbackArena/Models/CubeCritter.fbx";
        const string Prefab = "Assets/ShooterParty/MiniGames/KnockbackArena/Prefabs/CubeCritter_Tomato.prefab";

        [Test]
        public void ImportedClipsAndBlendShapes()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name, c => c);
            var expected = new[] { "Idle", "Run", "Spit", "Hit", "Fall", "Land", "Jump", "Victory", "VictoryLoop", "Defeat", "DefeatLoop", "Taunt", "Blink" };
            CollectionAssert.AreEquivalent(expected, clips.Keys);
            Assert.IsTrue(clips["Run"].isLooping && clips["Idle"].isLooping && !clips["Spit"].isLooping);
            Assert.AreEqual(12f / 30f, clips["Run"].length, 1e-3f);
            Assert.AreEqual(22f / 30f, clips["Spit"].length, 1e-3f);
            var spitCurves = AnimationUtility.GetCurveBindings(clips["Spit"]).Select(b => b.propertyName).ToList();
            Assert.Contains("blendShape.CheekPuff", spitCurves, "face animation must reach Unity per clip");

            var mesh = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Mesh>().First(m => m.blendShapeCount > 0);
            Assert.AreEqual(11, mesh.blendShapeCount);
            var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < mesh.blendShapeCount; i++)
                Assert.AreEqual(0f, smr.GetBlendShapeWeight(i), $"default face must be neutral ({mesh.GetBlendShapeName(i)})");
            var b = smr.bounds;
            Object.DestroyImmediate(go);
            Assert.AreEqual(0f, b.min.y, 0.02f, "stands on y = 0");
            Assert.AreEqual(1.2f, b.max.y, 0.05f, "about 1.2 m tall including the curl");
        }

        [Test]
        public void EveryCritterPrefabHasTheAnimatorController()
        {
            var ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ShooterParty/MiniGames/KnockbackArena/Animation/CubeCritter.controller");
            Assert.IsNotNull(ac);
            foreach (var name in new[] { "CubeCritter", "CubeCritter_Tomato", "CubeCritter_Teal", "CubeCritter_Chartreuse", "CubeCritter_Violet", "CubeCritter_Net" })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ShooterParty/MiniGames/KnockbackArena/Prefabs/{name}.prefab");
                Assert.IsNotNull(go, name);
                Assert.AreEqual(ac, go.GetComponentInChildren<Animator>().runtimeAnimatorController, name + " lost its animator controller");
            }
        }

        // The hit flash brightens the body through a property block on _BaseColor: no shader keyword, so neither the
        // editor's material re-validation nor build variant stripping can switch it off.
        [UnityTest]
        public IEnumerator HitFlashBrightensTheBodyThenClears()
        {
            var critter = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), new Vector3(20f, 0f, 0f), Quaternion.identity);
            critter.GetComponent<Rigidbody>().isKinematic = true;
            var smr = critter.GetComponentInChildren<SkinnedMeshRenderer>();
            int body = System.Array.FindIndex(smr.sharedMaterials, m => m && m.name.StartsWith("M_CC_Body"));
            Assert.GreaterOrEqual(body, 0, "the critter has a body material slot");
            var baseColour = smr.sharedMaterials[body].GetColor("_BaseColor");
            yield return null;
            critter.GetComponentInChildren<CritterAnimator>().PlayHit();
            yield return null;
            var block = new MaterialPropertyBlock();
            smr.GetPropertyBlock(block, body);
            Assert.IsTrue(block.HasColor("_BaseColor"), "the flash overrides the body colour");
            var lit = block.GetColor("_BaseColor");
            Assert.Greater(lit.g + lit.b, baseColour.g + baseColour.b + 0.5f, "Tomato flashes towards white");
            yield return new WaitForSeconds(0.3f);
            smr.GetPropertyBlock(block, body);
            Assert.IsFalse(block.HasColor("_BaseColor"), "the flash clears");
            Object.Destroy(critter);
        }

        [UnityTest]
        public IEnumerator SpitAnimatesTheFace()
        {
            var critter = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), new Vector3(10f, 0f, 0f), Quaternion.identity);
            critter.GetComponent<Rigidbody>().isKinematic = true;
            var smr = critter.GetComponentInChildren<SkinnedMeshRenderer>();
            int puff = smr.sharedMesh.GetBlendShapeIndex("CheekPuff");
            yield return null;
            critter.GetComponentInChildren<CritterAnimator>().PlaySpit();
            float peak = 0f;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                yield return null;
                peak = Mathf.Max(peak, smr.GetBlendShapeWeight(puff));
            }
            Assert.Greater(peak, 50f, "cheeks puff during the spit charge (Action layer drives blend shapes)");
            Object.Destroy(critter);
        }

        // Flat shots (the arena has no gravity and no lift) must never clip the floor, whatever the shooter is doing:
        // the spit pose, a run lean, a snap turn, a dash, a jump or a knock can all dip the long snout.
        static readonly string[] SpitPoses = { "idle", "run", "backpedal", "strafe", "turn90", "turn180", "dash", "airborne", "knocked", "light" };

        [UnityTest]
        public IEnumerator FlatSpitClearsTheFloorInEveryPose([ValueSource(nameof(SpitPoses))] string pose)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            var shooter = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), Vector3.zero, Quaternion.identity);
            var spit = shooter.GetComponent<CritterSpit>();
            var motor = shooter.GetComponent<CritterMotor>();
            spit.gravity = 0f;
            yield return new WaitForSeconds(0.4f);

            var move = pose == "idle" ? Vector3.zero : pose == "backpedal" ? Vector3.back : pose == "strafe" ? Vector3.right : Vector3.forward;
            var aim = pose == "turn90" ? Vector3.right : pose == "turn180" ? Vector3.back : Vector3.forward;
            if (pose == "light") shooter.GetComponent<Rigidbody>().mass = 0.05f * motor.StartMass;
            motor.Move(move);
            yield return new WaitForSeconds(0.3f);
            if (pose == "dash") { Assert.IsTrue(motor.Dash()); yield return new WaitForSeconds(0.05f); }
            if (pose == "airborne") { motor.Jump(); yield return new WaitForSeconds(0.15f); }
            if (pose == "knocked") { motor.Knockback(Vector3.back * 3.5f + Vector3.up * 0.9f); yield return new WaitForSeconds(0.05f); }

            Assert.IsTrue(spit.TryFire(aim), "fired");
            yield return new WaitForSeconds(0.2f);   // past the release on Spit frame 4
            var blob = Object.FindAnyObjectByType<SpitBlob>();
            Assert.IsNotNull(blob, pose + ": the blob left the snout");
            Assert.Greater(blob.transform.position.y, 0.15f + 0.05f, pose + ": the blob's bottom is clear of the floor");
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(blob && blob.transform.parent == null && blob.GetComponent<Rigidbody>().linearVelocity.magnitude > 5f,
                pose + ": still flying (it did not touch the floor and splat)");
            Object.Destroy(shooter); Object.Destroy(ground);
            if (blob) Object.Destroy(blob.gameObject);
        }

        [UnityTest]
        public IEnumerator SpitKnocksBackAnotherCritter()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var shooter = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            var target = Object.Instantiate(prefab, new Vector3(0f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f));
            yield return new WaitForSeconds(0.4f);   // settle on the ground
            Assert.IsTrue(shooter.GetComponent<CritterMotor>().Grounded, "ground check");
            var start = target.transform.position;

            Assert.IsTrue(shooter.GetComponent<CritterSpit>().TryFire(Vector3.forward));
            Assert.IsFalse(shooter.GetComponent<CritterSpit>().TryFire(Vector3.forward), "cooldown blocks an instant second shot");
            yield return new WaitForSeconds(0.2f);
            Assert.IsNotNull(Object.FindAnyObjectByType<SpitBlob>(), "blob leaves the mouth after 4 frames");
            var targetAnim = target.GetComponentInChildren<Animator>();
            bool hitPlayed = false;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                hitPlayed |= targetAnim.GetCurrentAnimatorStateInfo(0).IsName("Hit");
                yield return null;
            }
            Assert.Greater(target.transform.position.z - start.z, 0.3f, "target is pushed away from the shooter");
            Assert.IsTrue(hitPlayed, "target plays the Hit reaction");
            Object.Destroy(shooter); Object.Destroy(target); Object.Destroy(ground);
        }
    }
}
#endif
