using System.Collections.Generic;
using System.IO;
using System.Linq;
using KnockbackArena.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ShooterParty;
using ShooterParty.UI;
using ShooterParty.EditorTools;
using Unity.Netcode;

namespace KnockbackArena.EditorTools
{
    // Import settings for the world exports (blender/v2/make_world.py) and textures (env_textures.py, sprites.py).
    public class KnockbackWorldImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/KnockbackArena/Models/Arena/") && !assetPath.Contains("/KnockbackArena/Models/Props/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f; mi.useFileScale = true;
            mi.importBlendShapes = false; mi.importCameras = false; mi.importLights = false; mi.importVisibility = false;
            mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importNormals = ModelImporterNormals.Import;          // flat crystal facets come split from Blender
            mi.importTangents = ModelImporterTangents.None;          // KA/Ice maps in world space
            mi.isReadable = assetPath.EndsWith("/Podium_Ice.fbx");   // stand points are read from the mesh
        }

        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            if (assetPath.Contains("/KnockbackArena/Sprites/T_FX_"))
            {
                ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.sRGBTexture = true;
            }
            else if (assetPath.EndsWith("/T_Ice_Mask.png")) { ti.sRGBTexture = false; ti.wrapMode = TextureWrapMode.Repeat; }   // data, not colour
            else if (assetPath.EndsWith("/T_Ice_Detail.png")) { ti.sRGBTexture = true; ti.wrapMode = TextureWrapMode.Repeat; }
            else if (assetPath.EndsWith("/T_Sky_Panorama.png"))
            {
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                st.textureShape = TextureImporterShape.TextureCube;
                st.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;   // lat-long panorama -> cubemap, no wrap seam
                st.sRGBTexture = true; st.mipmapEnabled = true;
                ti.SetTextureSettings(st);
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
        }
    }

    public static class ArenaSetup
    {
        const string Root = CubeCritterSetup.Root;
        public const string ScenePath = Root + "/Scenes/KnockbackArena_Ice.unity";
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        [MenuItem("Knockback Arena/Rebuild Everything (critters, VFX, UI, audio, ice arena scene)")]
        public static void RebuildEverything()
        {
            CubeCritterSetup.RebuildAll();
            UiSetup.BuildAll();
            BuildAll();
            ShooterParty.EditorTools.ShooterPartySetup.BuildAll();   // core assets last: it registers every network prefab
            AssetDatabase.SaveAssets();
            Debug.Log("[KnockbackArena] everything rebuilt");
        }

        public static void BatchRebuildEverything() => RebuildEverything();

        public static void BuildAll()
        {
            foreach (var d in new[] { "Prefabs/Arena", "Prefabs/Props", "Scenes" }) Directory.CreateDirectory(Path.Combine(Root, d));
            AssetDatabase.ImportAsset(Root + "/Models/Arena", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Root + "/Models/Props", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            foreach (var t in new[] { "T_Ice_Mask.png", "T_Ice_Detail.png", "T_Sky_Panorama.png" })
                AssetDatabase.ImportAsset($"{Root}/Textures/{t}", ImportAssetOptions.ForceUpdate);
            var ice = IceMaterial();
            var arena = ModelPrefab("Arena_Ice_16", "Arena", ice, go =>
            {
                var col = go.GetComponentsInChildren<MeshFilter>().First(f => f.name == "Floor_Collider");
                col.GetComponent<MeshRenderer>().enabled = false;
                // one collider for the whole floor: per-tile colliders snag a sliding box at the seams
                var mc = col.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = col.sharedMesh; mc.sharedMaterial = IcePhysics();
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("Island_Underside")))
                    r.shadowCastingMode = ShadowCastingMode.Off;
            });
            var backdrop = ModelPrefab("Arena_Backdrop", "Arena", ice, go =>
            {
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            });
            foreach (var kit in AssetDatabase.FindAssets("Kit_ t:GameObject", new[] { Root + "/Models/Arena" }))
            {
                var n = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(kit));
                ModelPrefab(n, "Arena", ice, null);   // modular pieces for custom layouts (add your own floor collider)
            }
            Vector3[] stands = null;
            var podium = ModelPrefab("Podium_Ice", "Arena", ice, go =>
            {
                var mf = go.GetComponentInChildren<MeshFilter>();
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                stands = PodiumStands(mf);
            });
            BuildCrown();
            var pickup = BuildPickup();
            BuildMiniGame();
            BuildScene(arena, backdrop, pickup);
            ShooterPartySetup.EnsureInBuildSettings(ScenePath);
        }

        // ------------------------------------------------------------------ materials
        static Material IceMaterial()
        {
            var path = $"{Root}/Materials/M_Ice.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("KA/Ice");
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/T_Ice_Detail.png"));
            m.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/T_Ice_Mask.png"));
            // tuned on in-engine captures: a bluer floor than the shader default (sunlight + Neutral tonemapping pale it),
            // a soft rim (the 14 deg camera sees the far floor at grazing angles), faint inner cracks
            m.SetColor("_TopColor", Hex("#92BEE8"));
            m.SetColor("_SideColor", Hex("#3B8CC2"));
            m.SetColor("_DeepColor", Hex("#123E86"));
            m.SetFloat("_RimStrength", 0.14f);
            m.SetFloat("_DepthStrength", 0.16f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material LitMat(string name, string shader, Color c, float smooth, float metal = 0f, bool coat = false)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (coat) { m.SetFloat("_ClearCoat", 1f); m.EnableKeyword("_CLEARCOAT"); m.SetFloat("_ClearCoatMask", 1f); m.SetFloat("_ClearCoatSmoothness", 0.95f); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static PhysicsMaterial IcePhysics()
        {
            var path = $"{Root}/Materials/PM_Ice.physicMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (!pm) { pm = new PhysicsMaterial("PM_Ice"); AssetDatabase.CreateAsset(pm, path); }
            // a little slicker than the playground floor: pushes slide ~1.6 m, and mass loss makes later hits go further
            pm.dynamicFriction = 0.3f; pm.staticFriction = 0.32f; pm.bounciness = 0f;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        static Material Sky()
        {
            var path = $"{Root}/Materials/M_Sky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Skybox/Cubemap");
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
            m.SetTexture("_Tex", AssetDatabase.LoadAssetAtPath<Cubemap>($"{Root}/Textures/T_Sky_Panorama.png"));
            m.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ prefabs
        static GameObject ModelPrefab(string fbx, string folder, Material mat, System.Action<GameObject> configure)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/{folder}/{fbx}.fbx");
            var name = fbx.StartsWith("Kit_") ? fbx.Substring(4) : fbx;
            var go = new GameObject(name);
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
            vis.name = "Visual";
            foreach (var r in vis.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            configure?.Invoke(go);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            var sub = fbx.StartsWith("Kit_") ? "Arena/Kit" : "Arena";
            Directory.CreateDirectory($"{Root}/Prefabs/{sub}");
            var p = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{sub}/{name}.prefab");
            Object.DestroyImmediate(go);
            return p;
        }

        // top centre of each octagonal column, ranked by height (tallest = 1st); read from the mesh so the
        // Blender -> Unity axis mapping never matters
        static Vector3[] PodiumStands(MeshFilter mf)
        {
            // the flat column tops are the only exactly-upward faces; group them by height level
            var mesh = mf.sharedMesh;
            var v = mesh.vertices; var n = mesh.normals;
            var tops = Enumerable.Range(0, v.Length)
                .Where(i => mf.transform.TransformDirection(n[i]).y > 0.9995f)
                .Select(i => mf.transform.TransformPoint(v[i]) - mf.transform.root.position).ToArray();
            var cols = tops.GroupBy(p => Mathf.Round(p.y * 20f))
                .Select(g => new Vector3(g.Average(p => p.x), g.Max(p => p.y), g.Average(p => p.z)))
                .OrderByDescending(p => p.y).Take(4).ToArray();
            Debug.Log("[Arena] podium stands: " + string.Join(", ", cols.Select(c => c.ToString("F2"))));
            return cols;
        }

        static GameObject BuildCrown()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Props/Crown.fbx");
            // candy-gold plastic, not metal: real metal mirrors the blue sky and goes olive; toys are plastic anyway
            var gold = LitMat("M_Prop_Gold", "Universal Render Pipeline/Complex Lit", Hex("#FFC23D"), 0.8f, 0.25f, true);
            var gem = LitMat("M_Prop_Gem", "Universal Render Pipeline/Complex Lit", Hex("#FF3D6E"), 0.9f, 0f, true);
            var go = new GameObject("Crown");
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
            vis.name = "Visual";
            foreach (var r in vis.GetComponentsInChildren<MeshRenderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) ms[i] = i == 1 ? gem : gold;
                r.sharedMaterials = ms;
            }
            // bone-local offset from the critter's rest pose: around the curl, a jaunty tilt to one side
            var critter = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CubeCritterSetup.Fbx));
            var top = LeaderCrown.Find(critter.transform, "Top");
            var want = Matrix4x4.TRS(new Vector3(0.01f, 1.005f, -0.01f), Quaternion.Euler(-4f, 0f, 9f), Vector3.one);
            var local = top.worldToLocalMatrix * want;
            var lc = go.AddComponent<LeaderCrown>();
            lc.boneLocalPosition = local.GetColumn(3);
            lc.boneLocalRotation = local.rotation;
            lc.boneLocalScale = local.lossyScale;
            Object.DestroyImmediate(critter);
            var p = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/Props/Crown.prefab");
            Object.DestroyImmediate(go);
            return p;
        }

        static GameObject BuildPickup()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Props/Pickup_Jump.fbx");
            var gold = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/M_Prop_Gold.mat");
            var pink = LitMat("M_Prop_Pink", "Universal Render Pipeline/Complex Lit", Hex("#FF4FA3"), 0.85f, 0f, true);
            var white = LitMat("M_Prop_White", "Universal Render Pipeline/Complex Lit", Color.white, 0.85f, 0f, true);
            var go = new GameObject("Pickup_Jump");
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 0.65f; sc.center = new Vector3(0f, 0.8f, 0f);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            visual.localPosition = new Vector3(0f, 0.8f, 0f);
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
            vis.transform.localScale = Vector3.one * 1.25f;
            foreach (var r in vis.GetComponentsInChildren<MeshRenderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) ms[i] = i == 0 ? gold : i == 1 ? pink : white;
                r.sharedMaterials = ms;
            }
            PrefabUtility.InstantiatePrefab(CubeCritterSetup.FxPrefabs["VFX_PickupSparkles"], visual);
            // soft gold ring on the ice under it, so the spot reads from across the arena
            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "GroundRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(go.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = Vector3.one * 1.4f;
            var rm = new Material(VfxSetup.FxMat("Ring")) { name = "M_FX_PickupRing" };
            var rmPath = $"{Root}/Materials/M_FX_PickupRing.mat";
            AssetDatabase.DeleteAsset(rmPath); AssetDatabase.CreateAsset(rm, rmPath);
            rm.SetColor("_BaseColor", new Color(1f, 0.84f, 0.3f, 0.6f));
            var rr = ring.GetComponent<MeshRenderer>(); rr.sharedMaterial = rm; rr.shadowCastingMode = ShadowCastingMode.Off; rr.receiveShadows = false;
            go.AddComponent<NetworkObject>();   // spawned by the round's controller at the scene's pickup spots
            var jp = go.AddComponent<JumpPickup>();
            jp.visual = visual;
            jp.collectFx = CubeCritterSetup.FxPrefabs["VFX_PickupCollect"];
            var p = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/Props/Pickup_Jump.prefab");
            Object.DestroyImmediate(go);
            return p;
        }

        // ------------------------------------------------------------------ scene
        // ------------------------------------------------------------------ mini-game assets
        public const string DefinitionPath = Root + "/Data/KnockbackArena.asset";
        public const string ControllerPath = Root + "/Prefabs/Net_KnockbackArenaGame.prefab";

        static MiniGameDefinition BuildMiniGame()
        {
            Directory.CreateDirectory(Root + "/Data");
            var effectPath = Root + "/Data/ShotEffect_Knockback.asset";
            var effect = AssetDatabase.LoadAssetAtPath<KnockbackShotEffect>(effectPath);
            if (!effect) { effect = ScriptableObject.CreateInstance<KnockbackShotEffect>(); AssetDatabase.CreateAsset(effect, effectPath); }
            var def = AssetDatabase.LoadAssetAtPath<MiniGameDefinition>(DefinitionPath);
            if (!def) { def = ScriptableObject.CreateInstance<MiniGameDefinition>(); AssetDatabase.CreateAsset(def, DefinitionPath); }
            def.id = "knockback_arena";
            def.displayName = "Knockback Arena";
            def.tagline = "Spit everyone off the ice.";
            def.howToPlay = new[]
            {
                "Every hit pushes and makes the target lighter, so the next hit pushes further.",
                "Knock someone off the ice for a point. The top scorer wears the crown.",
                "Grab a spring to jump. Dash (Shift) to save yourself at the edge.",
            };
            def.controlsHint = "WASD move · Mouse aim · Click spit · Shift dash · Space jump";
            def.sceneName = "KnockbackArena_Ice";
            def.critterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CubeCritter_Net.prefab").GetComponent<NetworkObject>();
            def.minPlayers = 3; def.maxPlayers = 4; def.durationSeconds = 120f;
            def.placementPoints = new[] { 4, 3, 2, 1 };
            def.supportsBots = true;
            def.critter = CritterConfig.Default;
            def.shotEffect = effect;
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/UI/Sprites/UI_IconSpring.png");

            var go = new GameObject("Net_KnockbackArenaGame");
            go.AddComponent<NetworkObject>();
            var game = go.AddComponent<KnockbackArenaGame>();
            game.Definition = def;
            game.fallPoof = CubeCritterSetup.FxPrefabs["VFX_FallPoof"];
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, ControllerPath);
            Object.DestroyImmediate(go);
            def.controllerPrefab = prefab.GetComponent<NetworkObject>();
            EditorUtility.SetDirty(def);
            return def;
        }

        // ------------------------------------------------------------------ scene
        // The networked round's scene: world, light, camera and the MiniGameScene markers. No networked objects:
        // the session spawns the controller, which spawns critters and pickups at the markers.
        static void BuildScene(GameObject arena, GameObject backdrop, GameObject pickup)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // light + sky + fog (ice brief 5 and 7): sun 60 deg, warm white; fog colour = the panorama's horizon
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.color = Hex("#FFF6E8");
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(58f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = Sky();
            RenderSettings.ambientMode = AmbientMode.Trilight;   // the brief's gradient; skybox ambient would over-blue the critters
            RenderSettings.ambientSkyColor = Hex("#9CC3EE");
            RenderSettings.ambientEquatorColor = Hex("#B9D3EE");
            RenderSettings.ambientGroundColor = Hex("#3D6FB0");
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 30f; RenderSettings.fogEndDistance = 160f; RenderSettings.fogColor = Hex("#4A92E2");

            PrefabUtility.InstantiatePrefab(arena);
            PrefabUtility.InstantiatePrefab(backdrop);
            var fx = CubeCritterSetup.FxPrefabs;
            PrefabUtility.InstantiatePrefab(fx["VFX_SnowAmbient"]);
            PrefabUtility.InstantiatePrefab(fx["VFX_SplatDecals"]);

            // camera: over-the-shoulder rig + local-only shake + post
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; cam.fieldOfView = 55f; cam.farClipPlane = 400f; cam.nearClipPlane = 0.1f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<OverShoulderCamera>();
            camGo.AddComponent<CameraShake>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camGo.transform.SetPositionAndRotation(new Vector3(9f, 6f, -14f), Quaternion.Euler(18f, -30f, 0f));
            new GameObject("Post FX").AddComponent<Volume>().sharedProfile = PostProfile();

            // spawns (ice brief 3: 4 m from two edges, 8 m to the nearest neighbours) and the two spring spots
            var markers = new GameObject("MiniGameScene").AddComponent<MiniGameScene>();
            Transform Marker(string n, Vector3 p) { var t = new GameObject(n).transform; t.position = p; t.SetParent(markers.transform, true); return t; }
            var spots = new[] { new Vector3(-4f, 0.05f, -4f), new Vector3(4f, 0.05f, 4f), new Vector3(4f, 0.05f, -4f), new Vector3(-4f, 0.05f, 4f) };
            markers.spawnPoints = spots.Select((p, i) => Marker("Spawn_" + i, p)).ToArray();
            markers.pickupSpots = new[] { Marker("Pickup_0", new Vector3(-4f, 0f, 0f)), Marker("Pickup_1", new Vector3(4f, 0f, 0f)) };
            markers.pickupPrefab = pickup.GetComponent<NetworkObject>();

            // nothing is baked (realtime only), but generating once stores the ambient/reflection data with the scene
            var ls = new LightingSettings { name = "LS_KnockbackArena_Ice", bakedGI = false, realtimeGI = false };
            var lsPath = $"{Root}/Scenes/LS_KnockbackArena_Ice.lighting";
            AssetDatabase.DeleteAsset(lsPath); AssetDatabase.CreateAsset(ls, lsPath);
            Lightmapping.lightingSettings = ls;
            EditorSceneManager.SaveScene(scene, ScenePath);
            Lightmapping.Bake();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static VolumeProfile PostProfile()
        {
            var path = $"{Root}/Scenes/KA_Arena_PostFX.asset";
            AssetDatabase.DeleteAsset(path);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, path);
            var bloom = prof.Add<Bloom>(true);
            bloom.threshold.Override(1.1f); bloom.intensity.Override(0.3f); bloom.scatter.Override(0.6f);   // >= 1.1 keeps chartreuse from blooming into the ice
            prof.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var ca = prof.Add<ColorAdjustments>(true);
            ca.saturation.Override(8f); ca.postExposure.Override(0f);
            prof.Add<Vignette>(true).intensity.Override(0.15f);
            foreach (var comp in prof.components) AssetDatabase.AddObjectToAsset(comp, prof);
            EditorUtility.SetDirty(prof);
            return prof;
        }
    }
}
