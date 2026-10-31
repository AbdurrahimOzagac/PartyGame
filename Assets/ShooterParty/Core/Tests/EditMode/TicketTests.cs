using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace ShooterParty.Tests
{
    public class TicketTests
    {
        [Serializable] class Vector { public string name, ticket, secret_hex, error; public long now; public bool valid; public TicketData data; }
        [Serializable] class Vectors { public Vector[] items; }

        static readonly byte[] Key = Encoding.ASCII.GetBytes("0123456789abcdef0123456789abcdef");

        static byte[] Hex(string h)
        {
            var b = new byte[h.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
            return b;
        }

        static TicketData Sample() => new TicketData { v = 1, room = "BCDFG", pid = "a1b2c3d4e5f60718", name = "Mochi", leader = true, exp = 1000 };

        [Test]
        public void MatchesSharedVectors()
        {
            var json = File.ReadAllText(Path.Combine(Application.dataPath, "ShooterParty/Core/Tests/EditMode/ticket_vectors.json"), Encoding.UTF8);
            var vectors = JsonUtility.FromJson<Vectors>("{\"items\":" + json + "}").items;
            Assert.GreaterOrEqual(vectors.Length, 10);
            foreach (var v in vectors)
            {
                bool ok = Ticket.TryVerify(v.ticket, Hex(v.secret_hex), v.now, out var d, out var error);
                Assert.AreEqual(v.valid, ok, v.name + ": " + error);
                Assert.AreEqual(v.error, error ?? "", v.name);
                if (!ok) continue;
                Assert.AreEqual(v.data.room, d.room, v.name);
                Assert.AreEqual(v.data.pid, d.pid, v.name);
                Assert.AreEqual(v.data.name, d.name, v.name);
                Assert.AreEqual(v.data.leader, d.leader, v.name);
                Assert.AreEqual(v.data.exp, d.exp, v.name);
            }
        }

        [Test]
        public void RoundTrip()
        {
            Assert.IsTrue(Ticket.TryVerify(Ticket.Sign(Sample(), Key), Key, 999, out var d, out var error), error);
            Assert.AreEqual("BCDFG", d.room); Assert.AreEqual("Mochi", d.name); Assert.IsTrue(d.leader); Assert.AreEqual(1000, d.exp);
        }

        [Test]
        public void FlippedByteFails()
        {
            var t = Ticket.Sign(Sample(), Key);
            int i = t.IndexOf('.') + 3;
            var flipped = t.Substring(0, i) + (t[i] == 'A' ? 'B' : 'A') + t.Substring(i + 1);
            Assert.IsFalse(Ticket.TryVerify(flipped, Key, 0, out _, out var error));
            Assert.AreEqual("bad signature", error);
        }

        [Test]
        public void ExpiredFails()
        {
            Assert.IsTrue(Ticket.TryVerify(Ticket.Sign(Sample(), Key), Key, 1000, out _, out _), "valid at exp");
            Assert.IsFalse(Ticket.TryVerify(Ticket.Sign(Sample(), Key), Key, 1001, out _, out var error));
            Assert.AreEqual("expired", error);
        }

        [Test]
        public void OversizeFails()
        {
            var t = Ticket.Sign(Sample(), Key);
            Assert.IsFalse(Ticket.TryVerify(t + new string('A', 1025 - t.Length), Key, 0, out _, out var error));
            Assert.AreEqual("malformed", error);
        }

        [Test]
        public void GarbageIsMalformed()
        {
            foreach (var bad in new[] { null, "", "abc", "a.b.c", "!!!.###" })
            {
                Assert.IsFalse(Ticket.TryVerify(bad, Key, 0, out _, out var error), bad);
                Assert.AreEqual("malformed", error, bad);
            }
        }
    }
}
