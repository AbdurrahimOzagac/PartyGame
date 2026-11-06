using System.Linq;
using ShooterParty.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ShooterParty.EditorTools.UiKit;

namespace ShooterParty.EditorTools
{
    // The lobby, from code:
    //   LobbyView inside UI_Root   party code, seat cards, playlist panel + builder, options, Start / Ready / Leave
    //   Scenes/SP_Lobby.unity      the ice floe with four pedestals and a critter on each (LobbyStage)
    // Layout reference: 1920 x 1080. The pedestals sit in the left 60 % of the frame; the playlist panel owns the right.
    public static class LobbyBuilder
    {
        public const string LobbyScenePath = ShooterPartySetup.LobbyScenePath;
        static readonly Vector2 C = new Vector2(0.5f, 0.5f), Top = new Vector2(0.5f, 1f);
        const float PedestalGap = 1.7f, PedestalHeight = 0.42f;

        public static LobbyView BuildLobbyView(Transform parent, MiniGameCatalog catalog)
        {
            var v = UiBuilder.Screen<LobbyView>(parent, "Lobby");
            var root = R(v.transform, "Content", C, Vector2.zero, Vector2.zero);
            Fill(root);
            BuildCode(v, root);
            v.slotArea = R(root, "Seats", C, Vector2.zero, Vector2.zero);
            Fill(v.slotArea);
            for (int i = 0; i < 4; i++) v.slots[i] = BuildSeat(v.slotArea, i);
            v.builder = BuildPlaylist(root, catalog);

            var replace = R(root, "ReplaceLeavers", new Vector2(1f, 1f), new Vector2(-350f, -872f), new Vector2(620f, 60f));
            v.replaceRow = replace.gameObject;
            I(replace, "Fill", S("UI_Pill"), new Color(0.086f, 0.1f, 0.227f, 0.82f), C, Vector2.zero, new Vector2(620f, 64f), Image.Type.Sliced);
            v.replaceLeavers = UiBuilder.Toggle(replace, "Toggle", "A bot replaces anyone who leaves", Vector2.zero, 600f);
            replace.GetComponentInChildren<TMP_Text>().fontSize = 28f;

            v.statusText = T(root, "Status", "", 28f, Paper, new Vector2(1f, 0f), new Vector2(-350f, 156f), new Vector2(620f, 40f));
            v.startButton = Btn(root, "Start", "START", new Vector2(1f, 0f), new Vector2(-350f, 84f), new Vector2(620f, 100f), Chartreuse, Ink);
            v.readyButton = Btn(root, "Ready", "READY!", new Vector2(1f, 0f), new Vector2(-350f, 84f), new Vector2(620f, 100f), Teal, Paper);
            v.readyLabel = v.readyButton.GetComponentInChildren<TMP_Text>();
            v.leaveButton = Btn(root, "Leave", "LEAVE", new Vector2(0f, 0f), new Vector2(160f, 76f), new Vector2(240f, 80f), Card, Paper);

            for (int c = 0; c < 4; c++)
            {
                var faces = Faces(CritterLook.Names[c]);   // Neutral, Happy, Hurt, Sad
                v.neutralFaces[c] = faces[0];
                v.happyFaces[c] = faces[1];
            }
            return v;
        }

        static void BuildCode(LobbyView v, Transform root)
        {
            var p = Panel(root, "CodePanel", new Vector2(0f, 1f), new Vector2(340f, -150f), new Vector2(600f, 220f), Card);
            v.codeLabel = T(p, "Label", "PARTY CODE", 24f, Muted, Top, new Vector2(0f, -40f), new Vector2(540f, 30f), TextAlignmentOptions.Left, false, false);
            v.codeText = T(p, "Code", "BKFZT", 84f, Paper, Top, new Vector2(-70f, -108f), new Vector2(400f, 96f), TextAlignmentOptions.Left);
            v.codeText.characterSpacing = 12f;
            v.copyButton = Btn(p, "Copy", "COPY", new Vector2(1f, 1f), new Vector2(-100f, -108f), new Vector2(150f, 66f), Glass, Paper);
            v.codeHint = T(p, "Hint", "", 24f, Muted, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(540f, 30f), TextAlignmentOptions.Left, false, false);
        }

