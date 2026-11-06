using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ShooterParty;
using ShooterParty.UI;

namespace KnockbackArena.UI
{
    // World-space mass ring above a critter (U3): fill = its NetworkCritter.Mass, in the player's colour, on an ink keyline with a
    // white rim (Chartreuse on ice and Violet on sky are near-invisible without it). Faces the camera (copying
    // its rotation, which does not skew at screen edges) and grows a little with distance so it stays legible.
    public class MassRing : MonoBehaviour
    {
        public Image fill;
        public TMP_Text percent;
        public float height = 1.62f;

        NetworkCritter critter;
        Canvas canvas;
        Camera cam;
        float shown = 1f;

        public void Bind(NetworkCritter c, Color colour)
        {
            critter = c;
            fill.color = colour;
            transform.SetParent(c.transform, false);   // goes away with the critter
            transform.localPosition = Vector3.up * height;
            shown = c.MassFraction;
        }

        void LateUpdate()
        {
            if (!critter) return;
            if (!canvas) canvas = GetComponent<Canvas>();
            canvas.enabled = !critter.Eliminated.Value;   // a world canvas is not a Renderer, so elimination does not hide it
            if (!canvas.enabled) return;
            if (!cam) cam = Camera.main;
            shown = Mathf.MoveTowards(shown, critter.MassFraction, Time.deltaTime / 0.25f);
            fill.fillAmount = shown;
            percent.text = Mathf.RoundToInt(shown * 100f).ToString();
            if (!cam) return;
            transform.rotation = cam.transform.rotation;
            float d = Vector3.Distance(cam.transform.position, transform.position);
            transform.localScale = Vector3.one * Mathf.Clamp(d / 8f, 1f, 2.5f) * 0.004f;
        }
    }
}
