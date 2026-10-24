"""Mints one join ticket for local tests (e2e_local.sh). The room service mints them in production.

    python3 make_ticket.py --secret-hex HEX --room BCDFG --pid p1 --name Mochi [--leader] [--ttl 60]
"""
import argparse
import time

import tickets

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--secret-hex", required=True)
    ap.add_argument("--room", required=True)
    ap.add_argument("--pid", required=True)
    ap.add_argument("--name", required=True)
    ap.add_argument("--leader", action="store_true")
    ap.add_argument("--ttl", type=int, default=60)
    a = ap.parse_args()
    print(tickets.sign({"v": 1, "room": a.room, "pid": a.pid, "name": a.name, "leader": a.leader,
                        "exp": int(time.time()) + a.ttl}, bytes.fromhex(a.secret_hex)))
