#if UNITY_EDITOR   // play mode tests that load assets through the AssetDatabase
using System.Collections;
using System.Linq;
using KnockbackArena.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using ShooterParty;
using ShooterParty.UI;
using ShooterParty.Tests;

namespace KnockbackArena.Tests
{
    // The ice arena, props, VFX, UI and audio wiring (see projects/game_assets/knockback_arena/blender/v2/README.md).
    public class KnockbackArenaTests
    {
        const string Root = "Assets/ShooterParty/MiniGames/KnockbackArena";
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>($"{Root}/{path}");
        static GameObject Spawn(string prefab, Vector3 pos) => Object.Instantiate(Load<GameObject>(prefab), pos, Quaternion.identity);

        [UnityTest]
        public IEnumerator ArenaFloorIsOneColliderAndCrittersStandOnIt()
        {
            var arena = Spawn("Prefabs/Arena/Arena_Ice_16.prefab", Vector3.zero);
            var cols = arena.GetComponentsInChildren<Collider>();
            Assert.AreEqual(1, cols.Length, "one floor collider (per-tile colliders snag sliding boxes at the seams)");
            Assert.AreEqual(0.3f, cols[0].sharedMaterial.dynamicFriction, 1e-3f);
            var critter = Spawn("Prefabs/CubeCritter_Teal.prefab", new Vector3(5f, 0.3f, 5f));   // inside a corner tile
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(critter.GetComponent<CritterMotor>().Grounded);
            Assert.AreEqual(0f, critter.transform.position.y, 0.05f, "walkable top is y = 0");
            var off = Spawn("Prefabs/CubeCritter_Violet.prefab", new Vector3(7.6f, 0.3f, 7.6f));   // cut-away corner: no floor
            yield return new WaitForSeconds(0.6f);
            Assert.Less(off.transform.position.y, -0.5f, "the rounded corners are open");
            Object.Destroy(arena); Object.Destroy(critter); Object.Destroy(off);
        }

        [UnityTest]
        public IEnumerator HitsLowerMassSoLaterHitsPushFurther()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f); ground.transform.localScale = new Vector3(60f, 1f, 60f);
            var c = Spawn("Prefabs/CubeCritter_Tomato.prefab", Vector3.zero);
            var m = c.GetComponent<CritterMotor>();
            m.massLossPerHit = 0.1f;
            yield return new WaitForSeconds(0.4f);
            var rb = c.GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;
            m.Knockback(Vector3.forward * 3.5f);
            yield return new WaitForFixedUpdate();                // impulses land on the next physics step
            float first = rb.linearVelocity.z;
            yield return new WaitForSeconds(1.2f);
            rb.linearVelocity = Vector3.zero;
            m.Knockback(Vector3.forward * 3.5f);
            yield return new WaitForFixedUpdate();
            float second = rb.linearVelocity.z;
            Assert.AreEqual(0.8f, m.MassFraction, 1e-3f, "each hit takes 10 % of the starting mass");
            Assert.Greater(second, first * 1.05f, "same shot, lighter cube, faster push");
            Assert.AreEqual(Vector3.one, c.transform.localScale, "size is unchanged; only mass drops");
            Object.Destroy(c); Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator JumpPickupGivesAJumpCharge()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(20f, -0.5f, 0f); ground.transform.localScale = new Vector3(10f, 1f, 10f);
            var pickup = Spawn("Prefabs/Props/Pickup_Jump.prefab", new Vector3(20f, 0f, 0f));
            var c = Spawn("Prefabs/CubeCritter_Chartreuse.prefab", new Vector3(20f, 0f, -2f));
            var m = c.GetComponent<CritterMotor>();
            m.jumpCharges = 0;
            yield return new WaitForSeconds(0.3f);
            m.Jump(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(0, m.jumpCharges, "no charge: no jump");
            for (float t = 0f; t < 1.2f && m.jumpCharges == 0; t += Time.deltaTime) { m.Move(Vector3.forward); yield return null; }
            Assert.AreEqual(1, m.jumpCharges, "touching the spring grants one jump");
            Assert.IsFalse(pickup.GetComponent<JumpPickup>().Available, "then it hides until it respawns");
            Object.Destroy(c); Object.Destroy(pickup); Object.Destroy(ground);
        }

        [Test]
        public void SpitBlobDeformsAndTakesTheShootersColour()
        {
            var blob = Load<GameObject>("Prefabs/SpitBlob.prefab");
            var smr = blob.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(smr, "blend shapes only apply on a SkinnedMeshRenderer");
            Assert.GreaterOrEqual(smr.sharedMesh.GetBlendShapeIndex("Stretch"), 0);
            Assert.GreaterOrEqual(smr.sharedMesh.GetBlendShapeIndex("Squash"), 0);
            Assert.AreEqual("KA/Goo", smr.sharedMaterial.shader.name);
            var sb = blob.GetComponent<SpitBlob>();
            Assert.IsNotNull(sb.trail); Assert.IsNotNull(sb.ribbon); Assert.IsNotNull(sb.impactFx); Assert.IsNotNull(sb.visual);
            foreach (var p in new[] { "Tomato", "Teal", "Chartreuse", "Violet" })
                Assert.AreEqual("M_CC_Goo_" + p, Load<GameObject>($"Prefabs/CubeCritter_{p}.prefab").GetComponent<CritterSpit>().blobMaterial.name);
        }

