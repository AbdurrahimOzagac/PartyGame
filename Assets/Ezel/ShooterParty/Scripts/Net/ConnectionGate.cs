using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ShooterParty
{
    // What a client sends as NGO ConnectionData. Online, the name inside the signed ticket wins.
    [Serializable]
    public class JoinPayload
    {
        public const int MaxBytes = 1024;
        public int v;
        public string name;
        public string ticket;

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
    }

    public struct ApprovedPlayer { public string pid, name; public bool leader; }

    // Decides whether a connecting client may enter. Online (server != null) it needs a valid, unused, unexpired
    // ticket for this room; Local play needs only a matching version and a clean name. The texts are shown to
    // the player as the disconnect reason.
    public static class ConnectionGate
    {
        public const string BadRequest = "Bad join request";
        public const string BadTicket = "Invalid ticket — join again from the menu";
        public const string Expired = "Join ticket expired — join again from the menu";
        public const string WrongRoom = "That ticket is for another room";
        public const string Replayed = "Ticket already used — join again from the menu";
        public const string OldVersion = "Update your game";

        // ticketUsed(key) returns true when the key was already used, and records it otherwise.
        public static bool Approve(byte[] payload, ServerArgs server, Func<string, bool> ticketUsed, long nowUnix,
                                   out ApprovedPlayer p, out string reason)
        {
            p = default;
            reason = BadRequest;
            if (payload == null || payload.Length == 0 || payload.Length > JoinPayload.MaxBytes) return false;
            JoinPayload j;
            try { j = JsonUtility.FromJson<JoinPayload>(Encoding.UTF8.GetString(payload)); }
            catch (ArgumentException) { return false; }
            if (j == null) return false;
            if (j.v != PartyProtocol.Version) { reason = OldVersion; return false; }

            if (server == null)
            {
                if (!NameRules.TryClean(j.name, out var clean, out reason)) return false;
                p = new ApprovedPlayer { pid = NewPid(), name = clean, leader = false };
                reason = null;
                return true;
            }

            if (!Ticket.TryVerify(j.ticket, server.secret, nowUnix, out var t, out var err))
            {
                reason = err == "expired" ? Expired : BadTicket;
                return false;
            }
            if (t.room != server.room) { reason = WrongRoom; return false; }
            if (!NameRules.TryClean(t.name, out var ticketName, out _)) { reason = BadTicket; return false; }
            if (ticketUsed(t.pid + ":" + t.exp)) { reason = Replayed; return false; }
            p = new ApprovedPlayer { pid = t.pid, name = ticketName, leader = t.leader };
            reason = null;
            return true;
        }

        public static string NewPid()
        {
            var b = new byte[8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
        }
    }
}
