"""One-time join tickets: base64url(payload JSON) "." base64url(HMAC-SHA256(key, payload segment)).

The signature covers the payload segment's bytes, so neither side needs canonical JSON.
The C# twin is Assets/Ezel/ShooterParty/Scripts/Net/Ticket.cs. Both are checked against ticket_vectors.json.
"""
import base64
import binascii
import hashlib
import hmac
import json
import re

MAX_LEN = 1024
_B64U = re.compile(r"^[A-Za-z0-9_-]+$")
FIELDS = {"v": int, "room": str, "pid": str, "name": str, "leader": bool, "exp": int}


def _b64u(b: bytes) -> str:
    return base64.urlsafe_b64encode(b).rstrip(b"=").decode("ascii")


def _unb64u(s: str) -> bytes:
    return base64.urlsafe_b64decode(s + "=" * (-len(s) % 4))


def _mac(key: bytes, payload: str) -> bytes:
    return hmac.new(key, payload.encode("ascii"), hashlib.sha256).digest()


def sign(data: dict, key: bytes) -> str:
    payload = _b64u(json.dumps(data, separators=(",", ":"), ensure_ascii=False).encode("utf-8"))
    return payload + "." + _b64u(_mac(key, payload))


def verify(ticket, key: bytes, now: int):
    """Returns (data, None) or (None, "malformed" | "bad signature" | "expired")."""
    if not isinstance(ticket, str) or len(ticket) > MAX_LEN or ticket.count(".") != 1:
        return None, "malformed"
    payload, sig = ticket.split(".")
    if not _B64U.match(payload) or not _B64U.match(sig):
        return None, "malformed"
    try:
        sig_bytes = _unb64u(sig)
        payload.encode("ascii")
    except (binascii.Error, ValueError, UnicodeEncodeError):
        return None, "malformed"
    if not payload or not hmac.compare_digest(_mac(key, payload), sig_bytes):
        return None, "bad signature"
    try:
        data = json.loads(_unb64u(payload).decode("utf-8"))
    except (binascii.Error, ValueError):
        return None, "malformed"
    # bool is an int subclass, so check exact types
    if not isinstance(data, dict) or any(type(data.get(k)) is not t for k, t in FIELDS.items()):
        return None, "malformed"
    if now > data["exp"]:
        return None, "expired"
    return data, None
