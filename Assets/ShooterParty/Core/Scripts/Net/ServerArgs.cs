using System;
using System.Collections;
using System.Linq;

namespace ShooterParty
{
    // Command line of one game-server process, started by the room service:
    //   Game -batchmode -nographics -party-server -slot 1 -port 7701 -wsPath /g/1 -room BCDFG -minPlayers 3 -replaceLeavers 1
    // The ticket secret comes from the environment (PARTY_TICKET_SECRET, 64 hex chars), never the argv (ps shows argv).
    public class ServerArgs
    {
        public const string Alphabet = "BCDFGHJKLMNPQRSTVWXZ";
        public int slot, port, minPlayers = 3;
        public string wsPath, room;
        public bool replaceLeavers;
        public byte[] secret;

        // false + error == null: not a server invocation. false + error: a broken server invocation.
        public static bool TryParse(string[] argv, IDictionary env, out ServerArgs a, out string error)
        {
            a = null;
            error = null;
            if (argv == null || !argv.Contains("-party-server")) return false;
            string Get(string k) { int i = Array.IndexOf(argv, k); return i >= 0 && i + 1 < argv.Length ? argv[i + 1] : null; }
            var r = new ServerArgs();
            if (!int.TryParse(Get("-slot"), out r.slot) || r.slot < 1 || r.slot > 9) { error = "bad -slot"; return false; }
            if (!int.TryParse(Get("-port"), out r.port) || r.port < 1024 || r.port > 65535) { error = "bad -port"; return false; }
            r.wsPath = Get("-wsPath") ?? "/g/" + r.slot;
            if (!r.wsPath.StartsWith("/")) { error = "bad -wsPath"; return false; }
            r.room = Get("-room");
            if (r.room == null || r.room.Length != 5 || r.room.Any(c => Alphabet.IndexOf(c) < 0)) { error = "bad -room"; return false; }
            var mp = Get("-minPlayers");
            if (mp != null && (!int.TryParse(mp, out r.minPlayers) || r.minPlayers < 1 || r.minPlayers > 4)) { error = "bad -minPlayers"; return false; }
            r.replaceLeavers = Get("-replaceLeavers") == "1";
            var hex = env?["PARTY_TICKET_SECRET"] as string;
            if (string.IsNullOrEmpty(hex)) { error = "PARTY_TICKET_SECRET missing"; return false; }
            if (hex.Length != 64 || !hex.All(Uri.IsHexDigit)) { error = "PARTY_TICKET_SECRET must be 64 hex chars"; return false; }
            r.secret = new byte[32];
            for (int i = 0; i < 32; i++) r.secret[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            a = r;
            return true;
        }
    }
}
