# Server operations

How to build, deploy, watch and switch off ShooterParty's online server, and what to do when a network blocks it. The architecture is in [ARCHITECTURE.md](ARCHITECTURE.md) section 2.

Everything here uses placeholders. `<ssh-host>` is the SSH name of the server (an `~/.ssh/config` entry on your machine). Never commit real host names, addresses, user names or keys: this repo is public. The public game address, `partyshooter.ezellm.com`, is fine.

The server is a Linux ARM64 machine (a Raspberry Pi 5) that is reachable only through a Cloudflare Tunnel. It has no open inbound ports.

## 1. What runs where

| Path on the server | What | Owner, mode |
|---|---|---|
| `/mnt/apps/partyshooter/server/` | the Unity game server build (`ShooterPartyServer`) | root, read-only to the service |
| `/mnt/apps/partyshooter/rooms/` | the room service (`room_service.py`, `codes.py`, `tickets.py`, `ratelimit.py`) | root, read-only to the service |
| `/mnt/apps/partyshooter/home/` | home and working directory of the `partyshooter` system user; the only writable path (Unity writes `~/.config/unity3d`) | partyshooter, 0700 |
| `/etc/partyshooter/secret` | ticket HMAC key: 32 random bytes as 64 hex characters | root:partyshooter, 0640 |
| `/etc/partyshooter/maintenance` | if this file exists, create and join answer 503 | root |
| `/etc/systemd/system/partyshooter-rooms.service` | room service + game servers (from `projects/shooterparty/server/systemd/`) | root |
| `/etc/systemd/system/cloudflared-partyshooter.service` | the game's own tunnel | root |
| `/etc/cloudflared/partyshooter.yml` | tunnel ingress (from `projects/shooterparty/server/cloudflared/partyshooter.yml.example`) | root, 0644 |
| `/etc/cloudflared/partyshooter.json` | tunnel credential, passed to the unit with `LoadCredential` | root, 0600 |
| `/usr/local/bin/partyshooter-status` | status script | root |
| `~/partyshooter-stage/` (deploying user) | rsync staging area used by `deploy.sh` | you |

