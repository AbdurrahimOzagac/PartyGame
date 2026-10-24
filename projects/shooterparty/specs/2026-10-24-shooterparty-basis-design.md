# ShooterParty game basis: design spec

Date: 2026-10-24. Author: Ezel (with Claude). Status: approved by peer review (Opus, 2026-10-24) with the changes folded in below; spikes 1-2 run (§9).
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
| Server | Approach A: everything on the Pi, reached only through Cloudflare. The router is shared with the building, so no port forwarding is possible. |
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
 Pi, every listener bound to 127.0.0.1
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
| `POST /api/v1/rooms {name, replaceLeaversWithBots}` | 201 `{code, wsUrl, ticket, rejoinToken}` | 503 all rooms busy |
| `POST /api/v1/rooms/{code}/join {name}` | `{wsUrl, ticket, rejoinToken}` | 404 unknown code; 409 full; 409 in progress |
| `POST /api/v1/rooms/{code}/rejoin {rejoinToken}` | `{wsUrl, ticket}` for the same player | 404 unknown room or token |
| `GET /api/v1/status` | `{version, rooms: n, players: n, up}`, public, no codes | |

The detailed status (per-room phase, players, tick ms, RTT) is served on a **separate loopback port, 127.0.0.1:7709**, which the tunnel does not route. Every tunnel request also arrives from 127.0.0.1, so a source-address check on the public port would protect nothing. The owner reads it over SSH.

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

- Netcode for GameObjects **2.13.3** with Unity Transport **2.7.4**, pinned in the manifest. NGO alone would resolve 2.6.0, and 2.7.2 fixed a Linux IL2CPP server crash when a WebSocket client drops.
- Multiplayer Play Mode **2.0.2**, for development only.
- Stay on NGO 2.x. 3.x needs Unity 6000.7.
- **Transport per mode:**
  - online uses UTP WebSocket. TLS is terminated at Cloudflare, and the server runs plain `ws` on loopback. On the client, UTP connects in plain `ws` to an in-process **`WssBridge`** on 127.0.0.1 (below);
  - Local and Practice use UTP UDP.
- Network tick 30 Hz; physics fixed step 50 Hz.

### WssBridge (client only)

Stock UTP 2.7.4 writes `Host: <resolved IP:port>` in its WebSocket handshake (`Runtime/WebSocket/WebSocket.cs`, `Connect`). Cloudflare answers **403** to any request whose Host is an IP (verified with curl against ezellm.com). UTP's resolver also prefers IPv6, and Cloudflare publishes AAAA records.

The bridge (~100 lines of .NET) fixes both without forking UTP:
1. It listens on 127.0.0.1 on an ephemeral port.
2. It resolves the hostname itself, IPv4 first, and opens TCP to port 443 with `NoDelay` on both sockets.
3. It runs `SslStream.AuthenticateAsClientAsync(hostname)`, which gives proper SNI and system-CA validation.
4. It rewrites the `Host` line of the first request and then pipes bytes both ways untouched. Either side closing closes both.
5. A dev-only `delayMs` knob simulates latency, replacing the Multiplayer Tools simulator.

On the server side, UTP's handshake parser only requires that a Host header be present. It has a 2.9 KB buffer, and the path must match exactly. All of that fits what cloudflared forwards.

### Authority split, chosen for ~150 ms RTT

The research default was full server authority. It was rejected because NGO has no rigidbody prediction, so your own movement would lag by RTT plus the interpolation delay (~200 ms).

| Thing | Who simulates | Who decides | How it syncs |
|---|---|---|---|
| Your own critter's movement, jump, dash, facing | its owner client (dynamic rigidbody) | owner, within server sanity checks | `NetworkTransform` with Owner authority, interpolated on others (kinematic on non-owners) |
| Bot critters | server | server | `NetworkTransform` with Server authority |
| Dash and jump charges | owner | owner; the server only grants pickup charges, via RPC | not synced; the speed check bounds abuse |
| Shots (spit blobs) | server: physical blob; everyone: cosmetic blob | server | see "Shots" below |
| Hits, knockback, mass | server | server | `KnockbackRpc(Δv, newMass, roundEpoch)` to the victim's owner. Δv is computed with the server's mass, and the owner *sets* the mass, never subtracts. Mass is also a server `NetworkVariable` for HUDs |
| Critter-critter contact | nobody | n/a | **off in v1** (collision layer). Remote critters are kinematic proxies, so shoves would differ per screen. Revisit after the 150 ms playtest |
| Elimination, score, placement, crown | server | server | `NetworkVariable`s on the session and players |
| Pickups | server (trigger vs reported positions) | server | `NetworkVariable` availability plus a collect RPC to all |

### Shots

