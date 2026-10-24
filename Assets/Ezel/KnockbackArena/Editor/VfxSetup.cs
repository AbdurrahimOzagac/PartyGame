using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ShooterParty;
using ShooterParty.UI;

namespace KnockbackArena.EditorTools
{
    // Builds every particle prefab in VFX/ from the generated white sprites in Sprites/ (blender/v2/sprites.py).
    // Numbers follow the VFX brief (renders/../blender/v2/research/vfx_brief.md). Colour is applied at runtime by
    // Fx.Play: children named "Tint*" take the player colour, "Pastel*" a lighter one, the rest stay as authored.
    // Each one-shot has a silent root "timer" system whose stopAction destroys the whole effect.
    public static class VfxSetup
    {
        const string Root = CubeCritterSetup.Root;
        const string Dir = Root + "/VFX";

        public static Dictionary<string, ParticleSystem> BuildAll()
        {
            System.IO.Directory.CreateDirectory(Dir);
            var d = new Dictionary<string, ParticleSystem>();
            void Save(GameObject go)
            {
                d[go.name] = PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/{go.name}.prefab").GetComponent<ParticleSystem>();
                Object.DestroyImmediate(go);
            }
            Save(SpitPop()); Save(Impact()); Save(TimeoutPop()); Save(SpitTrail()); Save(SpeedLines()); Save(LandPuff());
            Save(FallPoof()); Save(Confetti()); Save(DashBurst()); Save(PickupSparkles()); Save(PickupCollect());
            Save(SnowAmbient()); Save(SplatDecalSystem());
            return d;
        }

        // ------------------------------------------------------------------ materials
        public static Material FxMat(string tex, bool additive = false, bool doubleSided = false)
        {
            var path = $"{Root}/Materials/M_FX_{tex}{(additive ? "_Add" : "")}.mat";
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            if (tex != "White") m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Sprites/T_FX_{tex}.png"));
            m.SetColor("_BaseColor", Color.white);
            // what URP's ParticleGUI writes for Transparent + Alpha / Additive (URP 17.3 source)
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", doubleSided ? 0f : 2f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ building blocks
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static AnimationCurve Curve(params float[] tv)
        {
            var c = new AnimationCurve();
            for (int i = 0; i + 1 < tv.Length; i += 2) c.AddKey(tv[i], tv[i + 1]);
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        static Gradient Fade(float holdUntil, float startAlpha = 1f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, holdUntil), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // silent root whose lifetime is the whole effect; destroying it takes the children along
        static GameObject Timer(string name, float length)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.duration = length; m.loop = false; m.playOnAwake = false; m.stopAction = ParticleSystemStopAction.Destroy;
            var e = ps.emission; e.enabled = false;
            var s = ps.shape; s.enabled = false;
            go.GetComponent<ParticleSystemRenderer>().enabled = false;
            return go;
        }

        static ParticleSystem Sys(GameObject parent, string name, Material mat, Vector2 life, Vector2 speed, Vector2 size,
                                  int burst, Color colour, float gravity = 0f, bool loop = false, float duration = 0.5f)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.duration = duration; m.loop = loop; m.playOnAwake = loop;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            m.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            m.startColor = colour;
            m.gravityModifier = gravity;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.maxParticles = 200;
            var e = ps.emission;
            e.rateOverTime = 0f;
            if (burst > 0) e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 2f;
            return ps;
        }

        static void Cone(ParticleSystem ps, float angle, float radius, Vector3 euler = default)
        {
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = radius; sh.rotation = euler;
        }

        static void SizeOver(ParticleSystem ps, params float[] tv)
        {
            var s = ps.sizeOverLifetime; s.enabled = true; s.size = new ParticleSystem.MinMaxCurve(1f, Curve(tv));
        }

        static void ColourOver(ParticleSystem ps, Gradient g) { var c = ps.colorOverLifetime; c.enabled = true; c.color = g; }

