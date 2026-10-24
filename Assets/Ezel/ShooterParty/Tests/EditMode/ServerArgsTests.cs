using System.Collections;
using NUnit.Framework;

namespace ShooterParty.Tests
{
    public class ServerArgsTests
    {
        const string Secret = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
        static readonly string[] Full = { "Game", "-batchmode", "-party-server", "-slot", "2", "-port", "7702", "-wsPath", "/g/2", "-room", "BCDFG", "-minPlayers", "3", "-replaceLeavers", "1" };
        static IDictionary Env(string secret = Secret) => new Hashtable { { "PARTY_TICKET_SECRET", secret } };

        [Test]
        public void FullArgvParses()
        {
            Assert.IsTrue(ServerArgs.TryParse(Full, Env(), out var a, out var error), error);
            Assert.AreEqual(2, a.slot); Assert.AreEqual(7702, a.port); Assert.AreEqual("/g/2", a.wsPath);
            Assert.AreEqual("BCDFG", a.room); Assert.AreEqual(3, a.minPlayers); Assert.IsTrue(a.replaceLeavers);
            Assert.AreEqual(32, a.secret.Length); Assert.AreEqual(1, a.secret[1]);
        }

        [Test]
        public void NotAServerInvocation()
        {
            Assert.IsFalse(ServerArgs.TryParse(new[] { "Game", "-batchmode" }, Env(), out _, out var error));
            Assert.IsNull(error);
        }

        [Test]
        public void DefaultsPathAndPlayers()
        {
            Assert.IsTrue(ServerArgs.TryParse(new[] { "-party-server", "-slot", "1", "-port", "7701", "-room", "BCDFG" }, Env(), out var a, out var error), error);
            Assert.AreEqual("/g/1", a.wsPath); Assert.AreEqual(3, a.minPlayers); Assert.IsFalse(a.replaceLeavers);
        }

        [Test]
        public void MissingSecretFails()
        {
            Assert.IsFalse(ServerArgs.TryParse(Full, new Hashtable(), out _, out var error));
            Assert.AreEqual("PARTY_TICKET_SECRET missing", error);
        }

        [Test]
        public void ShortSecretFails()
        {
            Assert.IsFalse(ServerArgs.TryParse(Full, Env("abcd"), out _, out var error));
            StringAssert.Contains("64 hex", error);
        }

        [Test]
        public void BadPortFails()
        {
            var argv = (string[])Full.Clone(); argv[6] = "99999";
            Assert.IsFalse(ServerArgs.TryParse(argv, Env(), out _, out var error));
            StringAssert.Contains("-port", error);
        }

        [Test]
        public void BadRoomFails()
        {
            var argv = (string[])Full.Clone(); argv[10] = "AAAAA";
            Assert.IsFalse(ServerArgs.TryParse(argv, Env(), out _, out var error));
            StringAssert.Contains("-room", error);
        }
    }
}
