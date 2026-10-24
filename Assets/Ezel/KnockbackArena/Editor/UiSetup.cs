using System.IO;
using System.Linq;
using KnockbackArena.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using ShooterParty;
using ShooterParty.UI;

namespace KnockbackArena.EditorTools
{
    // Builds the UI kit (UI brief, A1-A3): Fredoka TMP font assets (static atlases, outline presets), sprite
    // import settings, and the prefabs HUD_KnockbackArena, MassRing and Lobby_KnockbackArena.
    // Needs TMP Essential Resources in Assets/TextMesh Pro (Window > TextMeshPro > Import TMP Essential Resources).
    public static class UiSetup
    {
        const string Root = CubeCritterSetup.Root;
        const string UiDir = Root + "/UI";
        public static readonly Color Ink = new Color32(0x16, 0x1A, 0x3A, 0xFF);
        static readonly Color Chartreuse = new Color32(0xD4, 0xF2, 0x1C, 0xFF);
        static readonly Color Gold = new Color32(0xFF, 0xD2, 0x3F, 0xFF);

        public static TMP_FontAsset Bold, Semi;
        public static Material BoldOutline, SemiOutline;

        public static void BuildAll()
        {
            ImportSprites();
            Bold = FontAsset("Fredoka-Bold");
            Semi = FontAsset("Fredoka-SemiBold");
            BoldOutline = OutlinePreset(Bold, 0.28f, true);
            SemiOutline = OutlinePreset(Semi, 0.22f, false);
            BuildHud();
            BuildMassRing();
            BuildLobby();
        }

        // ------------------------------------------------------------------ assets
        static void ImportSprites()
        {
            void Sprite(string path, Vector4 border, bool mips = false)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (!ti) { Debug.LogWarning("[UI] missing " + path); return; }
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100;
                ti.spriteBorder = border;   // L, B, R, T
                ti.mipmapEnabled = mips;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            foreach (var n in new[] { "UI_Panel", "UI_PanelOutline", "UI_PanelShadow" }) Sprite($"{UiDir}/Sprites/{n}.png", new Vector4(40, 40, 40, 40));
            Sprite($"{UiDir}/Sprites/UI_Pill.png", new Vector4(32, 31, 32, 31));
            foreach (var n in new[] { "UI_Circle", "UI_Ring", "UI_Crosshair", "UI_IconWeight", "UI_IconSpring", "UI_IconDash", "UI_IconCrown", "UI_IconCheck" })
                Sprite($"{UiDir}/Sprites/{n}.png", Vector4.zero);
            foreach (var g in AssetDatabase.FindAssets("Portrait_CC_ t:Texture2D", new[] { Root + "/Portraits" }))
                Sprite(AssetDatabase.GUIDToAssetPath(g), Vector4.zero, true);
        }

        public static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/Sprites/{name}.png");

