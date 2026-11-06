using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ShooterParty
{
    [Serializable]
    public class TicketData
    {
        public int v;
        public string room, pid, name;
        public bool leader;
        public long exp;    // unix seconds; the ticket is accepted up to and including this second
    }

    // One-time join ticket: base64url(payload JSON) "." base64url(HMAC-SHA256(key, payload segment)).
    // The room service (projects/shooterparty/server/rooms/tickets.py) issues them; the game server checks them in
    // connection approval. Both sides are tested against Tests/EditMode/ticket_vectors.json.
    public static class Ticket
    {
        public const int MaxLength = 1024;

        public static string Sign(TicketData d, byte[] key)
        {
            var payload = B64U(Encoding.UTF8.GetBytes(JsonUtility.ToJson(d)));
            return payload + "." + B64U(Mac(key, payload));
        }

        public static bool TryVerify(string ticket, byte[] key, long nowUnix, out TicketData d, out string error)
        {
            d = null;
            error = "malformed";
            if (string.IsNullOrEmpty(ticket) || ticket.Length > MaxLength) return false;
            var parts = ticket.Split('.');
            if (parts.Length != 2 || !IsB64U(parts[0]) || !IsB64U(parts[1])) return false;
            byte[] sig, json;
            try { sig = UnB64U(parts[1]); json = UnB64U(parts[0]); }
            catch (FormatException) { return false; }
            if (!FixedTimeEquals(Mac(key, parts[0]), sig)) { error = "bad signature"; return false; }
            try { d = JsonUtility.FromJson<TicketData>(Encoding.UTF8.GetString(json)); }
            catch (ArgumentException) { d = null; }
            if (d == null || d.v < 1 || string.IsNullOrEmpty(d.room) || string.IsNullOrEmpty(d.pid) || string.IsNullOrEmpty(d.name) || d.exp <= 0)
            { d = null; return false; }
            if (nowUnix > d.exp) { d = null; error = "expired"; return false; }
            error = null;
            return true;
        }

        static byte[] Mac(byte[] key, string payload)
        {
            using var h = new HMACSHA256(key);
            return h.ComputeHash(Encoding.ASCII.GetBytes(payload));
        }

        static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        static bool IsB64U(string s)
        {
            if (s.Length == 0) return false;
            foreach (var c in s)
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_')) return false;
            return true;
        }

        static string B64U(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        static byte[] UnB64U(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(s + new string('=', (4 - s.Length % 4) % 4));
        }
    }
}
