using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        public const string SessionManagerPath = Root + "/Prefabs/Net_SessionManager.prefab";
        public const string LobbyScenePath = Root + "/Scenes/SP_Lobby.unity";
        public const string BootScenePath = Root + "/Scenes/SP_Boot.unity";

        [MenuItem("ShooterParty/Rebuild core assets")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Data");
            Directory.CreateDirectory(Root + "/Scenes");
            UiKit.Init();
            var catalog = BuildCatalog();   // first: the lobby UI has a button per mini-game and preset
            var ui = UiBuilder.BuildUiRoot(catalog);
            UiBuilder.BuildMenuScene();
            SaveSingleton<ShotSystem>(ShotSystemPath);
            SaveSingleton<SessionManager>(SessionManagerPath, sm => sm.catalog = catalog);
            var prefabs = BuildNetPrefabs();
            LobbyBuilder.BuildLobbyScene();
            BuildBootScene(prefabs, ui);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShooterParty] core assets rebuilt");
        }

        static void SaveSingleton<T>(string path, System.Action<T> setup = null) where T : Component
        {
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            go.AddComponent<NetworkObject>();
            var c = go.AddComponent<T>();
            setup?.Invoke(c);
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
            // Built-in playlists cycle through the catalog; with one mini-game they repeat it. Curate by hand later.
            var ids = catalog.games.Select(g => g.id).ToList();
            catalog.presets = ids.Count == 0 ? new List<MiniGameCatalog.Preset>() : new[] { ("Quick", 3), ("Classic", 5), ("Marathon", 8) }
                .Select(p => new MiniGameCatalog.Preset { name = p.Item1, games = Enumerable.Range(0, p.Item2).Select(i => ids[i % ids.Count]).ToList() })
                .ToList();
            EditorUtility.SetDirty(catalog);
            Debug.Log("[ShooterParty] mini-games: " + string.Join(", ", catalog.games.Select(g => g.id)));
            return catalog;
        }

        // First scene of every build: PartyNet (prefab list + server singletons) and the Bootstrap that picks
        // server / autopilot / menu from the command line. Task 14 adds the UI root here.
        public static void BuildBootScene(NetworkPrefabsList prefabs, GameObject uiRoot)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // UI, audio and input live here for the whole run (Persistent keeps them across scene loads)
            var persistent = new GameObject("Persistent");
            persistent.AddComponent<ShooterParty.UI.Persistent>();
            ((GameObject)PrefabUtility.InstantiatePrefab(uiRoot)).transform.SetParent(persistent.transform, false);
            var sfx = new GameObject("Sfx");
            sfx.transform.SetParent(persistent.transform, false);
            sfx.AddComponent<SfxPlayer>().bank = AssetDatabase.LoadAssetAtPath<SfxBank>(UiKit.ArtRoot + "/Audio/SFX_Bank.asset");
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            es.transform.SetParent(persistent.transform, false);
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
            var go = new GameObject("PartyNet");
            var net = go.AddComponent<PartyNet>();
            net.prefabs = prefabs;
            net.serverSingletons = new[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>(ShotSystemPath).GetComponent<NetworkObject>(),
                AssetDatabase.LoadAssetAtPath<GameObject>(SessionManagerPath).GetComponent<NetworkObject>(),
            };
            go.AddComponent<Bootstrap>();
            EditorSceneManager.SaveScene(scene, BootScenePath);
            EnsureInBuildSettings(BootScenePath, first: true);
        }

        // NetworkSceneManager can only load scenes listed in the build settings.
        public static void EnsureInBuildSettings(string scenePath, bool first = false)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int at = scenes.FindIndex(s => s.path == scenePath);
            if (at == 0 || (at > 0 && !first)) return;
            if (at > 0) scenes.RemoveAt(at);
            var entry = new EditorBuildSettingsScene(scenePath, true);
            if (first) scenes.Insert(0, entry); else scenes.Add(entry);
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
