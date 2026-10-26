import json
import os
import sys
import time
import unittest
import urllib.error
import urllib.request

import codes
import room_service
import tickets

HERE = os.path.dirname(os.path.abspath(__file__))
FAKE = [sys.executable, os.path.join(HERE, "fake_game_server.py")]
SECRET = bytes(range(32))


class Clock:
    def __init__(self):
        self.t = 1000.0

    def __call__(self):
        return self.t


def wait_for(cond, timeout=5.0):
    end = time.time() + timeout
    while time.time() < end:
        if cond():
            return True
        time.sleep(0.02)
    return cond()


class RoomServiceTests(unittest.TestCase):
    def setUp(self):
        self.clock = Clock()
        self.svc = room_service.RoomService(
            server_cmd=FAKE, slots=2, slot_base_port=7701, secret=SECRET,
            ws_url_template="wss://partyshooter.example/g/{slot}", min_players=3, clock=self.clock,
            ready_timeout=5.0, maintenance_file=os.path.join(HERE, "no-such-maintenance-file"))
        self.http = room_service.serve(self.svc, "127.0.0.1", 0)
        self.status = room_service.serve_status(self.svc, "127.0.0.1", 0)
        self.base = "http://127.0.0.1:%d/api/v1" % self.http.server_address[1]
        self.status_url = "http://127.0.0.1:%d/" % self.status.server_address[1]

    def tearDown(self):
        self.http.shutdown(); self.status.shutdown()
        self.svc.stop_all()

    def call(self, method, path, body=None, version="1", headers=None, raw=None):
        data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
        req = urllib.request.Request(self.base + path, data=data, method=method)
        if version is not None:
            req.add_header("X-ShooterParty-Version", version)
        req.add_header("Content-Type", "application/json")
        for k, v in (headers or {}).items():
            req.add_header(k, v)
        try:
            with urllib.request.urlopen(req, timeout=10) as r:
                return r.status, json.loads(r.read() or b"{}")
        except urllib.error.HTTPError as e:
            with e:
                return e.code, json.loads(e.read() or b"{}")

    def create(self, name="Ezel", ip="10.0.0.1", replace=False):
        return self.call("POST", "/rooms", {"name": name, "replaceLeaversWithBots": replace}, headers={"CF-Connecting-IP": ip})

    def fake(self, code):
        return self.svc.rooms[code].proc

    def send(self, code, line):
        p = self.fake(code)
        p.stdin.write(line + "\n"); p.stdin.flush()

    # ------------------------------------------------------------------ codes
    def test_code_alphabet_and_length(self):
        for _ in range(200):
            c = codes.new_code(set())
            self.assertEqual(len(c), 5)
            self.assertTrue(all(ch in "BCDFGHJKLMNPQRSTVWXZ" for ch in c))

    def test_code_normalisation(self):
        self.assertEqual(codes.normalise(" bc-df g "), "BCDFG")
        self.assertIsNone(codes.normalise("AEIOU"))
        self.assertIsNone(codes.normalise("BCDF"))

    def test_code_cooldown_10min(self):
        s, r = self.create()
        code = r["code"]
        self.send(code, "end")
        self.assertTrue(wait_for(lambda: code not in self.svc.rooms))
        self.assertIn(code, self.svc.blocked_codes())
        self.clock.t += 601
        self.assertNotIn(code, self.svc.blocked_codes())

    # ------------------------------------------------------------------ create / join / rejoin
    def test_create_returns_code_ws_ticket_rejoin(self):
        s, r = self.create()
        self.assertEqual(s, 201, r)
        self.assertEqual(r["wsUrl"], "wss://partyshooter.example/g/1")
        d, err = tickets.verify(r["ticket"], SECRET, int(time.time()))
        self.assertIsNone(err)
        self.assertEqual((d["room"], d["name"], d["leader"]), (r["code"], "Ezel", True))
        self.assertTrue(r["rejoinToken"])
        args = self.svc.rooms[r["code"]].args
        self.assertIn("-room", args); self.assertEqual(args[args.index("-port") + 1], "7701")
        self.assertEqual(args[args.index("-wsPath") + 1], "/g/1")

    def test_secret_is_passed_in_env_not_argv(self):
        s, r = self.create()
        room = self.svc.rooms[r["code"]]
        self.assertNotIn(SECRET.hex(), " ".join(room.args))
        self.assertEqual(room.env["PARTY_TICKET_SECRET"], SECRET.hex())

    def test_third_room_503(self):
        self.assertEqual(self.create(ip="10.0.0.1")[0], 201)
        self.assertEqual(self.create(ip="10.0.0.2")[0], 201)
        s, r = self.create(ip="10.0.0.3")
        self.assertEqual(s, 503)
        self.assertIn("busy", r["error"])

    def test_join_unknown_404_message(self):
        s, r = self.call("POST", "/rooms/BCDFG/join", {"name": "Zesty"})
        self.assertEqual(s, 404)
        self.assertEqual(r["error"], "No room with code BCDFG")

    def test_join_accepts_lowercase_and_dashes(self):
        code = self.create()[1]["code"]
        s, r = self.call("POST", "/rooms/%s-%s/join" % (code[:2].lower(), code[2:].lower()), {"name": "Zesty"})
        self.assertEqual(s, 200, r)

    def test_join_full_409(self):
        code = self.create()[1]["code"]
        for n in ["Bea", "Cem", "Dee"]:
            self.assertEqual(self.call("POST", "/rooms/%s/join" % code, {"name": n})[0], 200)
        s, r = self.call("POST", "/rooms/%s/join" % code, {"name": "Eve"})
        self.assertEqual(s, 409)
        self.assertEqual(r["error"], "Room is full")

    def test_join_in_progress_409(self):
        code = self.create()[1]["code"]
        self.send(code, "state Intro 3 3 a,b,c")
        self.assertTrue(wait_for(lambda: self.svc.rooms[code].phase == "Intro"))
        s, r = self.call("POST", "/rooms/%s/join" % code, {"name": "Late"})
        self.assertEqual(s, 409)
        self.assertEqual(r["error"], "Session in progress")

    def test_rejoin_issues_ticket_for_same_pid(self):
        code, first = None, None
        s, r = self.create()
        code, token = r["code"], r["rejoinToken"]
        pid = tickets.verify(r["ticket"], SECRET, int(time.time()))[0]["pid"]
        self.send(code, "state Playing 3 2 x,y")
        self.assertTrue(wait_for(lambda: self.svc.rooms[code].phase == "Playing"))
        s, r = self.call("POST", "/rooms/%s/rejoin" % code, {"rejoinToken": token})
        self.assertEqual(s, 200, r)
        d = tickets.verify(r["ticket"], SECRET, int(time.time()))[0]
        self.assertEqual(d["pid"], pid)
        self.assertFalse(d["leader"])
        self.assertEqual(self.call("POST", "/rooms/%s/rejoin" % code, {"rejoinToken": "nope"})[0], 404)

    # ------------------------------------------------------------------ input checks
    def test_version_header_426(self):
        s, r = self.call("POST", "/rooms", {"name": "Ezel"}, version=None)
        self.assertEqual(s, 426)
        self.assertEqual(r["error"], "Update your game")
        self.assertEqual(self.call("POST", "/rooms", {"name": "Ezel"}, version="0")[0], 426)

    def test_body_over_2kb_413(self):
        s, r = self.call("POST", "/rooms", raw=b'{"name":"' + b"a" * 3000 + b'"}')
        self.assertEqual(s, 413)

    def test_bad_json_400(self):
        self.assertEqual(self.call("POST", "/rooms", raw=b"{not json")[0], 400)

    def test_bad_name_400(self):
        s, r = self.create(name="<size=400>x")
        self.assertEqual(s, 400)
        self.assertEqual(r["error"], "Use letters, numbers, spaces, _ and - only")
        self.assertEqual(self.create(name="a")[0], 400)
        self.assertEqual(self.create(name="Çağla")[0], 201)

    def test_unknown_route_404(self):
        self.assertEqual(self.call("GET", "/nope")[0], 404)

    def test_create_rate_limit_429(self):
        for i in range(6):
            s, r = self.create(ip="10.9.9.9")
            if s == 201:
                self.send(r["code"], "end")
                wait_for(lambda: r["code"] not in self.svc.rooms)
        s, r = self.create(ip="10.9.9.9")
        self.assertEqual(s, 429)
        self.assertEqual(self.create(ip="10.9.9.8")[0], 201, "another address is not affected")

    def test_failed_lookup_rate_limit_429(self):
        for _ in range(10):
            self.assertEqual(self.call("POST", "/rooms/BCDFG/join", {"name": "Zed"}, headers={"CF-Connecting-IP": "10.1.1.1"})[0], 404)
        s, r = self.call("POST", "/rooms/BCDFG/join", {"name": "Zed"}, headers={"CF-Connecting-IP": "10.1.1.1"})
        self.assertEqual(s, 429)
        self.clock.t += 301
        self.assertEqual(self.call("POST", "/rooms/BCDFG/join", {"name": "Zed"}, headers={"CF-Connecting-IP": "10.1.1.1"})[0], 404)

    def test_maintenance_503(self):
        path = os.path.join(HERE, "maint.tmp")
        open(path, "w").close()
        try:
            self.svc.maintenance_file = path
            s, r = self.create()
            self.assertEqual(s, 503)
            self.assertIn("maintenance", r["error"])
        finally:
            os.remove(path)

    # ------------------------------------------------------------------ lifecycle / status
    def test_server_exit_frees_slot(self):
        code = self.create(ip="10.0.0.1")[1]["code"]
        self.create(ip="10.0.0.2")
        self.send(code, "crash")
        self.assertTrue(wait_for(lambda: code not in self.svc.rooms))
        s, r = self.create(ip="10.0.0.3")
        self.assertEqual(s, 201)
        self.assertEqual(r["wsUrl"], "wss://partyshooter.example/g/1", "the freed slot is reused")

    def test_server_that_never_gets_ready_503(self):
        self.svc.server_cmd = FAKE + ["-noReady"]
        self.svc.ready_timeout = 0.5
        s, r = self.create()
        self.assertEqual(s, 503)
        self.assertEqual(len(self.svc.rooms), 0)

    # ---- final review fixes
    def test_failed_server_start_frees_the_slot(self):
        self.svc.server_cmd = ["/no/such/binary"]   # mid-deploy, not executable, out of memory...
        for _ in range(3):
            status, body = self.create()
            self.assertEqual(503, status)
            self.assertIn("Couldn't start a room", body["error"])
        self.assertEqual(0, len(self.svc.rooms), "no slot is held by a server that never started")

    def test_non_string_name_is_400_not_500(self):
        for bad in (5, ["Ezel"], {"x": 1}, True):
            status, body = self.call("POST", "/rooms", {"name": bad})
            self.assertEqual(400, status, repr(bad))

    def test_negative_content_length_is_400(self):
        import http.client
        port = self.http.server_address[1]
        c = http.client.HTTPConnection("127.0.0.1", port, timeout=5)
        c.putrequest("POST", "/api/v1/rooms")
        c.putheader("X-ShooterParty-Version", "1")
        c.putheader("Content-Length", "-1")
        c.endheaders()
        self.assertEqual(400, c.getresponse().status)
        c.close()

    def test_rejoin_still_works_during_a_wrong_code_lockout(self):
        status, room = self.create(ip="10.0.0.9")
        for _ in range(11):   # a classroom behind one IP mistypes codes
            self.call("POST", "/rooms/BCDFG/join", {"name": "Kid"}, headers={"CF-Connecting-IP": "10.0.0.9"})
        self.assertEqual(429, self.call("POST", "/rooms/BCDFG/join", {"name": "Kid"}, headers={"CF-Connecting-IP": "10.0.0.9"})[0])
        status, body = self.call("POST", "/rooms/%s/rejoin" % room["code"], {"rejoinToken": room["rejoinToken"]},
                                 headers={"CF-Connecting-IP": "10.0.0.9"})
        self.assertEqual(200, status, "the 144-bit token is the proof; the code-guess lockout must not block a drop recovery")

    def test_refused_attempts_do_not_extend_a_limit(self):
        lim = room_service.ratelimit.Limiter(self.clock, limit=2, window=10.0)
        self.assertTrue(lim.hit("k")); self.assertTrue(lim.hit("k"))
        for _ in range(5):   # keeps retrying every 2 s while refused
            self.clock.t += 2.0
            lim.hit("k")
        self.clock.t += 0.5   # 10.5 s after the two counted hits
        self.assertTrue(lim.hit("k"), "only counted hits fill the window")

    def test_logs_do_not_keep_player_ips(self):
        import contextlib, io
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            self.create(ip="203.0.113.77")
            self.call("POST", "/rooms/BCDFG/join", {"name": "Kid"}, headers={"CF-Connecting-IP": "203.0.113.77"})
        self.assertNotIn("203.0.113.77", out.getvalue())
        self.assertIn('"ip": "', out.getvalue(), "a stable pseudonym is still logged, so one network's activity can be followed")

    def test_status_public_has_no_codes(self):
        code = self.create()[1]["code"]
        s, r = self.call("GET", "/status")
        self.assertEqual(s, 200)
        self.assertEqual((r["version"], r["rooms"]), (1, 1))
        self.assertNotIn(code, json.dumps(r))

    def test_status_detail_only_on_status_port(self):
        code = self.create()[1]["code"]
        s, r = self.call("GET", "/status?detail=1")
        self.assertNotIn(code, json.dumps(r), "the public port never shows detail")
        with urllib.request.urlopen(self.status_url, timeout=5) as resp:
            detail = json.loads(resp.read())
        self.assertEqual(detail["rooms"][0]["code"], code)


if __name__ == "__main__":
    unittest.main()
