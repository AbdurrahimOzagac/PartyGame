using UnityEngine;

namespace ShooterParty
{
    // One-shot particle effects. Textures and gradients are white; colour comes from here, so one prefab
    // serves all four players. Child systems opt in by name: "Tint*" takes the player colour, "Pastel*" a
    // lighter version of it; everything else keeps its own colour (white stars, ice chips).
    public static class Fx
    {
        public static ParticleSystem Play(ParticleSystem prefab, Vector3 pos, Quaternion rot, Color tint)
        {
            if (!prefab) return null;
            var fx = Object.Instantiate(prefab, pos, rot);
            Tint(fx, tint);
            fx.Play(true);
            return fx;
        }

        public static void Tint(ParticleSystem root, Color tint)
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var n = ps.gameObject.name;
                if (!n.StartsWith("Tint") && !n.StartsWith("Pastel")) continue;
                var c = n.StartsWith("Pastel") ? Color.Lerp(tint, Color.white, 0.6f) : tint;
                var main = ps.main;
                c.a = main.startColor.color.a;
                main.startColor = c;
            }
        }

        public static Color ColourOf(Material m) =>
            m && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
    }
}
