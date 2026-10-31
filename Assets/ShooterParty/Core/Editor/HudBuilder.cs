using ShooterParty.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ShooterParty.EditorTools.UiKit;

namespace ShooterParty.EditorTools
{
    // The session's in-game views inside UI_Root, the same for every mini-game:
    //   PartyHud (+ CrownView)   cards, timer, aim, dash/jump, 3-2-1-GO, OUT!, spectate label, widget slot
    //   IntroCard                name, tagline, how to play, controls, ROUND x OF n
    //   RoundResultsView         the round's ranking, points into totals
    //   StandingsView            bar race of the totals, next game
    //   FinalView                champion and places over SP_Lobby's podium
    // Layout reference: 1920 x 1080.
    public static class HudBuilder
    {
        static readonly Vector2 C = new Vector2(0.5f, 0.5f), Top = new Vector2(0.5f, 1f);
        static Color InkA(float a) => new Color(Ink.r, Ink.g, Ink.b, a);

        public static Sprite[] AllFaces()
        {
            var all = new Sprite[16];
            for (int c = 0; c < 4; c++)
            {
                var f = Faces(CritterLook.Names[c]);   // Neutral, Happy, Hurt, Sad = HudCard.Face order
                for (int i = 0; i < 4; i++) all[c * 4 + i] = f[i];
            }
            return all;
        }

        static RectTransform Content(Component screen)
        {
            var rt = R(screen.transform, "Content", C, Vector2.zero, Vector2.zero);
            Fill(rt);
            return rt;
        }

        // ------------------------------------------------------------------ HUD
        public static PartyHud BuildHud(Transform parent)
        {
            var hud = UiBuilder.Screen<PartyHud>(parent, "HUD");
            var root = Content(hud);
            hud.faces = AllFaces();
            var crowns = hud.gameObject.AddComponent<CrownView>();
            crowns.crownPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/Prefabs/Props/Crown.prefab")?.GetComponent<LeaderCrown>();

            // cards: 4 x 300x100, 16 px gaps, 32 px from the top
            hud.cards = new HudCard[4];
            for (int i = 0; i < 4; i++) hud.cards[i] = SeatCard(root, i);
            hud.widgetSlot = R(root, "MiniGameWidget", Top, new Vector2(0f, -200f), new Vector2(1260f, 80f));
            hud.gameName = T(root, "GameName", "KNOCKBACK ARENA", 24f, new Color(1f, 1f, 1f, 0.85f), new Vector2(0f, 1f), new Vector2(200f, -40f), new Vector2(320f, 30f), TextAlignmentOptions.Left, false);
            var timerPill = I(root, "TimerPill", S("UI_Pill"), InkA(0.85f), Top, new Vector2(0f, -170f), new Vector2(170f, 58f), Image.Type.Sliced);
            hud.timer = T(timerPill.transform, "Timer", "2:00", 40f, Color.white, C, new Vector2(0f, 2f), new Vector2(170f, 54f));

            // crosshair + cooldown ring (centre)
            hud.crosshair = R(root, "Crosshair", C, Vector2.zero, new Vector2(64f, 64f));
            hud.cooldownTrack = I(hud.crosshair, "CooldownTrack", S("UI_Ring"), InkA(0.35f), C, Vector2.zero, new Vector2(50f, 50f));
            hud.cooldownRing = Filled(hud.crosshair, "CooldownRing", S("UI_Ring"), Color.white, Vector2.zero, new Vector2(50f, 50f), Image.FillMethod.Radial360);
            hud.crosshairImage = I(hud.crosshair, "Reticle", S("UI_Crosshair"), Color.white, C, Vector2.zero, new Vector2(64f, 64f));

            // dash pips + jump slot (bottom right, 48 px in)
            var br = new Vector2(1f, 0f);
            var dash = R(root, "Dash", br, new Vector2(-260f, 86f), new Vector2(150f, 70f));
            T(dash, "Label", "DASH  <size=70%><alpha=#AA>SHIFT", 20f, Color.white, C, new Vector2(0f, 22f), new Vector2(150f, 26f), TextAlignmentOptions.Center, false);
            hud.dashPips = new Image[3]; hud.dashFills = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector2(-36f + 36f * i, -10f);
                I(dash, "PipOutline" + i, S("UI_Circle"), Ink, C, pos, new Vector2(32f, 32f));
                hud.dashPips[i] = I(dash, "Pip" + i, S("UI_Circle"), Color.white, C, pos, new Vector2(26f, 26f));
                hud.dashFills[i] = Filled(dash, "PipFill" + i, S("UI_Circle"), new Color32(0xFF, 0xC2, 0x33, 0xFF), pos, new Vector2(26f, 26f), Image.FillMethod.Radial360);
            }
            var jump = Panel(root, "JumpSlot", br, new Vector2(-96f, 96f), new Vector2(80f, 80f), InkA(0.85f), 0.6f);
            hud.jumpSlot = jump.gameObject.AddComponent<CanvasGroup>();
            hud.jumpIcon = I(jump, "Icon", S("UI_IconSpring"), Gold, C, Vector2.zero, new Vector2(52f, 52f));
            hud.jumpHint = T(jump, "Hint", "SPACE", 16f, Color.white, C, new Vector2(0f, -52f), new Vector2(90f, 22f), TextAlignmentOptions.Center, false);

