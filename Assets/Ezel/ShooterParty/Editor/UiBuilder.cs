using System.IO;
using System.Linq;
using ShooterParty.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static ShooterParty.EditorTools.UiKit;

namespace ShooterParty.EditorTools
{
    // Builds the core UI from code (like every other generated asset here):
    //   Prefabs/UI_Root.prefab   menu, join dialog + progress, dialog, toasts, settings, pause, loading curtain
    //   Scenes/SP_Menu.unity     the menu's living backdrop (ice floe, critters, drifting camera)
    // Layout reference: 1920 x 1080, scaled by height.
    public static class UiBuilder
    {
        public const string Root = ShooterPartySetup.Root;
        public const string UiRootPath = Root + "/Prefabs/UI_Root.prefab";
        public const string MenuScenePath = Root + "/Scenes/SP_Menu.unity";

        static readonly Color Teal = new Color32(0x00, 0xB3, 0xA4, 0xFF);
        static readonly Color Tomato = new Color32(0xFF, 0x4F, 0x2E, 0xFF);
        static readonly Color Violet = new Color32(0x7B, 0x2F, 0xD0, 0xFF);
        static readonly Color Card = new Color32(0x22, 0x28, 0x5A, 0xFF);
        static readonly Color Pink = new Color32(0xFF, 0x5C, 0x8A, 0xFF);
        static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        public static GameObject BuildUiRoot()
        {
            var go = new GameObject("UI_Root", typeof(RectTransform));
            OverlayCanvas(go, 10);
            var root = go.AddComponent<UiRoot>();
            var t = go.transform;
            root.menu = BuildMenu(t);
            root.loadingCurtain = BuildCurtain(t, out root.loadingLabel);
            root.nameDialog = BuildNameDialog(t);
            root.progress = BuildProgress(t);
            root.settings = BuildSettings(t);
            root.pause = BuildPause(t);
            root.dialog = BuildDialog(t);
            root.toasts = BuildToasts(t);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, UiRootPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------ screens
        static T Screen<T>(Transform parent, string name) where T : Component
        {
            var rt = R(parent, name, C, Vector2.zero, Vector2.zero);
            Fill(rt);
            rt.gameObject.AddComponent<CanvasGroup>();
            return rt.gameObject.AddComponent<T>();
        }

        static void Scrim(Transform parent, float alpha = 0.62f)
        {
            var s = I(parent, "Scrim", null, new Color(0.03f, 0.04f, 0.11f, alpha), C, Vector2.zero, Vector2.zero);
            Fill(s.rectTransform);
            s.raycastTarget = true;   // a modal swallows clicks meant for what is behind it
        }

        static MenuScreen BuildMenu(Transform parent)
        {
            var m = Screen<MenuScreen>(parent, "Menu");
            var shade = I(m.transform, "LeftShade", S("UI_FadeH"), new Color(0.03f, 0.04f, 0.11f, 0.7f), new Vector2(0f, 0.5f), new Vector2(560f, 0f), new Vector2(1120f, 1080f));   // fades out to the right, no hard edge
            shade.rectTransform.anchorMin = new Vector2(0f, 0f); shade.rectTransform.anchorMax = new Vector2(0f, 1f); shade.rectTransform.sizeDelta = new Vector2(1120f, 0f);
            var col = R(m.transform, "Column", new Vector2(0f, 0.5f), new Vector2(440f, 0f), new Vector2(760f, 1000f));
            var logo1 = T(col, "LogoShooter", "SHOOTER", 150f, Chartreuse, new Vector2(0.5f, 1f), new Vector2(-6f, -110f), new Vector2(760f, 150f));
            logo1.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);
            var logo2 = T(col, "LogoParty", "PARTY", 170f, Gold, new Vector2(0.5f, 1f), new Vector2(60f, -250f), new Vector2(760f, 170f));
            logo2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            T(col, "Tagline", "Same cube. Same shot. New rules.", 34f, Paper, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(760f, 50f), TextAlignmentOptions.Center, false, true);

            T(col, "OnlineLabel", "PLAY ONLINE", 26f, Muted, new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(640f, 36f), TextAlignmentOptions.Left, false, false);
            m.create = Btn(col, "Create", "CREATE ROOM", new Vector2(0.5f, 1f), new Vector2(-162f, -510f), new Vector2(316f, 96f), Chartreuse, Ink);
            m.join = Btn(col, "Join", "JOIN ROOM", new Vector2(0.5f, 1f), new Vector2(162f, -510f), new Vector2(316f, 96f), Teal, Paper);
            T(col, "LocalLabel", "SAME WI-FI", 26f, Muted, new Vector2(0.5f, 1f), new Vector2(0f, -598f), new Vector2(640f, 36f), TextAlignmentOptions.Left, false, false);
            m.localHost = Btn(col, "LocalHost", "HOST", new Vector2(0.5f, 1f), new Vector2(-162f, -664f), new Vector2(316f, 84f), Card, Paper);
            m.localJoin = Btn(col, "LocalJoin", "JOIN BY ADDRESS", new Vector2(0.5f, 1f), new Vector2(162f, -664f), new Vector2(316f, 84f), Card, Paper);
            m.practice = Btn(col, "Practice", "PRACTICE VS BOTS", new Vector2(0.5f, 1f), new Vector2(0f, -770f), new Vector2(640f, 90f), Violet, Paper);
            m.settingsButton = Btn(col, "Settings", "SETTINGS", new Vector2(0.5f, 1f), new Vector2(-162f, -872f), new Vector2(316f, 72f), Card, Paper);
            m.quit = Btn(col, "Quit", "QUIT", new Vector2(0.5f, 1f), new Vector2(162f, -872f), new Vector2(316f, 72f), Card, Paper);
            m.version = T(m.transform, "Version", "v1", 22f, new Color(1f, 1f, 1f, 0.8f), new Vector2(1f, 0f), new Vector2(-170f, 30f), new Vector2(320f, 30f), TextAlignmentOptions.Right, false, true);
            return m;
        }