        static LobbyView.SlotCard BuildSeat(Transform area, int i)
        {
            var size = new Vector2(280f, 132f);
            var card = new LobbyView.SlotCard { root = R(area, "Seat_" + i, C, new Vector2(-600f + 300f * i, -260f), size) };
            var empty = R(card.root, "Empty", C, Vector2.zero, size);
            I(empty, "Fill", S("UI_Panel"), new Color(0.086f, 0.1f, 0.227f, 0.55f), C, Vector2.zero, size, Image.Type.Sliced);
            I(empty, "Stroke", S("UI_PanelOutline"), new Color(1f, 1f, 1f, 0.25f), C, Vector2.zero, size, Image.Type.Sliced);
            T(empty, "Text", "Open seat", 28f, Muted, C, Vector2.zero, new Vector2(240f, 40f), TextAlignmentOptions.Center, false, false);
            card.emptyLabel = empty.gameObject;

            var body = R(card.root, "Body", C, Vector2.zero, size);
            card.body = body.gameObject;
            Panel(body, "Card", C, Vector2.zero, size, Card);
            card.colourBar = I(body, "Colour", S("UI_Pill"), Tomato, Top, new Vector2(0f, -10f), new Vector2(size.x - 40f, 8f), Image.Type.Sliced);
            card.portrait = I(body, "Face", null, Color.white, new Vector2(0f, 0.5f), new Vector2(56f, 8f), new Vector2(76f, 76f));
            card.portrait.preserveAspect = true;
            var ready = I(body, "Ready", S("UI_Circle"), Chartreuse, new Vector2(0f, 0.5f), new Vector2(86f, -22f), new Vector2(34f, 34f));
            I(ready.transform, "Check", S("UI_IconCheck"), Ink, C, Vector2.zero, new Vector2(24f, 24f));
            card.readyMark = ready.gameObject;
            card.nameLabel = T(body, "Name", "Player", 30f, Paper, new Vector2(0f, 1f), new Vector2(186f, -46f), new Vector2(164f, 38f), TextAlignmentOptions.Left, true, false);
            card.nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            card.nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            card.statusLabel = T(body, "Status", "not ready", 22f, Muted, new Vector2(0f, 1f), new Vector2(186f, -78f), new Vector2(164f, 28f), TextAlignmentOptions.Left, false, false);
            card.pingDot = I(body, "Ping", S("UI_Circle"), LobbyView.Good, new Vector2(0f, 0f), new Vector2(112f, 26f), new Vector2(14f, 14f));
            card.pingLabel = T(body, "PingMs", "48 ms", 20f, Muted, new Vector2(0f, 0f), new Vector2(164f, 26f), new Vector2(90f, 24f), TextAlignmentOptions.Left, false, false);
            card.colour = Btn(body, "Colour", "COLOUR", new Vector2(1f, 0f), new Vector2(-58f, 26f), new Vector2(96f, 32f), Glass, Paper);
            card.kick = Btn(body, "Kick", "×", new Vector2(1f, 1f), new Vector2(-24f, -26f), new Vector2(36f, 36f), new Color(1f, 0.31f, 0.18f, 0.85f), Paper);
            card.kick.GetComponentInChildren<TMP_Text>().fontSize = 30f;
            card.leaderTag = Tag(body, "Leader", "LEADER", Gold, Ink, new Vector2(0f, 2f), 112f);
            card.botTag = Tag(body, "Bot", "BOT", Muted, Ink, new Vector2(0f, 2f), 76f);
            card.youTag = Tag(body, "You", "YOU", Teal, Paper, new Vector2(-96f, 2f), 66f);
            return card;
        }

        // a small pill sitting on a card's top edge
        static GameObject Tag(Transform parent, string name, string text, Color fill, Color ink, Vector2 pos, float width)
        {
            var t = I(parent, name, S("UI_Pill"), fill, Top, pos, new Vector2(width, 30f), Image.Type.Sliced);
            T(t.transform, "Text", text, 19f, ink, C, new Vector2(0f, 1f), new Vector2(width, 26f), TextAlignmentOptions.Center, true, false);
            return t.gameObject;
        }