            hud.countdown = T(root, "Countdown", "3", 320f, Color.white, C, new Vector2(0f, 60f), new Vector2(900f, 400f));

            // OUT! banner (eliminated player) + toast (everyone else) + spectate label
            hud.banner = R(root, "OutBanner", C, Vector2.zero, new Vector2(2600f, 220f));
            hud.banner.localRotation = Quaternion.Euler(0f, 0f, 4f);
            hud.bannerBg = I(hud.banner, "Ribbon", null, Color.white, C, Vector2.zero, new Vector2(2600f, 220f));
            I(hud.banner, "EdgeTop", null, new Color(1f, 1f, 1f, 0.5f), C, new Vector2(0f, 104f), new Vector2(2600f, 12f));
            I(hud.banner, "EdgeBottom", null, new Color(0f, 0f, 0f, 0.18f), C, new Vector2(0f, -104f), new Vector2(2600f, 12f));
            hud.bannerTitle = T(hud.banner, "Title", "OUT!", 200f, Color.white, C, new Vector2(-60f, 8f), new Vector2(800f, 240f));
            hud.bannerPlace = T(hud.banner, "Place", "4th", 56f, Color.white, C, new Vector2(330f, -40f), new Vector2(200f, 80f));
            hud.toast = Panel(root, "Toast", Top, new Vector2(0f, -190f), new Vector2(640f, 72f), InkA(0.92f));
            hud.toast.gameObject.AddComponent<CanvasGroup>();
            hud.toastPortrait = I(hud.toast, "Portrait", null, Color.white, C, new Vector2(-276f, 6f), new Vector2(76f, 76f));
            hud.toastText = T(hud.toast, "Text", "TEAL is OUT!", 30f, Color.white, C, new Vector2(30f, 0f), new Vector2(540f, 60f), TextAlignmentOptions.Left, false);
            var spec = I(root, "Spectate", S("UI_Pill"), InkA(0.88f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(620f, 60f), Image.Type.Sliced);
            hud.spectateLabel = T(spec.transform, "Text", "YOU'RE OUT", 28f, Color.white, C, new Vector2(0f, 1f), new Vector2(600f, 50f), TextAlignmentOptions.Center, true, false);
            hud.spectateLabel.richText = true;
            return hud;
        }