        static UiScreen BuildCurtain(Transform parent, out TMP_Text label)
        {
            var c = Screen<UiScreen>(parent, "LoadingCurtain");
            var bg = I(c.transform, "Background", null, new Color(0.06f, 0.08f, 0.2f, 1f), C, Vector2.zero, Vector2.zero);
            Fill(bg.rectTransform);
            var box = R(c.transform, "Content", C, Vector2.zero, new Vector2(800f, 300f));
            var sp = I(box, "Spinner", S("UI_Ring"), Chartreuse, C, new Vector2(0f, 50f), new Vector2(90f, 90f), Image.Type.Filled);
            sp.fillAmount = 0.72f;
            sp.gameObject.AddComponent<Spinner>();
            label = T(box, "Label", "Loading…", 48f, Paper, C, new Vector2(0f, -60f), new Vector2(800f, 70f));
            return c;
        }

        static NameCodeDialog BuildNameDialog(Transform parent)
        {
            var d = Screen<NameCodeDialog>(parent, "NameDialog");
            Scrim(d.transform);
            var p = Panel(d.transform, "Panel", C, Vector2.zero, new Vector2(820f, 640f), Card);
            d.title = T(p, "Title", "Create a room", 58f, Paper, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(760f, 70f));
            d.hint = T(p, "Hint", "", 28f, Muted, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(760f, 40f), TextAlignmentOptions.Center, false, false);
            Label(p, "NameLabel", "YOUR NAME", new Vector2(0f, -178f));
            d.nameField = Input(p, "Name", new Vector2(0f, -232f), new Vector2(640f, 76f), "2-16 letters or numbers");
            d.codeRow = R(p, "CodeRow", new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(760f, 140f)).gameObject;
            Label(d.codeRow.transform, "CodeLabel", "PARTY CODE", new Vector2(0f, 48f), C);
            d.codeField = Input(d.codeRow.transform, "Code", new Vector2(0f, -6f), new Vector2(640f, 84f), "BCDFG", 52f, C);
            d.codeField.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            d.addressRow = R(p, "AddressRow", new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(760f, 140f)).gameObject;
            Label(d.addressRow.transform, "AddressLabel", "HOST ADDRESS", new Vector2(0f, 48f), C);
            d.addressField = Input(d.addressRow.transform, "Address", new Vector2(0f, -6f), new Vector2(640f, 76f), "192.168.1.20", 40f, C);
            d.botsRow = R(p, "BotsRow", new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(640f, 70f)).gameObject;
            d.bots = Toggle(d.botsRow.transform, "Bots", "A bot replaces anyone who leaves", Vector2.zero, 640f);
            d.error = T(p, "Error", "", 30f, Pink, new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(760f, 44f), TextAlignmentOptions.Center, false, false);
            d.cancel = Btn(p, "Cancel", "BACK", new Vector2(0.5f, 0f), new Vector2(-170f, 82f), new Vector2(300f, 90f), new Color(1f, 1f, 1f, 0.12f), Paper);
            d.confirm = Btn(p, "Confirm", "CREATE", new Vector2(0.5f, 0f), new Vector2(170f, 82f), new Vector2(300f, 90f), Chartreuse, Ink);
            d.confirmLabel = d.confirm.GetComponentInChildren<TMP_Text>();
            return d;
        }

