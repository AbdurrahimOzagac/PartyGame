"""Stands in for the Unity game server in room-service tests.

Prints @@READY, then obeys commands on stdin:
    state <phase> <humans> <connected> [pid,pid,...]   -> @@STATE {...}
    end                                                 -> @@END and exit 0
    crash                                               -> exit 1 without @@END
Started with the same arguments the room service gives the real server; -noReady skips @@READY.
"""
import json
import sys

if "-noReady" not in sys.argv:
    print("@@READY", flush=True)
for line in sys.stdin:
    parts = line.split()
    if not parts:
        continue
    if parts[0] == "state":
        pids = parts[4].split(",") if len(parts) > 4 else []
        print("@@STATE " + json.dumps({"phase": parts[1], "humans": int(parts[2]), "connected": int(parts[3]), "pids": pids}), flush=True)
    elif parts[0] == "end":
        print("@@END", flush=True)
        sys.exit(0)
    elif parts[0] == "crash":
        sys.exit(1)
