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
    //   Data/MiniGameCatalog.asset      every MiniGameDefinition under Assets/Ezel
    // A mini-game's networked prefabs (critter variants, pickups) are registered automatically by living there.
    public static class ShooterPartySetup
    {
        public const string Root = "Assets/Ezel/ShooterParty";
        public const string NetPrefabsPath = Root + "/Data/NetPrefabs.asset";
        public const string ShotSystemPath = Root + "/Prefabs/Net_ShotSystem.prefab";
        public const string CatalogPath = Root + "/Data/MiniGameCatalog.asset";

        [MenuItem("ShooterParty/Rebuild core assets")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Data");
            SaveSingleton<ShotSystem>(ShotSystemPath);
            BuildNetPrefabs();
            BuildCatalog();
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

        public static MiniGameCatalog BuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MiniGameCatalog>(CatalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<MiniGameCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.games = AssetDatabase.FindAssets("t:MiniGameDefinition", new[] { "Assets/Ezel" })
                .Select(g => AssetDatabase.LoadAssetAtPath<MiniGameDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d).OrderBy(d => d.displayName).ToList();
            foreach (var d in catalog.games)
            {
                if (string.IsNullOrEmpty(d.sceneName) || !d.controllerPrefab || !d.critterPrefab)
                    Debug.LogWarning($"[ShooterParty] mini-game '{d.id}' is missing its scene, controller or critter prefab");
            }
            EditorUtility.SetDirty(catalog);
            Debug.Log("[ShooterParty] mini-games: " + string.Join(", ", catalog.games.Select(g => g.id)));
            return catalog;
        }

        // NetworkSceneManager can only load scenes listed in the build settings.
        public static void EnsureInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
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
