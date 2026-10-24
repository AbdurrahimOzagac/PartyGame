#if UNITY_EDITOR   // loads prefabs through the AssetDatabase
using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ShooterParty.Tests
{
    // Practice-host harness for PlayMode tests: registers prefabs, starts a host on an ephemeral UDP port,
    // spawns critters, and tears everything down so the next test starts clean.
    public static class NetTestRig
    {
        public const string CritterPrefab = "Assets/Ezel/KnockbackArena/Prefabs/CubeCritter_Net.prefab";
        public const string OfflineCritterPrefab = "Assets/Ezel/KnockbackArena/Prefabs/CubeCritter.prefab";

        public static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(go, "missing " + path);
            return go;
        }

        public static PartyNet StartPractice(params GameObject[] prefabs)
        {
            // a host start sweeps the scene and rejects unspawned NetworkObjects left by earlier tests
            foreach (var stray in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
                if (!stray.IsSpawned && stray.gameObject.scene.IsValid()) Object.DestroyImmediate(stray.gameObject);
            var net = PartyNet.Ensure();
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            foreach (var p in prefabs) list.Add(new NetworkPrefab { Prefab = p });
            net.prefabs = list;
            Assert.IsTrue(net.StartPractice("Tester"), "practice host failed to start");
            return net;
        }

        public const string NetPrefabs = "Assets/Ezel/ShooterParty/Data/NetPrefabs.asset";
        public const string ShotSystemPrefab = "Assets/Ezel/ShooterParty/Prefabs/Net_ShotSystem.prefab";

        // Practice host with the real prefab list and server singletons (ShotSystem, ...), as SP_Boot configures it.
        public static PartyNet StartNetworked()
        {
            foreach (var stray in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
                if (!stray.IsSpawned && stray.gameObject.scene.IsValid()) Object.DestroyImmediate(stray.gameObject);
            var net = PartyNet.Ensure();
            net.prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetPrefabs);
            Assert.IsNotNull(net.prefabs, "missing " + NetPrefabs);
            net.serverSingletons = new[] { Load(ShotSystemPrefab).GetComponent<NetworkObject>() };
            Assert.IsTrue(net.StartPractice("Tester"), "practice host failed to start");
            Assert.IsNotNull(ShotSystem.Instance, "ShotSystem spawned on server start");
            return net;
        }

        public static NetworkCritter Spawn(int slot, bool bot, ulong owner = NetworkManager.ServerClientId, Vector3 pos = default)
        {
            var go = Object.Instantiate(Load(CritterPrefab), pos, Quaternion.identity);
            var nc = go.GetComponent<NetworkCritter>();
            nc.ServerSetup(slot, slot % 4, bot, CritterConfig.Default, PlayerModifiers.One, epoch: 1);
            var no = go.GetComponent<NetworkObject>();
            if (owner == NetworkManager.ServerClientId) no.Spawn(destroyWithScene: true);
            else no.SpawnWithOwnership(owner, destroyWithScene: true);
            return nc;
        }

        public static IEnumerator Teardown()
        {
            foreach (var nc in Object.FindObjectsByType<NetworkCritter>(FindObjectsSortMode.None)) Object.Destroy(nc.gameObject);
            foreach (var b in Object.FindObjectsByType<SpitBlob>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
            yield return null;   // let critters go before the network does, so no event fires an RPC mid-shutdown
            if (PartyNet.Instance)
            {
                PartyNet.Instance.Leave();
                Object.Destroy(PartyNet.Instance.gameObject);
            }
            yield return null;
            yield return null;
        }
    }
}
#endif