        static PlaylistBuilder BuildPlaylist(Transform root, MiniGameCatalog catalog)
        {
            var p = Panel(root, "Playlist", new Vector2(1f, 1f), new Vector2(-350f, -440f), new Vector2(620f, 800f), Card);
            var b = p.gameObject.AddComponent<PlaylistBuilder>();
            T(p, "Label", "PLAYLIST", 24f, Muted, Top, new Vector2(0f, -40f), new Vector2(560f, 30f), TextAlignmentOptions.Left, false, false);
            b.title = T(p, "Title", "Quick", 46f, Paper, Top, new Vector2(0f, -86f), new Vector2(560f, 58f), TextAlignmentOptions.Left);
            b.nameField = UiBuilder.Input(p, "Name", new Vector2(0f, -88f), new Vector2(560f, 64f), "Name your playlist", 34f);
            b.countLabel = T(p, "Count", "3 games", 24f, Muted, Top, new Vector2(0f, -136f), new Vector2(560f, 30f), TextAlignmentOptions.Left, false, false);

            var presets = R(p, "Presets", Top, new Vector2(0f, -190f), new Vector2(560f, 56f));
            b.presetBar = presets.gameObject;
            var names = catalog.presets.Select(x => x.name.ToUpperInvariant()).Append("CUSTOM").ToArray();
            float w = (560f - 12f * (names.Length - 1)) / names.Length;
            var buttons = names.Select((n, i) => Btn(presets, n, n, new Vector2(0f, 0.5f), new Vector2(w * 0.5f + i * (w + 12f), 0f), new Vector2(w, 54f), Glass, Paper)).ToArray();
            b.presetButtons = buttons.Take(catalog.presets.Count).ToArray();
            b.customButton = buttons.Last();
            b.customButton.targetGraphic.color = Violet;

            for (int i = 0; i < b.rows.Length; i++) b.rows[i] = Row(p, i);

            var edit = R(p, "Edit", Top, new Vector2(0f, -710f), new Vector2(560f, 130f));
            b.editBar = edit.gameObject;
            b.addIds = catalog.games.Select(g => g.id).ToArray();
            float aw = Mathf.Min(380f, (390f - 10f * (b.addIds.Length - 1)) / Mathf.Max(1, b.addIds.Length));
            b.addButtons = catalog.games.Select((g, i) => Btn(edit, "Add_" + g.id, "+ " + g.displayName.ToUpperInvariant(), new Vector2(0f, 1f),
                new Vector2(aw * 0.5f + i * (aw + 10f), -28f), new Vector2(aw, 54f), Glass, Paper)).ToArray();
            b.shuffleButton = Btn(edit, "Shuffle", "SHUFFLE", new Vector2(1f, 1f), new Vector2(-80f, -28f), new Vector2(160f, 54f), Glass, Paper);
            b.cancelButton = Btn(edit, "Cancel", "CANCEL", new Vector2(0f, 0f), new Vector2(100f, 32f), new Vector2(200f, 64f), Glass, Paper);
            b.applyButton = Btn(edit, "Apply", "USE PLAYLIST", new Vector2(1f, 0f), new Vector2(-170f, 32f), new Vector2(340f, 64f), Chartreuse, Ink);
            b.errorLabel = T(p, "Error", "", 24f, Pink, Top, new Vector2(0f, -136f), new Vector2(560f, 30f), TextAlignmentOptions.Left, false, false);   // takes the count's line
            return b;
        }

        static PlaylistBuilder.Row Row(Transform panel, int i)
        {
            var rt = R(panel, "Row_" + i, Top, new Vector2(0f, -244f - 40f * i), new Vector2(560f, 36f));
            I(rt, "Fill", S("UI_Pill"), new Color(1f, 1f, 1f, 0.06f), C, Vector2.zero, new Vector2(560f, 36f), Image.Type.Sliced);
            var r = new PlaylistBuilder.Row
            {
                root = rt.gameObject,
                index = T(rt, "Index", "1", 22f, Muted, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(36f, 30f), TextAlignmentOptions.Center, false, false),
                label = T(rt, "Game", "Knockback Arena", 26f, Paper, new Vector2(0f, 0.5f), new Vector2(206f, 0f), new Vector2(320f, 34f), TextAlignmentOptions.Left, true, false),
                up = IconBtn(rt, "Up", new Vector2(-122f, 0f), 0f),
                down = IconBtn(rt, "Down", new Vector2(-78f, 0f), 180f),
                remove = Btn(rt, "Remove", "×", new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(36f, 32f), new Color(1f, 0.31f, 0.18f, 0.85f), Paper),
            };
            r.remove.GetComponentInChildren<TMP_Text>().fontSize = 28f;
            return r;
        }

        static Button IconBtn(Transform parent, string name, Vector2 pos, float angle)
        {
            var b = Btn(parent, name, "", new Vector2(1f, 0.5f), pos, new Vector2(36f, 32f), Glass, Paper);
            var icon = I(b.transform, "Icon", S("UI_IconArrow"), Paper, C, Vector2.zero, new Vector2(20f, 20f));
            icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            return b;
        }

