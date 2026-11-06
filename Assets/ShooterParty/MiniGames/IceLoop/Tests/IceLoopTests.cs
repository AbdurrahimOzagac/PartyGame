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

namespace IceLoop.Tests
{
    public class IceLoopTests
    {
        const string ControllerPrefab = "Assets/ShooterParty/MiniGames/IceLoop/Prefabs/Net_IceLoopGame.prefab";

        class TestSessionHost : ISessionHost
        {
            public List<SlotInfo> slots = new List<SlotInfo>();
            public IReadOnlyList<SlotInfo> RoundSlots => slots;
            public int Epoch => 1;
            public float TimeScale => 1f;
            public List<RankEntry> ranking;
            public void ReportScores(IReadOnlyList<(int slot, int score)> s, int crownSlot) { }
            public void ReportRound(IReadOnlyList<RankEntry> r) => ranking = r.ToList();
        }

        TestSessionHost host;
        IceLoopGame game;
        IceTrack track;
        readonly List<GameObject> junk = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SessionFlow.TimeScaleForTests = 1f;
            MiniGameController.Host = null;
            foreach (var g in junk) if (g) Object.Destroy(g);
            junk.Clear();
            if (game && game.IsSpawned) game.NetworkObject.Despawn(true);
            yield return NetTestRig.Teardown();
        }

        [Test]
        public void LayoutIsAClosedLoopWithWholeGatesThatNeverFoldOverItself()
        {
            var c = IceTrackLayout.Resample(2f);
            Assert.AreEqual(0, c.Length % 10, "whole gates");
            Assert.GreaterOrEqual(c.Length, 150, "a long lap (>= 300 m)");
            Assert.Less(Vector3.Distance(c[0], Vector3.zero), 2f, "the start line is at the origin");
            Assert.Less(Vector3.Distance(c[c.Length - 1], c[0]), 3f, "closed");
            for (int i = 0; i < c.Length; i++)
                for (int j = i + 25; j < c.Length && j - i < c.Length - 25; j++)
                    Assert.Greater(Vector2.Distance(new Vector2(c[i].x, c[i].z), new Vector2(c[j].x, c[j].z)), 9f, $"points {i} and {j} are too close");
        }


        // The real scene, the real road and real bots through the whole session loop at 1/10 time.
        [UnityTest]
        public IEnumerator BotsRaceTheRealRoadThroughASession()
        {
            SessionFlow.TimeScaleForTests = 0.1f;
            NetTestRig.StartSession();
            for (float t = 0f; t < 10f && !(SessionManager.Instance && SessionManager.Instance.Players.Count == 4); t += Time.unscaledDeltaTime) yield return null;
            var s = SessionManager.Instance;
            Assert.IsNotNull(s);
            s.RequestPlaylist("Ice", new[] { "ice_loop" });
            for (float t = 0f; t < 3f && s.PlaylistIds.Count != 1; t += Time.unscaledDeltaTime) yield return null;
            s.RequestStart();
            int best = 0;
            for (float t = 0f; t < 90f && s.Phase.Value != SessionPhase.Final; t += Time.unscaledDeltaTime)
            {
                if (s.Phase.Value == SessionPhase.Playing)
                    for (int i = 0; i < s.Players.Count; i++) best = Mathf.Max(best, s.Players[i].roundScore);
                yield return null;
            }
            Assert.AreEqual(SessionPhase.Final, s.Phase.Value, "the session reaches the final");
            Assert.GreaterOrEqual(best, 3, "the bots drive the real road and pass checkpoints");
            Assert.AreEqual(1, s.ServerFlow.Rankings.Count);
        }

