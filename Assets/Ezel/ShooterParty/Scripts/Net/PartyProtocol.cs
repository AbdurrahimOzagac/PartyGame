namespace ShooterParty
{
    public static class PartyProtocol
    {
        // Bump when client and server stop understanding each other; the room service checks it too
        // (header X-ShooterParty-Version) and old clients are told "Update your game".
        public const int Version = 1;
        public const int LocalPort = 7777;
        public const string RoomServiceUrl = "https://partyshooter.ezellm.com/api/v1";
    }

    public enum PartyMode { Offline, Practice, LocalHost, LocalClient, OnlineClient, DedicatedServer }
}