        static HudCard SeatCard(Transform root, int i)
        {
            var card = Panel(root, "Card" + i, Top, new Vector2(-474f + 316f * i, -82f), new Vector2(300f, 100f), InkA(0.92f), 0.25f);
            var hc = card.gameObject.AddComponent<HudCard>();
            hc.group = card.gameObject.AddComponent<CanvasGroup>();
            hc.outline = I(card, "LocalStroke", S("UI_PanelOutline"), Color.white, C, Vector2.zero, new Vector2(306f, 106f), Image.Type.Sliced);
            hc.band = I(card, "Band", S("UI_Pill"), Color.white, C, new Vector2(-139f, 0f), new Vector2(8f, 72f), Image.Type.Sliced);
            hc.portraitBg = I(card, "PortraitBg", S("UI_Circle"), Color.white, C, new Vector2(-96f, 0f), new Vector2(76f, 76f));
            hc.portrait = I(card, "Portrait", null, Color.white, C, new Vector2(-96f, 5f), new Vector2(98f, 98f));
            hc.portrait.preserveAspect = true;
            hc.nameText = T(card, "Name", "PLAYER", 24f, Color.white, C, new Vector2(4f, 28f), new Vector2(110f, 30f), TextAlignmentOptions.Left, false);
            hc.nameText.overflowMode = TextOverflowModes.Ellipsis;
            hc.statGroup = R(card, "Stat", C, new Vector2(4f, -8f), new Vector2(110f, 50f));
            hc.statText = T(hc.statGroup, "Value", "100%", 35f, Color.white, C, new Vector2(0f, 0f), new Vector2(110f, 48f), TextAlignmentOptions.Left);
            hc.statText.rectTransform.pivot = new Vector2(0.2f, 0.5f);
            var track = I(hc.statGroup, "BarTrack", S("UI_Pill"), new Color(1f, 1f, 1f, 0.15f), C, new Vector2(0f, -28f), new Vector2(110f, 10f), Image.Type.Sliced);
            hc.barGhost = Filled(hc.statGroup, "BarGhost", S("UI_Pill"), new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, -28f), new Vector2(110f, 10f), Image.FillMethod.Horizontal);
            hc.barFill = Filled(hc.statGroup, "BarFill", S("UI_Pill"), Color.white, new Vector2(0f, -28f), new Vector2(110f, 10f), Image.FillMethod.Horizontal);
            hc.scoreText = T(card, "Score", "0", 40f, Color.white, C, new Vector2(116f, 6f), new Vector2(56f, 48f));
            T(card, "PtsLabel", "PTS", 15f, new Color(1f, 1f, 1f, 0.6f), C, new Vector2(116f, -26f), new Vector2(56f, 20f), TextAlignmentOptions.Center, false, false);
            hc.connection = I(card, "Connection", S("UI_Circle"), LobbyView.Good, C, new Vector2(132f, 38f), new Vector2(12f, 12f));
            hc.crown = I(card, "Crown", S("UI_IconCrown"), Gold, C, new Vector2(-122f, 44f), new Vector2(44f, 44f));
            hc.crown.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            hc.crown.enabled = false;
            hc.deltaText = T(card, "Delta", "-8", 28f, HudCard.Pink, C, new Vector2(70f, 22f), new Vector2(60f, 34f));
            return hc;
        }

