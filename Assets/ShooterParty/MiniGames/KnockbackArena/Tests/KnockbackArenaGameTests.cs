#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterParty;
using ShooterParty.Tests;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace KnockbackArena.Tests
{
    public class KnockbackArenaGameTests
    {
        const string ControllerPrefab = "Assets/ShooterParty/MiniGames/KnockbackArena/Prefabs/Net_KnockbackArenaGame.prefab";

        // Stands in for SessionManager: decides the round's slots and records what the mini-game reports.
        class TestSessionHost : ISessionHost
        {
            public List<SlotInfo> slots = new List<SlotInfo>();
            public IReadOnlyList<SlotInfo> RoundSlots => slots;
            public int Epoch => 1;
            public float TimeScale => 1f;
            public List<RankEntry> ranking;
            public int crown = -1;
            public Dictionary<int, int> scores = new Dictionary<int, int>();
            public void ReportScores(IReadOnlyList<(int slot, int score)> s, int crownSlot) { foreach (var (k, v) in s) scores[k] = v; crown = crownSlot; }
            public void ReportRound(IReadOnlyList<RankEntry> r) => ranking = r.ToList();
        }

        TestSessionHost host;
        KnockbackArenaGame game;
        readonly List<GameObject> junk = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MiniGameController.Host = null;
            foreach (var g in junk) if (g) Object.Destroy(g);
            junk.Clear();
            if (game && game.IsSpawned) game.NetworkObject.Despawn(true);
            yield return NetTestRig.Teardown();
        }

        IEnumerator StartRound(int bots, float duration = 20f, float koWindow = 4f)
        {
            NetTestRig.StartNetworked();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f); floor.transform.localScale = new Vector3(16f, 1f, 16f);
            var scene = new GameObject("MiniGameScene").AddComponent<MiniGameScene>();
            scene.spawnPoints = new[] { new Vector3(-4f, 0.05f, -4f), new Vector3(4f, 0.05f, -4f), new Vector3(4f, 0.05f, 4f), new Vector3(-4f, 0.05f, 4f) }
                .Select((p, i) => { var t = new GameObject("Spawn_" + i).transform; t.position = p; junk.Add(t.gameObject); return t; }).ToArray();
            scene.pickupSpots = new Transform[0];
            junk.Add(floor); junk.Add(scene.gameObject);
            host = new TestSessionHost();
            for (int i = 0; i < bots; i++)
                host.slots.Add(new SlotInfo { slot = i, ownerClientId = NetworkManager.ServerClientId, isBot = true, colourIndex = i, mods = PlayerModifiers.One });
            MiniGameController.Host = host;
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ControllerPrefab));
            game = go.GetComponent<KnockbackArenaGame>();
            var def = Object.Instantiate(game.Definition);
            def.durationSeconds = duration;
            game.Definition = def;
            game.koCreditSeconds = koWindow;
            go.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            game.ServerSetupRound();
            yield return null;
            foreach (var ai in Object.FindObjectsByType<ArenaBotAI>(FindObjectsSortMode.None)) Object.Destroy(ai);   // deterministic: no brains
            yield return new WaitForSeconds(0.2f);
            game.ServerStartRound();
            yield return null;
        }

        NetworkCritter C(int slot) => game.CritterOf(slot);

        static void Drop(NetworkCritter c) { var rb = c.GetComponent<Rigidbody>(); rb.position = new Vector3(c.transform.position.x, -3.5f, c.transform.position.z); rb.linearVelocity = Vector3.down * 5f; }

        IEnumerator WaitForRanking(float timeout = 3f)
        {
            for (float t = 0f; t < timeout && host.ranking == null; t += Time.deltaTime) yield return null;
            Assert.IsNotNull(host.ranking, "round should have ended");
        }

        static int RankOf(List<RankEntry> r, int slot) => r.First(e => e.slot == slot).rank;

        [UnityTest]
        public IEnumerator RoundEndsWithOneLeftAndRanksBySurvival()
        {
            yield return StartRound(3);
            Assert.AreEqual(3, NetworkCritter.All.Count);
            Drop(C(2));
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(C(2) == null || C(2).Eliminated.Value);
            Drop(C(1));
            yield return WaitForRanking();
            Assert.AreEqual(1, RankOf(host.ranking, 0));
            Assert.AreEqual(2, RankOf(host.ranking, 1));
            Assert.AreEqual(3, RankOf(host.ranking, 2));
            Assert.AreEqual(0, NetworkCritter.All.Count, "critters are despawned before the result is reported");
        }

        [UnityTest]
        public IEnumerator KoCreditGoesToLastHitterWithinWindow()
        {
            yield return StartRound(4, koWindow: 0.4f);
            game.NotifyHit(new ShotHit { shooterSlot = 1, victimSlot = 0 });
            yield return new WaitForSeconds(0.2f);
            Drop(C(0));                                  // inside the window: slot 1 gets the KO
            yield return new WaitForSeconds(0.2f);
            game.NotifyHit(new ShotHit { shooterSlot = 1, victimSlot = 2 });
            yield return new WaitForSeconds(0.6f);
            Drop(C(2));                                  // outside the window: nobody gets it
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(1, game.ScoreOf(1));
            Assert.AreEqual(0, game.ScoreOf(3));
        }

        [UnityTest]
        public IEnumerator CrownFollowsScore()
        {
            yield return StartRound(4);
            Assert.AreEqual(-1, host.crown, "no crown at 0");
            game.NotifyHit(new ShotHit { shooterSlot = 3, victimSlot = 0 });
            Drop(C(0));
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(3, host.crown);
            Assert.AreEqual(1, host.scores[3]);
        }

        [UnityTest]
        public IEnumerator TimerEndRanksByKosThenMass()
        {
            yield return StartRound(4, duration: 1.2f);
            game.NotifyHit(new ShotHit { shooterSlot = 1, victimSlot = 3 });
            Drop(C(3));
            C(0).Mass.Value = 0.6f;                      // lighter than slot 2 -> ranks below it
            yield return WaitForRanking(3f);
            Assert.AreEqual(1, RankOf(host.ranking, 1), "most KOs");
            Assert.AreEqual(2, RankOf(host.ranking, 2), "more mass left");
            Assert.AreEqual(3, RankOf(host.ranking, 0));
            Assert.AreEqual(4, RankOf(host.ranking, 3), "fell");
        }

        [UnityTest]
        public IEnumerator SimultaneousFallsShareRank()
        {
            yield return StartRound(3);
            Drop(C(1)); Drop(C(2));
            yield return WaitForRanking();
            Assert.AreEqual(1, RankOf(host.ranking, 0));
            Assert.AreEqual(2, RankOf(host.ranking, 1));
            Assert.AreEqual(2, RankOf(host.ranking, 2));
        }

        [UnityTest]
        public IEnumerator CampingOutsideFootprintIsEliminated()
        {
            yield return StartRound(4);
            var c = C(2);
            var rb = c.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            for (float t = 0f; t < 1.3f; t += Time.deltaTime) { rb.position = new Vector3(11f, 0.05f, 0f); yield return null; }
            Assert.IsTrue(!c || c.Eliminated.Value, "hovering 3 m outside the ice for over a second is out");
        }
    }
}
#endif
