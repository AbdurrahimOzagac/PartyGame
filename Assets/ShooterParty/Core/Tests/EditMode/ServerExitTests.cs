using NUnit.Framework;

namespace ShooterParty.Tests
{
    // When a room's game server quits (freeing its slot). A finished session that everyone left on purpose ends at
    // once; a mass drop never does, because the players are rejoining.
    public class ServerExitTests
    {
        static string Why(bool sessionOver, int humans, int connected, double sinceDrop, float emptyFor = 0f, float idle = 0f, float up = 100f) =>
            ServerRuntime.ExitReason(sessionOver, humans, connected, sinceDrop, emptyFor, idle, up);

        [Test] public void SessionOverAndEveryoneLeftOnPurposeExitsAtOnce() => Assert.AreEqual("session over, everyone left", Why(true, 0, 0, 1e9));
        [Test] public void SessionOverButEveryoneJustDroppedWaits() => Assert.IsNull(Why(true, 0, 0, 5));
        [Test] public void MidSessionMassDropWaitsForTheRejoins() => Assert.IsNull(Why(false, 3, 0, 5, emptyFor: 30f));
        [Test] public void EmptyForAMinuteExits() => Assert.AreEqual("empty", Why(false, 3, 0, 61, emptyFor: 61f));
        [Test] public void PlayersConnectedNeverExitsEarly() => Assert.IsNull(Why(true, 3, 3, 1e9));
        [Test] public void LongIdleLobbyExits() => Assert.AreEqual("idle in lobby", Why(false, 1, 1, 1e9, idle: 31f * 60f));
        [Test] public void HardCapExits() => Assert.AreEqual("time limit", Why(false, 3, 3, 1e9, up: 2f * 3600f + 1f));
    }
}