        static JoinProgressView BuildProgress(Transform parent)
        {
            var v = Screen<JoinProgressView>(parent, "JoinProgress");
            Scrim(v.transform);
            var p = Panel(v.transform, "Panel", C, Vector2.zero, new Vector2(720f, 380f), Card);
            var sp = I(p, "Spinner", S("UI_Ring"), Chartreuse, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(76f, 76f), Image.Type.Filled);
            sp.fillAmount = 0.72f;
            sp.gameObject.AddComponent<Spinner>();
            v.step = T(p, "Step", "Contacting server…", 44f, Paper, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(680f, 56f));
            v.detail = T(p, "Detail", "", 28f, Muted, new Vector2(0.5f, 1f), new Vector2(0f, -224f), new Vector2(680f, 80f), TextAlignmentOptions.Center, false, false);
            v.detail.textWrappingMode = TextWrappingModes.Normal;
            v.cancel = Btn(p, "Cancel", "CANCEL", new Vector2(0.5f, 0f), new Vector2(0f, 66f), new Vector2(300f, 84f), new Color(1f, 1f, 1f, 0.12f), Paper);
            v.cancelLabel = v.cancel.GetComponentInChildren<TMP_Text>();
            return v;
        }

        static Dialog BuildDialog(Transform parent)
        {
            var d = Screen<Dialog>(parent, "Dialog");
            Scrim(d.transform, 0.7f);
            var p = Panel(d.transform, "Panel", C, Vector2.zero, new Vector2(760f, 400f), Card);
            d.title = T(p, "Title", "Leave the room?", 52f, Paper, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(700f, 64f));
            d.body = T(p, "Body", "", 30f, Muted, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(660f, 110f), TextAlignmentOptions.Center, false, false);
            d.body.textWrappingMode = TextWrappingModes.Normal;
            d.secondary = Btn(p, "Secondary", "STAY", new Vector2(0.5f, 0f), new Vector2(-165f, 72f), new Vector2(290f, 86f), new Color(1f, 1f, 1f, 0.12f), Paper);
            d.secondaryLabel = d.secondary.GetComponentInChildren<TMP_Text>();
            d.primary = Btn(p, "Primary", "LEAVE", new Vector2(0.5f, 0f), new Vector2(165f, 72f), new Vector2(290f, 86f), Tomato, Paper);
            d.primaryLabel = d.primary.GetComponentInChildren<TMP_Text>();
            return d;
        }

