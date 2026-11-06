using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abd.KnockbackArena.EditorTools
{
    // Menüden "Abd > Knockback Arena > Build Scene" ile sahneyi, prefab'ları ve materyalleri kurar.
    // Tekrar çalıştırmak güvenli: ayar dosyası ve materyaller korunur, prefab/sahne yeniden yazılır.
    public static class KnockbackArenaBuilder
    {
        const string Root = "Assets/Abd/KnockbackArena";

        [MenuItem("Abd/Knockback Arena/Build Scene")]
        public static void Build()
        {
            EnsureFolders();

            var settings = LoadOrCreate<KnockbackArenaSettings>($"{Root}/KnockbackArenaSettings.asset");
            var frictionless = LoadOrCreatePhysicsMaterial($"{Root}/Materials/Frictionless.asset");
            var playerMat = LoadOrCreateMaterial($"{Root}/Materials/Player.mat", new Color(0.2f, 0.55f, 1f));
            var dummyMat = LoadOrCreateMaterial($"{Root}/Materials/Dummy.mat", new Color(1f, 0.45f, 0.2f));
            var barrelMat = LoadOrCreateMaterial($"{Root}/Materials/Barrel.mat", new Color(0.15f, 0.15f, 0.18f));
            var bulletMat = LoadOrCreateMaterial($"{Root}/Materials/Bullet.mat", new Color(1f, 0.9f, 0.2f));
            var platformMat = LoadOrCreateMaterial($"{Root}/Materials/Platform.mat", new Color(0.35f, 0.38f, 0.42f));

            Bullet bulletPrefab = BuildBulletPrefab(bulletMat);
            GameObject playerPrefab = BuildPlayerPrefab(settings, frictionless, playerMat, barrelMat, bulletPrefab);
            GameObject dummyPrefab = BuildDummyPrefab(settings, frictionless, dummyMat);

            BuildScene(settings, platformMat, playerPrefab, dummyPrefab);

            Debug.Log("Knockback Arena kuruldu: " + Root + "/Scenes/KnockbackArena.unity");
        }

        static Bullet BuildBulletPrefab(Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bullet";
            Object.DestroyImmediate(go.GetComponent<Collider>()); // çarpışmayı Bullet kendi tarıyor
            go.transform.localScale = Vector3.one * 0.3f;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.AddComponent<Bullet>();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/Bullet.prefab");
            Object.DestroyImmediate(go);
            return saved.GetComponent<Bullet>();
        }

        static GameObject BuildPlayerPrefab(KnockbackArenaSettings settings, PhysicsMaterial frictionless,
            Material bodyMat, Material barrelMat, Bullet bulletPrefab)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Player";
            go.GetComponent<Renderer>().sharedMaterial = bodyMat;
            go.GetComponent<Collider>().sharedMaterial = frictionless;

            var rb = go.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Ateş ağzı: silindir, burnun önünde.
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            Object.DestroyImmediate(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(go.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0f, 0.7f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.25f, 0.2f, 0.25f);
            barrel.GetComponent<Renderer>().sharedMaterial = barrelMat;

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(go.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.95f);

            Ensure<PlayerInputReader>(go);
            var motor = Ensure<PlayerMotor>(go);
            var shooter = Ensure<PlayerShooter>(go);
            var receiver = Ensure<KnockbackReceiver>(go);
            Ensure<MassColorFeedback>(go);
            var elimination = Ensure<EliminationOnFall>(go);

            SetReference(motor, "settings", settings);
            SetReference(shooter, "settings", settings);
            SetReference(shooter, "bulletPrefab", bulletPrefab);
            SetReference(shooter, "muzzle", muzzle.transform);
            SetReference(receiver, "settings", settings);
            SetReference(elimination, "settings", settings);

            var saved = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/Player.prefab");
            Object.DestroyImmediate(go);
            return saved;
        }

        static GameObject BuildDummyPrefab(KnockbackArenaSettings settings, PhysicsMaterial frictionless, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Dummy";
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.GetComponent<Collider>().sharedMaterial = frictionless;
            go.AddComponent<Rigidbody>();
            // Dummy yalnızca savrulabilen bir küp: girdi ve ateş yok.
            Ensure<MassColorFeedback>(go);
            var receiver = Ensure<KnockbackReceiver>(go);
            var elimination = Ensure<EliminationOnFall>(go);

            SetReference(receiver, "settings", settings);
            SetBool(receiver, "dragWhenUnlocked", true);
            SetReference(elimination, "settings", settings);

            var saved = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/Dummy.prefab");
            Object.DestroyImmediate(go);
            return saved;
        }

        static void BuildScene(KnockbackArenaSettings settings, Material platformMat,
            GameObject playerPrefab, GameObject dummyPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Üst yüzü y = 0 olan düz platform.
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform";
            platform.transform.position = new Vector3(0f, -0.5f, 0f);
            platform.transform.localScale = new Vector3(20f, 1f, 20f);
            platform.GetComponent<Renderer>().sharedMaterial = platformMat;

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 0.5f, -6f);

            Vector3[] dummyPositions =
            {
                new Vector3(-4f, 0.5f, 3f), new Vector3(4f, 0.5f, 3f),
                new Vector3(0f, 0.5f, 7f), new Vector3(-7f, 0.5f, -2f),
            };
            for (int i = 0; i < dummyPositions.Length; i++)
            {
                var dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab);
                dummy.name = $"Dummy {i + 1}";
                dummy.transform.position = dummyPositions[i];
            }

            var cameraObject = Camera.main.gameObject;
            var tpsCamera = cameraObject.AddComponent<TpsCamera>();
            var cameraSo = new SerializedObject(tpsCamera);
            cameraSo.FindProperty("target").objectReferenceValue = player.transform;
            cameraSo.FindProperty("settings").objectReferenceValue = settings;
            cameraSo.ApplyModifiedPropertiesWithoutUndo();
            if (new SerializedObject(tpsCamera).FindProperty("settings").objectReferenceValue == null)
                Debug.LogError("TpsCamera.settings atanamadı, kamera Inspector'dan elle atanmalı.");
            cameraObject.AddComponent<Crosshair>();

            new GameObject("Arena Match").AddComponent<ArenaMatch>();

            EditorSceneManager.SaveScene(scene, $"{Root}/Scenes/KnockbackArena.unity");
        }

        // RequireComponent'ler bazı bileşenleri kendiliğinden ekler, çift eklememek için.
        static T Ensure<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        static void EnsureFolders()
        {
            EnsureFolder("Assets/Abd", "KnockbackArena");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Scenes");
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
                AssetDatabase.CreateFolder(parent, name);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material LoadOrCreateMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static PhysicsMaterial LoadOrCreatePhysicsMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material != null) return material;
            material = new PhysicsMaterial("Frictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
