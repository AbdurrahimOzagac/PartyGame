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
using ShooterParty.EditorTools;
using static ShooterParty.EditorTools.UiKit;

namespace KnockbackArena.EditorTools
{
    // Builds the UI kit (UI brief, A1-A3): Fredoka TMP font assets (static atlases, outline presets), sprite
    // import settings, and the prefabs HUD_KnockbackArena, and MassRing.
    // Needs TMP Essential Resources in Assets/TextMesh Pro (Window > TextMeshPro > Import TMP Essential Resources).
    public static class UiSetup
    {
        const string Root = CubeCritterSetup.Root;
        const string UiDir = Root + "/UI";

        public static void BuildAll()
        {
            UiKit.Init();
            BuildHud();
            BuildMassRing();
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
