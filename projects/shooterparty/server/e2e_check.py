"""Checks a game server's stdout after an end-to-end run.

    python3 e2e_check.py server.out --humans 3 --rounds 2 [--expect-rejoin]
Passes when @@READY, @@RESULT and @@END are present and every slot's total equals the placement points
(4/3/2/1, tied ranks share the higher value) summed over the reported rankings.
--expect-rejoin also needs a mid-session dip in connected humans that recovers (a drop and a rejoin), and
points for every human seat (the rejoined player kept the seat and played on).
"""
import argparse
import json
import sys

TABLE = [4, 3, 2, 1]


def check(lines, humans, rounds, expect_rejoin=False):
    errors = []
    proto = [l for l in lines if l.startswith("@@")]
    if expect_rejoin:
        states = [json.loads(l[len("@@STATE "):]) for l in proto if l.startswith("@@STATE ")]
        live = [st for st in states if st.get("phase") not in ("Lobby", None) and "connected" in st]
        dip = next((i for i, st in enumerate(live) if st["connected"] < st["humans"]), None)
        if dip is None:
            errors.append("no rejoin: nobody dropped mid-session")
        elif not any(st["connected"] == st["humans"] for st in live[dip + 1:]):
            errors.append("no rejoin: the dropped player never came back")
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
        if expect_rejoin:
            for t in r["totals"]:
                if not t["bot"] and t["total"] <= 0:
                    errors.append("slot %d has no points after the rejoin" % t["slot"])
    return errors


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("server_out")
    ap.add_argument("--humans", type=int, default=3)
    ap.add_argument("--rounds", type=int, default=2)
    ap.add_argument("--expect-rejoin", action="store_true")
    a = ap.parse_args()
    with open(a.server_out, encoding="utf-8", errors="replace") as f:
        errs = check([l.rstrip("\n") for l in f], a.humans, a.rounds, a.expect_rejoin)
    for e in errs:
        print("FAIL:", e)
    print("OK" if not errs else "%d problem(s)" % len(errs))
    sys.exit(1 if errs else 0)
