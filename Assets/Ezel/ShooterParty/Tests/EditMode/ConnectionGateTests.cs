using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace ShooterParty.Tests
{
    public class ConnectionGateTests
    {
        const string SecretHex = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
        const long Now = 1700000000;
        ServerArgs server;
        HashSet<string> used;

        [SetUp]
        public void SetUp()
        {
            Assert.IsTrue(ServerArgs.TryParse(new[] { "-party-server", "-slot", "1", "-port", "7701", "-room", "BCDFG" },
                new Hashtable { { "PARTY_TICKET_SECRET", SecretHex } }, out server, out var e), e);
            used = new HashSet<string>();
        }

        string MintTicket(string room = "BCDFG", long exp = Now + 60, string name = "Mochi", bool leader = true) =>
            Ticket.Sign(new TicketData { v = 1, room = room, pid = "a1b2c3d4e5f60718", name = name, leader = leader, exp = exp }, server.secret);

        static byte[] Payload(string ticket = null, string name = "Zesty", int v = PartyProtocol.Version) =>
            Encoding.UTF8.GetBytes(JsonUtility.ToJson(new JoinPayload { v = v, name = name, ticket = ticket }));

        bool Online(byte[] payload, out ApprovedPlayer p, out string reason) =>
            ConnectionGate.Approve(payload, server, k => !used.Add(k), Now, out p, out reason);

        static bool Local(byte[] payload, out ApprovedPlayer p, out string reason) =>
            ConnectionGate.Approve(payload, null, _ => false, Now, out p, out reason);

        [Test]
        public void ValidOnlineTicketApproved()
        {
            Assert.IsTrue(Online(Payload(MintTicket()), out var p, out var reason), reason);
            Assert.AreEqual("a1b2c3d4e5f60718", p.pid); Assert.AreEqual("Mochi", p.name); Assert.IsTrue(p.leader);
        }

        [Test] public void WrongRoomRejected() { Assert.IsFalse(Online(Payload(MintTicket(room: "ZZZZZ")), out _, out var r)); Assert.AreEqual(ConnectionGate.WrongRoom, r); }
        [Test] public void ExpiredRejected() { Assert.IsFalse(Online(Payload(MintTicket(exp: Now - 1)), out _, out var r)); Assert.AreEqual(ConnectionGate.Expired, r); }
        [Test] public void MissingTicketRejected() { Assert.IsFalse(Online(Payload(null), out _, out var r)); Assert.AreEqual(ConnectionGate.BadTicket, r); }
        [Test] public void ForgedTicketRejected() { Assert.IsFalse(Online(Payload(MintTicket() + "x"), out _, out var r)); Assert.AreEqual(ConnectionGate.BadTicket, r); }
        [Test] public void BadNameInsideTicketRejected() { Assert.IsFalse(Online(Payload(MintTicket(name: "<b>x</b>")), out _, out var r)); Assert.AreEqual(ConnectionGate.BadTicket, r); }

        [Test]
        public void ReplayRejected()
        {
            var t = MintTicket();
            Assert.IsTrue(Online(Payload(t), out _, out _));
            Assert.IsFalse(Online(Payload(t), out _, out var r));
            Assert.AreEqual(ConnectionGate.Replayed, r);
        }

        [Test]
        public void OversizePayloadRejected()
        {
            Assert.IsFalse(Online(Payload(MintTicket(), name: new string('a', 1100)), out _, out var r));
            Assert.AreEqual(ConnectionGate.BadRequest, r);
        }

        [Test] public void GarbagePayloadRejected() { Assert.IsFalse(Local(new byte[] { 1, 2, 3 }, out _, out var r)); Assert.AreEqual(ConnectionGate.BadRequest, r); }
        [Test] public void NullPayloadRejected() { Assert.IsFalse(Local(null, out _, out var r)); Assert.AreEqual(ConnectionGate.BadRequest, r); }
        [Test] public void OldVersionRejected() { Assert.IsFalse(Online(Payload(MintTicket(), v: 0), out _, out var r)); Assert.AreEqual("Update your game", r); }

        [Test]
        public void LocalApprovedWithGeneratedPid()
        {
            Assert.IsTrue(Local(Payload(name: "  Zesty "), out var p, out var reason), reason);
            Assert.AreEqual("Zesty", p.name); Assert.IsFalse(p.leader);
            StringAssert.IsMatch("^[0-9a-f]{16}$", p.pid);
        }

        [Test]
        public void LocalBadNameRejectedWithRuleText()
        {
            Assert.IsFalse(Local(Payload(name: "<size=99>x"), out _, out var r));
            Assert.AreEqual(NameRules.CharsetError, r);
        }
    }
}
