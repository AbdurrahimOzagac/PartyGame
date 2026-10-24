"""ShooterParty room service: party codes, signed join tickets, and one game-server process per room.

    python3 room_service.py --server-cmd /mnt/apps/partyshooter/server/ShooterPartyServer \\
        --secret-file /etc/partyshooter/secret --listen 127.0.0.1:7700 --status-listen 127.0.0.1:7709

Public API (behind the Cloudflare tunnel, every request needs X-ShooterParty-Version: 1):
    POST /api/v1/rooms               {name, replaceLeaversWithBots} -> 201 {code, wsUrl, ticket, rejoinToken}
    POST /api/v1/rooms/{code}/join   {name}                         -> {wsUrl, ticket, rejoinToken}
    POST /api/v1/rooms/{code}/rejoin {rejoinToken}                  -> {wsUrl, ticket}
    GET  /api/v1/status                                             -> {version, rooms, players, up}
Errors are {"error": "<sentence to show the player>"}. The detailed status (codes, phases, stats) is only on
--status-listen, a loopback port the tunnel does not route. Standard library only; state is in memory.
"""
import argparse
import hashlib
import json
import os
import secrets
import shlex
import subprocess
import sys
import threading
import time
import unicodedata
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import codes
import ratelimit
import tickets

VERSION = 1
MAX_BODY = 2048
TICKET_TTL = 60
CODE_COOLDOWN = 600
MAX_PLAYERS = 4


def log(event, **fields):
    print(json.dumps(dict(event=event, t=round(time.time(), 3), **fields)), flush=True)


class ApiError(Exception):
    def __init__(self, status, message):
        super().__init__(message)
        self.status, self.message = status, message


def clean_name(raw, low=2, high=16, what="Name"):
    """Mirror of NameRules.TryClean (C#): NFC, collapsed spaces, letters/digits/space/_/- only."""
    if raw is not None and not isinstance(raw, str):
        raise ApiError(400, "Bad request")
    s = " ".join(unicodedata.normalize("NFC", raw or "").split(" "))
    s = " ".join(p for p in s.split(" ") if p)
    if len(s) < low:
        raise ApiError(400, "%s needs at least %d character%s" % (what, low, "" if low == 1 else "s"))
    if len(s) > high:
        raise ApiError(400, "%s can be at most %d characters" % (what, high))
    for ch in s:
        cat = unicodedata.category(ch)
        if not (cat.startswith("L") or cat == "Nd" or ch in " _-"):
            raise ApiError(400, "Use letters, numbers, spaces, _ and - only")
    return s


class Room:
    def __init__(self, code, slot, port, args, env):
        self.code, self.slot, self.port, self.args, self.env = code, slot, port, args, env
        self.proc = None
        self.ready = threading.Event()
        self.phase = "Lobby"
        self.connected_pids = []
        self.pending = {}          # pid -> ticket expiry (issued, not connected yet)
        self.rejoin = {}           # token -> (pid, name)
        self.stats = {}
        self.created = time.time()

    def seats_taken(self, now):
        self.pending = {p: exp for p, exp in self.pending.items() if exp > now and p not in self.connected_pids}
        return len(set(self.connected_pids) | set(self.pending))