        static void Spin(ParticleSystem ps, float minDeg, float maxDeg)
        {
            var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var ro = ps.rotationOverLifetime; ro.enabled = true;
            ro.z = new ParticleSystem.MinMaxCurve(minDeg * Mathf.Deg2Rad, maxDeg * Mathf.Deg2Rad);
        }

        static void Stretch(ParticleSystem ps, float velocityScale, float lengthScale)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = velocityScale; r.lengthScale = lengthScale; r.cameraVelocityScale = 0f;
        }

        static void Drag(ParticleSystem ps, float dampen)
        {
            var l = ps.limitVelocityOverLifetime; l.enabled = true; l.limit = 0f; l.dampen = dampen;
        }

        static void Sheet(ParticleSystem ps, int x, int y)
        {
            var t = ps.textureSheetAnimation; t.enabled = true; t.numTilesX = x; t.numTilesY = y;
            t.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            t.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);   // normalised: one random tile per particle
        }

        static readonly Color GlintGold = new Color(1f, 0.89f, 0.45f);

        // ------------------------------------------------------------------ effects
        // V1: goo pop at the mouth (0.3 s), cone along the aim (+Z)
        static GameObject SpitPop()
        {
            var root = Timer("VFX_SpitPop", 0.4f);
            var puffs = Sys(root, "Pastel_Puffs", FxMat("Puff"), new Vector2(0.25f, 0.35f), new Vector2(0.6f, 1.2f), new Vector2(0.12f, 0.2f), 6, Color.white);
            Cone(puffs, 25f, 0.04f); SizeOver(puffs, 0f, 0.6f, 0.4f, 1.1f, 1f, 1.2f); ColourOver(puffs, Fade(0.5f)); Drag(puffs, 0.5f); Spin(puffs, -60f, 60f);
            var drops = Sys(root, "Tint_Droplets", FxMat("Droplet"), new Vector2(0.25f, 0.35f), new Vector2(2f, 3f), new Vector2(0.04f, 0.07f), 3, Color.white, 0.9f);
            Cone(drops, 18f, 0.03f); SizeOver(drops, 0f, 1f, 0.7f, 1f, 1f, 0f);
            var ring = Sys(root, "Ring", FxMat("Ring"), new Vector2(0.2f, 0.2f), Vector2.zero, new Vector2(0.5f, 0.5f), 1, new Color(1, 1, 1, 0.7f));
            SizeOver(ring, 0f, 0.3f, 1f, 1f); ColourOver(ring, Fade(0.2f));
            var glow = Sys(root, "Pastel_Glow", FxMat("Glow", true), new Vector2(0.12f, 0.12f), Vector2.zero, new Vector2(0.35f, 0.35f), 1, Color.white);
            SizeOver(glow, 0f, 0.6f, 1f, 1f); ColourOver(glow, Fade(0.1f));
            return root;
        }

        // V2: impact along the surface normal (+Z): goo droplets, white star burst, streaks, ice chips, sparkles
        static GameObject Impact()
        {
            var root = Timer("VFX_Impact", 0.8f);
            var drops = Sys(root, "Tint_Droplets", FxMat("Droplet"), new Vector2(0.4f, 0.6f), new Vector2(2.5f, 4.5f), new Vector2(0.05f, 0.11f), 12, Color.white, 0.9f);
            Cone(drops, 70f, 0.05f); SizeOver(drops, 0f, 1f, 0.75f, 1f, 1f, 0f);
            var star = Sys(root, "Star", FxMat("StarBurst"), new Vector2(0.18f, 0.18f), Vector2.zero, new Vector2(1f, 1f), 1, new Color(1, 1, 1, 0.92f));
            SizeOver(star, 0f, 0.6f, 0.55f, 1f, 1f, 0f);
            var sm = star.main; sm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var streaks = Sys(root, "Streaks", FxMat("SpeedLine"), new Vector2(0.12f, 0.16f), new Vector2(6f, 6f), new Vector2(0.06f, 0.08f), 6, Color.white);
            Cone(streaks, 60f, 0.05f); Stretch(streaks, 0.05f, 2f); ColourOver(streaks, Fade(0.3f));
            var chips = Sys(root, "IceChips", FxMat("IceChips"), new Vector2(0.5f, 0.7f), new Vector2(3f, 5f), new Vector2(0.05f, 0.1f), 8, Color.white, 1.2f);
            Cone(chips, 35f, 0.08f); Sheet(chips, 4, 1); Spin(chips, 360f, 720f);
            var cm = chips.main; cm.startColor = new ParticleSystem.MinMaxGradient(Hex("#E8F8FF"), Hex("#A8DDF5"));
            var spark = Sys(root, "Sparkles", FxMat("Sparkle", true), new Vector2(0.25f, 0.35f), new Vector2(1f, 2f), new Vector2(0.08f, 0.14f), 3, Color.white);
            SizeOver(spark, 0f, 0f, 0.3f, 1f, 1f, 0f);
            return root;
        }

        // a blob that runs out of time pops into a few sparkles instead of vanishing
        static GameObject TimeoutPop()
        {
            var root = Timer("VFX_TimeoutPop", 0.4f);
            var spark = Sys(root, "Pastel_Sparkles", FxMat("Sparkle", true), new Vector2(0.25f, 0.35f), new Vector2(0.8f, 1.6f), new Vector2(0.08f, 0.14f), 3, Color.white);
            SizeOver(spark, 0f, 0f, 0.3f, 1f, 1f, 0f);
            var ring = Sys(root, "Pastel_Ring", FxMat("Ring"), new Vector2(0.15f, 0.15f), Vector2.zero, new Vector2(0.45f, 0.45f), 1, Color.white);
            SizeOver(ring, 0f, 0.3f, 1f, 1f); ColourOver(ring, Fade(0.1f));
            return root;
        }

        // droplet chain + sparkles behind the blob (child of the blob, world space, emitted by distance)
        static GameObject SpitTrail()
        {
            var drops = Sys(null, "Tint_Trail", FxMat("Droplet"), new Vector2(0.3f, 0.3f), Vector2.zero, new Vector2(0.09f, 0.13f), 0, Color.white, 0.3f, loop: true, duration: 1f);
            var e = drops.emission; e.rateOverDistance = 4f;
            SizeOver(drops, 0f, 1f, 1f, 0f);
            var iv = drops.inheritVelocity; iv.enabled = true; iv.mode = ParticleSystemInheritVelocityMode.Initial; iv.curve = new ParticleSystem.MinMaxCurve(0.1f);
            var spark = Sys(drops.gameObject, "Sparkles", FxMat("Sparkle", true), new Vector2(0.35f, 0.35f), Vector2.zero, new Vector2(0.08f, 0.12f), 0, Color.white, loop: true, duration: 1f);
            var se = spark.emission; se.rateOverDistance = 0.34f;
            SizeOver(spark, 0f, 0f, 0.25f, 1f, 1f, 0f);
            return drops.gameObject;
        }

        // V4: streaks + snow spray while knocked back fast; CritterFx turns emission on (rateOverDistance) above 6 m/s
        static GameObject SpeedLines()
        {
            var lines = Sys(null, "VFX_SpeedLines", FxMat("SpeedLine"), new Vector2(0.2f, 0.2f), Vector2.zero, new Vector2(0.03f, 0.05f), 0, new Color(1, 1, 1, 0.7f), loop: true, duration: 1f);
            var sh = lines.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1f, 1f, 0.6f); sh.position = new Vector3(0f, 0.6f, 0f);
            var iv = lines.inheritVelocity; iv.enabled = true; iv.mode = ParticleSystemInheritVelocityMode.Initial; iv.curve = new ParticleSystem.MinMaxCurve(0.25f);
            Stretch(lines, 0.12f, 2f); ColourOver(lines, Fade(0.2f));
            var snow = Sys(lines.gameObject, "Snow", FxMat("Puff"), new Vector2(0.3f, 0.45f), new Vector2(0.3f, 0.8f), new Vector2(0.12f, 0.2f), 0, new Color(1, 1, 1, 0.9f), loop: true, duration: 1f);
            var ss = snow.shape; ss.shapeType = ParticleSystemShapeType.Box; ss.scale = new Vector3(0.6f, 0.05f, 0.3f); ss.position = new Vector3(0f, 0.04f, 0f);
            SizeOver(snow, 0f, 0.5f, 1f, 1.3f); ColourOver(snow, Fade(0.3f));
            return lines.gameObject;
        }

        // V5: snow puff ring at the feet on landing (scale the instance by impact speed)
        static GameObject LandPuff()
        {
            var root = Timer("VFX_LandPuff", 0.6f);
            var puffs = Sys(root, "Snow", FxMat("Puff"), new Vector2(0.4f, 0.5f), new Vector2(1.5f, 2.5f), new Vector2(0.15f, 0.3f), 8, Hex("#F4FAFF"));
            var sh = puffs.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.35f; sh.radiusThickness = 0f; sh.rotation = new Vector3(90f, 0f, 0f);
            puffs.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            SizeOver(puffs, 0f, 0.6f, 1f, 1.3f); ColourOver(puffs, Fade(0.35f)); Drag(puffs, 0.35f); Spin(puffs, -40f, 40f);
            var glints = Sys(root, "Glints", FxMat("Sparkle", true), new Vector2(0.3f, 0.4f), new Vector2(0.5f, 1.2f), new Vector2(0.05f, 0.09f), 4, Color.white);
            var gs = glints.shape; gs.shapeType = ParticleSystemShapeType.Circle; gs.radius = 0.4f; gs.rotation = new Vector3(90f, 0f, 0f);
            SizeOver(glints, 0f, 0f, 0.3f, 1f, 1f, 0f);
            return root;
        }

        // V6: poof + stars + ring when a critter drops out of the arena
        static GameObject FallPoof()
        {
            var root = Timer("VFX_FallPoof", 1.1f);
            var puffs = Sys(root, "Pastel_Puffs", FxMat("Puff"), new Vector2(0.6f, 1f), new Vector2(3f, 5f), new Vector2(0.4f, 0.8f), 18, Color.white);
            SizeOver(puffs, 0f, 0.5f, 0.3f, 1f, 1f, 1.2f); ColourOver(puffs, Fade(0.45f)); Drag(puffs, 0.6f); Spin(puffs, -90f, 90f);
            var stars = Sys(root, "Stars", FxMat("Star5"), new Vector2(0.8f, 1f), new Vector2(4f, 6f), new Vector2(0.15f, 0.25f), 7, Hex("#FFE45C"), -0.2f);
            var stm = stars.main; stm.startColor = new ParticleSystem.MinMaxGradient(Hex("#FFE45C"), Color.white);
            Spin(stars, 300f, 420f); Drag(stars, 0.4f); ColourOver(stars, Fade(0.7f));
            var ring = Sys(root, "Ring", FxMat("Ring"), new Vector2(0.25f, 0.25f), Vector2.zero, new Vector2(2.5f, 2.5f), 1, Color.white);
            SizeOver(ring, 0f, 0.12f, 1f, 1f); ColourOver(ring, Fade(0.3f));
            return root;
        }

        // V7: winner confetti, 3D spinning quads from a 4x2 sheet, player colours + white + gold; shoots up
        static GameObject Confetti()
        {
            var root = Timer("VFX_Confetti", 3.6f);
            var c = Sys(root, "Confetti", FxMat("Confetti", false, true), new Vector2(2.8f, 3.5f), new Vector2(8f, 12f), new Vector2(0.06f, 0.12f), 150, Color.white, 0.35f, duration: 0.3f);
            Cone(c, 20f, 0.3f, new Vector3(-90f, 0f, 0f));
            var e = c.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 90), new ParticleSystem.Burst(0.15f, 60) });
            var g = new Gradient { mode = GradientMode.Fixed };
            g.SetKeys(new[] { new GradientColorKey(Hex("#FF4F2E"), 0.17f), new GradientColorKey(Hex("#00B3A4"), 0.34f), new GradientColorKey(Hex("#D4F21C"), 0.5f),
                              new GradientColorKey(Hex("#7B2FD0"), 0.67f), new GradientColorKey(Color.white, 0.84f), new GradientColorKey(Hex("#FFD23F"), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            var m = c.main; m.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
            m.startRotation3D = true;
            m.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); m.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var ro = c.rotationOverLifetime; ro.enabled = true; ro.separateAxes = true;
            ro.x = new ParticleSystem.MinMaxCurve(180f * Mathf.Deg2Rad, 540f * Mathf.Deg2Rad); ro.y = new ParticleSystem.MinMaxCurve(120f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);
            ro.z = new ParticleSystem.MinMaxCurve(90f * Mathf.Deg2Rad, 270f * Mathf.Deg2Rad);
            Drag(c, 0.12f);
            var n = c.noise; n.enabled = true; n.strength = 0.6f; n.frequency = 0.5f;
            Sheet(c, 4, 2);
            ColourOver(c, Fade(0.85f));
            var r = c.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            r.alignment = ParticleSystemRenderSpace.Local;
            var spark = Sys(root, "Sparkles", FxMat("Sparkle", true), new Vector2(0.6f, 1.2f), new Vector2(4f, 8f), new Vector2(0.1f, 0.18f), 40, GlintGold, 0.1f);
            Cone(spark, 30f, 0.3f, new Vector3(-90f, 0f, 0f)); SizeOver(spark, 0f, 0f, 0.2f, 1f, 1f, 0f); Drag(spark, 0.2f);
            return root;
        }

        // dash: puffs + streaks out the back (+Z = backwards when played), ring on the ground
        static GameObject DashBurst()
        {
            var root = Timer("VFX_DashBurst", 0.4f);
            var puffs = Sys(root, "Pastel_Puffs", FxMat("Puff"), new Vector2(0.25f, 0.35f), new Vector2(2f, 3f), new Vector2(0.14f, 0.24f), 6, Color.white);
            Cone(puffs, 30f, 0.15f); puffs.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            SizeOver(puffs, 0f, 0.6f, 1f, 1.3f); ColourOver(puffs, Fade(0.4f)); Drag(puffs, 0.5f);
            var streaks = Sys(root, "Streaks", FxMat("SpeedLine"), new Vector2(0.15f, 0.2f), new Vector2(5f, 7f), new Vector2(0.04f, 0.06f), 6, new Color(1, 1, 1, 0.8f));
            Cone(streaks, 12f, 0.35f); streaks.transform.localPosition = new Vector3(0f, 0.5f, 0f); Stretch(streaks, 0.06f, 2f); ColourOver(streaks, Fade(0.3f));
            var ring = Sys(root, "Pastel_Ring", FxMat("Ring"), new Vector2(0.3f, 0.3f), Vector2.zero, new Vector2(1.2f, 1.2f), 1, Color.white);
            SizeOver(ring, 0f, 0.25f, 1f, 1f); ColourOver(ring, Fade(0.3f));
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            return root;
        }

        // pickup idle: a few gold/white glints around the spring (local space, follows the bob)
        static GameObject PickupSparkles()
        {
            var s = Sys(null, "VFX_PickupSparkles", FxMat("Sparkle", true), new Vector2(0.6f, 0.9f), new Vector2(0.15f, 0.25f), new Vector2(0.06f, 0.11f), 0, GlintGold, loop: true, duration: 1f);
            var m = s.main; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.startColor = new ParticleSystem.MinMaxGradient(GlintGold, Color.white);
            var e = s.emission; e.rateOverTime = 3.5f;
            var sh = s.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            var v = s.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(0f); v.y = new ParticleSystem.MinMaxCurve(0.2f); v.z = new ParticleSystem.MinMaxCurve(0f);
            SizeOver(s, 0f, 0f, 0.3f, 1f, 1f, 0f);
            return s.gameObject;
        }

        static GameObject PickupCollect()
        {
            var root = Timer("VFX_PickupCollect", 0.7f);
            var stars = Sys(root, "Stars", FxMat("Star5"), new Vector2(0.4f, 0.6f), new Vector2(3f, 4f), new Vector2(0.1f, 0.18f), 12, Hex("#FFD23F"), 0.3f);
            var stm = stars.main; stm.startColor = new ParticleSystem.MinMaxGradient(Hex("#FFD23F"), Color.white);
            Spin(stars, 200f, 400f); Drag(stars, 0.3f); SizeOver(stars, 0f, 1f, 0.7f, 1f, 1f, 0f);
            var ring = Sys(root, "Ring", FxMat("Ring"), new Vector2(0.2f, 0.2f), Vector2.zero, new Vector2(1.5f, 1.5f), 1, Hex("#FFE9A0"));
            SizeOver(ring, 0f, 0.2f, 1f, 1f); ColourOver(ring, Fade(0.3f));
            var rise = Sys(root, "Rising", FxMat("Sparkle", true), new Vector2(0.5f, 0.6f), new Vector2(1.5f, 2.5f), new Vector2(0.08f, 0.14f), 6, GlintGold);
            Cone(rise, 15f, 0.25f, new Vector3(-90f, 0f, 0f)); SizeOver(rise, 0f, 0f, 0.2f, 1f, 1f, 0f);
            return root;
        }

        // ambient: sparse sparkles over the arena + slow snowflakes drifting down
        static GameObject SnowAmbient()
        {
            var s = Sys(null, "VFX_SnowAmbient", FxMat("Sparkle", true), new Vector2(2f, 3f), Vector2.zero, new Vector2(0.05f, 0.09f), 0, Color.white, loop: true, duration: 5f);
            var m = s.main; m.maxParticles = 40; m.prewarm = true;
            var e = s.emission; e.rateOverTime = 6f;
            var sh = s.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(18f, 2.5f, 18f); sh.position = new Vector3(0f, 1.25f, 0f);
            SizeOver(s, 0f, 0f, 0.5f, 1f, 1f, 0f);
            var f = Sys(s.gameObject, "Flakes", FxMat("Glow"), new Vector2(7f, 9f), Vector2.zero, new Vector2(0.04f, 0.08f), 0, new Color(1, 1, 1, 0.85f), 0.015f, loop: true, duration: 5f);
            var fm = f.main; fm.prewarm = true; fm.maxParticles = 220;
            var fe = f.emission; fe.rateOverTime = 22f;
            var fs = f.shape; fs.shapeType = ParticleSystemShapeType.Box; fs.scale = new Vector3(34f, 0.2f, 34f); fs.position = new Vector3(0f, 7f, 0f);
            var n = f.noise; n.enabled = true; n.strength = 0.25f; n.frequency = 0.3f;
            ColourOver(f, Fade(0.85f));
            return s.gameObject;
        }

        // splat decals left on the ice (SplatDecals component emits into this one system)
        static GameObject SplatDecalSystem()
        {
            var s = Sys(null, "VFX_SplatDecals", FxMat("Splat"), new Vector2(4f, 4f), Vector2.zero, new Vector2(0.75f, 0.75f), 0, Color.white, loop: true, duration: 5f);
            var m = s.main; m.maxParticles = 64;
            var sh = s.shape; sh.enabled = false;
            SizeOver(s, 0f, 0.55f, 0.04f, 1.08f, 0.1f, 1f, 0.75f, 1f, 1f, 0.8f);   // quick spread, hold 3 s, shrink to 0.8
            ColourOver(s, Fade(0.75f));
            s.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            s.gameObject.AddComponent<SplatDecals>();
            return s.gameObject;
        }
    }
}
