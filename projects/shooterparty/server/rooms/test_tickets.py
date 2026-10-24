import json
import os
import unittest

import tickets

KEY = bytes(range(32))
VECTORS = os.path.join(os.path.dirname(__file__), "..", "..", "..", "..",
                       "Assets", "Ezel", "ShooterParty", "Tests", "EditMode", "ticket_vectors.json")


def data(**kw):
    d = {"v": 1, "room": "BCDFG", "pid": "a1b2c3d4e5f60718", "name": "Mochi", "leader": False, "exp": 1000}
    d.update(kw)
    return d


class TicketTests(unittest.TestCase):
    def test_round_trip(self):
        d, err = tickets.verify(tickets.sign(data(), KEY), KEY, now=999)
        self.assertIsNone(err)
        self.assertEqual(d, data())

    def test_valid_at_exact_expiry(self):
        self.assertIsNone(tickets.verify(tickets.sign(data(), KEY), KEY, now=1000)[1])

    def test_expired(self):
        self.assertEqual(tickets.verify(tickets.sign(data(), KEY), KEY, now=1001)[1], "expired")

    def test_wrong_key(self):
        self.assertEqual(tickets.verify(tickets.sign(data(), KEY), b"x" * 32, now=0)[1], "bad signature")

    def test_tampered_payload(self):
        t = tickets.sign(data(), KEY)
        other = tickets.sign(data(room="ZZZZZ"), KEY)
        forged = other.split(".")[0] + "." + t.split(".")[1]
        self.assertEqual(tickets.verify(forged, KEY, now=0)[1], "bad signature")

    def test_malformed(self):
        for bad in ["", "abc", "a.b.c", "x" * 1025, None, "!!!.###"]:
            self.assertEqual(tickets.verify(bad, KEY, now=0)[1], "malformed", bad)

    def test_unicode_name(self):
        d, err = tickets.verify(tickets.sign(data(name="Çağla"), KEY), KEY, now=0)
        self.assertIsNone(err)
        self.assertEqual(d["name"], "Çağla")

    def test_missing_field_is_malformed(self):
        d = data(); del d["pid"]
        self.assertEqual(tickets.verify(tickets.sign(d, KEY), KEY, now=0)[1], "malformed")

    def test_committed_vectors_match_generator(self):
        import make_vectors
        with open(VECTORS, encoding="utf-8") as f:
            self.assertEqual(json.load(f), make_vectors.build())

    def test_vectors_verify_as_declared(self):
        with open(VECTORS, encoding="utf-8") as f:
            for v in json.load(f):
                d, err = tickets.verify(v["ticket"], bytes.fromhex(v["secret_hex"]), v["now"])
                self.assertEqual(err is None, v["valid"], v["name"])
                self.assertEqual(err or "", v["error"], v["name"])


if __name__ == "__main__":
    unittest.main()
