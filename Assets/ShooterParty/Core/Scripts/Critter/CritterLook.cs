using System.Collections.Generic;
using UnityEngine;

namespace ShooterParty
{
    // Paints one critter prefab in any of the four player colours (body, emblem, spit goo), so the networked game
    // needs a single critter prefab. Index order matches CubeCritterSetup.Players.
    public class CritterLook : MonoBehaviour
    {
        public static readonly string[] Names = { "Tomato", "Teal", "Chartreuse", "Violet" };
        public static readonly Color[] Colours =
        {
            new Color32(0xFF, 0x4F, 0x2E, 0xFF), new Color32(0x00, 0xB3, 0xA4, 0xFF),
            new Color32(0xD4, 0xF2, 0x1C, 0xFF), new Color32(0x7B, 0x2F, 0xD0, 0xFF),
        };

        public Material[] body = new Material[4], emblem = new Material[4], goo = new Material[4];

        readonly List<(Renderer r, int index, bool emblem)> slots = new List<(Renderer, int, bool)>();
        public int Current { get; private set; } = -1;

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    var n = ms[i] ? ms[i].name : "";
                    if (n.StartsWith("M_CC_Body")) slots.Add((r, i, false));
                    else if (n.StartsWith("M_CC_Emblem")) slots.Add((r, i, true));
                }
            }
        }

        public void Apply(int index)
        {
            index = Mathf.Clamp(index, 0, Names.Length - 1);
            if (index == Current) return;
            Current = index;
            foreach (var (r, i, isEmblem) in slots)
            {
                var ms = r.sharedMaterials;
                var m = isEmblem ? emblem[index] : body[index];
                if (m) { ms[i] = m; r.sharedMaterials = ms; }
            }
            var spit = GetComponent<CritterSpit>();
            if (spit && goo[index]) spit.blobMaterial = goo[index];
        }
    }
}
