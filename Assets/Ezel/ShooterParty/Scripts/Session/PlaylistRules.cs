using System;
using System.Collections.Generic;

namespace ShooterParty
{
    // A playlist is a name plus 1-10 mini-game ids (repeats allowed), built in the lobby for one session.
    public static class PlaylistRules
    {
        public const int MaxNameLength = 24, MaxGames = 10;

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
                if (!isKnownId(id)) { error = $"Unknown mini-game '{id}'"; return false; }
            error = null;
            return true;
        }
    }
}