class RoomService:
    def __init__(self, server_cmd, slots, slot_base_port, secret, ws_url_template, min_players=3,
                 clock=time.monotonic, ready_timeout=30.0, maintenance_file="/etc/partyshooter/maintenance", log_dir=None):
        self.server_cmd = list(server_cmd)
        self.slots, self.slot_base_port, self.secret = slots, slot_base_port, secret
        self.ws_url_template, self.min_players, self.clock = ws_url_template, min_players, clock
        self.ready_timeout, self.maintenance_file, self.log_dir = ready_timeout, maintenance_file, log_dir
        self.rooms = {}            # code -> Room
        self.cooldown = {}         # code -> free again at
        self.lock = threading.RLock()
        self.started = time.time()
        self.create_limit = ratelimit.Limiter(clock, 6, 600)
        self.join_limit = ratelimit.Limiter(clock, 30, 600)
        self.miss_limit = ratelimit.Limiter(clock, 10, 60, block=300)
        self.rejoin_limit = ratelimit.Limiter(clock, 60, 600)   # own budget: a drop recovery never waits on code guesses
        self.salt = secrets.token_bytes(16)                      # per run: logs follow a network without storing its IP

    # ------------------------------------------------------------------ helpers
    def blocked_codes(self):
        now = self.clock()
        with self.lock:
            self.cooldown = {c: t for c, t in self.cooldown.items() if t > now}
            return set(self.rooms) | set(self.cooldown)

    def ws_url(self, room):
        return self.ws_url_template.format(slot=room.slot, port=room.port)

    def ticket_for(self, room, pid, name, leader):
        exp = int(time.time()) + TICKET_TTL
        room.pending[pid] = self.clock() + TICKET_TTL
        return tickets.sign({"v": 1, "room": room.code, "pid": pid, "name": name, "leader": leader, "exp": exp}, self.secret)

    def anon(self, ip):
        return hashlib.sha256(self.salt + (ip or "").encode()).hexdigest()[:10]

    def check_open(self):
        if self.maintenance_file and os.path.exists(self.maintenance_file):
            raise ApiError(503, "ShooterParty is down for maintenance — try again later")

    # ------------------------------------------------------------------ API
    def create(self, ip, body):
        self.check_open()
        name = clean_name(body.get("name"))
        replace = bool(body.get("replaceLeaversWithBots", False))
        if not self.create_limit.hit(ip):
            raise ApiError(429, "Too many rooms from your network — wait a few minutes")
        with self.lock:
            used = {r.slot for r in self.rooms.values()}
            free = [s for s in range(1, self.slots + 1) if s not in used]
            if not free:
                raise ApiError(503, "All rooms are busy — try again in a few minutes")
            slot = free[0]
            code = codes.new_code(self.blocked_codes())
            port = self.slot_base_port + slot - 1
            args = ["-batchmode", "-nographics", "-logFile", "-", "-party-server", "-slot", str(slot), "-port", str(port),
                    "-wsPath", "/g/%d" % slot, "-room", code, "-minPlayers", str(self.min_players), "-replaceLeavers", "1" if replace else "0"]
            env = dict(os.environ, PARTY_TICKET_SECRET=self.secret.hex())
            room = Room(code, slot, port, args, env)
            self.rooms[code] = room
        try:
            self.start(room)
        except Exception as e:   # binary missing mid-deploy, not executable, out of memory: free the slot
            with self.lock:
                self.rooms.pop(code, None)
            log("server_start_failed", code=code, error=repr(e)[:200])
            raise ApiError(503, "Couldn't start a room — try again")
        if not room.ready.wait(self.ready_timeout):
            self.kill(room)
            raise ApiError(503, "Couldn't start a room — try again")
        pid = secrets.token_hex(8)
        token = secrets.token_urlsafe(18)
        with self.lock:
            room.rejoin[token] = (pid, name)
            ticket = self.ticket_for(room, pid, name, True)
        log("room_created", code=code, slot=slot, ip=self.anon(ip))
        return 201, {"code": code, "wsUrl": self.ws_url(room), "ticket": ticket, "rejoinToken": token}

    def find(self, ip, typed):
        if self.miss_limit.blocked_now(ip):
            raise ApiError(429, "Too many wrong codes — wait a few minutes")
        code = codes.normalise(typed)
        with self.lock:
            room = self.rooms.get(code) if code else None
        if not room:
            if not self.miss_limit.hit(ip):
                raise ApiError(429, "Too many wrong codes — wait a few minutes")
            raise ApiError(404, "No room with code %s" % (code or (typed or "").upper()[:8]))
        return room

    def join(self, ip, typed, body):
        self.check_open()
        name = clean_name(body.get("name"))
        if not self.join_limit.hit(ip):
            raise ApiError(429, "Too many attempts — wait a few minutes")
        room = self.find(ip, typed)
        with self.lock:
            if room.phase != "Lobby":
                raise ApiError(409, "Session in progress")
            if room.seats_taken(self.clock()) >= MAX_PLAYERS:
                raise ApiError(409, "Room is full")
            pid = secrets.token_hex(8)
            token = secrets.token_urlsafe(18)
            room.rejoin[token] = (pid, name)
            ticket = self.ticket_for(room, pid, name, False)
        log("join", code=room.code, ip=self.anon(ip))
        return 200, {"wsUrl": self.ws_url(room), "ticket": ticket, "rejoinToken": token}

    # The token (144 random bits) is the proof, so rejoin skips the wrong-code lock-out: one classroom's typos must
    # not block everyone's drop recovery. An unknown room and a bad token give the same answer (no code oracle).
    def rejoin(self, ip, typed, body):
        if not self.rejoin_limit.hit(ip):
            raise ApiError(429, "Too many attempts — wait a few minutes")
        token = body.get("rejoinToken")
        code = codes.normalise(typed)
        with self.lock:
            room = self.rooms.get(code) if code else None
            entry = room.rejoin.get(token) if room and isinstance(token, str) else None
            if not entry:
                raise ApiError(404, "Can't rejoin that room")
            pid, name = entry
            ticket = self.ticket_for(room, pid, name, False)
        log("rejoin", code=room.code, ip=self.anon(ip))
        return 200, {"wsUrl": self.ws_url(room), "ticket": ticket}

    def status(self):
        with self.lock:
            return {"version": VERSION, "rooms": len(self.rooms),
                    "players": sum(len(r.connected_pids) for r in self.rooms.values()), "up": int(time.time() - self.started)}

    def status_detail(self):
        with self.lock:
            return dict(self.status(), rooms=[{
                "code": r.code, "slot": r.slot, "phase": r.phase, "connected": len(r.connected_pids),
                "pending": len(r.pending), "stats": r.stats, "ageSeconds": int(time.time() - r.created)} for r in self.rooms.values()])

    # ------------------------------------------------------------------ game-server processes
    def start(self, room):
        room.proc = subprocess.Popen(self.server_cmd + room.args, env=room.env, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                     stderr=subprocess.STDOUT, text=True, bufsize=1)
        threading.Thread(target=self.read, args=(room,), daemon=True).start()
        log("server_started", code=room.code, slot=room.slot, pid=room.proc.pid)

    def read(self, room):
        out = open(os.path.join(self.log_dir, "%s-%d.log" % (room.code, room.proc.pid)), "w") if self.log_dir else None
        for line in room.proc.stdout:
            if out:
                out.write(line); out.flush()
            line = line.rstrip("\n")
            if line == "@@READY":
                room.ready.set()
            elif line.startswith("@@STATE "):
                try:
                    st = json.loads(line[8:])
                    with self.lock:
                        room.phase = st.get("phase", room.phase)
                        room.connected_pids = list(st.get("pids", [])) if st.get("connected", 0) else []
                except ValueError:
                    pass
            elif line.startswith("@@STATS "):
                try:
                    room.stats = json.loads(line[8:])
                    log("server_stats", code=room.code, **room.stats)   # every 10 s: tick ms, players, rtt (journald)
                except (ValueError, TypeError):
                    pass
            elif line == "@@END":
                log("server_end", code=room.code)
            elif line.startswith("@@RESULT "):
                log("session_result", code=room.code, result=line[9:200])
            elif "Exception" in line or "[Error]" in line:
                log("server_error", code=room.code, line=line[:300])
            elif line.strip():
                log("server_log", code=room.code, line=line[:500])   # Unity's own log (-logFile -): ~200 lines a session
        rc = room.proc.wait()
        if out:
            out.close()
        with self.lock:
            if self.rooms.get(room.code) is room:
                del self.rooms[room.code]
                self.cooldown[room.code] = self.clock() + CODE_COOLDOWN
        log("server_exited", code=room.code, slot=room.slot, rc=rc)

    def kill(self, room):
        if room.proc and room.proc.poll() is None:
            room.proc.terminate()
            try:
                room.proc.wait(5)
            except subprocess.TimeoutExpired:
                room.proc.kill()
        with self.lock:
            if self.rooms.get(room.code) is room:
                del self.rooms[room.code]

    def stop_all(self):
        for room in list(self.rooms.values()):
            self.kill(room)


