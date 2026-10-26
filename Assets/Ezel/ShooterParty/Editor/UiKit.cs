using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using ShooterParty.UI;

namespace ShooterParty.EditorTools
{
    // The shared UI look, as code: Fredoka with an ink outline, rounded "sticker" panels, candy buttons.
    // Every screen builder (core menus, lobby, HUD, and mini-game widgets) uses these, so the game reads as one.
    // The fonts and sprites live with the shared art in KnockbackArena/UI for now (see ARCHITECTURE.md).
    public static class UiKit
    {
        public const string ArtRoot = "Assets/Ezel/KnockbackArena";
        const string Root = ArtRoot;
        const string UiDir = ArtRoot + "/UI";
        public static readonly Color Ink = new Color32(0x16, 0x1A, 0x3A, 0xFF);
        public static readonly Color Chartreuse = new Color32(0xD4, 0xF2, 0x1C, 0xFF);
        public static readonly Color Gold = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        public static readonly Color Paper = new Color32(0xF4, 0xF7, 0xFF, 0xFF);
        public static readonly Color Muted = new Color32(0x9A, 0xA6, 0xD6, 0xFF);
        public static readonly Color Teal = new Color32(0x00, 0xB3, 0xA4, 0xFF);
        public static readonly Color Tomato = new Color32(0xFF, 0x4F, 0x2E, 0xFF);
        public static readonly Color Violet = new Color32(0x7B, 0x2F, 0xD0, 0xFF);
        public static readonly Color Card = new Color32(0x22, 0x28, 0x5A, 0xFF);
        public static readonly Color Pink = new Color32(0xFF, 0x5C, 0x8A, 0xFF);
        public static readonly Color Glass = new Color(1f, 1f, 1f, 0.12f);   // secondary buttons on a card

        // Loads (or makes) the fonts and imports the sprites; call once before building.
        public static void Init()
        {
            ImportSprites();
            Bold = FontAsset("Fredoka-Bold");
            Semi = FontAsset("Fredoka-SemiBold");
            BoldOutline = OutlinePreset(Bold, 0.28f, true);
            SemiOutline = OutlinePreset(Semi, 0.22f, false);
        }

        public static TMP_FontAsset Bold, Semi;
        public static Material BoldOutline, SemiOutline;

        // ------------------------------------------------------------------ assets
        public static void ImportSprites()
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
            foreach (var n in new[] { "UI_Circle", "UI_Ring", "UI_Crosshair", "UI_IconWeight", "UI_IconSpring", "UI_IconDash", "UI_IconCrown", "UI_IconCheck", "UI_FadeH", "UI_IconArrow" })
                Sprite($"{UiDir}/Sprites/{n}.png", Vector4.zero);
            foreach (var g in AssetDatabase.FindAssets("Portrait_CC_ t:Texture2D", new[] { Root + "/Portraits" }))
                Sprite(AssetDatabase.GUIDToAssetPath(g), Vector4.zero, true);
        }

        public static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/Sprites/{name}.png");

        public static Sprite[] Faces(string player) => new[] { "Neutral", "Happy", "Hurt", "Sad" }
            .Select(e => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Portraits/Portrait_CC_{player}_{e}.png")).ToArray();

        public static TMP_FontAsset FontAsset(string ttf)
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

        public static Material OutlinePreset(TMP_FontAsset fa, float width, bool underlay)
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
        public static RectTransform R(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
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

        public static void Fill(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        public static Image I(Transform parent, string name, Sprite sprite, Color c, Vector2 anchor, Vector2 pos, Vector2 size, Image.Type type = Image.Type.Simple)
        {
            var img = R(parent, name, anchor, pos, size).gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = c; img.type = type; img.raycastTarget = false;
            if (type == Image.Type.Sliced) img.pixelsPerUnitMultiplier = 1.6f;   // corners ~25 px on screen
            return img;
        }

        public static Image Filled(Transform parent, string name, Sprite sprite, Color c, Vector2 pos, Vector2 size, Image.FillMethod method)
        {
            var img = I(parent, name, sprite, c, new Vector2(0.5f, 0.5f), pos, size, Image.Type.Filled);
            img.fillMethod = method;
            img.fillOrigin = method == Image.FillMethod.Radial360 ? (int)Image.Origin360.Top : (int)Image.OriginHorizontal.Left;
            img.fillClockwise = true;
            img.fillAmount = 1f;
            return img;
        }

        public static TextMeshProUGUI T(Transform parent, string name, string text, float size, Color c, Vector2 anchor, Vector2 pos, Vector2 box,
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
        public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill, float stroke = 0.85f)
        {
            var rt = R(parent, name, anchor, pos, size);
            var sh = I(rt, "Shadow", S("UI_Panel"), new Color(0.043f, 0.055f, 0.141f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), size, Image.Type.Sliced);
            var bg = I(rt, "Fill", S("UI_Panel"), fill, new Vector2(0.5f, 0.5f), Vector2.zero, size, Image.Type.Sliced);
            float gh = Mathf.Min(size.y * 0.42f, 26f);   // a lip, not a band: tall modals would get a two-tone fill
            var gl = I(rt, "Gloss", S("UI_Panel"), new Color(1f, 1f, 1f, 0.035f), new Vector2(0.5f, 1f), new Vector2(0f, -5f - gh * 0.5f), new Vector2(size.x - 10f, gh), Image.Type.Sliced);
            if (stroke > 0f) I(rt, "Stroke", S("UI_PanelOutline"), new Color(1f, 1f, 1f, stroke), new Vector2(0.5f, 0.5f), Vector2.zero, size, Image.Type.Sliced);
            return rt;
        }

        public static Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color fill, Color text)
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

        public static Canvas OverlayCanvas(GameObject go, int order)
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

        public static GameObject Save(GameObject go, string name)
        {
            var p = PrefabUtility.SaveAsPrefabAsset(go, $"{UiDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return p;
        }

    }
}