        // ------------------------------------------------------------------ intro card
        public static IntroCard BuildIntro(Transform parent)
        {
            var v = UiBuilder.Screen<IntroCard>(parent, "Intro");
            var scrim = I(v.transform, "Scrim", null, new Color(0.03f, 0.04f, 0.11f, 0.5f), C, Vector2.zero, Vector2.zero);
            Fill(scrim.rectTransform);
            var p = Panel(v.transform, "Panel", C, new Vector2(0f, 20f), new Vector2(1120f, 640f), Card);
            var pill = I(p, "Round", S("UI_Pill"), Gold, Top, new Vector2(0f, 0f), new Vector2(260f, 52f), Image.Type.Sliced);
            v.round = T(pill.transform, "Text", "ROUND 1 OF 3", 26f, Ink, C, new Vector2(0f, 1f), new Vector2(250f, 44f), TextAlignmentOptions.Center, true, false);
            v.icon = I(p, "Icon", null, Color.white, new Vector2(0f, 1f), new Vector2(110f, -110f), new Vector2(120f, 120f));
            v.icon.preserveAspect = true;
            v.title = T(p, "Title", "Knockback Arena", 96f, Paper, Top, new Vector2(0f, -112f), new Vector2(1000f, 112f));
            v.tagline = T(p, "Tagline", "Tagline", 34f, Muted, Top, new Vector2(0f, -190f), new Vector2(1000f, 44f), TextAlignmentOptions.Center, false, false);
            for (int i = 0; i < v.lines.Length; i++)
            {
                var row = R(p, "HowTo" + i, Top, new Vector2(0f, -282f - 80f * i), new Vector2(900f, 64f));
                var dot = I(row, "Dot", S("UI_Circle"), Chartreuse, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(40f, 40f));
                T(dot.transform, "N", (i + 1).ToString(), 24f, Ink, C, new Vector2(0f, 1f), new Vector2(40f, 36f), TextAlignmentOptions.Center, true, false);
                v.lines[i] = T(row, "Text", "Line", 32f, Paper, new Vector2(0f, 0.5f), new Vector2(480f, 0f), new Vector2(820f, 60f), TextAlignmentOptions.Left, false, false);
                v.lines[i].textWrappingMode = TextWrappingModes.Normal;
            }
            var ctl = I(p, "Controls", S("UI_Pill"), new Color(1f, 1f, 1f, 0.08f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(960f, 54f), Image.Type.Sliced);
            v.controls = T(ctl.transform, "Text", "WASD move", 26f, Muted, C, new Vector2(0f, 1f), new Vector2(940f, 48f), TextAlignmentOptions.Center, false, false);
            I(p, "ProgressTrack", S("UI_Pill"), new Color(1f, 1f, 1f, 0.1f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(960f, 12f), Image.Type.Sliced);
            v.progress = Filled(p, "Progress", S("UI_Pill"), Chartreuse, new Vector2(0f, -290f), new Vector2(960f, 12f), Image.FillMethod.Horizontal);
            return v;
        }

        // ------------------------------------------------------------------ round results
        public static RoundResultsView BuildResults(Transform parent)
        {
            var v = UiBuilder.Screen<RoundResultsView>(parent, "RoundResults");
            v.faces = AllFaces();
            var scrim = I(v.transform, "Scrim", null, new Color(0.03f, 0.04f, 0.11f, 0.35f), C, Vector2.zero, Vector2.zero);
            Fill(scrim.rectTransform);
            var p = Panel(v.transform, "Panel", new Vector2(1f, 0.5f), new Vector2(-420f, 0f), new Vector2(740f, 760f), InkA(0.94f));
            v.title = T(p, "Title", "ROUND 1 RESULTS", 56f, Color.white, Top, new Vector2(0f, -66f), new Vector2(680f, 70f));
            v.subtitle = T(p, "Subtitle", "Knockback Arena", 28f, Muted, Top, new Vector2(0f, -118f), new Vector2(680f, 36f), TextAlignmentOptions.Center, false, false);
            for (int i = 0; i < 4; i++)
            {
                var row = R(p, "Row" + i, C, new Vector2(0f, 150f - 126f * i), new Vector2(660f, 112f));
                I(row, "Bg", S("UI_Panel"), new Color(1f, 1f, 1f, i == 0 ? 0.16f : 0.08f), C, Vector2.zero, new Vector2(660f, 112f), Image.Type.Sliced);
                var r = new RoundResultsView.Row { root = row };
                r.badge = I(row, "Badge", S("UI_Circle"), Color.white, C, new Vector2(-268f, 0f), new Vector2(84f, 84f));
                r.place = T(row, "Place", "1st", 30f, Ink, C, new Vector2(-268f, 0f), new Vector2(84f, 40f), TextAlignmentOptions.Center, true, false);
                r.crown = I(row, "Crown", S("UI_IconCrown"), Gold, C, new Vector2(-296f, 46f), new Vector2(50f, 50f));
                r.crown.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
                r.portrait = I(row, "Portrait", null, Color.white, C, new Vector2(-172f, 6f), new Vector2(108f, 108f));
                r.portrait.preserveAspect = true;
                r.playerName = T(row, "Name", "PLAYER", 36f, Color.white, C, new Vector2(-10f, 16f), new Vector2(210f, 44f), TextAlignmentOptions.Left, false);
                r.playerName.overflowMode = TextOverflowModes.Ellipsis;
                r.plus = T(row, "Plus", "+4", 30f, Chartreuse, C, new Vector2(-50f, -26f), new Vector2(100f, 36f), TextAlignmentOptions.Left);
                r.total = T(row, "Total", "0", 56f, Color.white, C, new Vector2(256f, 0f), new Vector2(120f, 70f));
                v.rows[i] = r;
            }
            T(p, "TotalLabel", "TOTAL", 18f, Muted, C, new Vector2(256f, 222f), new Vector2(120f, 24f), TextAlignmentOptions.Center, false, false);
            return v;
        }

        // ------------------------------------------------------------------ standings
        public static StandingsView BuildStandings(Transform parent)
        {
            var v = UiBuilder.Screen<StandingsView>(parent, "Standings");
            v.faces = AllFaces();
            var scrim = I(v.transform, "Scrim", null, new Color(0.03f, 0.04f, 0.11f, 0.55f), C, Vector2.zero, Vector2.zero);
            Fill(scrim.rectTransform);
            var p = Panel(v.transform, "Panel", C, new Vector2(0f, 30f), new Vector2(1220f, 760f), InkA(0.94f));
            v.title = T(p, "Title", "STANDINGS", 56f, Color.white, Top, new Vector2(0f, -66f), new Vector2(1100f, 70f));
            v.title.richText = true;
            v.barWidth = 640f; v.rowSpacing = 112f; v.firstRowY = 180f;
            for (int i = 0; i < 4; i++)
            {
                var row = R(p, "Row" + i, C, new Vector2(0f, 180f - 112f * i), new Vector2(1120f, 100f));
                var r = new StandingsView.Row { root = row };
                r.rank = T(row, "Rank", "1", 44f, Muted, new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(60f, 60f));
                r.portrait = I(row, "Portrait", null, Color.white, new Vector2(0f, 0.5f), new Vector2(120f, 4f), new Vector2(96f, 96f));
                r.portrait.preserveAspect = true;
                r.crown = I(row, "Crown", S("UI_IconCrown"), Gold, new Vector2(0f, 0.5f), new Vector2(92f, 40f), new Vector2(44f, 44f));
                r.crown.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
                r.playerName = T(row, "Name", "PLAYER", 34f, Color.white, new Vector2(0f, 0.5f), new Vector2(276f, 0f), new Vector2(180f, 44f), TextAlignmentOptions.Left, false);
                r.playerName.overflowMode = TextOverflowModes.Ellipsis;
                I(row, "Track", S("UI_Pill"), new Color(1f, 1f, 1f, 0.07f), new Vector2(0f, 0.5f), new Vector2(380f + 320f, 0f), new Vector2(640f, 40f), Image.Type.Sliced);
                r.bar = R(row, "Bar", new Vector2(0f, 0.5f), new Vector2(380f, 0f), new Vector2(100f, 40f), new Vector2(0f, 0.5f));
                r.barFill = I(r.bar, "Fill", S("UI_Pill"), Tomato, C, Vector2.zero, Vector2.zero, Image.Type.Sliced);
                Fill(r.barFill.rectTransform);
                r.total = T(row, "Total", "0", 48f, Color.white, new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(110f, 60f));
                v.rows[i] = r;
            }
            var next = I(p, "Next", S("UI_Pill"), new Color(1f, 1f, 1f, 0.08f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(760f, 76f), Image.Type.Sliced);
            v.nextLabel = T(next.transform, "Label", "NEXT UP", 22f, Gold, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(160f, 30f), TextAlignmentOptions.Left, true, false);
            v.nextGame = T(next.transform, "Game", "Knockback Arena", 34f, Paper, C, new Vector2(30f, 0f), new Vector2(380f, 44f), TextAlignmentOptions.Left);
            v.nextRound = T(next.transform, "Round", "Round 2 of 3", 24f, Muted, new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(180f, 30f), TextAlignmentOptions.Right, false, false);
            return v;
        }

        // ------------------------------------------------------------------ final
        public static FinalView BuildFinal(Transform parent)
        {
            var v = UiBuilder.Screen<FinalView>(parent, "Final");
            var root = Content(v);
            var p = Panel(root, "Panel", new Vector2(1f, 1f), new Vector2(-360f, -300f), new Vector2(640f, 520f), InkA(0.9f));
            v.championLabel = T(p, "Label", "CHAMPION", 34f, Gold, Top, new Vector2(0f, -54f), new Vector2(580f, 44f));
            v.championName = T(p, "Name", "Mochi", 76f, Paper, Top, new Vector2(0f, -130f), new Vector2(580f, 96f));
            v.championName.enableAutoSizing = true; v.championName.fontSizeMin = 40f; v.championName.fontSizeMax = 76f;
            v.championDetail = T(p, "Detail", "12 points · 2 round wins", 28f, Muted, Top, new Vector2(0f, -196f), new Vector2(580f, 36f), TextAlignmentOptions.Center, false, false);
            for (int i = 0; i < v.places.Length; i++)
            {
                var row = I(p, "Place" + i, S("UI_Pill"), new Color(1f, 1f, 1f, 0.07f), Top, new Vector2(0f, -280f - 70f * i), new Vector2(540f, 58f), Image.Type.Sliced);
                v.places[i] = T(row.transform, "Text", "2.  Tofu  9", 32f, Paper, C, new Vector2(0f, 1f), new Vector2(500f, 50f), TextAlignmentOptions.Left, false, false);
                v.places[i].richText = true;
            }
            v.backLabel = T(root, "Back", "Back to the lobby in 12", 30f, Paper, new Vector2(1f, 0f), new Vector2(-360f, 150f), new Vector2(640f, 40f));
            v.leaveButton = Btn(root, "Leave", "LEAVE", new Vector2(1f, 0f), new Vector2(-360f, 80f), new Vector2(300f, 84f), Card, Paper);
            return v;
        }
    }
}