        static Toasts BuildToasts(Transform parent)
        {
            var area = R(parent, "Toasts", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(760f, 300f), new Vector2(0.5f, 1f));
            var toasts = area.gameObject.AddComponent<Toasts>();
            var row = R(area, "Toast", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(700f, 66f), new Vector2(0.5f, 1f));
            row.gameObject.AddComponent<CanvasGroup>();
            I(row, "Fill", S("UI_Pill"), new Color(0.086f, 0.1f, 0.227f, 0.94f), C, Vector2.zero, new Vector2(700f, 66f), Image.Type.Sliced);
            I(row, "Stroke", S("UI_Pill"), new Color(1f, 1f, 1f, 0.12f), C, Vector2.zero, new Vector2(704f, 70f), Image.Type.Sliced).transform.SetAsFirstSibling();
            T(row, "Text", "Bea joined", 32f, Paper, C, new Vector2(0f, 1f), new Vector2(660f, 50f), TextAlignmentOptions.Center, false, false);
            toasts.template = row;
            return toasts;
        }

        static SettingsView BuildSettings(Transform parent)
        {
            var s = Screen<SettingsView>(parent, "Settings");
            Scrim(s.transform, 0.7f);
            var p = Panel(s.transform, "Panel", C, Vector2.zero, new Vector2(900f, 960f), Card);
            T(p, "Title", "SETTINGS", 56f, Paper, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(800f, 66f));
            float y = -150f;
            s.master = SliderRow(p, "Master volume", ref y, 0f, 1f);
            s.music = SliderRow(p, "Music", ref y, 0f, 1f);
            s.effects = SliderRow(p, "Sound effects", ref y, 0f, 1f);
            s.sensitivity = SliderRow(p, "Mouse speed", ref y, 0.2f, 3f);
            y -= 10f;
            s.invertY = Toggle(p, "InvertY", "Invert mouse Y", new Vector2(0f, y), 760f, new Vector2(0.5f, 1f)); y -= 76f;
            s.vsync = Toggle(p, "Vsync", "VSync", new Vector2(0f, y), 760f, new Vector2(0.5f, 1f)); y -= 76f;
            s.netStats = Toggle(p, "NetStats", "Show network stats (F3)", new Vector2(0f, y), 760f, new Vector2(0.5f, 1f)); y -= 86f;
            Stepper(p, "Display", "Display", new Vector2(0f, y), out s.displayPrev, out s.displayNext, out s.displayValue); y -= 80f;
            Stepper(p, "Resolution", "Resolution", new Vector2(0f, y), out s.resPrev, out s.resNext, out s.resValue);
            s.back = Btn(p, "Back", "DONE", new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(320f, 88f), Chartreuse, Ink);
            return s;
        }

        static PauseMenu BuildPause(Transform parent)
        {
            var m = Screen<PauseMenu>(parent, "Pause");
            Scrim(m.transform, 0.55f);
            var p = Panel(m.transform, "Panel", C, Vector2.zero, new Vector2(560f, 520f), Card);
            T(p, "Title", "PAUSED", 60f, Paper, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(500f, 70f));
            T(p, "Note", "The round keeps going!", 26f, Muted, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(500f, 36f), TextAlignmentOptions.Center, false, false);
            m.resume = Btn(p, "Resume", "RESUME", new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(420f, 88f), Chartreuse, Ink);
            m.settingsButton = Btn(p, "Settings", "SETTINGS", new Vector2(0.5f, 1f), new Vector2(0f, -316f), new Vector2(420f, 80f), new Color(1f, 1f, 1f, 0.12f), Paper);
            m.leave = Btn(p, "Leave", "LEAVE ROOM", new Vector2(0.5f, 1f), new Vector2(0f, -416f), new Vector2(420f, 80f), Tomato, Paper);
            return m;
        }

        // ------------------------------------------------------------------ controls
        static void Label(Transform parent, string name, string text, Vector2 pos, Vector2? anchor = null) =>
            T(parent, name, text, 24f, Muted, anchor ?? new Vector2(0.5f, 1f), pos, new Vector2(640f, 32f), TextAlignmentOptions.Left, false, false);