1. **Fire.** The shooter plays the spit animation, sound and pop at once, and launches a **cosmetic local blob**. It sends `FireRpc(origin, dir)`.
2. **Validate and spawn.** The server checks the cooldown against the **client's own fire timestamps**, so a TCP stall followed by a burst doesn't trip it. It checks that the origin is within `speed × lag + 0.5 m` of the shooter's latest received state. It spawns the authoritative physical blob (`SpitBlob` with collisions, server-only) and broadcasts `BlobSpawned(id, shooter, p0, v0, serverTime)`.
3. **Others' blobs.** Non-shooter clients spawn a cosmetic blob from `p0` and `v0`, advanced by the elapsed server time. The shooter's local blob blends onto the authoritative path over 0.12 s.
4. **Cosmetic blobs never decide anything.** They pass through critters, and they stop only on world geometry or when the server's hit event arrives.
5. **Hit.** The server's blob touches a critter (positions as last reported, collider +0.1 m generous) or the floor. The server applies the mass change and knockback and broadcasts `BlobHit(id, victim, point, normal)` or `BlobSplat(id, point)`.
6. **Hit effects.** The shooter gets `BlobHit` immediately, as hit confirmation. The victim plays it on `KnockbackRpc`. Observers get it once the victim's owner acknowledges applying the knockback, so the splat travels the same path as the victim's flight and they line up. Each client plays the absorb or splat, the hit-stop, the camera shake and the sound at the event's point, and the cosmetic blob snaps there. Floor splats go to everyone immediately.
7. **Victim's knockback.** If the victim is a human, the server sends `KnockbackRpc(Δv, newMass, epoch)` to that owner. The owner applies Δv as a velocity change, sets the mass, and drops events from an old round epoch. Bots are knocked back directly on the server.

**Accepted cost.** The victim sees the hit and the knockback about RTT/2 after the server's decision. A blob can look like it "just missed" or "just hit" by up to ~0.3 m of movement. The generous radius and the event-driven visuals keep this honest.

### Remote critter presentation

On non-owner copies the motor is disabled and the rigidbody is kinematic.
- Locomotion animation comes from position deltas.
- One-shot actions (spit, dash, jump, land) come from a tiny owner `ActionRpc(kind)` relayed to the others, so their FX and SFX play.
- HUDs and mass rings read the mass `NetworkVariable`, never `rb.mass`.

### Server sanity checks (anti-cheat sized for homework)

Online traffic is TCP: a lost packet stalls everything and then arrives in a burst, so checks keyed on arrival time misfire.
- **v1 logs violations and does not correct.** Correction teleports would also rubber-band honest players.
- **Movement:** speed is measured on owner timestamps. Above (dashSpeed × 1.3 + 2 m/s), outside recent knockback, it is logged. A kick happens only on gross abuse: more than 3× the cap sustained over 2 s of client time.
- **Rates:** token buckets per client with at least 2 s of burst (fire 6/s, other RPCs 20/s). Excess is dropped and logged; a kick only for sustained flooding (10× the rate for 5 s).
- **Elimination:** the server eliminates when the reported `y < -2.5`. A client that sits outside the arena footprint (plus margin) and is not falling for more than 1 s is also eliminated. A mid-round disconnect counts as a fall at that moment.

### Session and scene flow over the network

