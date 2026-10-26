"""Party codes: 5 letters from 20 consonants (no vowels, so no words; no digits, so no 0/O or 1/I mix-ups).

20^5 = 3.2 M codes; with 2 live rooms and failed lookups capped at 10/min per address, guessing one is
impractical. Typed codes are upper-cased with spaces and dashes removed.
"""
import secrets

ALPHABET = "BCDFGHJKLMNPQRSTVWXZ"
LENGTH = 5


def new_code(taken):
    while True:
        c = "".join(secrets.choice(ALPHABET) for _ in range(LENGTH))
        if c not in taken:
            return c


def normalise(typed):
    c = "".join(ch for ch in (typed or "").upper() if ch not in " -")
    return c if len(c) == LENGTH and all(ch in ALPHABET for ch in c) else None
