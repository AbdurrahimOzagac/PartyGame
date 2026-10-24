# ShooterParty game basis: design spec

Date: 2026-10-24. Author: Ezel (with Claude). Status: draft for peer review.
Research behind this spec, with sources: `projects/shooterparty/research/` (netcode brief, backend and security brief). The survey of the Pi is kept private, off-repo.

## 1. Goal

ShooterParty is a party game made of mini-games. Every mini-game uses the same cube critters and the same shot. Knockback Arena is the only mini-game today. Teammates will add more later, such as a race and a blast parkour.

This work delivers the **game basis**:
- a polished menu and front end;
- online rooms with party codes on our own server;
- a lobby with playlists;
- session scoring and a crown;
- a modular, documented framework that new mini-games plug into.

Knockback Arena is ported onto the basis as the first mini-game.

The game is graded school homework. The bar is polished UI, UX and HUD, few bugs, and smooth play at the measured latency.

### What the owner decided (2026-10-24)

| Topic | Decision |
|---|---|
| Joining | Party codes, no matchmaking. Someone creates a room; the lobby waits for at least 3 players; 4 is full. |
| Playlists | The room leader picks a built-in playlist or builds one in the lobby and names it. Nothing is saved between sessions. |
| Scoring | Each mini-game awards points. The most points over the session makes you champion. |
| Crown | During a mini-game, the player with the highest score *in that game* wears the crown, so everyone knows whom to target. |
| Identity | No accounts. A name is typed when joining. Nothing is stored server-side. |
| Bots | "Replace leavers with bots" is an option set when the room is created. |
| Platforms | macOS, Windows and Linux desktop clients. A Linux ARM64 dedicated server runs on the Pi. |
| Where played | Both at home and on school Wi-Fi for graded demos. School Wi-Fi is the strict case. |
| Code home | Everything stays under `Assets/Ezel/`. |
| Server | Approach A: everything on the Pi (trok), reached only through Cloudflare. The router is shared with the building, so no port forwarding is possible. |
| Later, not now | Comeback and underdog advantages, dynamic difficulty, cosmetics shop. The architecture leaves documented places for them. |

### Measured constraints

- The Pi 5 has 4 GB RAM and 4 cores. About 3.5 cores and about 2.3 GB are free.
- It runs Debian 13 with glibc 2.41 and the 16 KB-page kernel. It is on Wi-Fi and already runs other services.
- `ezellm.com` reaches the Pi only through a Cloudflare Tunnel. There are no inbound ports and no IPv6.
- Round trip through Cloudflare (Amsterdam edge) to the Pi and back: **110-230 ms, typically ~150 ms**, measured from the home LAN with keep-alive HTTPS. It is about the same for players elsewhere in Turkey.
- Cloudflare Tunnel public hostnames carry HTTP and WebSocket only, not UDP.

### Success criteria

1. **Online session:** three or four people on different networks complete a session against the Pi: create, join by code, lobby, playlist, play, results, standings and champion. The networks include one that blocks UDP.
2. **Feel:** at ~150 ms RTT, your own movement and shooting respond instantly. Others move smoothly. Hit reactions arrive within one round trip, with no rubber-banding during normal play.
3. **Local play:** works with no internet (one laptop hosts on Wi-Fi or a hotspot). Practice works fully offline with bots.
4. **Adding a mini-game:** a new mini-game needs only its own folder, assembly, scene, definition asset and controller. No core edits. `ADDING_A_MINIGAME.md` walks through it.
5. **Security:** the server exposes nothing but HTTPS/WSS via Cloudflare. A client cannot join without a valid ticket, and cannot change scores, mass or eliminations directly.
6. **Tests and builds:** automated tests pass. An end-to-end scripted session completes against a local server and against the Pi.

## 2. System architecture

```
 Player's game (macOS / Windows / Linux)
   │ 1. HTTPS  partyshooter.ezellm.com/api/v1/...   create room / join by code -> ticket
   │ 2. WSS    partyshooter.ezellm.com/g/1          lobby + games + results (Netcode for GameObjects)
   ▼
 Cloudflare edge ── tunnel "partyshooter" (its own credentials, separate from the website tunnel)
   ▼
 Pi (trok), every listener bound to 127.0.0.1
   ├─ room service     127.0.0.1:7700   Python 3.13 standard library only
   ├─ game server #1   127.0.0.1:7701   headless Unity (Linux ARM64 Dedicated Server), path /g/1
   └─ game server #2   127.0.0.1:7702   path /g/2      (hard cap: 2 rooms)
```