- One `NetworkManager` lives in a persistent bootstrap object.
- The server loads scenes with `NetworkSceneManager`, in single-scene mode: `SP_Lobby` → mini-game scene → `SP_Lobby` (Final shows in the lobby scene's podium area).
- Player critters are spawned by core per mini-game at the scene's spawn points: spawn as server, then hand ownership to the client. Core **despawns them explicitly before every scene load**, because NGO keeps objects spawned with `destroyWithScene=false` across Single-mode loads.
- Gameplay RPCs carry the round epoch, and input is gated on the phase.

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
  Critter/    CritterMotor, CritterAnimator, CritterSpit, SpitBlob, CritterFx, BlobShadow, LeaderCrown, NetworkCritter
  Shot/       ShotEffect (abstract ScriptableObject), KnockbackShotEffect lives in the mini-game
  Camera/     OverShoulderCamera, CameraShake
  Input/      LocalCritterInput (was PlaygroundPlayerInput)
  Audio/ FX/  Sfx, SfxBank, SfxPlayer, Fx, SplatDecals
  UI/         screens + HUD frame (section 6)
  Scenes/     SP_Boot, SP_Menu, SP_Lobby
  Docs/       ARCHITECTURE.md (incl. networking), ADDING_A_MINIGAME.md, SERVER_OPS.md
Assets/Ezel/KnockbackArena/            assembly KnockbackArena -> references ShooterParty
  Arena kit (models, KA/Ice shader, textures, sky), JumpPickup, KnockbackShotEffect, KnockbackArenaGame,
  ArenaBotBrain, Knockback HUD widgets (mass %, MassRing), scene KnockbackArena_Ice, its definition asset
projects/shooterparty/server/          room service, systemd units, cloudflared config, deploy script
```

- **Dependencies point one way:** mini-game → core. Core finds mini-games only through `MiniGameCatalog`, a ScriptableObject list of definitions.
- **Moving files: scripts only.** Each shared script moves to `ShooterParty/` with its `.meta`, renaming class and file together where the name changes. **The generated art stays where it is** (`KnockbackArena/Models, Animation, Materials, VFX, Audio, UI, Portraits, Prefabs`). Assets have no assembly dependency, and moving them would break `CubeCritterSetup.Root` (used by 5 generators) and the Blender export paths for no gain. `ARCHITECTURE.md` lists which art is shared. Moving it is a later cleanup.

### The mini-game contract

**`MiniGameDefinition`** (ScriptableObject) holds:
- `id`, `displayName`, `tagline`, `howToPlay` (2-3 lines), `controlsHint`, `icon`, `scene`;
- `minPlayers`/`maxPlayers`, `durationSeconds`, `placementPoints` (default `[4,3,2,1]`);
- `supportsBots`, `botBrain` (prefab), `critterPrefab` (defaults to core `NetworkCritter`; a variant may add parts), `shotEffect`, `hudWidget` (optional prefab);
- `critter` config: jump mode (off / pickup / on), dash charges, shot speed, gravity and cooldown, and charge time (0 = none).

**`ShotEffect`** (ScriptableObject):
- `ApplyServer(hit)`: effects on others, decided by the server.
- `OnFireLocal(shooter)`: owner-side, e.g. recoil steering.
- `OnOwnImpactLocal(shooter, point)`: owner-side self-effects such as blast-jumping off your own shot. A server-decided self-knockback would land a round trip plus the flight time late, which makes parkour unplayable.
- Knockback Arena uses only `ApplyServer`.

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
protected void RespawnAt(PartyPlayer p, Pose pose); // owner RPC teleport, server teleport for bots (checkpoints)
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

**Disconnects** (Cloudflare restarts and tunnel reconnects drop WebSockets, so this is routine, not an edge case):
- **Mid-round:** a disconnect counts as a fall at that moment. The slot and points are kept.
- **Rejoining:** the client auto-retries `rejoin` with the in-memory `rejoinToken` from its join response for 60 s. A rejoiner spectates until the next round. Rejoin is never by name, which would allow impersonation.
- **Bots:** with "replace leavers" on, a bot drives a slot whose human is gone, **from the next round start**.
- **Leaving mid-session:** if fewer than 2 humans remain, the session jumps to Final. A clean leave (menu → Leave) frees the slot at once.

**Edge rules:**
- A round ends when ≤ 1 critter is alive. Simultaneous falls share a rank.
- Placement points count from the top when fewer players are left: 3 players get 4/3/2.
- A slot whose human has left is not eligible for champion.
- Kick works in the lobby only.
- An eliminated top scorer keeps the crown, shown on the HUD card only.
- Going back to the lobby needs 3 humans again to start.

## 6. UI and UX

Visual language: the existing Fredoka type, 9-slice candy panels, Tomato/Teal/Chartreuse/Violet, Ease utilities and UI sounds.

**Screens, all keyboard and mouse navigable** (gamepad menu navigation is cut; gameplay keeps gamepad support):

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
  - `ReadWritePaths` for the service home, including Unity's `$HOME/.config/unity3d`. Unity runs with `-logFile -` so logs go to journald;
  - `RestrictAddressFamilies=AF_INET AF_INET6 AF_UNIX AF_NETLINK`, trimmed after testing;
  - `MemoryMax=1500M`, `CPUQuota=250%`, `TasksMax=512`, `Restart=on-failure`.
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
- **Status view:** the unrouted 127.0.0.1:7709 view gives the owner live detail (`ssh <ssh-host> partyshooter-status`).
- **In game:** the F3 overlay and the HUD connection dot.

## 8. Testing and verification

| Layer | What |
|---|---|
| EditMode (Unity) | `ScoreRules` (placements, ties, shared points, champion tie-breaks); playlist validation; name sanitiser; ticket sign/verify against **shared test vectors** also used by the Python tests; session state machine transitions with a fake clock; code alphabet |
| Python unittest | codes, tickets (same vectors), rate limits, request validation, room lifecycle with a fake server process (READY/STATE/END lines, crash, timeout) |
| PlayMode (Unity) | existing 10 Knockback tests adapted; Practice session runs end-to-end with bots and shortened timers (Lobby → 2 rounds → Final → Lobby); crown follows the score; bot replacement on a simulated disconnect |
| End-to-end (scripted) | macOS dedicated-server build plus 3 headless clients (`-autopilot`, bot-driven input) over WS on localhost, with the room service running locally. Asserts `@@END` and the standings. Then the same clients against the Pi through the tunnel. |
| Network feel | The WssBridge `delayMs` knob locally (150 ± 30 ms), plus real sessions through the tunnel. A captured video shows own-movement responsiveness and others' smoothness. |
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
  - `com.unity.transport` 2.7.4 (pinned);
  - `com.unity.multiplayer.playmode` 2.0.2;
  - `com.unity.toolchain.macos-arm64-linux` 1.1.0;
  - `com.unity.sdk.linux-arm64` 1.1.0.
- **Homebrew:** `cloudflared`, for the day-1 spike's quick tunnel.
- **Already installed (2026-10-24):** Hub modules `linux-server`, `linux-mono`, `windows-mono`, and `cloudflared` 2026.x.

## 9. Risks and day-1 spikes

Both spikes run before any framework work. Their outcome can change §2 and §3.

1. **UTP WebSocket through Cloudflare.** *Partly run (2026-10-24).*
   - **Found:**
     - Stock UTP sends `Host: <IP:port>`, and Cloudflare returns 403 for that (curl, verified). The peer reviewer found the same independently.
     - The fix is the client-side **WssBridge** (§3), and the reviewer agrees.
     - UTP's server-side handshake requirements fit cloudflared.
   - **Still to run:** the end-to-end check through a real Cloudflare tunnel. It is blocked until the owner approves opening a tunnel (the agent's permission system blocks public ingress by default).
   - **Fallback B:** a small custom NGO `NetworkTransport` over WebSocket, with `ClientWebSocket` on clients and an RFC 6455 server on loopback on the game server. UDP UTP stays for Local and Practice.
   - **Fallback C:** Unity Relay WSS.
2. **Unity Linux ARM64 server on the Pi.** *Passed (2026-10-24).*
   - **Build:** IL2CPP, cross-compiled on the Mac with `com.unity.toolchain.macos-arm64-linux` and `com.unity.sdk.linux-arm64` 1.1.0. Arm64 is selected with `UnityEditor.LinuxStandalone.UserBuildSettings.architecture = OSArchitecture.ARM64` (by reflection), plus `subtarget = Server`.
   - **First-run quirk:** the first batchmode build after adding the toolchain packages fails with "Unable to find an Linux Sysroot", because the packages are imported in that run. The second run succeeds. (Later found to be a different cause: Unity 6 keeps the Linux architecture per build profile, so the Arm64 setting landed on another profile and the build tried x64. `PartyBuild` now switches to the Linux server profile first.)
   - **Binary:** `GameAssembly.so` LOAD segments are aligned at 0x10000, so the **stock 16 KB kernel works and no kernel change is needed**. The shipped build is 91 MB.
   - **On the Pi:** an NGO WebSocket server on 127.0.0.1:7701 path `/g/1`, with a Mac client over an SSH forward. 828 pings in 30 s: p50 18 ms, p99 37 ms, max 66 ms. The server used **97 MB RSS and 1.8 % CPU**, and disconnected cleanly.
3. **Own-movement owner authority makes knockback on the victim arrive late** (~RTT/2). Mitigation: an instant hit-stop, flash and sound on the event, and tuning in the 150 ms simulator.
4. **School Wi-Fi blocking `*.ezellm.com` or doing TLS inspection.** The mitigation is Local play over a phone hotspot, tested before 16 Nov.
5. **Pi memory** (4 GB, shared with other services). `MemoryMax` stops the game from taking down other services. Spike 2 measured about 100 MB per idle room; this is re-measured with the real game.

## 10. Milestones (from the peer review)

| Milestone | Contents | Status / target |
|---|---|---|
| **M0** | commit the kit; spike 1 (with WssBridge, real tunnel); spike 2 | done, except spike 1's tunnel run |
| **M1, "one networked round"** | `NetworkCritter`, shots, knockback, elimination, remote presentation. Runs in Practice/Local UDP host and on the Pi over WSS with 3 autopilot clients, via a debug connect panel and one systemd slot with a dev ticket. 150 ms feel check. This is the Demo 1 fallback. | ~4 Nov |
| **M2** | session loop, preset playlists, crown, scoring; room service with tickets, codes and rejoin; menu, join and lobby screens; school Wi-Fi test | before 16 Nov |
| **M3** | playlist builder; standings and podium polish; settings; F3 overlay; bot replacement; sandbox hardening; docs; end-to-end test on the Pi | after Demo 1 |

## 11. Out of scope

- Matchmaking and quick play.
- Accounts, and anything saved server-side.
- Cosmetics and the shop.
- Comeback, underdog and dynamic-difficulty rules (only the seams exist).
- Other mini-games.
- Spectating a session from outside.
- A WebGL/mobile client.
- A landing or download page at `ezellm.com/partyshooter`.
- Fixing the Pi's unrelated services.
