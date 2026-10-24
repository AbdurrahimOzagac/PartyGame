"""Writes the shared ticket test vectors that the C# (TicketTests) and Python tests both check.

    python3 make_vectors.py      # rewrites Assets/Ezel/ShooterParty/Tests/EditMode/ticket_vectors.json
"""
import json
import os

import tickets

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..",
                   "Assets", "Ezel", "ShooterParty", "Tests", "EditMode", "ticket_vectors.json")
KEY = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"
OTHER = "ff" * 32


def _data(**kw):
    d = {"v": 1, "room": "BCDFG", "pid": "a1b2c3d4e5f60718", "name": "Mochi", "leader": True, "exp": 1700000060}
    d.update(kw)
    return d


def build():
    key = bytes.fromhex(KEY)
    good = tickets.sign(_data(), key)
    payload, sig = good.split(".")
    tampered = tickets.sign(_data(room="ZZZZZ"), key).split(".")[0] + "." + sig
    flipped = payload + "." + ("A" if sig[5] != "A" else "B").join([sig[:5], sig[6:]])
    cases = [
        ("valid", good, KEY, 1700000000, None),
        ("valid at exact expiry", good, KEY, 1700000060, None),
        ("expired", good, KEY, 1700000061, "expired"),
        ("wrong key", good, OTHER, 1700000000, "bad signature"),
        ("tampered payload", tampered, KEY, 1700000000, "bad signature"),
        ("flipped signature char", flipped, KEY, 1700000000, "bad signature"),
        ("unicode name", tickets.sign(_data(name="Çağla", leader=False), key), KEY, 1700000000, None),
        ("no dot", payload, KEY, 1700000000, "malformed"),
        ("two dots", good + ".x", KEY, 1700000000, "malformed"),
        ("oversize", good + "A" * (tickets.MAX_LEN + 1 - len(good)), KEY, 1700000000, "malformed"),
        ("missing field", tickets.sign({k: v for k, v in _data().items() if k != "pid"}, key), KEY, 1700000000, "malformed"),
    ]
    out = []
    for name, t, k, now, err in cases:
        got, got_err = tickets.verify(t, bytes.fromhex(k), now)
        assert got_err == err, (name, got_err, err)   # the generator checks itself
        out.append({"name": name, "ticket": t, "secret_hex": k, "now": now, "valid": err is None,
                    "error": err or "", "data": got if got else None})
    return out


if __name__ == "__main__":
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(build(), f, ensure_ascii=False, indent=1)
        f.write("\n")
    print("wrote", os.path.normpath(OUT))
