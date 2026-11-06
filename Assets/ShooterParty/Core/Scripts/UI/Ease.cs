using UnityEngine;

namespace ShooterParty.UI
{
    // The handful of easing curves the UI brief calls for (easings.net definitions), t in 0..1.
    public static class Ease
    {
        public static float OutBack(float t, float s = 1.70158f) { t = Mathf.Clamp01(t) - 1f; return 1f + (s + 1f) * t * t * t + s * t * t; }
        public static float InQuad(float t) { t = Mathf.Clamp01(t); return t * t; }
        public static float OutQuad(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        public static float OutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
    }
}
