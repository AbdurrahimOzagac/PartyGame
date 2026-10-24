using System;
using System.Collections.Generic;

namespace ShooterParty
{
    // A playlist is a name plus 1-10 mini-game ids (repeats allowed), built in the lobby for one session.
    public static class PlaylistRules
    {
        public const int MaxNameLength = 24, MaxGames = 10;

        // Only plain ids are echoed: refusals are logged, and a client string with a newline could forge an @@ line.
        static bool SafeId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 32) return false;
            foreach (char c in id) if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-')) return false;
            return true;
        }

        public static bool TryValidate(string name, IReadOnlyList<string> ids, Func<string, bool> isKnownId, out string error)
        {
            if (!NameRules.TryClean(name, 1, MaxNameLength, "Playlist name", out _, out error))
            {
                if (string.IsNullOrWhiteSpace(name)) error = "Give the playlist a name";
                return false;
            }
            if (ids == null || ids.Count == 0) { error = "Add at least one mini-game"; return false; }
            if (ids.Count > MaxGames) { error = $"A playlist can have at most {MaxGames} games"; return false; }
            foreach (var id in ids)
                if (!isKnownId(id)) { error = SafeId(id) ? $"Unknown mini-game '{id}'" : "Unknown mini-game"; return false; }
            error = null;
            return true;
        }
    }
}
