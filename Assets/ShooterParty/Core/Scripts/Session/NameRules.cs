using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShooterParty
{
    // Player and playlist names. The same rules run in the menu (instant feedback), on the game server (trust
    // boundary) and in the room service (projects/shooterparty/server/rooms). '<' and '>' never pass, so a name
    // can't inject TextMeshPro rich text into everyone's HUD.
    public static class NameRules
    {
        public const int MinLength = 2, MaxLength = 16;
        public const string CharsetError = "Use letters, numbers, spaces, _ and - only";

        public static bool TryClean(string raw, out string clean, out string error) =>
            TryClean(raw, MinLength, MaxLength, "Name", out clean, out error);

        internal static bool TryClean(string raw, int min, int max, string what, out string clean, out string error)
        {
            clean = null;
            var s = string.Join(" ", (raw ?? "").Normalize(NormalizationForm.FormC)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            if (s.Length < min) { error = $"{what} needs at least {min} character{(min == 1 ? "" : "s")}"; return false; }
            if (s.Length > max) { error = $"{what} can be at most {max} characters"; return false; }
            if (!s.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-')) { error = CharsetError; return false; }
            clean = s;
            error = null;
            return true;
        }

        // "Mochi" taken -> "Mochi 2"; the base is shortened so the result stays within MaxLength.
        public static string Unique(string clean, IEnumerable<string> taken)
        {
            var set = new HashSet<string>(taken ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!set.Contains(clean)) return clean;
            for (int n = 2; ; n++)
            {
                var suffix = " " + n;
                var candidate = clean.Substring(0, Math.Min(clean.Length, MaxLength - suffix.Length)).TrimEnd() + suffix;
                if (!set.Contains(candidate)) return candidate;
            }
        }
    }
}