        // A flat 4-gate circle on a big floor, 3 bots without brains: the rules are tested, not the physics of the real road.
        IEnumerator StartRound(float duration = 20f)
        {
            NetTestRig.StartNetworked();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f); floor.transform.localScale = new Vector3(120f, 1f, 120f);
            var world = new GameObject("MiniGameScene");
            var scene = world.AddComponent<MiniGameScene>();
            scene.spawnPoints = new Transform[0];
            track = world.AddComponent<IceTrack>();
            track.centre = Enumerable.Range(0, 40).Select(i => new Vector3(Mathf.Cos(i / 40f * Mathf.PI * 2f), 0f, Mathf.Sin(i / 40f * Mathf.PI * 2f)) * 30f).ToArray();
            track.gateStride = 10; track.halfWidth = 4f;
            junk.Add(floor); junk.Add(world);
            host = new TestSessionHost();
            for (int i = 0; i < 3; i++)
                host.slots.Add(new SlotInfo { slot = i, ownerClientId = NetworkManager.ServerClientId, isBot = true, colourIndex = i, mods = PlayerModifiers.One });
            MiniGameController.Host = host;
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ControllerPrefab));
            game = go.GetComponent<IceLoopGame>();
            var def = Object.Instantiate(game.Definition);
            def.durationSeconds = duration;
            game.Definition = def;
            go.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            game.ServerSetupRound();
            yield return null;
            foreach (var ai in Object.FindObjectsByType<IceBotAI>(FindObjectsSortMode.None)) Object.Destroy(ai);
            yield return new WaitForSeconds(0.2f);
            game.ServerStartRound();
            yield return null;
        }

        // Drive through gate g: a spot 3 m before it, then 1 m past it (a gate scores when its plane is crossed).
        IEnumerator Cross(int slot, int g)
        {
            var pose = track.GatePose(g);
            PutAt(slot, pose.position - pose.rotation * Vector3.forward * 3f + Vector3.up * 0.1f);
            yield return new WaitForSeconds(0.15f);
            PutAt(slot, pose.position + pose.rotation * Vector3.forward + Vector3.up * 0.1f);
            yield return new WaitForSeconds(0.15f);
        }

        void PutAt(int slot, Vector3 p) { var rb = game.CritterOf(slot).GetComponent<Rigidbody>(); rb.position = p; rb.linearVelocity = Vector3.zero; }

        [UnityTest]
        public IEnumerator GatesScoreInOrderAndFallsRestartAtTheLastGate()
        {
            yield return StartRound();
            yield return Cross(0, 2);   // skipping gate 1: no point
            Assert.AreEqual(0, game.ScoreOf(0), "gates count in order");
            yield return Cross(0, 1);
            Assert.AreEqual(1, game.ScoreOf(0));
            yield return Cross(0, 2);
            Assert.AreEqual(2, game.ScoreOf(0));

            PutAt(0, new Vector3(0f, -9f, 0f));   // into the water
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(game.IsAlive(0), "a fall is not an elimination");
            Assert.AreEqual(1, game.Falls(0));
            Assert.Less(Vector3.Distance(game.CritterOf(0).transform.position, track.RespawnPose(2, 0).position), 2f, "back at the last gate");
            Assert.AreEqual(2, game.ScoreOf(0), "the checkpoints stay");
        }

        [UnityTest]
        public IEnumerator CarsThatTouchAreShovedApart()
        {
            yield return StartRound();
            PutAt(0, new Vector3(0f, 0.1f, 0f));
            PutAt(1, new Vector3(1.5f, 0.1f, 0f));
            PutAt(2, new Vector3(0f, 0.1f, 20f));
            yield return new WaitForSeconds(0.2f);
            Assert.Less(game.CritterOf(0).GetComponent<Rigidbody>().linearVelocity.x, -1f, "the left car is pushed left");
            Assert.Greater(game.CritterOf(1).GetComponent<Rigidbody>().linearVelocity.x, 1f, "the right car is pushed right");
            Assert.Less(Mathf.Abs(game.CritterOf(2).GetComponent<Rigidbody>().linearVelocity.x), 0.5f, "a far car is left alone");
        }

        [UnityTest]
        public IEnumerator LapsKeepScoringAndTheTimerRanksByCheckpoints()
        {
            yield return StartRound(duration: 6f);
            // slot 1 does a full lap and a bit more: gates 1, 2, 3, 0, 1
            foreach (var g in new[] { 1, 2, 3, 0, 1 })
                yield return Cross(1, g);
            yield return Cross(2, 1);
            Assert.AreEqual(5, game.ScoreOf(1), "the finish line and the next lap count again");
            for (float t = 0f; t < 10f && host.ranking == null; t += Time.deltaTime) yield return null;
            Assert.IsNotNull(host.ranking, "the timer ends the round");
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, host.ranking.Select(e => e.slot).ToArray());
            Assert.AreEqual(new[] { 1, 2, 3 }, host.ranking.Select(e => e.rank).ToArray());
        }
    }
}
#endif