        public static Sprite[] Faces(string player) => new[] { "Neutral", "Happy", "Hurt", "Sad" }
            .Select(e => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Portraits/Portrait_CC_{player}_{e}.png")).ToArray();

        static TMP_FontAsset FontAsset(string ttf)
        {
            var path = $"{UiDir}/Fonts/{ttf} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{UiDir}/Fonts/{ttf}.ttf");
            // 90 pt sampling + padding 12 leaves room for the thick outline / underlay the brief asks for
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 12, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            fa.name = ttf + " SDF";
            AssetDatabase.CreateAsset(fa, path);
            fa.atlasTextures[0].name = fa.name + " Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa);
            var chars = "";
            for (char c = (char)32; c < 127; c++) chars += c;
            chars += "çöüıÇÖÜ·×…–—";
            if (!fa.TryAddCharacters(chars, out var missing)) Debug.Log($"[UI] {ttf}: glyphs not in font: {missing}");
            fa.atlasPopulationMode = AtlasPopulationMode.Static;   // deterministic atlas, no TTF needed at runtime
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        static Material OutlinePreset(TMP_FontAsset fa, float width, bool underlay)
        {
            var path = $"{UiDir}/Fonts/{fa.name} Outline.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(fa.material); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("TextMeshPro/Distance Field");
            m.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, width * 0.5f);
            if (underlay)
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.04f, 0.05f, 0.14f, 0.85f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.55f);
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, width);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ builders
        static RectTransform R(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void Fill(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        static Image I(Transform parent, string name, Sprite sprite, Color c, Vector2 anchor, Vector2 pos, Vector2 size, Image.Type type = Image.Type.Simple)
        {
            var img = R(parent, name, anchor, pos, size).gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = c; img.type = type; img.raycastTarget = false;
            if (type == Image.Type.Sliced) img.pixelsPerUnitMultiplier = 1.6f;   // corners ~25 px on screen
            return img;
        }

        static Image Filled(Transform parent, string name, Sprite sprite, Color c, Vector2 pos, Vector2 size, Image.FillMethod method)
        {
            var img = I(parent, name, sprite, c, new Vector2(0.5f, 0.5f), pos, size, Image.Type.Filled);
            img.fillMethod = method;
            img.fillOrigin = method == Image.FillMethod.Radial360 ? (int)Image.Origin360.Top : (int)Image.OriginHorizontal.Left;
            img.fillClockwise = true;
            img.fillAmount = 1f;
            return img;
        }

        static TextMeshProUGUI T(Transform parent, string name, string text, float size, Color c, Vector2 anchor, Vector2 pos, Vector2 box,
                                 TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true, bool outline = true)
        {
            var t = R(parent, name, anchor, pos, box).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? Bold : Semi;
            if (outline) t.fontSharedMaterial = bold ? BoldOutline : SemiOutline;
            t.text = text; t.fontSize = size; t.color = c; t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        // rounded "sticker" panel: hard shadow, ink fill, soft white stroke, top gloss
        static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill, float stroke = 0.85f)
        {
            var rt = R(parent, name, anchor, pos, size);
            var sh = I(rt, "Shadow", S("UI_Panel"), new Color(0.043f, 0.055f, 0.141f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), size, Image.Type.Sliced);
            var bg = I(rt, "Fill", S("UI_Panel"), fill, new Vector2(0.5f, 0.5f), Vector2.zero, size, Image.Type.Sliced);
            var gl = I(rt, "Gloss", S("UI_Panel"), new Color(1f, 1f, 1f, 0.035f), new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.22f), new Vector2(size.x - 10f, size.y * 0.42f), Image.Type.Sliced);
            if (stroke > 0f) I(rt, "Stroke", S("UI_PanelOutline"), new Color(1f, 1f, 1f, stroke), new Vector2(0.5f, 0.5f), Vector2.zero, size, Image.Type.Sliced);
            return rt;
        }

        static Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color fill, Color text)
        {
            var bg = I(parent, name, S("UI_Panel"), fill, anchor, pos, size, Image.Type.Sliced);
            bg.raycastTarget = true;
            I(bg.transform, "Shadow", S("UI_Panel"), new Color(0.043f, 0.055f, 0.141f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), size, Image.Type.Sliced).transform.SetAsFirstSibling();
            var b = bg.gameObject.AddComponent<Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); cb.fadeDuration = 0.06f; b.colors = cb;
            bg.gameObject.AddComponent<UiHoverSound>();
            T(bg.transform, "Label", label, size.y * 0.42f, text, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), size, TextAlignmentOptions.Center, true, false);
            return b;
        }

        static Canvas OverlayCanvas(GameObject go, int order)
        {
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        static GameObject Save(GameObject go, string name)
        {
            var p = PrefabUtility.SaveAsPrefabAsset(go, $"{UiDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return p;
        }

        // ------------------------------------------------------------------ HUD
        static void BuildHud()
        {
            var go = new GameObject("HUD_KnockbackArena", typeof(RectTransform));
            OverlayCanvas(go, 10);
            var hud = go.AddComponent<PartyHud>();
            var top = new Vector2(0.5f, 1f); var mid = new Vector2(0.5f, 0.5f);

            // cards: 4 x 300x100, 16 px gaps, 32 px from the top
            hud.cards = new HudCard[4];
            for (int i = 0; i < 4; i++)
            {
                var card = Panel(go.transform, "Card" + i, top, new Vector2(-474f + 316f * i, -82f), new Vector2(300f, 100f), new Color(Ink.r, Ink.g, Ink.b, 0.92f), 0.25f);
                var hc = card.gameObject.AddComponent<HudCard>();
                hc.group = card.gameObject.AddComponent<CanvasGroup>();
                hc.outline = I(card, "LocalStroke", S("UI_PanelOutline"), Color.white, mid, Vector2.zero, new Vector2(306f, 106f), Image.Type.Sliced);
                hc.band = I(card, "Band", S("UI_Pill"), Color.white, mid, new Vector2(-139f, 0f), new Vector2(8f, 72f), Image.Type.Sliced);
                hc.portraitBg = I(card, "PortraitBg", S("UI_Circle"), Color.white, mid, new Vector2(-96f, 0f), new Vector2(76f, 76f));
                hc.portrait = I(card, "Portrait", null, Color.white, mid, new Vector2(-96f, 5f), new Vector2(98f, 98f));
                hc.portrait.preserveAspect = true;
                hc.nameText = T(card, "Name", "PLAYER", 24f, Color.white, mid, new Vector2(4f, 28f), new Vector2(110f, 30f), TextAlignmentOptions.Left, false);
                hc.massGroup = R(card, "Mass", mid, new Vector2(4f, -8f), new Vector2(110f, 50f));
                I(hc.massGroup, "Icon", S("UI_IconWeight"), new Color(1f, 1f, 1f, 0.85f), mid, new Vector2(-43f, -1f), new Vector2(24f, 24f));
                hc.massText = T(hc.massGroup, "Value", "100%", 35f, Color.white, mid, new Vector2(12f, 0f), new Vector2(90f, 48f), TextAlignmentOptions.Left);
                hc.massText.rectTransform.pivot = new Vector2(0.2f, 0.5f);
                I(card, "BarTrack", S("UI_Pill"), new Color(1f, 1f, 1f, 0.15f), mid, new Vector2(4f, -36f), new Vector2(110f, 10f), Image.Type.Sliced);
                hc.barGhost = Filled(card, "BarGhost", S("UI_Pill"), new Color(1f, 1f, 1f, 0.9f), new Vector2(4f, -36f), new Vector2(110f, 10f), Image.FillMethod.Horizontal);
                hc.barFill = Filled(card, "BarFill", S("UI_Pill"), Color.white, new Vector2(4f, -36f), new Vector2(110f, 10f), Image.FillMethod.Horizontal);
                hc.scoreText = T(card, "Score", "0", 40f, Color.white, mid, new Vector2(116f, 6f), new Vector2(56f, 48f));
                T(card, "PtsLabel", "PTS", 15f, new Color(1f, 1f, 1f, 0.6f), mid, new Vector2(116f, -26f), new Vector2(56f, 20f), TextAlignmentOptions.Center, false, false);
                hc.crown = I(card, "Crown", S("UI_IconCrown"), Gold, mid, new Vector2(-122f, 44f), new Vector2(44f, 44f));
                hc.crown.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                hc.crown.enabled = false;
                hc.deltaText = T(card, "Delta", "-8", 28f, ShooterParty.UI.HudCard.Pink, mid, new Vector2(70f, 22f), new Vector2(60f, 34f));
                hud.cards[i] = hc;
            }

            // crosshair + cooldown ring (centre)
            hud.crosshair = R(go.transform, "Crosshair", mid, Vector2.zero, new Vector2(64f, 64f));
            hud.cooldownTrack = I(hud.crosshair, "CooldownTrack", S("UI_Ring"), new Color(Ink.r, Ink.g, Ink.b, 0.35f), mid, Vector2.zero, new Vector2(50f, 50f));
            hud.cooldownRing = Filled(hud.crosshair, "CooldownRing", S("UI_Ring"), Color.white, Vector2.zero, new Vector2(50f, 50f), Image.FillMethod.Radial360);
            hud.crosshairImage = I(hud.crosshair, "Reticle", S("UI_Crosshair"), Color.white, mid, Vector2.zero, new Vector2(64f, 64f));

            // dash pips + jump slot (bottom right, 48 px in)
            var br = new Vector2(1f, 0f);
            var dash = R(go.transform, "Dash", br, new Vector2(-260f, 86f), new Vector2(150f, 70f));
            T(dash, "Label", "DASH  <size=70%><alpha=#AA>SHIFT", 20f, Color.white, mid, new Vector2(0f, 22f), new Vector2(150f, 26f), TextAlignmentOptions.Center, false);
            hud.dashPips = new Image[3]; hud.dashFills = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector2(-36f + 36f * i, -10f);
                I(dash, "PipOutline" + i, S("UI_Circle"), Ink, mid, pos, new Vector2(32f, 32f));
                hud.dashPips[i] = I(dash, "Pip" + i, S("UI_Circle"), Color.white, mid, pos, new Vector2(26f, 26f));
                hud.dashFills[i] = Filled(dash, "PipFill" + i, S("UI_Circle"), new Color32(0xFF, 0xC2, 0x33, 0xFF), pos, new Vector2(26f, 26f), Image.FillMethod.Radial360);
            }
            var jump = Panel(go.transform, "JumpSlot", br, new Vector2(-96f, 96f), new Vector2(80f, 80f), new Color(Ink.r, Ink.g, Ink.b, 0.85f), 0.6f);
            hud.jumpSlot = jump.gameObject.AddComponent<CanvasGroup>();
            hud.jumpIcon = I(jump, "Icon", S("UI_IconSpring"), Gold, mid, Vector2.zero, new Vector2(52f, 52f));
            hud.jumpHint = T(jump, "Hint", "SPACE", 16f, Color.white, mid, new Vector2(0f, -52f), new Vector2(90f, 22f), TextAlignmentOptions.Center, false);

            // countdown
            hud.countdown = T(go.transform, "Countdown", "3", 320f, Color.white, mid, new Vector2(0f, 60f), new Vector2(900f, 400f));

            // OUT! banner (eliminated player) + toast (everyone else)
            hud.banner = R(go.transform, "OutBanner", mid, Vector2.zero, new Vector2(2600f, 220f));
            hud.banner.localRotation = Quaternion.Euler(0f, 0f, 4f);
            hud.bannerBg = I(hud.banner, "Ribbon", null, Color.white, mid, Vector2.zero, new Vector2(2600f, 220f));
            I(hud.banner, "EdgeTop", null, new Color(1f, 1f, 1f, 0.5f), mid, new Vector2(0f, 104f), new Vector2(2600f, 12f));
            I(hud.banner, "EdgeBottom", null, new Color(0f, 0f, 0f, 0.18f), mid, new Vector2(0f, -104f), new Vector2(2600f, 12f));
            hud.bannerTitle = T(hud.banner, "Title", "OUT!", 200f, Color.white, mid, new Vector2(-60f, 8f), new Vector2(800f, 240f));
            hud.bannerPlace = T(hud.banner, "Place", "4th", 56f, Color.white, mid, new Vector2(330f, -40f), new Vector2(200f, 80f));
            hud.toast = Panel(go.transform, "Toast", top, new Vector2(0f, -190f), new Vector2(640f, 72f), new Color(Ink.r, Ink.g, Ink.b, 0.92f));
            hud.toast.gameObject.AddComponent<CanvasGroup>();
            hud.toastPortrait = I(hud.toast, "Portrait", null, Color.white, mid, new Vector2(-276f, 6f), new Vector2(76f, 76f));
            hud.toastText = T(hud.toast, "Text", "TEAL is OUT!", 30f, Color.white, mid, new Vector2(30f, 0f), new Vector2(540f, 60f), TextAlignmentOptions.Left, false);

            // results (right 40 %): rows reveal 4th -> 1st
            hud.results = Panel(go.transform, "Results", new Vector2(1f, 0.5f), new Vector2(-400f, 0f), new Vector2(720f, 700f), new Color(Ink.r, Ink.g, Ink.b, 0.9f));
            T(hud.results, "Title", "RESULTS", 64f, Color.white, mid, new Vector2(0f, 290f), new Vector2(600f, 80f));
            hud.rows = new PartyHud.ResultRow[4];
            for (int i = 0; i < 4; i++)
            {
                var row = R(hud.results, "Row" + i, mid, new Vector2(0f, 170f - 122f * i), new Vector2(640f, 110f));
                I(row, "Bg", S("UI_Panel"), new Color(1f, 1f, 1f, i == 0 ? 0.16f : 0.08f), mid, Vector2.zero, new Vector2(640f, 110f), Image.Type.Sliced);
                var r = new PartyHud.ResultRow { root = row };
                r.badge = I(row, "Badge", S("UI_Circle"), Color.white, mid, new Vector2(-262f, 0f), new Vector2(84f, 84f));
                r.place = T(row, "Place", "1st", 30f, Ink, mid, new Vector2(-262f, 0f), new Vector2(84f, 40f), TextAlignmentOptions.Center, true, false);
                r.crown = I(row, "Crown", S("UI_IconCrown"), Gold, mid, new Vector2(-290f, 46f), new Vector2(50f, 50f));
                r.crown.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
                r.portrait = I(row, "Portrait", null, Color.white, mid, new Vector2(-168f, 6f), new Vector2(108f, 108f));
                r.playerName = T(row, "Name", "PLAYER", 36f, Color.white, mid, new Vector2(-10f, 14f), new Vector2(200f, 44f), TextAlignmentOptions.Left, false);
                r.plus = T(row, "Plus", "+4", 28f, Chartreuse, mid, new Vector2(-40f, -26f), new Vector2(80f, 34f), TextAlignmentOptions.Left);
                var chip = I(row, "Comeback", S("UI_Pill"), new Color32(0xFF, 0x3D, 0x8B, 0xFF), mid, new Vector2(80f, -26f), new Vector2(150f, 30f), Image.Type.Sliced);
                T(chip.transform, "Label", "+1 COMEBACK", 16f, Color.white, mid, new Vector2(0f, 1f), new Vector2(150f, 26f), TextAlignmentOptions.Center, true, false);
                r.comeback = chip.gameObject;
                r.total = T(row, "Total", "0", 56f, Color.white, mid, new Vector2(250f, 0f), new Vector2(110f, 70f));
                hud.rows[i] = r;
            }
            hud.resultsHint = T(hud.results, "Hint", "<b>R</b>  play again     <b>Esc</b>  lobby", 24f, new Color(1f, 1f, 1f, 0.8f), mid, new Vector2(0f, -300f), new Vector2(600f, 36f), TextAlignmentOptions.Center, false, false);
            Save(go, "HUD_KnockbackArena");
        }

        // ------------------------------------------------------------------ world-space mass ring
        static void BuildMassRing()
        {
            var go = new GameObject("MassRing", typeof(RectTransform));
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(128f, 128f);
            go.transform.localScale = Vector3.one * 0.004f;
            var ring = go.AddComponent<MassRing>();
            var mid = new Vector2(0.5f, 0.5f);
            I(go.transform, "Keyline", S("UI_Circle"), new Color(Ink.r, Ink.g, Ink.b, 0.85f), mid, Vector2.zero, new Vector2(128f, 128f));
            I(go.transform, "Rim", S("UI_Ring"), Color.white, mid, Vector2.zero, new Vector2(124f, 124f));
            I(go.transform, "Track", S("UI_Ring"), new Color(1f, 1f, 1f, 0.18f), mid, Vector2.zero, new Vector2(112f, 112f));
            ring.fill = Filled(go.transform, "Fill", S("UI_Ring"), Color.white, Vector2.zero, new Vector2(112f, 112f), Image.FillMethod.Radial360);
            ring.percent = T(go.transform, "Percent", "100", 44f, Color.white, mid, new Vector2(0f, 2f), new Vector2(110f, 60f));
            Save(go, "MassRing");
        }

        // ------------------------------------------------------------------ lobby (+ logo)
        static void BuildLobby()
        {
            var go = new GameObject("Lobby_KnockbackArena", typeof(RectTransform));
            OverlayCanvas(go, 20);
            var lp = go.AddComponent<LobbyPanel>();
            var mid = new Vector2(0.5f, 0.5f);
            var dim = I(go.transform, "Dim", null, new Color(Ink.r, Ink.g, Ink.b, 0.35f), mid, Vector2.zero, Vector2.zero);
            Fill(dim.rectTransform);

            // logo (U9): KNOCKBACK over ARENA, tilted, white face, thick ink outline, hard shadow
            var logo = R(go.transform, "Logo", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 240f));
            logo.localRotation = Quaternion.Euler(0f, 0f, 4f);
            T(logo, "Knockback", "KNOCKBACK", 128f, Color.white, mid, new Vector2(-20f, 40f), new Vector2(1000f, 150f));
            var arena = T(logo, "Arena", "ARENA", 104f, Chartreuse, mid, new Vector2(120f, -62f), new Vector2(700f, 130f));
            arena.characterSpacing = 18f;
            var cube = I(logo, "Critter", AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Portraits/Portrait_CC_Tomato_Happy.png"), Color.white, mid, new Vector2(-300f, -78f), new Vector2(160f, 160f));
            cube.preserveAspect = true;

            var panel = Panel(go.transform, "Panel", mid, new Vector2(0f, -110f), new Vector2(1400f, 720f), new Color(Ink.r, Ink.g, Ink.b, 0.92f));
            T(panel, "Title", "LOBBY", 44f, Color.white, mid, new Vector2(-480f, 300f), new Vector2(360f, 60f), TextAlignmentOptions.Left);
            // left: host / join
            lp.hostButton = Btn(panel, "Host", "HOST GAME", mid, new Vector2(-480f, 190f), new Vector2(360f, 84f), Chartreuse, Ink);
            lp.hostInfo = T(panel, "HostInfo", "", 22f, new Color(1f, 1f, 1f, 0.8f), mid, new Vector2(-480f, 128f), new Vector2(360f, 30f), TextAlignmentOptions.Center, false, false);
            T(panel, "JoinLabel", "JOIN BY IP", 24f, new Color(1f, 1f, 1f, 0.7f), mid, new Vector2(-480f, 70f), new Vector2(360f, 30f), TextAlignmentOptions.Left, false, false);
            lp.ipField = Field(panel, "IP", "192.168.1.24", new Vector2(-530f, 14f), new Vector2(260f, 72f));
            lp.portField = Field(panel, "Port", "7777", new Vector2(-342f, 14f), new Vector2(96f, 72f));
            lp.joinButton = Btn(panel, "Join", "JOIN", mid, new Vector2(-480f, -76f), new Vector2(360f, 76f), new Color32(0x00, 0xB3, 0xA4, 0xFF), Ink);
            // right: four slots
            lp.slots = new LobbyPanel.Slot[4];
            for (int i = 0; i < 4; i++)
            {
                var x = -120f + 236f * i;
                var s = new LobbyPanel.Slot();
                s.card = I(panel, "Slot" + i, S("UI_Panel"), Color.white, mid, new Vector2(x, 90f), new Vector2(220f, 320f), Image.Type.Sliced);
                I(s.card.transform, "Inner", S("UI_Panel"), new Color(Ink.r, Ink.g, Ink.b, 0.35f), mid, new Vector2(0f, 24f), new Vector2(196f, 196f), Image.Type.Sliced);
                s.portrait = I(s.card.transform, "Portrait", null, Color.white, mid, new Vector2(0f, 34f), new Vector2(200f, 200f));
                s.portrait.preserveAspect = true;
                s.playerName = T(s.card.transform, "Name", "", 28f, Color.white, mid, new Vector2(0f, -94f), new Vector2(200f, 36f));
                s.pill = I(s.card.transform, "Pill", S("UI_Pill"), Chartreuse, mid, new Vector2(0f, -136f), new Vector2(168f, 40f), Image.Type.Sliced);
                s.status = T(s.pill.transform, "Status", "READY", 20f, Ink, mid, new Vector2(10f, 1f), new Vector2(150f, 34f), TextAlignmentOptions.Center, true, false);
                s.check = I(s.pill.transform, "Check", S("UI_IconCheck"), Ink, mid, new Vector2(-56f, 0f), new Vector2(24f, 24f));
                lp.slots[i] = s;
            }
            // bottom: ready + start
            lp.readyButton = Btn(panel, "Ready", "READY!", mid, new Vector2(120f, -250f), new Vector2(300f, 84f), Color.white, Ink);
            lp.readyLabel = lp.readyButton.GetComponentInChildren<TextMeshProUGUI>();
            lp.startButton = Btn(panel, "Start", "START", mid, new Vector2(480f, -250f), new Vector2(300f, 84f), Chartreuse, Ink);
            T(panel, "Hint", "<b>Enter</b> start  ·  WASD move  ·  mouse aim  ·  click spit  ·  Shift dash  ·  Space jump (pickup)", 22f,
              new Color(1f, 1f, 1f, 0.7f), mid, new Vector2(0f, -330f), new Vector2(1300f, 30f), TextAlignmentOptions.Center, false, false);
            Save(go, "Lobby_KnockbackArena");
        }

        static TMP_InputField Field(Transform parent, string name, string value, Vector2 pos, Vector2 size)
        {
            var bg = I(parent, name, S("UI_Panel"), Color.white, new Vector2(0.5f, 0.5f), pos, size, Image.Type.Sliced);
            bg.raycastTarget = true;
            var area = R(bg.transform, "Text Area", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(28f, 12f));
            area.gameObject.AddComponent<RectMask2D>();
            var ph = T(area, "Placeholder", value, 30f, new Color(Ink.r, Ink.g, Ink.b, 0.4f), new Vector2(0.5f, 0.5f), Vector2.zero, area.sizeDelta, TextAlignmentOptions.Left, false, false);
            var tx = T(area, "Text", "", 30f, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, area.sizeDelta, TextAlignmentOptions.Left, false, false);
            var f = bg.gameObject.AddComponent<TMP_InputField>();
            f.textViewport = area; f.textComponent = tx; f.placeholder = ph;
            f.fontAsset = Semi; f.pointSize = 30f;
            f.text = value;
            return f;
        }
    }
}
