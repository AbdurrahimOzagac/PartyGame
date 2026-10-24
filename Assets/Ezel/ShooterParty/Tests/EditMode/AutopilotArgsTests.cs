using NUnit.Framework;

namespace ShooterParty.Tests
{
    public class AutopilotArgsTests
    {
        [Test]
        public void ParsesOnlineJoin()
        {
            Assert.IsTrue(AutopilotArgs.TryParse(new[] { "Game", "-autopilot", "-join", "ws://127.0.0.1:7701/g/1", "-ticket", "abc.def", "-name", "Ann", "-dropAt", "25" }, out var a));
            Assert.AreEqual("ws://127.0.0.1:7701/g/1", a.join); Assert.AreEqual("abc.def", a.ticket); Assert.AreEqual("Ann", a.name);
            Assert.AreEqual(25f, a.dropAt); Assert.IsNull(a.local);
            CollectionAssert.AreEqual(new[] { "knockback_arena", "knockback_arena" }, a.playlist);
        }

        [Test]
        public void ParsesLocalJoinPlaylistAndLeave()
        {
            Assert.IsTrue(AutopilotArgs.TryParse(new[] { "-autopilot", "-local", "192.168.1.5", "-name", "Bea", "-playlist", "a,b,c", "-leaveAt", "Loading" }, out var a));
            Assert.AreEqual("192.168.1.5", a.local); CollectionAssert.AreEqual(new[] { "a", "b", "c" }, a.playlist);
            Assert.AreEqual(SessionPhase.Loading, a.leaveAt); Assert.Less(a.dropAt, 0f);
        }

        [Test]
        public void NotAutopilot() => Assert.IsFalse(AutopilotArgs.TryParse(new[] { "Game", "-batchmode" }, out _));

        [Test]
        public void NeedsAWayIn() => Assert.IsFalse(AutopilotArgs.TryParse(new[] { "-autopilot", "-name", "x" }, out _));
    }
}
