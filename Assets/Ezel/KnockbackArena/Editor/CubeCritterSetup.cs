using System.Collections.Generic;
using System.IO;
using System.Linq;
using KnockbackArena.Playground;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ShooterParty;
using ShooterParty.UI;

namespace KnockbackArena.EditorTools
{
    // Import settings for the Blender exports (re-applied on every re-export).
    public class CubeCritterImport : AssetPostprocessor
    {
        static readonly string[] Loops = { "Idle", "Run", "Fall", "VictoryLoop", "DefeatLoop" };

        void OnPreprocessModel()
        {
            if (!assetPath.EndsWith("/CubeCritter.fbx") && !assetPath.EndsWith("/SpitBlob.fbx")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importBlendShapes = true;
            mi.importBlendShapeNormals = ModelImporterNormals.Calculate;  // collapsed face islands need recomputed normals
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            if (assetPath.EndsWith("/CubeCritter.fbx"))
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
            }
        }

        void OnPostprocessModel(GameObject g)
        {
            if (!assetPath.EndsWith("/CubeCritter.fbx")) return;
            // Blender's FBX exporter writes the face of the LAST baked take as the default weights; reset to neutral
            foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>())
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++) smr.SetBlendShapeWeight(i, 0f);
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.EndsWith("/CubeCritter.fbx")) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;   // one per Blender action, named "CubeCritter|CC_<Clip>"
            foreach (var c in clips)
            {
                var n = c.takeName.Split('|').Last();
                if (n.StartsWith("CC_")) n = n.Substring(3);
                c.name = n;
                c.loopTime = Loops.Contains(n);
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/KnockbackArena/Textures/T_CC_")) return;
            var ti = (TextureImporter)assetImporter;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.sRGBTexture = true;
        }
    }

    public static class CubeCritterSetup
    {
        public const string Root = "Assets/Ezel/KnockbackArena";
        public const string Fbx = Root + "/Models/CubeCritter.fbx";
        public const string BlobFbx = Root + "/Models/SpitBlob.fbx";
        public const string ScenePath = Root + "/Scenes/CubeCritter_Playground.unity";

        // Colour-blind-checked palette; emblem atlas cell (UV offset) per player: dot, ring, stripes, chevron.
        public static Dictionary<string, ParticleSystem> FxPrefabs;   // VFX prefabs from the last rebuild

        public static readonly (string name, string hex, Vector2 cell)[] Players =
        {
            ("Tomato", "#FF4F2E", new Vector2(0f, 0.5f)), ("Teal", "#00B3A4", new Vector2(0.5f, 0.5f)),
            ("Chartreuse", "#D4F21C", new Vector2(0f, 0f)), ("Violet", "#7B2FD0", new Vector2(0.5f, 0f)),
        };

        [MenuItem("Knockback Arena/Rebuild CubeCritter (materials, animator, prefabs, playground)")]
        public static void RebuildAll()
        {
            foreach (var d in new[] { "Materials", "Prefabs", "Scenes", "Animation" })
                Directory.CreateDirectory(Path.Combine(Root, d));
            AssetDatabase.ImportAsset(Fbx, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(BlobFbx, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Root + "/Textures/T_CC_Blush.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Root + "/Textures/T_CC_Emblems.png", ImportAssetOptions.ForceUpdate);

            foreach (var stale in new[] { "Prefabs/VFX_SpitPop.prefab", "Prefabs/VFX_SpitSplat.prefab", "Materials/M_CC_FX_Goo.mat", "Textures/T_CC_GooDot.png" })
                AssetDatabase.DeleteAsset($"{Root}/{stale}");   // v2.0 placeholders, replaced by VFX/
            AudioSetup.BuildBank();
            var mats = BuildMaterials();
            var mask = BuildMask();
            var ac = BuildController(mask);
            FxPrefabs = VfxSetup.BuildAll();
            var blob = BuildBlobPrefab(mats["Goo_Tomato"]);
            var prefabs = BuildCritterPrefabs(ac, blob, mats);
            BuildPlayground(prefabs, mats);
            AssetDatabase.SaveAssets();
            Debug.Log("[CubeCritter] rebuild done");
        }

        // ------------------------------------------------------------------ materials (URP)
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static Material Mat(string name, string shader, Color color, float smoothness)
        {
            var path = $"{Root}/Materials/M_CC_{name}.mat";
            var sh = Shader.Find(shader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void ClearCoat(Material m, float coatSmoothness)
        {
            m.SetFloat("_ClearCoat", 1f);
            m.EnableKeyword("_CLEARCOAT");
            m.SetFloat("_ClearCoatMask", 1f);
            m.SetFloat("_ClearCoatSmoothness", coatSmoothness);
        }

        static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static void Cutout(Material m)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }

        static Dictionary<string, Material> BuildMaterials()
        {
            var blush = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_CC_Blush.png");
            var emblems = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_CC_Emblems.png");
            var d = new Dictionary<string, Material>();
            d["Eye"] = Mat("Eye", "Universal Render Pipeline/Complex Lit", Hex("#0D0D12"), 0.92f);
            ClearCoat(d["Eye"], 0.97f);
            d["EyeShine"] = Mat("EyeShine", "Universal Render Pipeline/Unlit", Color.white, 0f);
            d["Mouth"] = Mat("Mouth", "Universal Render Pipeline/Lit", Hex("#3A1020"), 0.55f);
            d["Tongue"] = Mat("Tongue", "Universal Render Pipeline/Lit", Hex("#FF7A8A"), 0.7f);
            d["Feet"] = Mat("Feet", "Universal Render Pipeline/Lit", Hex("#3B2A24"), 0.7f);
            var b = Mat("Blush", "Universal Render Pipeline/Unlit", new Color(1f, 0.35f, 0.47f, 0.85f), 0f);
            b.SetTexture("_BaseMap", blush);
            Transparent(b);
            d["Blush"] = b;
            foreach (var (name, hex, cell) in Players)
            {
                var body = Mat("Body_" + name, "Universal Render Pipeline/Complex Lit", Hex(hex), 0.62f);
                ClearCoat(body, 0.93f);
                d["Body_" + name] = body;
                var e = Mat("Emblem_" + name, "Universal Render Pipeline/Lit", Color.Lerp(Hex(hex), Color.white, 0.45f), 0.62f);
                e.SetTexture("_BaseMap", emblems);
                e.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));
                e.SetTextureOffset("_BaseMap", cell);
                Cutout(e);
                d["Emblem_" + name] = e;
            }
            d["Arena"] = Mat("Playground_Ground", "Universal Render Pipeline/Lit", Hex("#B9D9F2"), 0.25f);
            foreach (var (name, hex, _) in Players)
            {
                // candy goo (KA/Goo): chartreuse lifts toward lemon rather than white so it never reads as snot
                var g = Mat("Goo_" + name, "KA/Goo", Hex(hex), 0.92f);
                g.SetColor("_CoreColor", name == "Chartreuse" ? Hex("#F2FF8A") : Color.white);
                d["Goo_" + name] = g;
            }
            var shadow = VfxSetup.FxMat("BlobShadow");
            shadow.SetColor("_BaseColor", Hex("#1D3F7A"));
            d["Shadow"] = shadow;
            return d;
        }

        public static void ApplyPlayerMaterials(GameObject go, string player, Dictionary<string, Material> mats)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    var n = ms[i] ? ms[i].name : "";
                    if (n.StartsWith("M_CC_Body")) ms[i] = mats["Body_" + player];
                    else if (n.StartsWith("M_CC_Emblem")) ms[i] = mats["Emblem_" + player];
                    else if (n.StartsWith("M_CC_")) { var k = n.Substring(5); if (mats.ContainsKey(k)) ms[i] = mats[k]; }
                }
                r.sharedMaterials = ms;
            }
        }

        // ------------------------------------------------------------------ animator
        static Dictionary<string, AnimationClip> Clips() =>
            AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name, c => c);

        static AvatarMask BuildMask()
        {
            // upper body + face: Body subtree and the mesh (blend shapes). Root/COG/feet keep running underneath.
            var path = $"{Root}/Animation/CC_UpperBodyMask.mask";
            AssetDatabase.DeleteAsset(path);
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
            var mask = new AvatarMask();
            mask.AddTransformPath(tmp.transform, true);
            for (int i = 0; i < mask.transformCount; i++)
            {
                var p = mask.GetTransformPath(i);
                mask.SetTransformActive(i, p.Contains("/COG/Body") || p == "CubeCritter_Mesh");
            }
            Object.DestroyImmediate(tmp);
            AssetDatabase.CreateAsset(mask, path);
            return mask;
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, Motion m, float x, float y)
        {
            var s = sm.AddState(name, new Vector3(x, y, 0f));
            s.motion = m;
            s.writeDefaultValues = false;   // every clip keys every channel, and empty override states must not reset the base
            return s;
        }

        static AnimatorStateTransition Go(AnimatorState a, AnimatorState b, float duration, float exitTime = -1f)
        {
            var t = a.AddTransition(b);
            t.hasFixedDuration = true;
            t.duration = duration;
            t.hasExitTime = exitTime >= 0f;
            if (exitTime >= 0f) t.exitTime = exitTime;
            return t;
        }

        static AnimatorController BuildController(AvatarMask mask)
        {
            var path = $"{Root}/Animation/CubeCritter.controller";
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            var clips = Clips();
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter(new AnimatorControllerParameter { name = "RunRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ac.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            foreach (var t in new[] { "Spit", "Hit", "Jump", "Win", "Lose", "Taunt" })
                ac.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var bt = new BlendTree { name = "Locomotion", blendParameter = "Speed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(bt, ac);
            bt.AddChild(clips["Idle"], 0f);
            bt.AddChild(clips["Run"], 1.2f);
            var loco = State(sm, "Locomotion", bt, 300, 0);
            loco.speedParameterActive = true;
            loco.speedParameter = "RunRate";
            sm.defaultState = loco;
            var jump = State(sm, "Jump", clips["Jump"], 560, -120);
            var air = State(sm, "Airborne", clips["Fall"], 820, 0);
            var land = State(sm, "Land", clips["Land"], 560, 120);
            var hit = State(sm, "Hit", clips["Hit"], 300, 220);
            var taunt = State(sm, "Taunt", clips["Taunt"], 40, -120);
            var win = State(sm, "Victory", clips["Victory"], 40, 120);
            var winLoop = State(sm, "VictoryLoop", clips["VictoryLoop"], 40, 240);
            var lose = State(sm, "Defeat", clips["Defeat"], 560, 300);
            var loseLoop = State(sm, "DefeatLoop", clips["DefeatLoop"], 820, 300);

            Go(loco, jump, 0.04f).AddCondition(AnimatorConditionMode.If, 0, "Jump");
            Go(jump, air, 0.08f, 0.95f);
            Go(jump, land, 0.03f, 0.5f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            Go(loco, air, 0.12f).AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            Go(air, land, 0.03f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            Go(land, loco, 0.1f, 0.55f);
            var anyHit = sm.AddAnyStateTransition(hit);
            anyHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            anyHit.duration = 0.02f; anyHit.hasFixedDuration = true; anyHit.canTransitionToSelf = true;
            Go(hit, air, 0.1f, 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            Go(hit, loco, 0.15f, 0.9f);
            Go(loco, taunt, 0.06f).AddCondition(AnimatorConditionMode.If, 0, "Taunt");
            Go(taunt, loco, 0.12f, 0.95f);
            Go(taunt, loco, 0.12f).AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            Go(loco, win, 0.08f).AddCondition(AnimatorConditionMode.If, 0, "Win");
            Go(win, winLoop, 0.05f, 1f);
            Go(winLoop, loco, 0.2f).AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            var anyLose = sm.AddAnyStateTransition(lose);
            anyLose.AddCondition(AnimatorConditionMode.If, 0, "Lose");
            anyLose.duration = 0.1f; anyLose.hasFixedDuration = true;
            Go(lose, loseLoop, 0.05f, 1f);
            Go(loseLoop, loco, 0.2f).AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");

            // Action layer: spit over anything (run, idle, airborne). CritterAnimator drives its weight.
            var asm = new AnimatorStateMachine { name = "Action", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(asm, ac);
            var empty = State(asm, "Empty", null, 300, 0);
            asm.defaultState = empty;
            var spit = State(asm, "Spit", clips["Spit"], 300, 120);
            var anySpit = asm.AddAnyStateTransition(spit);
            anySpit.AddCondition(AnimatorConditionMode.If, 0, "Spit");
            anySpit.duration = 0f; anySpit.hasFixedDuration = true; anySpit.canTransitionToSelf = true;
            Go(spit, empty, 0.05f, 0.97f);
            ac.AddLayer(new AnimatorControllerLayer
            {
                name = "Action", stateMachine = asm, avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override, defaultWeight = 0f,
            });
            EditorUtility.SetDirty(ac);
            return ac;
        }

        // ------------------------------------------------------------------ prefabs
        static PhysicsMaterial Slippery()
        {
            var path = $"{Root}/Materials/PM_Critter.physicMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (!pm) { pm = new PhysicsMaterial("PM_Critter"); AssetDatabase.CreateAsset(pm, path); }
            pm.dynamicFriction = 0.4f; pm.staticFriction = 0.45f; pm.bounciness = 0f;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;
            return pm;
        }

        static SpitBlob BuildBlobPrefab(Material goo)
        {
            var go = new GameObject("SpitBlob");
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BlobFbx), go.transform);
            vis.name = "Visual";
            // Unity 6 imports this blend-shape-only mesh with a plain MeshRenderer, which never applies the
            // Stretch / Squash weights: swap in a SkinnedMeshRenderer on the same mesh
            PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var mf in vis.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh; var host = mf.gameObject;
                Object.DestroyImmediate(host.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(mf);
                var smr = host.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.updateWhenOffscreen = true;   // tiny, fast and stretched: never cull by stale bounds
            }
            foreach (var r in vis.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = goo; r.shadowCastingMode = ShadowCastingMode.On; }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.2f; rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<SphereCollider>().radius = 0.15f;   // hitbox; the visual is 0.17
            var trail = (ParticleSystem)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<ParticleSystem>(Root + "/VFX/Tint_Trail.prefab"), go.transform);
            var ribbonGo = new GameObject("Ribbon");
            ribbonGo.transform.SetParent(go.transform, false);
            var ribbon = ribbonGo.AddComponent<TrailRenderer>();
            ribbon.time = 0.15f; ribbon.minVertexDistance = 0.1f;
            ribbon.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)); ribbon.widthMultiplier = 0.14f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.38f, 0f), new GradientAlphaKey(0f, 1f) });
            ribbon.colorGradient = g;
            ribbon.alignment = LineAlignment.View; ribbon.textureMode = LineTextureMode.Stretch; ribbon.numCapVertices = 4;
            ribbon.shadowCastingMode = ShadowCastingMode.Off; ribbon.receiveShadows = false; ribbon.autodestruct = false;
            ribbon.sharedMaterial = VfxSetup.FxMat("White");
            var blob = go.AddComponent<SpitBlob>();
            blob.visual = vis.transform;
            blob.trail = trail;
            blob.ribbon = ribbon;
            blob.impactFx = FxPrefabs["VFX_Impact"];
            blob.timeoutFx = FxPrefabs["VFX_TimeoutPop"];
            var p = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/SpitBlob.prefab");
            Object.DestroyImmediate(go);
            return p.GetComponent<SpitBlob>();
        }

        static Dictionary<string, GameObject> BuildCritterPrefabs(AnimatorController ac, SpitBlob blob, Dictionary<string, Material> mats)
        {
            var go = new GameObject("CubeCritter");
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx), go.transform);
            vis.name = "Visual";
            var anim = vis.GetComponent<Animator>();   // editor GetComponent returns a fake null, so no ?? here
            if (!anim) anim = vis.AddComponent<Animator>();
            anim.runtimeAnimatorController = ac;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            vis.AddComponent<CritterAnimator>();
            go.AddComponent<Rigidbody>().mass = 1f;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.53f, 0f);
            box.size = new Vector3(0.92f, 0.98f, 0.86f);
            box.sharedMaterial = Slippery();
            go.AddComponent<CritterMotor>();
            var spit = go.AddComponent<CritterSpit>();
            spit.blobPrefab = blob;
            spit.popFx = FxPrefabs["VFX_SpitPop"];
            spit.blobMaterial = mats["Goo_Tomato"];
            var cfx = go.AddComponent<CritterFx>();
            cfx.speedLines = (ParticleSystem)PrefabUtility.InstantiatePrefab(FxPrefabs["VFX_SpeedLines"], go.transform);
            cfx.landPuff = FxPrefabs["VFX_LandPuff"];
            cfx.dashBurst = FxPrefabs["VFX_DashBurst"];
            var sh = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sh.name = "BlobShadow";
            Object.DestroyImmediate(sh.GetComponent<Collider>());
            sh.transform.SetParent(go.transform, false);
            var shr = sh.GetComponent<MeshRenderer>();
            shr.sharedMaterial = mats["Shadow"]; shr.shadowCastingMode = ShadowCastingMode.Off; shr.receiveShadows = false;
            go.AddComponent<BlobShadow>().shadow = shr;
            // one prefab can be painted in any player colour (menu, lobby, podium and the networked critter)
            var look = go.AddComponent<CritterLook>();
            for (int i = 0; i < Players.Length; i++)
            {
                var n = Players[i].name;
                look.body[i] = mats["Body_" + n]; look.emblem[i] = mats["Emblem_" + n]; look.goo[i] = mats["Goo_" + n];
            }
            ApplyPlayerMaterials(vis, "Tomato", mats);
            var basePath = $"{Root}/Prefabs/CubeCritter.prefab";
            var baseAsset = PrefabUtility.SaveAsPrefabAsset(go, basePath);
            Object.DestroyImmediate(go);

            var outp = new Dictionary<string, GameObject>();
            foreach (var (name, _, _) in Players)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset);
                ApplyPlayerMaterials(inst, name, mats);
                // the controller asset was just recreated: set it explicitly, or the variant can record a stale null
                inst.GetComponentInChildren<Animator>().runtimeAnimatorController = ac;
                inst.GetComponent<CritterSpit>().blobMaterial = mats["Goo_" + name];
                inst.name = "CubeCritter_" + name;
                outp[name] = PrefabUtility.SaveAsPrefabAsset(inst, $"{Root}/Prefabs/CubeCritter_{name}.prefab");   // variant of the base
                Object.DestroyImmediate(inst);
            }
            // networked variant (ShooterParty core): only this prefab carries Netcode components. NetworkRigidbody keeps a
            // body kinematic until spawned, and a host start rejects loose NetworkObjects, so offline uses stay on the above.
            var net = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset);
            net.GetComponentInChildren<Animator>().runtimeAnimatorController = ac;
            var no = net.AddComponent<NetworkObject>();
            no.DontDestroyWithOwner = true;          // a leaver's critter stays until the round handles it
            var nt = net.AddComponent<CritterNetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.UseHalfFloatPrecision = true;
            nt.UseQuaternionCompression = true;
            nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;   // critters only yaw
            nt.SyncScaleX = false; nt.SyncScaleY = false; nt.SyncScaleZ = false;
            net.AddComponent<NetworkRigidbody>().UseRigidBodyForMotion = true;
            net.AddComponent<NetworkCritter>();
            net.name = "CubeCritter_Net";
            outp["Net"] = PrefabUtility.SaveAsPrefabAsset(net, $"{Root}/Prefabs/CubeCritter_Net.prefab");
            Object.DestroyImmediate(net);
            return outp;
        }

        // ------------------------------------------------------------------ playground scene
        static void BuildPlayground(Dictionary<string, GameObject> prefabs, Dictionary<string, Material> mats)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.78f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.84f, 0.93f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.42f, 0.48f);

            var arena = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arena.name = "Arena (14 m, open edges)";
            arena.transform.position = new Vector3(0f, -0.5f, 0f);
            arena.transform.localScale = new Vector3(14f, 0.5f, 14f);
            Object.DestroyImmediate(arena.GetComponent<Collider>());
            arena.AddComponent<MeshCollider>();
            arena.GetComponent<Renderer>().sharedMaterial = mats["Arena"];

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["Tomato"]);
            player.name = "Player (Tomato)";
            player.transform.position = new Vector3(0f, 0f, -3.5f);
            var input = player.AddComponent<LocalCritterInput>();
            player.AddComponent<PlaygroundControls>();   // Esc / click mouse toggle, debug clip keys, key hint

            var spots = new[] { ("Teal", new Vector3(-3.2f, 0f, 2f)), ("Chartreuse", new Vector3(0f, 0f, 3.6f)), ("Violet", new Vector3(3.2f, 0f, 2f)) };
            foreach (var (name, pos) in spots)
            {
                var d = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[name]);
                d.name = $"Dummy ({name})";
                d.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(player.transform.position - pos));
                d.AddComponent<PlaygroundDummyAI>().target = player.transform;
            }

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#8FD0FF");
            cam.fieldOfView = 55f;
            camGo.AddComponent<AudioListener>();
            var osc = camGo.AddComponent<OverShoulderCamera>();
            osc.target = player.transform;
            input.cam = osc;
            camGo.transform.SetPositionAndRotation(player.transform.position + new Vector3(0.6f, 1.9f, -3.7f), Quaternion.Euler(14f, 0f, 0f));
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            new GameObject("Post FX").AddComponent<Volume>().sharedProfile = BuildVolumeProfile();

            PrefabUtility.InstantiatePrefab(FxPrefabs["VFX_SplatDecals"]);
            var bank = AssetDatabase.LoadAssetAtPath<SfxBank>(AudioSetup.BankPath);
            if (bank) new GameObject("Sfx").AddComponent<SfxPlayer>().bank = bank;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static VolumeProfile BuildVolumeProfile()
        {
            var path = $"{Root}/Scenes/CC_Playground_PostFX.asset";
            AssetDatabase.DeleteAsset(path);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, path);
            var bloom = prof.Add<Bloom>(true);
            bloom.threshold.Override(0.95f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.6f);
            prof.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var ca = prof.Add<ColorAdjustments>(true);
            ca.saturation.Override(12f); ca.postExposure.Override(0.1f);
            prof.Add<Vignette>(true).intensity.Override(0.18f);
            foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);   // persist the overrides inside the asset
            EditorUtility.SetDirty(prof);
            return prof;
        }

        // ------------------------------------------------------------------ batch entry points
        public static void BatchRebuild() => RebuildAll();
    }
}