### Room service

About 250 lines of Python with no third-party packages. It runs as a sandboxed systemd service and keeps all state in memory.

**What it does:**
- **Room codes:** issues them; see Codes below.
- **Tickets:** signs one-time join tickets (§7, item 2).
- **Slots and processes:** assigns a free slot (1 or 2) and starts one game-server process per room. It waits for the server's `READY` line and reaps the process when it exits.
- **Room state:** reads the state each server prints to stdout, so it knows the player count and phase for join decisions.
- **Limits:** rate-limits and size-limits every request.

**Codes:** 5 letters from `BCDFGHJKLMNPQRSTVWXZ` (20 consonants, no vowels, no digits). That gives 3.2 M codes. A closed code is not reused for 10 minutes.

**API** (JSON; every request carries `X-ShooterParty-Version`; a mismatch returns 426 "update your game"):

| Endpoint | Returns | Errors |
|---|---|---|
| `POST /api/v1/rooms {name, replaceLeaversWithBots}` | 201 `{code, wsUrl, ticket}` | 503 all rooms busy |
| `POST /api/v1/rooms/{code}/join {name}` | `{wsUrl, ticket}` | 404 unknown code; 409 full; 409 in progress (unless `name` matches a disconnected player, which is a rejoin) |
| `GET /api/v1/status` | `{version, rooms: n, players: n, up}`, public, no codes | |
| `GET /api/v1/status?detail=1` | per-room phase, players, tick ms, RTT | only from 127.0.0.1, for the owner over SSH/Tailscale |

### Game server

The same Unity project, built as a Linux ARM64 Dedicated Server (IL2CPP). Each session runs in its own process.

- **Command line:** `-slot N -port 77xx -wsPath /g/N -room CODE -minPlayers 3 -replaceLeavers 0|1`. The ticket secret comes from the environment.
- **Authority:** it is the authority for the lobby, the session flow, mini-game rules, hits, mass, eliminations, scores and the crown.
- **Exit:** the process exits when the session is over and everyone has left, when it has been empty for 60 s, when the lobby has been idle for 30 min, or at a hard cap of 2 h.
- **Stdout protocol** for the room service (one JSON per line, prefixed `@@`):
  - `@@READY`
  - `@@STATE {phase, players, connected}`, on change
  - `@@STATS {tickMs, players, rttAvgMs}`, every 10 s
  - `@@END`

### Other modes, same code path

- **Local play:** one client calls NGO `StartHost` on UDP 7777 and shows its LAN IP(s) in the lobby. The others type that address. There is no room service, no tickets and no internet. This is the demo plan B.
- **Practice:** `StartHost` bound to 127.0.0.1, with bots in the free slots (`minPlayers` 1). It is solo play, and the default way to test a mini-game.

## 3. Netcode model

### Stack

- Netcode for GameObjects **2.13.3** with Unity Transport **2.7.4** (resolved by NGO).
- Multiplayer Play Mode **2.0.2**, for development only.
- Stay on NGO 2.x. 3.x needs Unity 6000.7.
- **Transport per mode:**
  - online uses UTP WebSocket with TLS terminated at Cloudflare, so the client uses `wss` to 443 and the server plain `ws` on loopback;
  - Local and Practice use UTP UDP.
- Network tick 30 Hz; physics fixed step 50 Hz.

### Authority split, chosen for ~150 ms RTT

The research default was full server authority. It was rejected because NGO has no rigidbody prediction, so your own movement would lag by RTT plus the interpolation delay (~200 ms).

| Thing | Who simulates | Who decides | How it syncs |
|---|---|---|---|
| Your own critter's movement, jump, dash, facing | its owner client (dynamic rigidbody) | owner, within server sanity checks | `NetworkTransform` with Owner authority, interpolated on others (kinematic on non-owners) |
| Bot critters | server | server | `NetworkTransform` with Server authority |
| Dash and jump charges | owner, predicted | server | the owner sends `UseDash`/`UseJump` RPCs; the server keeps the count in a `NetworkVariable`, and a rejected use corrects the owner |
| Shots (spit blobs) | server: physical blob; everyone: cosmetic blob | server | see "Shots" below |
| Hits, knockback, mass | server | server | knockback goes to the victim's owner via RPC; mass is a server `NetworkVariable` |
| Elimination, score, placement, crown | server | server | `NetworkVariable`s on the session and players |
| Pickups | server (trigger vs reported positions) | server | `NetworkVariable` availability plus a collect RPC to all |

