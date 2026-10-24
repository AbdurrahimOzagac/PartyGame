using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ShooterParty.EditorTools
{
    // Generates the core's assets from code, like the Knockback Arena builders do:
    //   Prefabs/Net_ShotSystem.prefab   server singleton for networked shots
    //   Data/NetPrefabs.asset           every prefab under Assets/Ezel whose root has a NetworkObject
    // A mini-game's networked prefabs (critter variants, pickups) are registered automatically by living there.
    public static class ShooterPartySetup
    {
        public const string Root = "Assets/Ezel/ShooterParty";
        public const string NetPrefabsPath = Root + "/Data/NetPrefabs.asset";
        public const string ShotSystemPath = Root + "/Prefabs/Net_ShotSystem.prefab";

        [MenuItem("ShooterParty/Rebuild core assets")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Data");
            SaveSingleton<ShotSystem>(ShotSystemPath);
            BuildNetPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("[ShooterParty] core assets rebuilt");
        }

        static void SaveSingleton<T>(string path) where T : Component
        {
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            go.AddComponent<NetworkObject>();
            go.AddComponent<T>();
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        public static NetworkPrefabsList BuildNetPrefabs()
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetPrefabsPath);
            if (!list)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, NetPrefabsPath);
            }
            foreach (var p in list.PrefabList.ToList()) list.Remove(p);
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Ezel" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(go => go && go.GetComponent<NetworkObject>())
                .OrderBy(go => AssetDatabase.GetAssetPath(go));
            foreach (var go in prefabs) list.Add(new NetworkPrefab { Prefab = go });
            EditorUtility.SetDirty(list);
            Debug.Log("[ShooterParty] network prefabs: " + string.Join(", ", list.PrefabList.Select(p => p.Prefab.name)));
            return list;
        }
    }
}