# ---------------------------------------------------------------------- HTTP glue
def make_handler(svc, public):
    class Handler(BaseHTTPRequestHandler):
        server_version = "ShooterPartyRooms/1"
        sys_version = ""

        def log_message(self, fmt, *a):
            pass

        def reply(self, status, obj):
            body = json.dumps(obj).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(body)

        def ip(self):
            # trusted only because this listener is loopback behind cloudflared
            return self.headers.get("CF-Connecting-IP") or self.client_address[0]

        def handle_api(self, method):
            if not public:
                return self.reply(200, svc.status_detail()) if method == "GET" else self.reply(405, {"error": "GET only"})
            try:
                if self.headers.get("X-ShooterParty-Version") != str(VERSION):
                    raise ApiError(426, "Update your game")
                path = self.path.split("?")[0].rstrip("/")
                body = {}
                if method == "POST":
                    try:
                        n = int(self.headers.get("Content-Length") or 0)
                    except ValueError:
                        raise ApiError(400, "Bad request")
                    if n < 0:
                        raise ApiError(400, "Bad request")
                    if n > MAX_BODY:
                        raise ApiError(413, "Request too large")
                    try:
                        body = json.loads(self.rfile.read(n) or b"{}")
                    except ValueError:
                        raise ApiError(400, "Bad request")
                    if not isinstance(body, dict):
                        raise ApiError(400, "Bad request")
                parts = path.split("/")
                if method == "GET" and path == "/api/v1/status":
                    return self.reply(200, svc.status())
                if method == "POST" and path == "/api/v1/rooms":
                    return self.reply(*svc.create(self.ip(), body))
                if method == "POST" and len(parts) == 6 and parts[:4] == ["", "api", "v1", "rooms"]:
                    if parts[5] == "join":
                        return self.reply(*svc.join(self.ip(), parts[4], body))
                    if parts[5] == "rejoin":
                        return self.reply(*svc.rejoin(self.ip(), parts[4], body))
                raise ApiError(404, "Not found")
            except ApiError as e:
                if e.status in (429, 503):
                    log("refused", status=e.status, ip=svc.anon(self.ip()), error=e.message)
                self.reply(e.status, {"error": e.message})
            except Exception as e:   # never leak a stack trace to the internet
                log("bug", error=repr(e))
                self.reply(500, {"error": "Something went wrong — try again"})

        def do_GET(self):
            self.handle_api("GET")

        def do_POST(self):
            self.handle_api("POST")

    return Handler