### Shots

1. **Fire.** The shooter plays the spit animation, sound and pop at once, and launches a **cosmetic local blob**. It sends `FireRpc(origin, dir)`.
2. **Validate and spawn.** The server checks the cooldown and that the origin is within 1.5 m of its view of the shooter. It spawns the authoritative physical blob (`SpitBlob` with collisions, server-only) and broadcasts `BlobSpawned(id, shooter, p0, v0, serverTime)`.
3. **Others' blobs.** Non-shooter clients spawn a cosmetic blob from `p0` and `v0`, advanced by the elapsed server time. The shooter's local blob blends onto the authoritative path over 0.12 s.
4. **Cosmetic blobs never decide anything.** They pass through critters, and they stop only on world geometry or when the server's hit event arrives.
5. **Hit.** The server's blob touches a critter (positions as last reported, collider +0.1 m generous) or the floor. The server applies the mass change and knockback and broadcasts `BlobHit(id, victim, point, normal)` or `BlobSplat(id, point)`.
6. **Hit effects.** Every client plays the absorb or splat, the hit-stop, the camera shake and the sound at the event's point. The cosmetic blob snaps to that point.
7. **Victim's knockback.** If the victim is a human, the server sends `KnockbackRpc(impulse, newMass)` to that owner, and the owner applies it to its own rigidbody. Bots are knocked back directly on the server.

**Accepted cost.** The victim sees the hit and the knockback about RTT/2 after the server's decision. A blob can look like it "just missed" or "just hit" by up to ~0.3 m of movement. The generous radius and the event-driven visuals keep this honest.

### Server sanity checks (anti-cheat sized for homework)

- **Movement:** speed above (moveSpeed or dashSpeed) × 1.3 + 2 m/s, outside recent knockback, triggers a correction to the last good position. Three corrections in 5 s means a kick. A teleport of more than 3 m between ticks is also a correction.
- **Rates:** RPCs are capped per client per second (fire 6, dash 4, others 10). Excess is dropped; persistent excess means a kick.
- **Elimination:** the server eliminates when the reported `y < -2.5`. A client that sits outside the arena footprint (plus margin) and is not falling for more than 1 s is also eliminated.

### Session and scene flow over the network