        [Test]
        public void EverySoundIdHasClips()
        {
            var bank = Load<SfxBank>("Audio/SFX_Bank.asset");
            var ids = new[] { "Spit", "Hit", "Splat", "BlobPop", "Whoosh", "Wheee", "Splash", "Land", "Step", "Jump", "Pickup", "Beep", "Go",
                              "KO", "Fanfare", "UiClick", "UiHover", "DashReady", "MusicGameplay", "MusicLobby" };
            foreach (var id in ids)
            {
                var e = bank.entries.FirstOrDefault(x => x.id == id);
                Assert.IsNotNull(e, id);
                Assert.IsTrue(e.clips.Length > 0 && e.clips.All(c => c), $"{id} has clips");
            }
            Assert.IsTrue(bank.entries.First(e => e.id == "MusicGameplay").clips[0].length > 10f, "music is a loop, not a sting");
        }

        // The Knockback widget turns each critter's networked Mass into its card's stat line.
        [UnityTest]
        public IEnumerator HudCardShowsMassDroppingOnHit()
        {
            NetTestRig.StartNetworked();
            var c = NetTestRig.Spawn(0, bot: true, pos: new Vector3(-20f, 0f, 0f));
            c.GetComponent<Rigidbody>().isKinematic = true;
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShooterParty/Core/Prefabs/UI_Root.prefab"));
            var hud = root.GetComponentInChildren<PartyHud>(true);
            hud.BindSeat(0, 0, "MOCHI", 1, false);
            Object.Instantiate(Load<GameObject>("UI/HUD_KnockbackWidget.prefab"), hud.widgetSlot);
            yield return null; yield return null;
            var card = hud.CardFor(0);
            Assert.AreEqual("100%", card.statText.text);
            c.Mass.Value = 0.65f * c.StartMass;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual("65%", card.statText.text, "the number rolls to the new mass");
            Assert.AreEqual(HudCard.Amber, card.statText.color, "below 70 % turns amber");
            Assert.IsFalse(hud.cards[1].gameObject.activeSelf, "unused cards hide");
            Object.Destroy(root);
        }

        [UnityTearDown] public IEnumerator TearDown() => NetTestRig.Teardown();   // also after a failed network test
    }
}
#endif

#if UNITY_EDITOR
namespace KnockbackArena.Tests
{
    // Separate class so it runs after the asset tests (it replaces the open scene).
    public class KnockbackArenaSceneTests
    {
        [UnityTest]
        public System.Collections.IEnumerator ZArenaSceneLoadsFullyWired()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/ShooterParty/MiniGames/KnockbackArena/Scenes/KnockbackArena_Ice.unity", new UnityEngine.SceneManagement.LoadSceneParameters());
            yield return null; yield return null;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var c in go.GetComponents<Component>()) Assert.IsNotNull(c, "missing script on " + go.name);
            var markers = Object.FindAnyObjectByType<MiniGameScene>();
            Assert.IsNotNull(markers, "spawn and pickup markers");
            Assert.AreEqual(4, markers.spawnPoints.Length, "four spawns");
            Assert.AreEqual(2, markers.pickupSpots.Length, "two spring spots");
            Assert.IsNotNull(markers.pickupPrefab, "the spring prefab the controller spawns");
            Assert.IsNotNull(Object.FindAnyObjectByType<OverShoulderCamera>());
            Assert.AreEqual(0, Object.FindObjectsByType<Unity.Netcode.NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                "the scene holds no networked objects: the round's controller spawns them");
            Assert.IsNotNull(SplatDecals.Instance);
            Assert.IsNotNull(RenderSettings.skybox); Assert.IsTrue(RenderSettings.fog);
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
        }

        [UnityTest]
        public System.Collections.IEnumerator ZPlaygroundKeepsItsDebugKeysOnItsOwnComponent()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/ShooterParty/MiniGames/KnockbackArena/Scenes/CubeCritter_Playground.unity", new UnityEngine.SceneManagement.LoadSceneParameters());
            yield return null; yield return null;
            var input = Object.FindAnyObjectByType<LocalCritterInput>();
            Assert.IsNotNull(input, "the playground player");
            Assert.IsNotNull(input.cam, "drives the scene camera");
            Assert.IsNotNull(input.GetComponent<Playground.PlaygroundControls>(), "taunt/win/lose keys, mouse toggle and hint: playground only");
            Assert.IsTrue(input.cam.Captured, "no menus here: the playground holds the mouse from the start");
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
        }
    }
}
#endif
