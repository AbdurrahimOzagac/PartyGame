namespace ShooterParty
{
    public static class PartyProtocol
    {
        // Bump when client and server stop understanding each other; the room service checks it too
        // (header X-ShooterParty-Version) and old clients are told "Update your game".
        public const int Version = 1;
        public const int LocalPort = 7777;
        public const string RoomServiceUrl = "https://partyshooter.ezellm.com/api/v1";

        // This build's identity on the wire: the protocol version plus every network prefab's id. Rebuilt prefabs get new
        // ids, so clients and the Pi server must come from the same build; the join gate compares these.
        public static string Fingerprint(System.Collections.Generic.IEnumerable<uint> prefabHashes)
        {
            uint h = 2166136261;   // FNV-1a
            void Mix(uint x) { for (int i = 0; i < 4; i++) { h ^= (x >> (8 * i)) & 0xFF; h *= 16777619; } }
            Mix((uint)Version);
            var sorted = new System.Collections.Generic.List<uint>(prefabHashes);
            sorted.Sort();
            foreach (var x in sorted) Mix(x);
            return h.ToString("x8");
        }
    }

    public enum PartyMode { Offline, Practice, LocalHost, LocalClient, OnlineClient, DedicatedServer }
}
