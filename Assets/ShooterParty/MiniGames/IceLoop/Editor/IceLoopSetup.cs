using System.IO;
using System.Linq;
using IceLoop.UI;
using ShooterParty;
using ShooterParty.EditorTools;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IceLoop.EditorTools
{
    // Builds everything Ice Loop owns from code (the generated files are not hand-edited): the track mesh and
    // scene, the sliding critter variant, the shot effect, the HUD widget, the controller prefab and the definition.
    // The shared art kit (ice material, sky, post FX, VFX) is loaded from the Knockback Arena folder.
    public static class IceLoopSetup
    {
        const string Root = "Assets/ShooterParty/MiniGames/IceLoop";
        const string Art = "Assets/ShooterParty/MiniGames/KnockbackArena";
        public const string ScenePath = Root + "/Scenes/IceLoop_Course.unity";
        public const string DefinitionPath = Root + "/Data/IceLoop.asset";
        public const string ControllerPath = Root + "/Prefabs/Net_IceLoopGame.prefab";
        const float HalfWidth = 4f, Thickness = 1.4f, WaterY = -2f;

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        [MenuItem("Ice Loop/Rebuild Everything (track, scene, assets)")]
        public static void RebuildEverything()
        {
            BuildAll();
            // Only the catalog and the network prefab list, not ShooterPartySetup.BuildAll(): that one regenerates the
            // SP_* scenes and bakes their lighting, and a bake without an OpenCL GPU wrecks their reflection probes.
            ShooterPartySetup.BuildCatalog();
            ShooterPartySetup.BuildNetPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("[IceLoop] everything rebuilt");
        }

        public static void BatchRebuildEverything() => RebuildEverything();

        public static void BuildAll()
        {
            foreach (var d in new[] { "Data", "Prefabs", "Scenes", "UI" }) Directory.CreateDirectory($"{Root}/{d}");
            var critter = BuildCritter();
            var effect = BuildEffect();
            var widget = BuildWidget();
            var def = BuildMiniGame(critter, effect, widget);
            BuildScene();
            ShooterPartySetup.EnsureInBuildSettings(ScenePath);
            EditorUtility.SetDirty(def);
        }

        // ------------------------------------------------------------------ bumper car
        // The shared critter sitting in a bumper car, with a slower, floatier motor: it takes ~1 s to change direction,
        // so bends have to be planned. Prefab variant of CubeCritter_Net, so the shared art and network setup stay in one place.
        static NetworkObject BuildCritter()
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Art + "/Prefabs/CubeCritter_Net.prefab"));
            var motor = inst.GetComponent<CritterMotor>();
            motor.moveSpeed = 9f;
            motor.acceleration = 9f;
            motor.airControl = 0.25f;
            motor.dashSpeed = 12.5f;
            motor.dashTime = 0.22f;
            motor.dashRecharge = 2.2f;
            motor.hitStun = 0.2f;   // a bump should jolt, not freeze

            // the critter sits up on the car's seat
            var anim = inst.GetComponentInChildren<CritterAnimator>();
            if (anim) { var t = anim.transform; while (t.parent && t.parent != inst.transform) t = t.parent; t.localPosition += Vector3.up * 0.32f; }

            var body = Mat("M_IceLoop_CarBody", Color.white, 0.35f);
            var rubber = Mat("M_IceLoop_CarRubber", Hex("#1B1F2A"), 0.15f);
            var chrome = Mat("M_IceLoop_CarChrome", Hex("#B8C2CC"), 0.55f);
            chrome.SetFloat("_Metallic", 0.3f);
            var lamp = Mat("M_IceLoop_CarLamp", Hex("#FFF1B8"), 0.3f);
            Emissive(lamp, Hex("#FFE27A"), 0.25f);

            var car = new GameObject("Car").transform;
            car.SetParent(inst.transform, false);
            Prim("Bumper", PrimitiveType.Cylinder, car, Vector3.zero, new Vector3(2.15f, 0.08f, 2.15f), rubber).transform.localPosition = new Vector3(0f, 0.17f, 0f);
            var shell = Prim("Shell", PrimitiveType.Cylinder, car, Vector3.zero, new Vector3(1.85f, 0.11f, 1.85f), body);
            shell.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            Prim("Rim", PrimitiveType.Cylinder, car, Vector3.zero, new Vector3(1.25f, 0.06f, 1.25f), chrome).transform.localPosition = new Vector3(0f, 0.5f, 0f);
            Prim("Pole", PrimitiveType.Cylinder, car, Vector3.zero, new Vector3(0.07f, 0.8f, 0.07f), chrome).transform.localPosition = new Vector3(0f, 1.1f, -0.7f);
            var beacon = Prim("Beacon", PrimitiveType.Sphere, car, Vector3.zero, new Vector3(0.22f, 0.22f, 0.22f), lamp);
            beacon.transform.localPosition = new Vector3(0f, 1.95f, -0.7f);
            foreach (var x in new[] { -0.5f, 0.5f })
                Prim("Headlight", PrimitiveType.Sphere, car, Vector3.zero, new Vector3(0.24f, 0.18f, 0.12f), lamp).transform.localPosition = new Vector3(x, 0.34f, 0.93f);
            foreach (var r in car.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.On; }
            var ic = car.gameObject.AddComponent<IceCar>();
            ic.tinted = new[] { shell.GetComponent<Renderer>() };
            ic.beacon = beacon.transform;

            var p = PrefabUtility.SaveAsPrefabAsset(inst, Root + "/Prefabs/CubeCritter_Ice.prefab");
            Object.DestroyImmediate(inst);
            return p.GetComponent<NetworkObject>();
        }

        static void Emissive(Material m, Color c, float intensity)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        static IceLoopShotEffect BuildEffect()
        {
            var path = Root + "/Data/ShotEffect_IceLoop.asset";
            var e = Load<IceLoopShotEffect>(path);
            if (!e) { e = ScriptableObject.CreateInstance<IceLoopShotEffect>(); AssetDatabase.CreateAsset(e, path); }
            e.impulse = 6f;
            EditorUtility.SetDirty(e);
            return e;
        }

        static GameObject BuildWidget()
        {
            var go = new GameObject("HUD_IceLoopWidget", typeof(RectTransform));
            go.AddComponent<IceLoopHudWidget>();
            var p = PrefabUtility.SaveAsPrefabAsset(go, Root + "/UI/HUD_IceLoopWidget.prefab");
            Object.DestroyImmediate(go);
            return p;
        }

        static MiniGameDefinition BuildMiniGame(NetworkObject critter, ShotEffect effect, GameObject widget)
        {
            var def = Load<MiniGameDefinition>(DefinitionPath);
            if (!def) { def = ScriptableObject.CreateInstance<MiniGameDefinition>(); AssetDatabase.CreateAsset(def, DefinitionPath); }
            def.id = "ice_loop";
            def.displayName = "Ice Loop";
            def.tagline = "Bumper cars on ice. Most checkpoints wins.";
            def.howToPlay = new[]
            {
                "Race round the loop for 2 minutes. Every checkpoint you pass is a point, and the loop repeats.",
                "The ice is slippery: brake early for bends. Dash (Shift) on the straights.",
                "Bump rivals and spit at them to send them into the water. A swim restarts you at your last checkpoint.",
            };
            def.controlsHint = "WASD move · Mouse aim · Hold click to spit · Shift dash";
            def.sceneName = "IceLoop_Course";
            def.critterPrefab = critter;
            def.minPlayers = 3; def.maxPlayers = 4; def.durationSeconds = 120f;
            def.placementPoints = new[] { 4, 3, 2, 1 };
            def.supportsBots = true;
            def.critter = new CritterConfig { jump = CritterConfig.JumpMode.Off, dashCharges = 3, shotSpeed = 14f, shotGravity = 3f, shotCooldown = 0.6f };
            def.shotEffect = effect;
            def.icon = Load<Sprite>(Art + "/UI/Sprites/UI_IconDash.png");
            def.hudWidget = widget;

            var go = new GameObject("Net_IceLoopGame");
            go.AddComponent<NetworkObject>();
            var game = go.AddComponent<IceLoopGame>();
            game.Definition = def;
            var poof = Load<GameObject>(Art + "/VFX/VFX_FallPoof.prefab");
            game.fallPoof = poof ? poof.GetComponent<ParticleSystem>() : null;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, ControllerPath);
            Object.DestroyImmediate(go);
            def.controllerPrefab = prefab.GetComponent<NetworkObject>();
            return def;
        }

        // ------------------------------------------------------------------ track mesh
        // A slab along the centre line: top (with snowy rims), two cliffs, underside. KA/Ice reads the vertex colour:
        // R = height (0 deep blue, 0.5 side, 1 top), G = snow, B = ambient occlusion, A = sparkle. Own vertices per face.
        const int Per = 10;   // vertices per section
        static Mesh BuildTrackMesh(Vector3[] c)
        {
            int n = c.Length;
            var v = new Vector3[n * Per]; var uv = new Vector2[n * Per]; var col = new Color[n * Per];
            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                var f = (c[(i + 1) % n] - c[(i + n - 1) % n]); f.y = 0f; f.Normalize();
                var r = new Vector3(f.z, 0f, -f.x);
                Vector3 L = c[i] - r * HalfWidth, R = c[i] + r * HalfWidth, down = Vector3.up * Thickness;
                // 0..3 top: rim L, inner L, inner R, rim R | 4,5 right cliff top/bottom | 6,7 left cliff top/bottom | 8,9 underside L/R
                int o = i * Per;
                v[o + 0] = L; v[o + 1] = L + r * 1.1f; v[o + 2] = R - r * 1.1f; v[o + 3] = R;
                v[o + 4] = R; v[o + 5] = R - down; v[o + 6] = L; v[o + 7] = L - down; v[o + 8] = L - down; v[o + 9] = R - down;
                // a gentle wobble so the road is not one flat colour, and the snow rim thins out in places
                float wob = 0.5f + 0.5f * Mathf.Sin(i * 0.37f);
                float rim = 0.55f + 0.4f * wob;
                col[o + 0] = new Color(1f, rim, 1f, 0.3f); col[o + 1] = new Color(1f, 0f, 1f, 0.3f);
                col[o + 2] = new Color(1f, 0f, 1f, 0.3f); col[o + 3] = new Color(1f, rim, 1f, 0.3f);
                col[o + 4] = new Color(0.62f, 0.15f, 0.95f, 0.5f); col[o + 5] = new Color(0f, 0f, 0.55f, 0f);
                col[o + 6] = new Color(0.62f, 0.15f, 0.95f, 0.5f); col[o + 7] = new Color(0f, 0f, 0.55f, 0f);
                col[o + 8] = new Color(0f, 0f, 0.45f, 0f); col[o + 9] = new Color(0f, 0f, 0.45f, 0f);
                float d = dist / 6f;
                for (int k = 0; k < Per; k++) uv[o + k] = new Vector2(k % 2, d);
                dist += Vector3.Distance(c[i], c[(i + 1) % n]);
            }
            var t = new System.Collections.Generic.List<int>();
            for (int i = 0; i < n; i++)
            {
                int a = i * Per, b = ((i + 1) % n) * Per;
                for (int k = 0; k < 3; k++)   // top, +y: three strips across (rim, middle, rim)
                    t.AddRange(new[] { a + k, b + k, a + k + 1, a + k + 1, b + k, b + k + 1 });
                t.AddRange(new[] { a + 4, b + 4, a + 5, a + 5, b + 4, b + 5 });   // right cliff
                t.AddRange(new[] { a + 6, a + 7, b + 6, a + 7, b + 7, b + 6 });   // left cliff
                t.AddRange(new[] { a + 8, a + 9, b + 8, a + 9, b + 9, b + 8 });   // underside
            }
            var mesh = new Mesh { name = "IceTrack", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = v; mesh.uv = uv; mesh.colors = col; mesh.triangles = t.ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ materials
        // Lit materials with no sky reflection and no sun glint: the sky panorama is bright, and on a flat quad or a
        // glossy shell its reflection (and the sun's glint at a grazing angle) burns straight to white.
        static Material Mat(string name, Color c, float smooth = 0.5f, bool unlit = false)
        {
            var path = $"{Root}/Prefabs/{name}.mat";
            var m = Load<Material>(path);
            var sh = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
            m.SetColor("_BaseColor", c);
            if (!unlit)
            {
                m.SetFloat("_Smoothness", Mathf.Min(smooth, 0.3f));
                m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Checker()
        {
            var texPath = Root + "/Prefabs/T_Checker.png";
            var tex = new Texture2D(16, 4);
            for (int x = 0; x < 16; x++) for (int y = 0; y < 4; y++) tex.SetPixel(x, y, (x + y) % 2 == 0 ? Color.white : Hex("#14243F"));
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.SaveAndReimport();
            var m = Mat("M_IceLoop_Checker", Color.white, 0.2f, true);
            m.SetTexture("_BaseMap", Load<Texture2D>(texPath));
            return m;
        }

        static PhysicsMaterial TrackPhysics()
        {
            var path = Root + "/Prefabs/PM_IceTrack.physicMaterial";
            var pm = Load<PhysicsMaterial>(path);
            if (!pm) { pm = new PhysicsMaterial("PM_IceTrack"); AssetDatabase.CreateAsset(pm, path); }
            pm.dynamicFriction = 0.05f; pm.staticFriction = 0.05f; pm.bounciness = 0f;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        // ------------------------------------------------------------------ scene
        static GameObject Prim(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());   // decoration only: never blocks a critter or a spit
            g.transform.SetParent(parent, false);
            g.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
            g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        // Our own post profile: Knockback's blooms at 1.1, which makes bright cars and a sunlit road glow all over.
        static VolumeProfile PostProfile()
        {
            var path = Root + "/Scenes/IceLoop_PostFX.asset";
            AssetDatabase.DeleteAsset(path);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, path);
            var bloom = prof.Add<Bloom>(true);
            bloom.threshold.Override(1.8f); bloom.intensity.Override(0.12f); bloom.scatter.Override(0.5f);
            prof.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var ca = prof.Add<ColorAdjustments>(true);
            ca.saturation.Override(0f); ca.postExposure.Override(-0.5f);
            prof.Add<Vignette>(true).intensity.Override(0.15f);
            foreach (var comp in prof.components) AssetDatabase.AddObjectToAsset(comp, prof);
            EditorUtility.SetDirty(prof);
            return prof;
        }

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ice = Load<Material>(Art + "/Materials/M_Ice.mat") ?? Mat("M_IceLoop_Fallback", Hex("#92BEE8"), 0.9f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 0.95f; sun.color = Hex("#FFF6E8");
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(52f, -50f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = Load<Material>(Art + "/Materials/M_Sky.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#9CC3EE");
            RenderSettings.ambientEquatorColor = Hex("#B9D3EE");
            RenderSettings.ambientGroundColor = Hex("#3D6FB0");
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.2f;   // the critters' clearcoat mirrors the bright panorama and burns out; turn the sky reflection down
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90f; RenderSettings.fogEndDistance = 420f; RenderSettings.fogColor = Hex("#9CC3EE");

            // the road
            var centre = IceTrackLayout.Resample(2f);
            var road = new GameObject("Track");
            var meshPath = Root + "/Prefabs/Mesh_IceTrack.asset";
            var mesh = BuildTrackMesh(centre);
            AssetDatabase.DeleteAsset(meshPath); AssetDatabase.CreateAsset(mesh, meshPath);
            road.AddComponent<MeshFilter>().sharedMesh = mesh;
            road.AddComponent<MeshRenderer>().sharedMaterial = ice;
            var mc = road.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.sharedMaterial = TrackPhysics();
            var track = road.AddComponent<IceTrack>();
            track.centre = centre; track.halfWidth = HalfWidth; track.gateStride = 10; track.waterY = WaterY;
            GameObjectUtility.SetStaticEditorFlags(road, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            // water: the lake under the road (visual only, the server decides when a critter is in it)
            var water = Prim("Water", PrimitiveType.Quad, null, new Vector3(-5f, WaterY, 30f), new Vector3(700f, 700f, 1f),
                             Mat("M_IceLoop_Water", Hex("#2F78B8"), 0f, unlit: true), Quaternion.Euler(90f, 0f, 0f));
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // checkpoint gates (an arch over the road every ~20 m) and the checkered start/finish line
            var gates = new GameObject("Gates").transform;
            var gateMat = Mat("M_IceLoop_Gate", Hex("#FFC23D"), 0.6f);
            var startMat = Mat("M_IceLoop_GateStart", Hex("#FF4F6E"), 0.6f);
            Emissive(gateMat, Hex("#FFB300"), 0.15f); Emissive(startMat, Hex("#FF2D55"), 0.2f);
            var checker = Checker();
            for (int g = 0; g < track.GateCount; g++)
            {
                var pose = track.GatePose(g);
                var m = g == 0 ? startMat : gateMat;
                var root = new GameObject(g == 0 ? "Gate_Start" : "Gate_" + g).transform;
                root.SetParent(gates, false); root.SetPositionAndRotation(pose.position, pose.rotation);
                float off = HalfWidth - 0.45f;
                foreach (var sign in new[] { -1f, 1f })
                {
                    var foot = pose.position + root.right * off * sign;
                    Prim("Footing", PrimitiveType.Cube, root, foot + Vector3.up * 0.25f, new Vector3(1f, 0.5f, 1f), ice, pose.rotation);
                    Prim("Post", PrimitiveType.Cylinder, root, foot + Vector3.up * 1.9f, new Vector3(0.38f, 1.4f, 0.38f), m);
                }
                Prim("Beam", PrimitiveType.Cube, root, pose.position + Vector3.up * 3.35f, new Vector3(off * 2f + 0.6f, 0.45f, 0.45f), g == 0 ? checker : m, pose.rotation);
                if (g == 0)
                    Prim("Line", PrimitiveType.Quad, root, pose.position + Vector3.up * 0.03f, new Vector3(HalfWidth * 2f, 1.2f, 1f), checker, pose.rotation * Quaternion.Euler(90f, 0f, 0f));
            }

            // floes and crystals round the lake from the shared kit (fixed seed, never closer than 14 m to the road)
            var bergs = new GameObject("Floes").transform;
            var rng = new System.Random(7);
            var kit = new[] { "Floe_A", "Floe_B", "Floe_C" }.Select(n => Load<GameObject>($"{Art}/Prefabs/Arena/Kit/{n}.prefab")).Where(p => p).ToArray();
            var crystals = new[] { "Crystal_Cluster_A", "Crystal_Cluster_B", "Crystal_Cluster_C" }.Select(n => Load<GameObject>($"{Art}/Prefabs/Arena/Kit/{n}.prefab")).Where(p => p).ToArray();
            for (int placed = 0, tries = 0; kit.Length > 0 && placed < 40 && tries < 2000; tries++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 380 - 210), 0f, (float)(rng.NextDouble() * 320 - 130));
                int near = track.Nearest(p);
                var q = track.Point(near);
                if (Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(q.x, 0f, q.z)) < 14f) continue;
                var floe = (GameObject)PrefabUtility.InstantiatePrefab(kit[rng.Next(kit.Length)], bergs);
                float s = 1.2f + (float)rng.NextDouble() * 2.2f;
                floe.transform.localScale = Vector3.one * s;
                floe.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                floe.transform.position = p;
                var bounds = floe.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
                floe.transform.position += Vector3.up * (WaterY + 0.45f - bounds.max.y);   // the top sits just above the water
                if (crystals.Length > 0 && rng.NextDouble() < 0.5)
                {
                    var cr = (GameObject)PrefabUtility.InstantiatePrefab(crystals[rng.Next(crystals.Length)], bergs);
                    cr.transform.position = new Vector3(p.x, WaterY + 0.45f, p.z);
                    cr.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                }
                placed++;
            }

            foreach (var fx in new[] { "VFX_SnowAmbient", "VFX_SplatDecals" })
            {
                var prefab = Load<GameObject>($"{Art}/VFX/{fx}.prefab");
                if (prefab) PrefabUtility.InstantiatePrefab(prefab);
            }

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; cam.fieldOfView = 65f; cam.farClipPlane = 600f; cam.nearClipPlane = 0.1f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<OverShoulderCamera>();
            follow.distance = 6.5f; follow.height = 2.6f; follow.shoulder = 0f;   // a car needs more room than a walking critter
            camGo.AddComponent<CameraShake>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var grid = track.GridPose(0);
            camGo.transform.SetPositionAndRotation(grid.position - grid.rotation * Vector3.forward * 8f + Vector3.up * 5f, Quaternion.Euler(18f, grid.rotation.eulerAngles.y, 0f));
            new GameObject("Post FX").AddComponent<Volume>().sharedProfile = PostProfile();

            var markers = new GameObject("MiniGameScene").AddComponent<MiniGameScene>();
            markers.spawnPoints = Enumerable.Range(0, 4).Select(i =>
            {
                var t = new GameObject("Spawn_" + i).transform;
                t.SetParent(markers.transform, false);
                t.position = track.GridPose(i).position;
                return t;
            }).ToArray();

            var ls = new LightingSettings { name = "LS_IceLoop", bakedGI = false, realtimeGI = false };
            var lsPath = Root + "/Scenes/LS_IceLoop.lighting";
            AssetDatabase.DeleteAsset(lsPath); AssetDatabase.CreateAsset(ls, lsPath);
            Lightmapping.lightingSettings = ls;
            EditorSceneManager.SaveScene(scene, ScenePath);
            Lightmapping.Bake();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