- One `NetworkManager` lives in a persistent bootstrap object.
- The server loads scenes with `NetworkSceneManager`, in single-scene mode: `SP_Lobby` → mini-game scene → `SP_Lobby` (Final shows in the lobby scene's podium area).
- Player critters are spawned by core per mini-game at the scene's spawn points, owned by their client. They are despawned on scene change.

### Bandwidth

| Direction | Budget |
|---|---|
| Server up, per client | ≤ 60 kbps (4 transforms at 30 Hz with NGO thresholds and half-float, plus events) |
| Whole server up, 8 players | < 1 Mbps |

These are budgets to measure with the stats overlay (§7).

## 4. Game framework (modules)

```
Assets/Ezel/ShooterParty/              assembly ShooterParty (runtime), ShooterParty.Editor, ShooterParty.Tests
  Session/    PartyPlayer, SessionManager (state machine), Playlist, ScoreRules, CrownController, BotReplacement
  MiniGames/  MiniGameDefinition, MiniGameCatalog, MiniGameController (base), PlayerModifiers
  Net/        Bootstrap, ConnectionFlow (online/local/practice), RoomServiceClient, Ticket, ConnectionApproval, NetStats
  Critter/    CritterMotor, CritterAnimator, CritterShooter (was CritterSpit), SpitBlob, CritterFx, BlobShadow, NetworkCritter, BotDriver
  Shot/       ShotEffect (abstract ScriptableObject), KnockbackShotEffect lives in the mini-game
  Camera/     OverShoulderCamera, CameraShake
  Input/      PlayerInputDriver (was PlaygroundPlayerInput)
  Audio/ FX/  Sfx, SfxBank, SfxPlayer, Fx, SplatDecals
  UI/         screens + HUD frame (section 6)
  Art/        shared generated assets: CubeCritter + SpitBlob models, animations, critter/goo materials,
              shared VFX, SFX + music, fonts, UI sprites, portraits, crown
  Scenes/     SP_Boot, SP_Menu, SP_Lobby
  Docs/       ARCHITECTURE.md, ADDING_A_MINIGAME.md, NETWORKING.md, SERVER_OPS.md
Assets/Ezel/KnockbackArena/            assembly KnockbackArena -> references ShooterParty
  Arena kit (models, KA/Ice shader, textures, sky), JumpPickup, KnockbackShotEffect, KnockbackArenaGame,
  ArenaBotBrain, Knockback HUD widgets (mass %, MassRing), scene KnockbackArena_Ice, its definition asset
projects/shooterparty/server/          room service, systemd units, cloudflared config, deploy script
```

- **Dependencies point one way:** mini-game → core. Core finds mini-games only through `MiniGameCatalog`, a ScriptableObject list of definitions.
- **Moving files:** each file moves with its `.meta`, so references keep working. Editor generator scripts move with what they generate: critter, VFX, audio and UI builders go to `ShooterParty.Editor`, the arena builder stays in `KnockbackArena.Editor`. The Blender scripts' export paths are updated to match.

### The mini-game contract

**`MiniGameDefinition`** (ScriptableObject) holds:
- `id`, `displayName`, `tagline`, `howToPlay` (2-3 lines), `controlsHint`, `icon`, `scene`;
- `minPlayers`/`maxPlayers`, `durationSeconds`, `placementPoints` (default `[4,3,2,1]`);
- `supportsBots`, `botBrain` (prefab), `critterPrefab` (defaults to core `NetworkCritter`; a variant may add parts), `shotEffect`, `hudWidget` (optional prefab).

**`MiniGameController`** (server-side NetworkBehaviour base, one per mini-game scene) has these hooks:

```csharp
protected virtual void OnSetup(IReadOnlyList<PartyPlayer> players)  // critters spawned, modifiers applied
protected virtual void OnRoundStart()                               // after the countdown
protected virtual void OnServerTick(float dt)
protected virtual IReadOnlyList<PartyPlayer> RankPlayers()          // final order; default: by Score desc
// helpers the game calls:
protected void AddScore(PartyPlayer p, int n); protected void SetScore(PartyPlayer p, int n);
protected void Eliminate(PartyPlayer p);         // core: out banner, spectate cam, bot stops
protected void EndRound();                       // core: ranking -> points -> results
public Transform[] SpawnPoints;
```

**Core does for every mini-game:**
- the intro card and countdown;
- the round timer, which calls `EndRound` at `durationSeconds`;
- the live crown on the highest in-game `Score` (no crown while everyone is at 0; ties keep the current wearer);
- elimination and spectating;
- results, points and standings;
- bot replacement;
- input, camera, audio and network stats.

**Extension seams for later mechanics.** Documented, with no rules shipped now:
- `ScoreRules.Award(roundRanking, standings)` is the one place round placement becomes session points. Comeback rules such as "last overall +1" or "ties go to the underdog" get added there.
- `PlayerModifiers` (moveSpeed, mass, cooldown and knockbackTaken multipliers, all 1 by default) is computed by `ScoreRules.ModifiersFor(player, standings)` before each round and applied by core to the critter. Dynamic difficulty and underdog advantages plug in there.

## 5. Session flow, scoring, crown

**Server state machine (`SessionManager.Phase`):**

```
Lobby -> Intro(i) -> Countdown -> Playing -> RoundResults -> Standings -> (i+1 < n ? Intro(i+1) : Final) -> Lobby
```

**Phase timings:**

| Phase | Duration |
|---|---|
| Intro card | 4 s |
| Countdown | 3 s |
| Round results | 6 s |
| Standings | 6 s |
| Final | 12 s |

**Lobby:**
- The creator is the **leader**, shown with a star. If the leader leaves, leadership passes to the longest-present player.
- **The leader can:**
  - pick a built-in playlist or build one;
  - toggle "Replace leavers with bots", preset at creation;
  - kick a player;
  - start, once ≥ `minPlayers` (3) humans are present and all are ready.
- **Players can:** set ready, and cycle to a free colour (Tomato, Teal, Chartreuse, Violet; slot colour by default).
- **Building a playlist:** add a mini-game from the catalog, remove it, reorder it, set a name. The length is 1-10, repeats are allowed, and the leader can shuffle the order. The server validates every mini-game id against the catalog.
- **Built-in playlists (assets):**
  - "Quick" (3 games);
  - "Classic" (5 games);
  - "Marathon" (8 games).

  With one mini-game, these are Knockback Arena repeated.
- **Joining after start:** a session in progress cannot be joined, except as a rejoin.

**Scoring:**
- Each round produces a ranking, via `RankPlayers`, then `placementPoints`. Tied ranks share the higher points.
- Session total = the sum over rounds.
- **Champion** = the highest total. Tie-breaks: more round wins, then the better placement in the last round, otherwise shared champions.

**Crown:** the live in-game leader, as defined in §4. It is shown on the critter ("Top" bone, existing `LeaderCrown`) and as a crown icon on the HUD card.

**Knockback Arena rules on the basis:**
- **Score:** +1 per knockout. A KO is credited to the last critter that hit the victim within 4 s before they fell, so the crown goes to the top KO scorer.
- **Ranking:** by survival. The last one standing is first. Earlier falls rank lower; ties break by KOs, then by mass left.
- **Round end:** one critter is left, or the 120 s timer runs out. At the timer, the remaining critters rank by KOs, then mass.
- The existing tuning carries over: 10 % mass loss per hit, 3 dashes, the jump pickup.

**Disconnects:**
- **Losing the connection:** the player's slot is held for 60 s, and their points stay.
- **Bots:** with "replace leavers" on, a bot drives their critter from the next round start. If they are mid-round, the bot takes over immediately.
- **Rejoining:** the client auto-retries the join for 30 s with the remembered code and name (in memory only). Rejoining by the same name reclaims the slot.
- **Leaving mid-session:** if fewer than 2 humans remain, the session jumps to Final. A clean leave (menu → Leave) frees the slot at once.

## 6. UI and UX

Visual language: the existing Fredoka type, 9-slice candy panels, Tomato/Teal/Chartreuse/Violet, Ease utilities and UI sounds.

**Screens, all keyboard, mouse and gamepad navigable:**

| Screen | Contents |
|---|---|
| Boot | Logo sting (~1.5 s, skippable) |
| Main menu (`SP_Menu`) | 3D backdrop with idle critters on the floe. Play Online (Create Room / Join Room), Local Play (Host / Join by address), Practice, Settings, Quit. A name field is shown when creating or joining (2-16 chars, letters, digits, space, `_-`; prefilled from the last local name). |
| Joining | A progress card for each step: Contacting server, Joining room, Loading. Errors: unknown code, room full, in progress, all rooms busy, server unreachable (offers Local Play), version mismatch. |
| Lobby (`SP_Lobby`, networked) | Large party code with a copy button. Four slots shown as 3D critters on pedestals with name tags, ready checks (critter plays Happy), leader star and ping dot. Playlist panel (presets + builder), options, Start (leader) / Ready (others), Leave. Local play shows the host IP instead of a code. |
| Intro card | Mini-game name, tagline, how-to-play, controls, round x of n, then 3-2-1-GO. |
| HUD frame | Top cards (portrait face, name, game score, crown icon, connection dot, mini-game stat line such as "mass 80 %"), timer, game name, crosshair + cooldown ring, dash pips, jump slot, toasts/banners (OUT!, KO by X), spectate label when out. The mini-game widget slot sits under the cards. |
| Pause (Esc / Start) | Resume, Settings, Leave room (confirm). The game keeps running; it's online. |
| Round results | Ranking with points earned flying into totals. |
| Standings | Animated bar race of totals, the next game's card. |
| Final | Champion podium (existing Podium_Ice), crown, confetti, fanfare. Back to lobby (same room, ready reset) / Leave. |
| Settings | Master / music / SFX volume, mouse sensitivity, invert Y, display mode, resolution, vsync, show net stats. Stored locally in PlayerPrefs (device-local settings are not "saving" in the owner's sense). |
| Toasts / dialogs | Connection lost (with auto-rejoin progress), player joined/left/replaced by bot, kicked. |
| Net stats overlay (F3) | RTT, jitter, server tick ms, send/receive kbps. The HUD connection dot is green < 120 ms, amber < 220 ms, red otherwise. |

**Transitions:** 0.25 s fades, with music crossfading between lobby and game.

The UI never blocks on the network without a visible state. Every wait has a spinner, a step label and a cancel.

## 7. Server operations and security

### Deployment (Pi)

- **System user** `partyshooter`: no login, no shell, home `/mnt/apps/partyshooter` (on the SSD; the SD card is 73 % full).
- **Files:**
  - `/mnt/apps/partyshooter/server/`: the Unity ARM64 server build;
  - `/mnt/apps/partyshooter/rooms/`: the room service;
  - `/etc/partyshooter/secret`: 32 random bytes, `root:partyshooter 0640`. It is never in git.
- **`partyshooter-rooms.service`** runs as that user and starts the game servers as children. Sandboxing:
  - `NoNewPrivileges`, `ProtectSystem=strict`, `ProtectHome`, `PrivateTmp`;
  - `ReadWritePaths` for logs only, `RestrictAddressFamilies=AF_INET AF_UNIX`;
  - `MemoryMax=1500M`, `CPUQuota=250%`, `TasksMax`, `Restart=on-failure`.
- **`cloudflared-partyshooter.service`** runs a named tunnel `partyshooter` from `/etc/cloudflared/partyshooter.yml`. Ingress:
  - `partyshooter.ezellm.com` path `^/api/` → `http://127.0.0.1:7700`;
  - `^/g/1$` → `:7701`;
  - `^/g/2$` → `:7702`;
  - everything else `http_status:404`.

  The website tunnel is untouched.
- **One-time owner action:** `cloudflared tunnel login` (one browser click). Then `tunnel create` and `tunnel route dns` run, and `cert.pem` is deleted afterwards; only the tunnel credential file stays.
- **Deploy:** `projects/shooterparty/server/deploy.sh` rsyncs over Tailscale SSH, installs the units, and restarts the service.
- **Kill switches:**
  - `systemctl stop cloudflared-partyshooter`: the game goes offline and the website stays up;
  - `stop partyshooter-rooms`;
  - delete the DNS record.

### Security measures (priority order)

1. **No inbound ports.** Everything binds 127.0.0.1, and only the game tunnel routes to it, by exact paths.
2. **Signed join tickets.** The ticket is `base64url(json{v, room, pid, name, leader, exp})` + "." + `base64url(HMAC-SHA256(secret, payload))`, with a 60 s expiry, single use (the server remembers `pid`s), and the room must match. NGO connection approval rejects anything else, including payloads over 1 KB.
3. **Server authority** over every result (§3), plus the sanity checks and per-client RPC rate caps.
4. **Input limits:**
   - names are trimmed, NFC-normalised and checked against an allowed-character regex; `<`, `>` and control characters are rejected (TMP rich text); duplicates get " 2";
   - playlist names: 24 characters, same charset;
   - request bodies ≤ 2 KB; JSON only;
   - unknown fields are ignored and missing ones rejected.
5. **Rate limits** in the room service, keyed by `CF-Connecting-IP` (trusted only because the listener is loopback behind cloudflared):
   - create: 6 per 10 min;
   - join attempts: 30 per 10 min;
   - failed code lookups: 10 per minute, after which 429 for 5 minutes;
   - a global cap of 2 rooms × 4 players.

   The limits are loose enough for a classroom sharing one public IP.
6. **The sandboxed service** above, and separate tunnel credentials.
7. **Secrets** are only on the Pi. The repo holds a `secret.example`. Automatic security updates stay on.
8. **Cloudflare zone:** Bot Fight Mode and "Under Attack" must be off, since a Unity client can't solve challenges. This is verified with `curl` and a real client after setup.

This repo is public. Nothing that describes the Pi's other services, its addresses, or its credentials goes into it. The private Pi survey stays off-repo.

### Observability

- **Room service log:** structured JSON lines to journald: room created/closed, join ok/denied (reason), server exit codes, rate-limit hits.
- **Game server log:** `@@STATS` every 10 s (tick ms avg/max, players, RTT avg/max, kbps), plus warnings for tick overruns (> 20 ms) and for corrections and kicks.
- **Status view:** `status?detail=1` from localhost gives the owner a live view (`ssh trok partyshooter-status`).
- **In game:** the F3 overlay and the HUD connection dot.

## 8. Testing and verification

| Layer | What |
|---|---|
| EditMode (Unity) | `ScoreRules` (placements, ties, shared points, champion tie-breaks); playlist validation; name sanitiser; ticket sign/verify against **shared test vectors** also used by the Python tests; session state machine transitions with a fake clock; code alphabet |
| Python unittest | codes, tickets (same vectors), rate limits, request validation, room lifecycle with a fake server process (READY/STATE/END lines, crash, timeout) |
| PlayMode (Unity) | existing 10 Knockback tests adapted; Practice session runs end-to-end with bots and shortened timers (Lobby → 2 rounds → Final → Lobby); crown follows the score; bot replacement on a simulated disconnect |
| End-to-end (scripted) | macOS dedicated-server build plus 3 headless clients (`-autopilot`, bot-driven input) over WS on localhost, with the room service running locally. Asserts `@@END` and the standings. Then the same clients against the Pi through the tunnel. |
| Network feel | UTP Simulator in development builds at 150 ± 30 ms and 1 % loss. A captured video shows own-movement responsiveness and others' smoothness. |
| Manual | the owner on Mac plus teammates on Windows; one session from school Wi-Fi before 16 Nov |

### Builds

A menu item, ShooterParty/Build/All, produces:
- macOS (Apple Silicon, Mono);
- Windows x64 (Mono);
- Linux x64 (Mono);
- the Linux ARM64 Dedicated Server (IL2CPP).

Output goes to `Builds/` (gitignored). Windows and Linux desktop builds can't be run here; they need one manual check.

### Tooling to install (on the Mac)

- **Unity Hub modules:** `linux-server`, `linux-mono`, `windows-mono`.
- **UPM packages in the team manifest:**
  - `com.unity.netcode.gameobjects` 2.13.3;
  - `com.unity.multiplayer.playmode` 2.0.2;
  - `com.unity.toolchain.macos-arm64-linux` 1.1.0;
  - `com.unity.sdk.linux-arm64` 1.1.0.
- **Homebrew:** `cloudflared`, for the day-1 spike's quick tunnel.

## 9. Risks and day-1 spikes

Both spikes run before any framework work. Their outcome can change §2 and §3.

1. **UTP WebSocket through Cloudflare.**
   - **Test:** a minimal NGO server (Mac, `ws` on 127.0.0.1:7701, path `/g/1`) and client (`wss://<quick-tunnel>.trycloudflare.com/g/1`), exchanging RPCs for 5 minutes. Measure RTT and check the hostname handling: TLS SNI/common name, the `Host` header, the path.
   - **Fallback B:** a small custom NGO `NetworkTransport` over WebSocket, with `ClientWebSocket` on clients and an RFC 6455 server on loopback on the game server. UDP UTP stays for Local and Practice.
   - **Fallback C:** Unity Relay WSS.
2. **Unity Linux ARM64 server on the Pi.**
   - **Test:** an IL2CPP Dedicated Server cross-compiled on the Mac, run on trok (16 KB pages), loading a physics scene and accepting a client over the tunnel.
   - If it crashes on 16 KB pages: ask the owner to switch to `kernel8.img` (4 KB pages, affects every service on the Pi).
   - If that is refused or fails, run the game server on a Mac/Linux machine as a stopgap, keeping the room service on the Pi.
3. **Own-movement owner authority makes knockback on the victim arrive late** (~RTT/2). Mitigation: an instant hit-stop, flash and sound on the event, and tuning in the 150 ms simulator.
4. **School Wi-Fi blocking `*.ezellm.com` or doing TLS inspection.** The mitigation is Local play over a phone hotspot, tested before 16 Nov.
5. **Pi memory** (4 GB with 10 containers). `MemoryMax` stops the game from taking down other services. Real RSS per room is measured in spike 2.

## 10. Out of scope

- Matchmaking and quick play.
- Accounts, and anything saved server-side.
- Cosmetics and the shop.
- Comeback, underdog and dynamic-difficulty rules (only the seams exist).
- Other mini-games.
- Spectating a session from outside.
- A WebGL/mobile client.
- A landing or download page at `ezellm.com/partyshooter`.
- Fixing the Pi's unrelated services.