| Listener (all 127.0.0.1) | What | Routed by the tunnel |
|---|---|---|
| `:7700` | room service API | `^/api/` |
| `:7709` | detailed status (codes, phases, stats) | **no** |
| `:7701`, `:7702` | game server for slot 1 and slot 2 | `^/g/1$`, `^/g/2$` |
| any other path | | 404 (the tunnel's catch-all rule) |

`partyshooter-rooms` runs as the `partyshooter` user with a strict systemd sandbox: `NoNewPrivileges`, `ProtectSystem=strict`, `MemoryMax=1500M`, `CPUQuota=250%`, `TasksMax=512`. It starts one game server per room as a child process (2 rooms at most) and restarts on failure. All its state is in memory: a restart closes every room.

## 2. Build the server

In Unity, run **ShooterParty > Build > Pi Server (Linux Arm64)** (`PartyBuild.PiServer`). The output is `Builds/PiServer/ShooterPartyServer` (gitignored). It is IL2CPP, cross-compiled with the `com.unity.toolchain.macos-arm64-linux` and `com.unity.sdk.linux-arm64` packages, and needs the Linux Dedicated Server build module.

From a shell, with the editor closed:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -projectPath . -executeMethod ShooterParty.EditorTools.PartyBuild.BatchPiServer
```

The first build after the toolchain packages are imported can fail with "Unable to find an Linux Sysroot". Run it again; the second run works. `deploy.sh` does not build for you.

Other targets are in the same menu: macOS, Windows, Linux x64, and All. Desktop builds go to `Builds/macOS/`, `Builds/Windows/` and `Builds/Linux/`.

## 3. Deploy

Requirements:
- on your machine: `ssh` with key login to `<ssh-host>`, and `rsync`;
- on the server: passwordless `sudo` for your user, plus `rsync`, `curl`, `python3` and `openssl`.

```sh
projects/shooterparty/server/deploy.sh <ssh-host> --status    # are rooms running?
projects/shooterparty/server/deploy.sh <ssh-host>             # install or update
projects/shooterparty/server/deploy.sh <ssh-host> path/to/build   # another build folder
```

What `deploy.sh <ssh-host>` does:
1. Stages the build, the room service (without tests and helpers), the units and `partyshooter-status` in `~/partyshooter-stage`.
2. Asks the running service how many rooms are open. If any are, it stops with exit 4 and "N room(s) running: not restarting". `FORCE=1 projects/shooterparty/server/deploy.sh <ssh-host>` drops them.
3. Creates the `partyshooter` user and the directories if they are missing.
4. Creates `/etc/partyshooter/secret` with `openssl rand -hex 32` **only if it does not exist**. The secret never leaves the server.
5. Tunes the network for a game host: UDP buffers up to 7.5 MB (`/etc/sysctl.d/90-partyshooter-net.conf`; cloudflared's QUIC warns "failed to sufficiently increase receive buffer size" without it) and Wi-Fi power saving off (`/etc/NetworkManager/conf.d/90-partyshooter-wifi.conf`, applied at once with `iw`). With power saving on, the Pi's Wi-Fi stalled for seconds; over the TCP WebSocket those stalls arrive as bursts that dropped players.
6. Copies the code root-owned and read-only, installs the unit and `/usr/local/bin/partyshooter-status`, then enables and restarts `partyshooter-rooms`.
7. Prints `partyshooter-rooms: active` and the public status JSON.

Every deploy needs a server build in `Builds/PiServer`, even when only Python files changed. Exit 2 means it found none.

## 4. Set up the tunnel (once)

The tunnel is separate from anything else on the server and has its own credential.

1. Install `cloudflared` on the server at `/usr/local/bin/cloudflared` (the unit runs that path).
2. On the server, run `cloudflared tunnel login` once and pick the zone in the browser. The link expires after about 8 minutes; if you approve later, the browser downloads `cert.pem` instead: copy it to `~/.cloudflared/cert.pem` on the server (`ssh <ssh-host> 'umask 077; mkdir -p ~/.cloudflared; cat > ~/.cloudflared/cert.pem' < ~/Downloads/cert.pem`) and delete the download.
3. On your machine, run `projects/shooterparty/server/deploy.sh <ssh-host> --tunnel`. It:
   - creates the tunnel `partyshooter` if it is missing;
   - routes DNS for `partyshooter.ezellm.com`;
   - installs the credential and the config;
   - starts `cloudflared-partyshooter`;
   - deletes `~/.cloudflared/cert.pem`, because the account certificate could create tunnels for the whole zone.
4. Check the zone settings (next section).

Running `--tunnel` again later needs a new `cloudflared tunnel login`, because the certificate is gone.

## 5. Cloudflare zone settings

A Unity client cannot solve a browser challenge. For `partyshooter.ezellm.com`:
- **Bot Fight Mode: off.**
- **Security level "I'm Under Attack": off.**
- Any custom WAF or rate-limiting rule that issues a challenge must skip this host.

Verify from any machine:

```sh
curl -s -i -H 'X-ShooterParty-Version: 1' https://partyshooter.ezellm.com/api/v1/status
#   200, Content-Type: application/json, body like {"version": 1, "rooms": 0, "players": 0, "up": 1234}
curl -s -o /dev/null -w '%{http_code}\n' https://partyshooter.ezellm.com/api/v1/status
#   426 (no version header: the body is {"error": "Update your game"})
curl -s -o /dev/null -w '%{http_code}\n' https://partyshooter.ezellm.com/anything
#   404 (the tunnel's catch-all)
```

An HTML page, or a 403/503 with a challenge, means a Cloudflare protection is on. Then do a real check: in the game, PLAY ONLINE > CREATE ROOM must reach the lobby.

## 6. Logs

```sh
ssh <ssh-host> journalctl -u partyshooter-rooms -f          # room service + game server events
ssh <ssh-host> journalctl -u cloudflared-partyshooter -f    # tunnel
ssh <ssh-host> "journalctl -u partyshooter-rooms -o cat --since today | grep '\"event\": \"server_stats\"'"
```

The room service writes one JSON object per line. Every line has `event` and `t` (unix time).

| `event` | When | Other fields |
|---|---|---|
| `listening` | the service started | `api`, `status`, `slots` |
| `room_created` | a room was created | `code`, `slot`, `ip` |
| `server_started` | a game server process was launched | `code`, `slot`, `pid` |
| `join`, `rejoin` | a join or rejoin ticket was issued | `code`, `ip` |
| `refused` | a 429 (rate limit) or 503 (busy, maintenance) answer | `status`, `ip`, `error` |
| `server_stats` | every 10 s per room, from the game server's `@@STATS` | `code`, `tickMs`, `tickMsMax`, `players`, `rttAvgMs` |
| `session_result` | a session reached its Final (`@@RESULT`) | `code`, `result` (first ~190 chars) |
| `server_end` | the game server announced its exit (`@@END`) | `code` |
| `server_exited` | the process ended; `rc` 0 is a normal exit | `code`, `slot`, `rc` |
| `server_error` | a game server output line containing `Exception` or `[Error]` | `code`, `line` |
| `server_log` | every other line of the game server's Unity log (about 200 per session) | `code`, `line` |
| `bug` | the service hit an unexpected error (the client got a 500) | `error` |

What normal looks like: `tickMs` around 16.7 (the server runs at 60 frames per second), and `tickMsMax` not far above 20. On the Pi a full session measured 16.7 ms average, 20 ms max, and a peak of about 190 MB RSS per room.

The game server's whole Unity log reaches journald as `server_log` events, so `journalctl -u partyshooter-rooms -o cat | grep '"code": "BKFZT"'` shows one room's full story. To also keep a plain file per room, add `--server-log-dir /mnt/apps/partyshooter/home/logs` to `ExecStart` in the unit and create that directory, owned by `partyshooter`. Each room then writes `<code>-<pid>.log` (nothing prunes those files).

## 7. Status

```sh
ssh <ssh-host> partyshooter-status
projects/shooterparty/server/deploy.sh <ssh-host> --status     # the same, from your machine
```

It prints the unrouted `127.0.0.1:7709` view: `version`, `players`, `up`, and per room `code`, `slot`, `phase`, `connected`, `pending` (tickets issued but not used yet), `stats` (the last `@@STATS`) and `ageSeconds`. If the service is down, it prints "room service not answering" and the unit's status.

In the game, F3 shows RTT, jitter, server tick and kbps. The HUD's connection dots are green below 120 ms, amber below 220 ms and red above.

## 8. Kill switches

From the mildest to the strongest:

| Action | Effect |
|---|---|
| `ssh <ssh-host> sudo touch /etc/partyshooter/maintenance` | new rooms and joins get 503 "ShooterParty is down for maintenance — try again later". Running rooms keep playing, and rejoins still work. Undo with `sudo rm /etc/partyshooter/maintenance`. |
| `ssh <ssh-host> sudo systemctl stop partyshooter-rooms` | every room closes at once. The API answers with an error from Cloudflare. Start it again with `sudo systemctl start partyshooter-rooms`. |
| `ssh <ssh-host> sudo systemctl stop cloudflared-partyshooter` | the game is offline from the internet. Nothing else on the server is touched. |
| delete the `partyshooter` DNS record in the Cloudflare dashboard | the name no longer resolves |

The units are enabled, so a reboot starts them again. Use `systemctl disable` as well if they must stay off.

## 9. Rotate the ticket secret

Rotate it if it may have leaked: anyone with the secret can sign join tickets for any room. Clients never see the secret, so no game update is needed. Restarting the service closes every room and forgets all rejoin tokens, so do it when the status shows no rooms.

```sh
ssh <ssh-host>
partyshooter-status                                    # no rooms?
sudo sh -c 'umask 077; openssl rand -hex 32 > /etc/partyshooter/secret'
sudo chown root:partyshooter /etc/partyshooter/secret && sudo chmod 0640 /etc/partyshooter/secret
sudo systemctl restart partyshooter-rooms
```

Alternative: `sudo rm /etc/partyshooter/secret` on the server, then run `deploy.sh <ssh-host>`. The deploy makes a new secret and restarts the service.

## 10. School Wi-Fi checklist

Online play uses only HTTPS and WebSockets over TLS on port **443** to `partyshooter.ezellm.com`. It uses no UDP and no other ports.

Test on the school network about a week before each demo:
1. **In a browser**, open `https://partyshooter.ezellm.com/api/v1/status`. You should see `{"error": "Update your game"}`. That is expected (a browser sends no version header) and proves that DNS, TLS and the tunnel work. A block page, a certificate warning or a timeout means the network blocks or intercepts the site.
2. **In the game**, choose PLAY ONLINE > CREATE ROOM.
   - "Server unreachable — try Local Play": HTTPS is blocked, or the server is down. Check from a phone on mobile data to tell which.
   - "Couldn't reach the game server — try again" or "Connection lost" right after the room was created: HTTPS works but the WebSocket does not.
3. **Join from a second laptop** with the code, and watch F3 for RTT and jitter.

If it is blocked:
- **Certificate warning in the browser:** the network intercepts TLS. The game checks certificates against the system's trusted roots, so it refuses the connection unless the laptop trusts the network's certificate. Use a hotspot.
- **Phone hotspot with mobile data:** connect the laptops to it and play online as usual.
- **No internet at all:** use Local play (next section) on a phone hotspot. A hotspot is also the safest local network, because school Wi-Fi often blocks traffic between devices.

## 11. Local play fallback

Local play needs no server, no tickets and no internet.

1. Put every laptop on the same network (same Wi-Fi or a phone hotspot).
2. The host chooses SAME WI-FI > HOST. The lobby shows the host's LAN address(es).
3. The others choose SAME WI-FI > JOIN BY ADDRESS and type that address (`address:port` only if the port is not 7777).
4. The host's firewall must let the game receive **UDP 7777**. Allow incoming connections when macOS or Windows asks.

Limits:
- A Local room needs 3 humans to start. Bots fill free seats only in Practice; a Local leader can still turn on "Replace leavers with bots".
- The host is the server: if the host quits, the room ends for everyone.
- There is no automatic rejoin. A dropped player can join again only when the room is back in the lobby.
- "Couldn't host — is another game using port 7777?" means another program holds the port. "Couldn't reach that address — are you on the same Wi-Fi?" on a joiner usually means a wrong address, another network, or a firewall.

## 12. End-to-end tests

`projects/shooterparty/server/e2e_local.sh` runs a dedicated server and 3 self-playing clients (`-autopilot`) over WebSockets, plays a 2-round session at 1/4 speed, and checks the result with `e2e_check.py`. One desktop build plays both roles.

1. Build the macOS player: **ShooterParty > Build > macOS**. The binary is `Builds/macOS/ShooterParty.app/Contents/MacOS/406Party`.
2. Run a scenario:
   ```sh
   BIN=Builds/macOS/ShooterParty.app/Contents/MacOS/406Party
   projects/shooterparty/server/e2e_local.sh basic "$BIN"
   ```

| Scenario | What it checks |
|---|---|
| `basic` | server + 3 clients with tickets from `rooms/make_ticket.py`; full session to the Final |
| `leave-during-load` | the third client leaves when the first round starts loading; the session still finishes |
| `rooms` | `room_service.py` starts the server; the clients create and join through its HTTP API, using the game's own `RoomServiceClient` |
| `drop` | like `rooms`, but client Cem's connection is cut 8 s into round 1 (`-bridge -dropAt 8`). It must rejoin with its token and finish with points (`e2e_check.py --expect-rejoin`) |

- Exit 0 means pass: `@@READY`, `@@RESULT` and `@@END` were seen, and every total equals the points from the reported rankings. Each run takes about 80 s.
- Logs go to `$E2E_OUT`, or to a temporary folder printed as `logs: ...`. `E2E_PORT` (default 7701) moves the ports.
- **Against the server**, for `basic` and `leave-during-load` only:
  ```sh
  E2E_REMOTE=<ssh-host> E2E_SERVER_DIR=Builds/PiServer E2E_PORT=7801 projects/shooterparty/server/e2e_local.sh basic "$BIN"
  ```
  This copies the server build to `~/shooterparty_e2e` on the host, runs it there, reaches it through an SSH port forward and prints the peak RSS. Set `E2E_PORT` away from the live slots (7701, 7702). Run it only when `partyshooter-status` shows no rooms: the script finds the test server by process name, so a live room could be measured instead.
- **Online smoke test** through Cloudflare, with the real service: start 3 autopilot clients with `-roomService https://partyshooter.ezellm.com/api/v1`. The first gets `-create` and logs `[Autopilot] room CODE`; the other two get `-code CODE`. Add `-batchmode -nographics -logFile <file> -name <Name>` to each.

The Python unit tests are in [ARCHITECTURE.md](ARCHITECTURE.md) section 9.

## 13. Changing the protocol

When client and server stop understanding each other (a changed RPC, network variable or join payload), bump the version so old clients get "Update your game" instead of strange errors. It is hard-coded in:
- `PartyProtocol.Version` (C#);
- `VERSION` in `rooms/room_service.py`;
- the `X-ShooterParty-Version` header in `deploy.sh` and `e2e_local.sh`.

Then deploy the server and hand out new client builds together.

Rebuilt network prefabs (any `ShooterParty > Rebuild core assets` or Knockback "rebuild everything") change their ids even without a version bump. Every build carries a fingerprint of its prefab ids (`PartyProtocol.Fingerprint`, sent in the join payload), and the game server turns a mismatch into "Update your game". So: build the Pi server and the clients from the same commit, and redeploy the server whenever you hand out new clients.