        public static TMP_InputField Input(Transform parent, string name, Vector2 pos, Vector2 size, string placeholder, float fontSize = 40f, Vector2? anchor = null)
        {
            var bg = I(parent, name, S("UI_Panel"), new Color(0.04f, 0.05f, 0.14f, 0.85f), anchor ?? new Vector2(0.5f, 1f), pos, size, Image.Type.Sliced);
            bg.raycastTarget = true;
            I(bg.transform, "Stroke", S("UI_PanelOutline"), new Color(1f, 1f, 1f, 0.3f), C, Vector2.zero, size, Image.Type.Sliced);
            var area = R(bg.transform, "Text Area", C, Vector2.zero, size - new Vector2(44f, 10f));
            area.gameObject.AddComponent<RectMask2D>();
            var ph = T(area, "Placeholder", placeholder, fontSize, new Color(Muted.r, Muted.g, Muted.b, 0.55f), C, Vector2.zero, area.sizeDelta, TextAlignmentOptions.Left, false, false);
            var tx = T(area, "Text", "", fontSize, Paper, C, Vector2.zero, area.sizeDelta, TextAlignmentOptions.Left, true, false);
            Fill(ph.rectTransform); Fill(tx.rectTransform);
            var f = bg.gameObject.AddComponent<TMP_InputField>();
            f.textViewport = area; f.textComponent = tx; f.placeholder = ph; f.fontAsset = Bold; f.pointSize = fontSize;
            f.customCaretColor = true; f.caretColor = Paper; f.caretWidth = 3; f.selectionColor = new Color(0f, 0.7f, 0.64f, 0.45f);
            f.targetGraphic = bg;
            return f;
        }

        static Slider SliderRow(Transform parent, string label, ref float y, float min, float max)
        {
            var row = R(parent, label, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(760f, 60f));
            y -= 76f;
            T(row, "Label", label, 32f, Paper, new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(340f, 50f), TextAlignmentOptions.Left, false, false);
            var track = I(row, "Track", S("UI_Pill"), new Color(1f, 1f, 1f, 0.12f), new Vector2(1f, 0.5f), new Vector2(-210f, 0f), new Vector2(400f, 22f), Image.Type.Sliced);
            track.raycastTarget = true;
            var fillArea = R(track.transform, "Fill Area", C, Vector2.zero, Vector2.zero); Fill(fillArea);
            var fill = I(fillArea, "Fill", S("UI_Pill"), Chartreuse, C, Vector2.zero, Vector2.zero, Image.Type.Sliced); Fill(fill.rectTransform);
            var handleArea = R(track.transform, "Handle Area", C, Vector2.zero, Vector2.zero); Fill(handleArea);
            handleArea.offsetMin = new Vector2(12f, 0f); handleArea.offsetMax = new Vector2(-12f, 0f);
            var handle = I(handleArea, "Handle", S("UI_Circle"), Paper, C, Vector2.zero, new Vector2(40f, 40f));
            handle.raycastTarget = true;
            var s = track.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight; s.minValue = min; s.maxValue = max; s.value = max * 0.5f;
            return s;
        }

        static Toggle Toggle(Transform parent, string name, string label, Vector2 pos, float width, Vector2? anchor = null)
        {
            var row = R(parent, name, anchor ?? C, pos, new Vector2(width, 60f));
            T(row, "Label", label, 32f, Paper, new Vector2(0f, 0.5f), new Vector2(width * 0.5f - 40f, 0f), new Vector2(width - 120f, 50f), TextAlignmentOptions.Left, false, false);
            var box = I(row, "Box", S("UI_Panel"), new Color(1f, 1f, 1f, 0.14f), new Vector2(1f, 0.5f), new Vector2(-32f, 0f), new Vector2(56f, 56f), Image.Type.Sliced);
            box.raycastTarget = true;
            var check = I(box.transform, "Check", S("UI_IconCheck"), Chartreuse, C, Vector2.zero, new Vector2(40f, 40f));
            var t = box.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            t.targetGraphic = box; t.graphic = check; t.isOn = false;
            box.gameObject.AddComponent<UiHoverSound>();
            return t;
        }

