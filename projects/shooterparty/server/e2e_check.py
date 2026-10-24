"""Checks a game server's stdout after an end-to-end run.

    python3 e2e_check.py server.out --humans 3 --rounds 2
Passes when @@READY, @@RESULT and @@END are present and every slot's total equals the placement points
(4/3/2/1, tied ranks share the higher value) summed over the reported rankings.
"""
import argparse
import json
import sys

TABLE = [4, 3, 2, 1]


def check(lines, humans, rounds):
    errors = []
    proto = [l for l in lines if l.startswith("@@")]
    for tag in ("@@READY", "@@RESULT", "@@END"):
        if not any(l.startswith(tag) for l in proto):
            errors.append("missing " + tag)
    results = [json.loads(l[len("@@RESULT "):]) for l in proto if l.startswith("@@RESULT ")]
    if results:
        r = results[-1]
        expected = {}
        for ranking in r["rankings"]:
            for e in ranking:
                expected[e["slot"]] = expected.get(e["slot"], 0) + TABLE[min(e["rank"], len(TABLE)) - 1]
        for t in r["totals"]:
            if t["total"] != expected.get(t["slot"], 0):
                errors.append("slot %d total %d != %d from rankings" % (t["slot"], t["total"], expected.get(t["slot"], 0)))
        if len(r["rankings"]) != rounds:
            errors.append("%d rounds played, expected %d" % (len(r["rankings"]), rounds))
        got_humans = sum(1 for t in r["totals"] if not t["bot"])
        if got_humans != humans:
            errors.append("%d human seats, expected %d" % (got_humans, humans))
    return errors


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("server_out")
    ap.add_argument("--humans", type=int, default=3)
    ap.add_argument("--rounds", type=int, default=2)
    a = ap.parse_args()
    with open(a.server_out, encoding="utf-8", errors="replace") as f:
        errs = check([l.rstrip("\n") for l in f], a.humans, a.rounds)
    for e in errs:
        print("FAIL:", e)
    print("OK" if not errs else "%d problem(s)" % len(errs))
    sys.exit(1 if errs else 0)