def serve(svc, host, port, public=True):
    httpd = ThreadingHTTPServer((host, port), make_handler(svc, public))
    httpd.daemon_threads = True
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    return httpd


def serve_status(svc, host, port):
    return serve(svc, host, port, public=False)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--server-cmd", required=True, help="game server executable (and fixed arguments)")
    ap.add_argument("--secret-file", required=True)
    ap.add_argument("--listen", default="127.0.0.1:7700")
    ap.add_argument("--status-listen", default="127.0.0.1:7709")
    ap.add_argument("--slots", type=int, default=2)
    ap.add_argument("--slot-base-port", type=int, default=7701)
    ap.add_argument("--ws-url-template", default="wss://partyshooter.ezellm.com/g/{slot}")
    ap.add_argument("--min-players", type=int, default=3)
    ap.add_argument("--maintenance-file", default="/etc/partyshooter/maintenance")
    ap.add_argument("--server-log-dir", help="keep each room's full server output here (<code>-<pid>.log)")
    a = ap.parse_args()
    secret = bytes.fromhex(open(a.secret_file).read().strip())
    if len(secret) != 32:
        sys.exit("the secret must be 32 bytes as 64 hex characters")
    svc = RoomService(shlex.split(a.server_cmd), a.slots, a.slot_base_port, secret, a.ws_url_template, a.min_players,
                      maintenance_file=a.maintenance_file, log_dir=a.server_log_dir)
    host, port = a.listen.rsplit(":", 1)
    shost, sport = a.status_listen.rsplit(":", 1)
    serve(svc, host, int(port))
    serve_status(svc, shost, int(sport))
    log("listening", api=a.listen, status=a.status_listen, slots=a.slots)
    try:
        while True:
            time.sleep(3600)
    except KeyboardInterrupt:
        svc.stop_all()


if __name__ == "__main__":
    main()