        static void Stepper(Transform parent, string name, string label, Vector2 pos, out Button prev, out Button next, out TMP_Text value)
        {
            var row = R(parent, name, new Vector2(0.5f, 1f), pos, new Vector2(760f, 64f));
            T(row, "Label", label, 32f, Paper, new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(340f, 50f), TextAlignmentOptions.Left, false, false);
            prev = Btn(row, "Prev", "<", new Vector2(1f, 0.5f), new Vector2(-376f, 0f), new Vector2(56f, 56f), new Color(1f, 1f, 1f, 0.14f), Paper);
            value = T(row, "Value", "1920 × 1080", 32f, Paper, new Vector2(1f, 0.5f), new Vector2(-210f, 0f), new Vector2(260f, 50f), TextAlignmentOptions.Center, true, false);
            next = Btn(row, "Next", ">", new Vector2(1f, 0.5f), new Vector2(-44f, 0f), new Vector2(56f, 56f), new Color(1f, 1f, 1f, 0.14f), Paper);
        }

        // ------------------------------------------------------------------ menu backdrop scene
        public static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.color = new Color32(0xFF, 0xF6, 0xE8, 0xFF);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(58f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/Materials/M_Sky.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(0x9C, 0xC3, 0xEE, 0xFF);
            RenderSettings.ambientEquatorColor = new Color32(0xB9, 0xD3, 0xEE, 0xFF);
            RenderSettings.ambientGroundColor = new Color32(0x3D, 0x6F, 0xB0, 0xFF);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 30f; RenderSettings.fogEndDistance = 160f; RenderSettings.fogColor = new Color32(0x4A, 0x92, 0xE2, 0xFF);
            foreach (var path in new[] { "/Prefabs/Arena/Arena_Ice_16.prefab", "/Prefabs/Arena/Arena_Backdrop.prefab", "/VFX/VFX_SnowAmbient.prefab" })
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + path);
                if (p) PrefabUtility.InstantiatePrefab(p);
            }
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.Skybox; c.fieldOfView = 50f; c.farClipPlane = 400f;
            cam.AddComponent<AudioListener>();
            c.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ArtRoot + "/Scenes/KA_Arena_PostFX.asset");
            if (profile) new GameObject("Post FX").AddComponent<Volume>().sharedProfile = profile;
            var backdrop = new GameObject("MenuBackdrop").AddComponent<MenuBackdrop>();
            backdrop.cameraRig = cam.transform;
            var critter = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/Prefabs/CubeCritter.prefab");
            var spots = new[] { new Vector3(-2.6f, 0.02f, 0.9f), new Vector3(-0.9f, 0.02f, -0.3f), new Vector3(0.9f, 0.02f, 0.4f), new Vector3(2.5f, 0.02f, -0.6f) };
            backdrop.critters = spots.Select((s, i) =>
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(critter);
                g.name = "MenuCritter_" + i;
                g.transform.SetPositionAndRotation(s, Quaternion.Euler(0f, 160f + i * 15f, 0f));
                g.GetComponent<Rigidbody>().isKinematic = true;
                return g.GetComponentInChildren<CritterAnimator>();
            }).ToArray();
            var ls = new LightingSettings { name = "LS_SP_Menu", bakedGI = false, realtimeGI = false };
            var lsPath = Root + "/Scenes/LS_SP_Menu.lighting";
            AssetDatabase.DeleteAsset(lsPath); AssetDatabase.CreateAsset(ls, lsPath);
            Lightmapping.lightingSettings = ls;
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            Lightmapping.Bake();
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            ShooterPartySetup.EnsureInBuildSettings(MenuScenePath);
        }
    }
}
