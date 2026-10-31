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
    // import settings, and the prefabs MassRing and HUD_KnockbackWidget (the mini-game's HUD widget).
    // Needs TMP Essential Resources in Assets/TextMesh Pro (Window > TextMeshPro > Import TMP Essential Resources).
    public static class UiSetup
    {
        const string Root = CubeCritterSetup.Root;
        const string UiDir = Root + "/UI";

        public static void BuildAll()
        {
            UiKit.Init();
            BuildMassRing();
            BuildHudWidget();
        }

        // ------------------------------------------------------------------ HUD widget
        // Knockback Arena's piece of the shared HUD (MiniGameDefinition.hudWidget): mass on every card, a mass ring
        // over every critter. The HUD itself is core (ShooterParty HudBuilder).
        static void BuildHudWidget()
        {
            var go = new GameObject("HUD_KnockbackWidget", typeof(RectTransform));
            var w = go.AddComponent<KnockbackHudWidget>();
            w.ringPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{UiDir}/MassRing.prefab").GetComponent<MassRing>();
            Save(go, "HUD_KnockbackWidget");
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