        // ------------------------------------------------------------------ SP_Lobby
        public static void BuildLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = UiBuilder.BuildIceBackdrop();
            // the pedestals' centre lands left of the frame's centre, leaving the right side to the playlist panel
            var focus = new Vector3(1.88f, 1.1f, 0f);
            cam.transform.position = focus + new Vector3(0f, 1.9f, -8.5f);
            cam.transform.rotation = Quaternion.LookRotation(focus - cam.transform.position);
            cam.fieldOfView = 40f;

            var stage = new GameObject("LobbyStage").AddComponent<LobbyStage>();
            var critter = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/Prefabs/CubeCritter.prefab");
            var ice = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/Materials/M_Ice.mat");
            for (int i = 0; i < 4; i++)
            {
                var x = (i - 1.5f) * PedestalGap;
                var pedestal = new GameObject("Pedestal_" + i).transform;
                pedestal.SetParent(stage.transform, false);
                pedestal.localPosition = new Vector3(x, 0f, 0f);
                // a short ice drum: the kit's floes are flat and sink into the arena top at lobby scale
                var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                drum.name = "Drum";
                Object.DestroyImmediate(drum.GetComponent<Collider>());
                drum.transform.SetParent(pedestal, false);
                drum.transform.localPosition = new Vector3(0f, PedestalHeight * 0.5f, 0f);
                drum.transform.localScale = new Vector3(1.3f, PedestalHeight * 0.5f, 1.3f);
                if (ice) drum.GetComponent<MeshRenderer>().sharedMaterial = ice;
                float top = PedestalHeight;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(critter);
                g.name = "LobbyCritter_" + i;
                var pos = new Vector3(x, top, 0f);
                var toCam = cam.transform.position - pos; toCam.y = 0f;
                g.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(toCam));
                g.transform.localScale = Vector3.one * 1.1f;
                g.GetComponent<Rigidbody>().isKinematic = true;
                stage.pedestals[i] = pedestal;
                stage.critters[i] = g.GetComponentInChildren<CritterAnimator>();
            }
            BuildPodium(stage, cam.transform.position);
            UiBuilder.SaveBackdropScene(scene, LobbyScenePath, "LS_SP_Lobby");
        }

        // The Final's podium, where the pedestals stand (LobbyStage swaps them): critters by total, crown, confetti.
        static void BuildPodium(LobbyStage stage, Vector3 camera)
        {
            stage.faceTowards = camera;
            stage.crownPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/Prefabs/Props/Crown.prefab")?.GetComponent<LeaderCrown>();
            stage.confettiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/VFX/VFX_Confetti.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/Prefabs/Arena/Podium_Ice.prefab");
            if (!prefab) { Debug.LogWarning("[Lobby] no Podium_Ice prefab; the final has no podium"); return; }
            var podium = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage.transform);
            podium.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            stage.podium = podium;
            var tops = PodiumStands(podium.GetComponentInChildren<MeshFilter>());
            stage.stands = new Vector3[4];
            for (int i = 0; i < 4; i++)
                stage.stands[i] = i < tops.Length ? tops[i]
                    : podium.transform.position + new Vector3(2.4f + 0.9f * (i - tops.Length), 0f, -1.2f);   // off the podium, on the ice
        }

        // Top centre of each podium column, tallest (1st) first, read from the mesh so the Blender -> Unity axis
        // mapping never matters. World positions.
        public static Vector3[] PodiumStands(MeshFilter mf)
        {
            // the flat column tops are the only exactly-upward faces; group them by height level
            var mesh = mf.sharedMesh;
            var v = mesh.vertices; var n = mesh.normals;
            var tops = Enumerable.Range(0, v.Length)
                .Where(i => mf.transform.TransformDirection(n[i]).y > 0.9995f)
                .Select(i => mf.transform.TransformPoint(v[i])).ToArray();
            var cols = tops.GroupBy(p => Mathf.Round(p.y * 20f))
                .Select(g => new Vector3(g.Average(p => p.x), g.Max(p => p.y), g.Average(p => p.z)))
                .OrderByDescending(p => p.y).Take(4).ToArray();
            Debug.Log("[Lobby] podium stands: " + string.Join(", ", cols.Select(c => c.ToString("F2"))));
            return cols;
        }
    }
}
