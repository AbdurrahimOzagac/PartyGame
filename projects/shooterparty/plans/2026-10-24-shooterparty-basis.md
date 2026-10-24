# ShooterParty Game Basis Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** turn the single-player Knockback Arena demo into ShooterParty. That means:
- a networked party game: menu, party-code rooms on the Pi, lobby, playlists, scoring and crown;
- a modular mini-game framework, with Knockback Arena as its first mini-game.

**Architecture:**
- **Netcode:** Netcode for GameObjects with owner-authoritative movement. The server decides hits, mass, eliminations, scores and the crown.
- **Server:** one headless Unity ARM64 server process per room on the Pi, behind a separate Cloudflare tunnel (WSS). A Python standard-library room service hands out codes and signed tickets.
- **Clients:** they reach Cloudflare through an in-process `WssBridge`, which fixes UTP's IP `Host` header. Local play and Practice use UDP host mode on the same code path.

**Tech Stack:**
- Unity 6000.3.9f1 with URP 17.3 and Input System;
- NGO 2.13.3 and UTP 2.7.4;
- TextMeshPro/UGUI;
- Python 3.13 standard library;
- systemd and cloudflared on Debian 13 (Pi 5).

**Spec:** `projects/shooterparty/specs/2026-10-24-shooterparty-basis-design.md` (read it first; this plan argues from it).

## Global Constraints

**Engine and packages**
- Unity **6000.3.9f1**, URP 17.3, Input System only (no legacy Input).
- Manifest pins:
  - `com.unity.netcode.gameobjects` **2.13.3**;
  - `com.unity.transport` **2.7.4**;
  - `com.unity.multiplayer.playmode` **2.0.2**;
  - `com.unity.toolchain.macos-arm64-linux` **1.1.0**;
  - `com.unity.sdk.linux-arm64` **1.1.0**.
- No NGO 3.x, no `com.unity.multiplayer.tools`, no third-party packages.

**Code layout**
- All Unity code lives under `Assets/Ezel/`:
  - core in `Assets/Ezel/ShooterParty/` (namespace `ShooterParty`, UI in `ShooterParty.UI`);
  - the mini-game in `Assets/Ezel/KnockbackArena/` (namespace `KnockbackArena`).
- Assembly references go **KnockbackArena → ShooterParty only**.
- Generated art stays where it is; only scripts move, each with its `.meta`.

**Network values**
- Online endpoints are `https://partyshooter.ezellm.com/api/v1/...` and `wss://partyshooter.ezellm.com/g/{1|2}`.
- Pi listeners bind **127.0.0.1**: rooms `7700`, games `7701`/`7702`, status detail `7709` (not routed).
- Protocol version `1`, sent as header `X-ShooterParty-Version: 1` and in the join payload.
- Network TickRate **30**, physics fixed step **0.02**.

**Game rules**
- Party code: 5 chars from `BCDFGHJKLMNPQRSTVWXZ`, case-insensitive input, spaces and dashes stripped.
- Players: `minPlayers` **3** humans to start online and in Local (1 in Practice), max **4**.
- Names: 2-16 chars of letters, digits, space, `_` and `-`, NFC, trimmed. Reject `<`, `>` and control, zero-width and bidi characters. Duplicates get the suffix `" 2"`, `" 3"`.
- Playlist: name ≤ 24 chars, same charset; 1-10 entries, each a catalog id; repeats allowed.
- Ticket: `base64url(payloadJson) + "." + base64url(HMAC-SHA256(secret, payloadBytes))`. Payload is `{v, room, pid, name, leader, exp}`, exp = issue + 60 s, single use per `pid`+`exp`.

**Timings and scoring**
- Session phases: Intro 4 s, Countdown 3 s, RoundResults 6 s, Standings 6 s, Final 12 s.
- Knockback round: 120 s. Placement points `[4,3,2,1]`, tied ranks share the higher value.
- Knockback tuning: KO credit window 4 s, out at `y < -2.5`, mass loss 10 % per hit.

**Workflow rules**
- Run Unity batchmode only in the scratch copy `scratchpad/unity_work/PartyGame`, then rsync `Assets/Ezel/`, `Packages/manifest.json`, `Packages/packages-lock.json` and `ProjectSettings/EditorBuildSettings.asset` back to the repo.
- After any repo → scratch sync, run `Knockback Arena/Rebuild Everything`.
- Commits use the user's identity with **no Claude trailers**. Never commit secrets, IPs or Pi service details. Never push.

## Review Focus

1. **A WebSocket drop mid-round.** Cloudflare restarts and tunnel reconnects cause these.
   - Expected: the client auto-rejoins within 60 s, spectates until the next round and keeps its points. Other players see a "reconnecting" toast and the round does not stall.
   - Pinned by `e2e_local.sh` scenario `drop` (Task 17).
2. **The leader leaving.**
   - In the lobby: leadership passes to the longest-present player, whose Start button appears.
   - Mid-session: play continues.
   - Pinned by `SessionFlowTests.LeaderLeavesPassesLeadership` (Task 10).
3. **Two players typing the same name.** The second becomes "Name 2" everywhere: cards, crown toast, results.
   - Pinned by `NameRulesTests.DuplicateGetsSuffix` (Task 2) and `SessionFlowTests.DuplicateNamesAreSuffixedOnJoin` (Task 10).
4. **A player leaving during a scene load or the results.** Expected: no exceptions or stuck phase, the slot is freed or held as the spec says, and the others continue.
   - Pinned by `e2e_local.sh` scenario `leave-during-load` (Task 11).
5. **A wrong code, or the server down or outdated.**
   - Expected: a clear message ("No room with code BCDFG", "Server unreachable — try Local Play", "Update your game"), back on the join screen with the typed name and code still there.
   - Pinned by `RoomServiceClientTests.MapsErrors` (Task 13).

---

## M1: foundations and one networked round

### Task 1: Packages, assemblies and the script move

**Files:**
- Modify: `Packages/manifest.json` (the pins above).
- Create assembly definitions:
  - `Assets/Ezel/ShooterParty/Scripts/ShooterParty.Runtime.asmdef`. References: `Unity.Netcode.Runtime`, `Unity.Networking.Transport`, `Unity.Collections`, `Unity.InputSystem`, `UnityEngine.UI`, `Unity.TextMeshPro`.
  - `Assets/Ezel/ShooterParty/Editor/ShooterParty.Editor.asmdef`.
  - `Assets/Ezel/ShooterParty/Tests/EditMode/ShooterParty.EditModeTests.asmdef` (Editor only).
  - `Assets/Ezel/ShooterParty/Tests/PlayMode/ShooterParty.PlayModeTests.asmdef`.
- Move (`git mv` with `.meta`) from `KnockbackArena/Scripts/` to `ShooterParty/Scripts/`, changing the namespace `KnockbackArena` → `ShooterParty`:
  - into `Critter/`: `CritterMotor`, `CritterAnimator`, `CritterSpit`, `SpitBlob`, `CritterFx`, `BlobShadow`, `LeaderCrown`;
  - into `Camera/`: `CameraShake`, `Playground/OverShoulderCamera`;
  - into `Audio/`: `Sfx`, `SfxBank`, `SfxPlayer`;
  - into `FX/`: `Fx`, `SplatDecals`;
  - into `UI/`: `UI/Ease`, `UI/UiHoverSound`, `UI/HudCard`;
  - renamed: `UI/KnockbackHud` → `UI/PartyHud`, and `Playground/PlaygroundPlayerInput` → `Input/LocalCritterInput`.
- Modify: `KnockbackArena.Runtime.asmdef`, `KnockbackArena.Editor.asmdef` and `KnockbackArena.Tests.asmdef` so each references `ShooterParty.Runtime`, plus `ShooterParty.Editor` from the editor assembly.
- Modify: every `using KnockbackArena…` in the moved code and its callers.

**Interfaces:**
- Produces: the type names above in namespace `ShooterParty` / `ShooterParty.UI`, with unchanged public APIs.
- `PartyHud` keeps `KnockbackHud`'s API.

- [ ] **Step 1:** Sync repo → scratch, apply the moves, and run `Knockback Arena/Rebuild Everything` in batchmode. Expected: no compile errors and no "missing script" warnings in the log.
- [ ] **Step 2:** Run the existing PlayMode tests: `Unity -batchmode -projectPath <scratch> -runTests -testPlatform PlayMode -testResults pm.xml`. Expected: **10/10 pass**. `EverySoundIdHasClips` and the scene test prove the moved MonoBehaviours still resolve.
- [ ] **Step 3:** Sync back to the repo and commit: `git commit -m "Move shared critter, camera, audio and HUD scripts into ShooterParty core; add NGO 2.13.3 / UTP 2.7.4"`.

### Task 2: Name and playlist rules

**Files:**
- Create: `ShooterParty/Scripts/Session/NameRules.cs`, `ShooterParty/Scripts/Session/PlaylistRules.cs`.
- Test: `ShooterParty/Tests/EditMode/NameRulesTests.cs`, `PlaylistRulesTests.cs`.

**Interfaces:**
- Produces:
  - `static bool NameRules.TryClean(string raw, out string clean, out string error)`.
  - `static string NameRules.Unique(string clean, IEnumerable<string> taken)`.
  - `static bool PlaylistRules.TryValidate(string name, IReadOnlyList<string> ids, Func<string,bool> isKnownId, out string error)`.

- [ ] **Step 1: Write the failing tests.**
  - `TryClean`:
    - `"  Mochi  "` → `"Mochi"`;
    - `"<size=400>x"` fails with "letters, numbers, spaces, _ and - only";
    - `"a"` fails (too short);
    - 17 chars fails;
    - `"Zeynep_2"` ok;
    - `"Çağla"` ok (letters include Turkish);
    - `"a​b"` fails.
  - `Unique` (`DuplicateGetsSuffix`): `("Mochi", ["Mochi"])` → `"Mochi 2"`, and `("Mochi", ["Mochi","Mochi 2"])` → `"Mochi 3"`. The result must stay ≤ 16 chars: truncate the base.
  - `PlaylistRules`:
    - an empty list fails;
    - 11 entries fail;
    - an unknown id fails with "Unknown mini-game 'x'";
    - a 25-char name fails;
    - `["knockback_arena"×3]` ok.
- [ ] **Step 2:** Run the EditMode tests (`-testPlatform EditMode`). Expected: FAIL (types missing).
- [ ] **Step 3:** Implement. NFC via `string.Normalize(NormalizationForm.FormC)`; allowed chars `char.IsLetterOrDigit || ' ' || '_' || '-'`.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add name and playlist validation rules"`.

### Task 3: Scoring and crown rules

**Files:**
- Create: `ShooterParty/Scripts/Session/ScoreRules.cs`, `ShooterParty/Scripts/MiniGames/PlayerModifiers.cs`.
- Test: `ShooterParty/Tests/EditMode/ScoreRulesTests.cs`.

**Interfaces:**
- Produces:
  - `struct RankEntry { int slot; int rank; }`, where rank is 1-based and ties share a rank.
  - `static int[] ScoreRules.Award(IReadOnlyList<RankEntry> ranking, int[] table, int slotCount)` returns points per slot.
  - `static IReadOnlyList<int> ScoreRules.Champions(IReadOnlyList<SlotTotals> totals)`, where `struct SlotTotals { int slot; int total; int wins; int lastRoundRank; bool eligible; }`.
  - `static int ScoreRules.NextCrown(int currentWearer, IReadOnlyList<(int slot,int score)> scores)` returns -1 for none.
  - `struct PlayerModifiers { float moveSpeed, mass, cooldown, knockbackTaken; static PlayerModifiers One; }`.
  - `static PlayerModifiers ScoreRules.ModifiersFor(int slot, IReadOnlyList<SlotTotals> standings)` returns `One`. This is the documented seam.

- [ ] **Step 1: Write the failing tests.**
  - `Award`:
    - ranks `[1,2,3,4]` → `[4,3,2,1]`;
    - ranks `[1,2,2,4]` → `[4,3,3,1]`;
    - 3 players `[1,2,3]` → `[4,3,2]`.
  - `Champions`:
    - unique max wins;
    - a tie on total → more wins;
    - then better `lastRoundRank`;
    - otherwise both returned;
    - an `eligible=false` slot is never returned.
  - `NextCrown`:
    - all zero → -1;
    - `(0:1, 1:2)` → 1;
    - a tie between current wearer 0 and slot 1 at 2 → stays 0;
    - the wearer at 0 while another has 1 → moves.
  - `ModifiersFor` returns `One`.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add scoring, champion and crown rules with the modifier seam"`.

### Task 4: Join tickets with shared test vectors

**Files:**
- Create: `ShooterParty/Scripts/Net/Ticket.cs`.
- Create: `projects/shooterparty/server/rooms/tickets.py` (`sign(d, key)`, `verify(t, key, now)`), `make_vectors.py` and `make_ticket.py`. Together they are the Python half, and `make_vectors.py` writes `ShooterParty/Tests/EditMode/ticket_vectors.json`. Task 12 adds the service around them.
- Test: `ShooterParty/Tests/EditMode/TicketTests.cs`.

**Interfaces:**
- Produces:
  - `[Serializable] class TicketData { int v; string room; string pid; string name; bool leader; long exp; }`.
  - `static string Ticket.Sign(TicketData d, byte[] key)`.
  - `static bool Ticket.TryVerify(string ticket, byte[] key, long nowUnix, out TicketData d, out string error)`.
  - Error strings: `"malformed"`, `"bad signature"`, `"expired"`.
  - The signature covers the base64url payload segment's bytes, so no JSON canonicalisation is needed. Use a constant-time compare.

- [ ] **Step 1: Write the failing tests.**
  - `MatchesSharedVectors`: for each vector, `TryVerify(ticket, hex(secret), now)` gives `valid` and `error`, and on success the fields match.
  - `RoundTrip`: Sign then Verify gives the same fields.
  - `FlippedByteFails` → `"bad signature"`.
  - `ExpiredFails` at `exp+1`.
  - `OversizeFails`: a ticket over 1024 chars → `"malformed"`.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement with `HMACSHA256` and `JsonUtility`.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add HMAC join tickets with shared test vectors"`.

### Task 5: WssBridge

**Files:**
- Create: `ShooterParty/Scripts/Net/WssBridge.cs`.
- Test: `ShooterParty/Tests/EditMode/WssBridgeTests.cs`.

**Interfaces:**
- Produces:
  - `sealed class WssBridge : IDisposable`.
  - `static WssBridge Start(string host, int port, bool tls, int delayMs = 0)`, with properties `int LocalPort`, `long BytesUp`, `long BytesDown`.
  - `void DropAll()`, a test hook that closes live pairs.
  - `static byte[] RewriteHost(byte[] head, int length, string host)`.
- Behaviour:
  - resolve IPv4 first (`Dns.GetHostAddresses`, InterNetwork before InterNetworkV6);
  - `NoDelay` on both sockets;
  - `SslStream.AuthenticateAsClientAsync(host)` when `tls`;
  - close both sides when either ends;
  - `delayMs` delays each forwarded chunk in both directions, keeping order.

- [ ] **Step 1: Write the failing tests.**
  - `RewriteHostReplacesOnlyHostLine`: input `"GET /g/1 HTTP/1.1\r\nUpgrade: websocket\r\nHost: 104.1.2.3:443\r\n\r\n"` → the Host line becomes `"Host: partyshooter.ezellm.com"` and the other lines are byte-identical.
  - `PipesThroughPlainEcho`: a local TcpListener echo server and `Start("127.0.0.1", echoPort, tls:false)`. Send a fake handshake plus 10 KB of payload; the echo returns the rewritten handshake and identical payload bytes; `BytesUp == BytesDown`.
  - `ClosingRemoteClosesLocal`: after the echo server closes, a local read returns 0 within 1 s.
  - `DelayAddsLatency`: with `delayMs:100`, the round trip is ≥ 190 ms.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement. Port the spike's `WssBridge`, adding the IPv4 preference, delay, counters and `DropAll`.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add WssBridge: client-side TLS and Host fix for Cloudflare"`.

### Task 6: PartyNet connection layer and connection approval

**Files:**
- Create in `ShooterParty/Scripts/Net/`: `PartyNet.cs`, `ServerArgs.cs`, `JoinPayload.cs`, `ConnectionGate.cs`, `PartyProtocol.cs`.
- Test: `ShooterParty/Tests/EditMode/ServerArgsTests.cs`, `ConnectionGateTests.cs`.

**Interfaces:**
- Consumes: `Ticket` (Task 4), `NameRules` (Task 2), `WssBridge` (Task 5).
- Produces:
  - `const int PartyProtocol.Version = 1`.
  - `enum PartyMode { Offline, Practice, LocalHost, LocalClient, OnlineClient, DedicatedServer }`.
  - `PartyNet` (MonoBehaviour singleton in SP_Boot, DontDestroyOnLoad; creates the `NetworkManager` + `UnityTransport` with TickRate 30). Methods:
    - `void StartPractice(string name)`;
    - `void StartLocalHost(string name)`, on UDP 0.0.0.0:7777;
    - `void JoinLocal(string address, string name)`;
    - `void JoinOnline(string wsUrl, string ticket, string name)`. `wss://h/p` goes through `WssBridge(tls)` to `127.0.0.1:LocalPort`; `ws://h:port/p` connects directly. Both use path p.
    - `void StartDedicatedServer(ServerArgs a)`, WebSocket on 127.0.0.1:a.port, path a.wsPath;
    - `void Leave()`.
  - `PartyNet` state: `PartyMode Mode`, `event Action<string> Disconnected` (the reason), `string LastDenyReason`.
  - `class ServerArgs { int slot, port; string wsPath, room; int minPlayers; bool replaceLeavers; byte[] secret; static bool TryParse(string[] argv, IDictionary env, out ServerArgs a, out string error); }`. It recognises `-party-server`, and the secret comes from env `PARTY_TICKET_SECRET` (hex).
  - `[Serializable] class JoinPayload { int v; string name; string ticket; }`, max 1024 bytes.
  - `static bool ConnectionGate.Approve(byte[] payload, ServerArgs serverOrNull, Func<string,bool> pidSeen, long now, out ApprovedPlayer p, out string reason)`, where `struct ApprovedPlayer { string pid; string name; bool leader; }`.
  - Approval rules:
    - online needs a valid ticket for `room`; a `pid`+`exp` already used is rejected as `"ticket already used"`;
    - local needs a version match and a clean name, and gets a generated pid;
    - `"version"` gives "Update your game".

- [ ] **Step 1: Write the failing tests.**
  - `ServerArgsTests`:
    - a full argv parses;
    - a missing secret fails with "PARTY_TICKET_SECRET missing";
    - a bad port fails.
  - `ConnectionGateTests`, each with its exact reason:
    - a valid online ticket is approved;
    - wrong room → `"wrong room"`;
    - expired;
    - replay;
    - oversize payload;
    - version 0 → `"Update your game"`;
    - a local bad name → the NameRules error.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement. `NetworkManager.ConnectionApprovalCallback` calls `ConnectionGate.Approve` and sets `response.Reason`. `Disconnected` surfaces `NetworkManager.DisconnectReason`.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add PartyNet modes, server args and ticket-checked connection approval"`.

### Task 7: NetworkCritter — owner authority and remote presentation

**Files:**
- Create: `ShooterParty/Scripts/Critter/NetworkCritter.cs`, `ShooterParty/Scripts/MiniGames/CritterConfig.cs` (the struct is defined in Task 9's Interfaces block; it is created here because `ServerSetup` takes it).
- Modify:
  - `CritterMotor.cs`: add `void SetMass(float m)` and `void ApplyVelocityChange(Vector3 dv)` (stun, hit anim and `Knocked`, without touching mass); keep `Knockback()` for offline.
  - `CritterAnimator.cs`: add `void DriveRemote(float speed, bool grounded)`.
  - `KnockbackArena/Editor/CubeCritterSetup.cs`: critter prefabs get `NetworkObject`, `NetworkTransform` (`AuthorityMode = Owner`, interpolate on, half-float position, quaternion compression), `NetworkRigidbody` (`UseRigidBodyForMotion = true`) and `NetworkCritter`.
- Test: `ShooterParty/Tests/PlayMode/NetworkCritterTests.cs`.

**Interfaces:**
- Consumes: `PartyNet` (Task 6).
- Produces `NetworkCritter : NetworkBehaviour`:
  - `NetworkVariable<int> Slot`, `NetworkVariable<float> Mass` (server-written), `NetworkVariable<bool> Eliminated`;
  - `bool Simulated` (true on the owner for humans, on the server for bots);
  - `static IReadOnlyList<NetworkCritter> All`;
  - `void ServerSetup(int slot, Color colour, CritterConfig cfg, PlayerModifiers mods)`;
  - `[Rpc(SendTo.Owner)] void KnockbackRpc(Vector3 dv, float newMass, int epoch)`;
  - `[Rpc(SendTo.Server)] void KnockbackAppliedRpc(int blobId)`;
  - `[Rpc(SendTo.Server, RequireOwnership = true)] void ActionRpc(CritterAction a)`. The server applies its token bucket and re-broadcasts with `[Rpc(SendTo.NotOwner)] ActionEchoRpc`. `enum CritterAction { Spit, Dash, Jump, Land }` is raised by the owner from the motor events;
  - `[Rpc(SendTo.Owner)] void TeleportRpc(Vector3 pos, Quaternion rot)`.
- Behaviour:
  - On non-simulated copies, disable `CritterMotor` and drive the animator from position deltas.
  - Ignore collisions between critters: `Physics.IgnoreCollision` for every collider pair, applied on spawn.
  - Attach `LocalCritterInput` only on the local human owner.
  - Offline (no `NetworkManager` listening), the component is inert, so the playground scene and the old tests keep working.
  - **Server speed check** (spec §3): measured on owner timestamps, it logs `speed violation slot=N v=… cap=…` above `dashSpeed×1.3+2` m/s outside 0.6 s after a knockback. It never corrects. A kick happens only above 3× the cap sustained for 2 s of client time.

- [ ] **Step 1: Write the failing tests.** All run in Practice host mode with 1 human (the host) and 1 bot, spawned via a test helper.
  - `HostCritterIsSimulatedBotIsServerSimulated`.
  - `RemoteCopyHasMotorDisabled`: spawn a critter with `SpawnWithOwnership(9999)` on the host, which gives a real non-owner copy. Assert `Simulated == false` and the motor disabled. Add a `ForceRemoteForTest` seam only if that spawn misbehaves (record it as a ruling).
  - Test teardown calls `NetworkManager.Shutdown()` and destroys the PartyNet and NetworkManager DontDestroyOnLoad objects. Practice binds UDP port **0** (ephemeral), so consecutive tests never collide.
  - `CrittersDoNotCollide`.
  - `OfflinePrefabStillMoves`: the old playground path.
  - EditMode `SpeedCheckTests` (new plain class `ShooterParty/Scripts/Critter/SpeedCheck.cs`, `SpeedCheck.Feed(double ownerTime, Vector3 pos, bool recentKnockback) → SpeedVerdict {Ok, Log, Kick}`; `NetworkCritter` feeds it from received transform ticks on the server):
    - 40 m/s for 0.5 s gives one `Log` and no `Kick`;
    - 3.5 × the cap for 2.1 s gives `Kick`;
    - 9 m/s with dash is `Ok`;
    - a burst of stalled samples, the same total distance arriving at once with the original owner timestamps, is `Ok`.
- [ ] **Step 2:** Run the PlayMode tests. Expected: FAIL.
- [ ] **Step 3:** Implement. Then rebuild prefabs via `Rebuild Everything`.
- [ ] **Step 4:** Run. Expected: PASS, and the old 10 still pass.
- [ ] **Step 5:** Commit: `"Add NetworkCritter: owner-authoritative movement, remote presentation, no critter-critter contact"`.

### Task 8: Networked shots and the ShotEffect hooks

**Files:**
- Create: `ShooterParty/Scripts/Shot/ShotSystem.cs`, `ShooterParty/Scripts/Shot/ShotEffect.cs`, `ShooterParty/Scripts/Shot/ShotHit.cs`, `KnockbackArena/Scripts/KnockbackShotEffect.cs`.
- Modify:
  - `CritterSpit.cs`: `TryFire` goes to `ShotSystem` when one is active, else the current offline path.
  - `SpitBlob.cs`: add `bool Cosmetic`. A cosmetic blob ignores critters and splats on world geometry. Add `void ForceImpact(Vector3 point, Vector3 normal, Transform rideOn)`. The impact handler goes through `event Action<SpitBlob, Collision> Impacted`, and offline keeps the current knockback.
- Test: `ShooterParty/Tests/PlayMode/ShotSystemTests.cs`.

**Interfaces:**
- Consumes: `NetworkCritter` (Task 7).
- Produces:
  - `ShotSystem : NetworkBehaviour` (singleton prefab, spawned by `PartyNet` when the server or host starts, `destroyWithScene=false`):
    - `void Fire(NetworkCritter shooter, Vector3 origin, Vector3 dir, double clientTime)`. This is the owner-side entry: cosmetic blob plus the shooter's `NetworkCritter.FireRpc`. The RPC lives on the critter as `[Rpc(SendTo.Server, RequireOwnership = true)] FireRpc(Vector3 origin, Vector3 dir, double clientTime)`, so the shooter is the RPC's object and can't be spoofed, even when host and bots share clientId 0.
    - `void ServerFire(NetworkCritter shooter, Vector3 origin, Vector3 dir, double time)`. `FireRpc` calls it after validating cooldown on client timestamps, origin within `speed*lag+0.5` and the token bucket (6/s, 12 burst). Server-simulated bots call it directly.
    - `[Rpc(SendTo.ClientsAndHost)] BlobSpawnedRpc(int id, int shooterSlot, Vector3 p0, Vector3 v0, double serverTime)`.
    - `BlobHitRpc(int id, int victimSlot, Vector3 point, Vector3 normal)`, sent to the shooter at once and to observers after the victim's ack.
    - `BlobSplatRpc(int id, Vector3 point, Vector3 normal)`, sent to everyone.
  - `abstract class ShotEffect : ScriptableObject`:
    - `abstract void ApplyServer(in ShotHit hit, ShotContext ctx)`;
    - `virtual void OnFireLocal(NetworkCritter shooter) {}`;
    - `virtual void OnOwnImpactLocal(NetworkCritter shooter, Vector3 point) {}`.
  - `struct ShotHit { int shooterSlot, victimSlot; Vector3 point, normal, velocity; }`.
  - `ShotContext` offers `void Knock(NetworkCritter victim, Vector3 impulse, float massLossFraction)`, which computes Δv with the server mass, sets `Mass` and sends `KnockbackRpc` (bots are applied directly), plus `int Epoch`.
  - `KnockbackShotEffect`: `impulse = 3.5f`, `massLossPerHit = 0.10f`, `minMass = 0.35f`. Direction is the horizontal blob velocity, plus up × 0.25.

- [ ] **Step 1: Write the failing tests** (Practice host).
  - `BotShotKnocksHostAndLowersMass`:
    - aim a bot at the host critter at 3 m;
    - within 1.5 s the host critter gets a horizontal velocity > 1 m/s;
    - `Mass.Value / start == 0.9f ± 0.001`.
  - `CooldownEnforcedOnServer`: 10 `FireRpc`s in one frame give ≤ 2 spawned blobs.
  - `FireRpcFromNonOwnerRejected`: a client calling `FireRpc` on another player's critter spawns nothing.
  - `CosmeticBlobDecidesNothing`: a cosmetic blob passing through a critter changes no velocity.
  - `OfflineSpitStillKnocks`: `SpitBlobDeformsAndTakesTheShootersColour` and the existing knockback test still pass.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `"Networked spit: server-decided hits, cosmetic blobs, KnockbackRpc with epoch"`.

### Task 9: The mini-game framework and Knockback Arena on it

**Files:**
- Create in `ShooterParty/Scripts/MiniGames/`: `MiniGameDefinition.cs`, `MiniGameCatalog.cs`, `MiniGameController.cs`, `ISessionHost.cs`. `CritterConfig.cs` exists from Task 7.
- Create in `KnockbackArena/Scripts/`: `KnockbackArenaGame.cs`.
- Modify:
  - `JumpPickup.cs`: make it a NetworkBehaviour. The server decides availability via a `NetworkVariable<bool>`, and collection grants `motor.AddJumpCharge` through an owner RPC.
  - `Playground/ArenaBotAI.cs`: move to `KnockbackArena/Scripts/ArenaBotAI.cs`, the bot brain prefab component. It runs only where `Simulated`.
  - `KnockbackArena/Editor/ArenaSetup.cs`:
    - the scene `KnockbackArena_Ice.unity` keeps the arena, sky, backdrop, pickups and spawns;
    - it gains an in-scene `KnockbackArenaGame` NetworkObject and an `OverShoulderCamera`;
    - it drops the old lobby, HUD and `KnockbackMatchDemo`.
- Create: the assets `KnockbackArena/Data/KnockbackArena.asset` (`MiniGameDefinition`) and `ShooterParty/Data/MiniGameCatalog.asset`.
- Delete: `Playground/KnockbackMatchDemo.cs`, `UI/LobbyPanel.cs` and their prefabs.
- Modify: `ArenaSetup` registers `KnockbackArena_Ice` in `EditorBuildSettings`, as NetworkSceneManager requires.
- Test: `KnockbackArena/Tests/KnockbackArenaGameTests.cs`. Update `ZArenaSceneLoadsFullyWired`.

**Interfaces:**
- Consumes: Tasks 3, 7 and 8.
- Produces:
  - `MiniGameDefinition` fields:
    - `id`, `displayName`, `tagline`;
    - `string[] howToPlay`, `controlsHint`;
    - `Sprite icon`, `string sceneName`;
    - `minPlayers`, `maxPlayers`, `durationSeconds`;
    - `int[] placementPoints = {4,3,2,1}`;
    - `supportsBots`;
    - `GameObject botBrain`, `NetworkObject critterPrefab`;
    - `ShotEffect shotEffect`, `GameObject hudWidget`;
    - `CritterConfig critter`.
  - `[Serializable] struct CritterConfig`:
    - `enum JumpMode { Off, Pickup, On } jump`;
    - `int dashCharges`;
    - `float shotSpeed`, `shotGravity`, `shotCooldown`, `chargeTime`.
    - Knockback values: `Pickup`, 3, 12, 3, 10/30, 0.
  - `MiniGameCatalog`: `List<MiniGameDefinition> games`, `MiniGameDefinition Find(string id)`.
  - `interface ISessionHost`:
    - `IReadOnlyList<SlotInfo> RoundSlots { get; }`;
    - `int Epoch { get; }`;
    - `float TimeScale { get; }`;
    - `void ReportScores(IReadOnlyList<(int slot,int score)> scores, int crownSlot)`;
    - `void ReportRound(IReadOnlyList<RankEntry> ranking)`.
  - `struct SlotInfo { int slot; ulong ownerClientId; bool isBot; Color colour; PlayerModifiers mods; }`.
  - The controller finds its host through `static ISessionHost MiniGameController.Host`. `SessionManager` sets it in Task 10, and tests set a `TestSessionHost`.
  - `abstract class MiniGameController : NetworkBehaviour`:
    - hooks: `OnSetup(IReadOnlyList<int> slots)`, `OnRoundStart()`, `OnServerTick(float dt)`, `OnHit(in ShotHit hit)` (called by `ShotSystem` right after `ShotEffect.ApplyServer`), `IReadOnlyList<RankEntry> RankPlayers()` (default: by Score desc);
    - helpers: `AddScore(int slot,int n)`, `SetScore`, `Eliminate(int slot)` (shows the out banner and switches that player's camera to spectate a living critter), `EndRound()`, `RespawnAt(int slot, Pose p)`, `NetworkCritter CritterOf(int slot)`, `Transform[] SpawnPoints`;
    - the base also spawns critters for `Host.RoundSlots` (server spawn, then `ChangeOwnership`), runs the timer from `durationSeconds × Host.TimeScale`, updates `ScoreRules.NextCrown` on score change through `ReportScores`, and despawns its critters before `ReportRound`.
  - `KnockbackArenaGame` rules: score +1 per KO, credited to the last hitter within **4 s**; rank by survival; out at `y < -2.5`; the round ends when **≤ 1 alive** or after 120 s (then by KOs, then mass). Simultaneous falls in the same tick share a rank.

- [ ] **Step 1: Write the failing tests.** Each runs a Practice host with 3 bots and `durationSeconds` overridden to 20.
  - `RoundEndsWithOneLeftAndRanksBySurvival`: force-drop bots in order 2, 1 by teleporting below -3. The ranking is `[0:1, 1:2, 2:3]`. Order the slots so the expected values are concrete.
  - `KoCreditGoesToLastHitterWithinWindow`: a hit by slot 1 at t, then a fall at t+3 s gives slot 1 score 1; a fall at t+5 s gives no credit.
  - `CrownFollowsScore`.
  - `TimerEndRanksByKosThenMass`.
  - `SimultaneousFallsShareRank`.
  - `CampingOutsideFootprintIsEliminated`: a critter held at y = 0, 3 m outside the arena for 1.2 s is eliminated.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement, then `Rebuild Everything`.
- [ ] **Step 4:** Run. Expected: PASS. The updated scene test checks that `KnockbackArenaGame` and the spawns are present, with no missing scripts.
- [ ] **Step 5:** Commit: `"Mini-game framework (definition, catalog, controller) and Knockback Arena rules on it"`.

### Task 10: SessionManager, SessionFlow and scene flow

**Files:**
- Create in `ShooterParty/Scripts/Session/`: `SessionFlow.cs` (pure C#), `SessionManager.cs` (NetworkBehaviour), `PlayerState.cs`, `PlaylistPreset.cs`, and `ShooterParty/Data/Playlists/{Quick,Classic,Marathon}.asset`.
- Create: `ShooterParty/Editor/ShooterPartySetup.cs`. It makes a stub `ShooterParty/Scenes/SP_Lobby.unity` (4 spawn markers and a camera) and registers it in `EditorBuildSettings` first. Task 15 fills the scene in.
- Test: `ShooterParty/Tests/EditMode/SessionFlowTests.cs`, `ShooterParty/Tests/PlayMode/PracticeSessionTests.cs`.

**Interfaces:**
- Consumes: Tasks 2, 3, 6 and 9.
- Produces:
  - `enum SessionPhase { Lobby, Loading, Intro, Countdown, Playing, RoundResults, Standings, Final }`.
  - `struct PlayerState : INetworkSerializable, IEquatable<PlayerState>` with fields:
    - `byte slot, colour`;
    - `FixedString32Bytes pid`, `FixedString64Bytes name`;
    - `ulong clientId`;
    - `bool isBot, connected, ready, leader, eliminated, eligible`;
    - `int total, roundScore, wins, lastRank`;
    - `double joinedAt`;
    - `ushort rttMs`: server-measured via `GetCurrentRtt(clientId)` about once a second; 0 for bots and the host.
  - `SessionFlow`, a pure state machine with an injected clock:
    - members: `Phase`, `RoundIndex`, `IReadOnlyList<PlayerState> Players`, `Playlist`;
    - commands: `AddPlayer(pid,name,isBot) → slot`, `Remove(pid, clean)`, `SetReady`, `SetColour`, `SetPlaylist`, `SetReplaceLeavers`, `Kick` (lobby only), `TryStart(pid) → error`;
    - events: `RoundDone(ranking)`, `Tick(double now)`;
    - outputs: `event Action<SessionPhase>`, `event Action<string sceneName> LoadScene`.
  - `SessionFlow` option `fillWithBots` is true in Practice. It fills up to 4 slots with bots named `Mochi`, `Zesty`, `Grape`, `Tofu` (first unused), with colours in slot order.
  - `SessionManager` (persistent NetworkObject spawned by the server at start; implements `ISessionHost`):
    - mirrors `SessionFlow` into `NetworkList<PlayerState>`, `NetworkVariable<SessionPhase>`, `NetworkVariable<double> PhaseEndsAt`, `NetworkVariable<int> RoundIndex`, `NetworkVariable<int> CrownSlot`, `NetworkVariable<int> Epoch`, `NetworkList<FixedString32Bytes> PlaylistIds`, `NetworkVariable<FixedString64Bytes> PlaylistName`;
    - lobby RPCs to the server, each validated against the sender's clientId and a per-client token bucket (20/s, 40 burst; excess dropped and logged): `SetReadyRpc`, `CycleColourRpc`, `SetPlaylistRpc(name, ids)`, `SetReplaceLeaversRpc`, `KickRpc(slot)`, `StartRpc`, `LeaveRpc`;
    - loads scenes with `NetworkManager.SceneManager.LoadScene(name, Single)`, despawning critters first;
    - starts Intro on `LoadEventCompleted`.

- [ ] **Step 1: Write the failing tests** (EditMode, fake clock).
  - `StartNeedsMinHumansAllReadyAndLeader` gives errors: "Need at least 3 players", "Everyone must be ready", "Only the leader can start".
  - `PhasesFollowSpecTimings` (4/3/—/6/6/12).
  - `TwoRoundPlaylistReachesFinalThenLobby`.
  - `LeaderLeavesPassesLeadership` goes to the earliest `joinedAt`.
  - `FewerThanTwoHumansJumpsToFinal`.
  - `BackToLobbyNeedsThreeAgain`.
  - `DuplicateNamesAreSuffixedOnJoin`.
  - `DisconnectMidRoundCountsAsFall`.
  - `ReplaceLeaversBotsFromNextRound`: with the option on, a disconnected slot is `isBot` from the next Intro.
  - `LeftHumanNotEligibleForChampion`.
  - `KickOnlyInLobby`.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement `SessionFlow`, then `SessionManager`.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5: PlayMode test** `PracticeSessionRunsTwoRoundsToFinal`:
  - the Practice host with 3 bots and timers scaled by 0.1 (test seam `SessionFlow.TimeScaleForTests`);
  - the playlist is `["knockback_arena","knockback_arena"]`;
  - the phases reach `Final`, then `Lobby`;
  - each slot's total equals the sum of `ScoreRules.Award` over the two rankings `SessionFlow` recorded (tied ranks share points, so a fixed 20 would be flaky);
  - no exceptions in the log.

  Expected: PASS.
- [ ] **Step 6:** Commit: `"Session flow: lobby, playlist rounds, scoring, final, leader and leaver rules"`.

### Task 11: Dedicated server mode, builds and the local end-to-end test

**Files:**
- Create: `ShooterParty/Scripts/Net/ServerRuntime.cs`, `ShooterParty/Scripts/Net/Autopilot.cs`, `ShooterParty/Editor/PartyBuild.cs`, `projects/shooterparty/server/e2e_local.sh`.

**Interfaces:**
- Consumes: Tasks 6 and 10.
- Produces:
  - `ServerRuntime`, active when `ServerArgs.TryParse` succeeds:
    - sets `Application.targetFrameRate = 60` and `vSyncCount = 0`;
    - prints `@@READY`, `@@STATE {"phase":..,"humans":n,"connected":n,"pids":[..]}` on change, `@@STATS {"tickMs":..,"tickMsMax":..,"players":n,"rttAvgMs":..}` every 10 s, `@@RESULT {"totals":[..],"rankings":[[..],..]}` after Final, and `@@END`;
    - exits when the session is over and everyone has left, when empty for 60 s, after lobby idle for 30 min, or at a 2 h cap.
  - `Autopilot`, enabled by the client flag `-autopilot [-dropAt s] [-leaveAt phase]`:
    - joins via `-join wsUrl -ticket t -name n`, or `-local addr`;
    - readies automatically;
    - the leader starts once humans ≥ min;
    - an `ArenaBotAI` drives the owned critter.
  - `PartyBuild` menu `ShooterParty/Build/` with entries Mac, Windows, Linux x64 and Pi Server:
    - Pi Server: `StandaloneLinux64` + `StandaloneBuildSubtarget.Server` + IL2CPP, with `UnityEditor.LinuxStandalone.UserBuildSettings.architecture = OSArchitecture.ARM64` set by reflection. Retry once if the first run reports "Unable to find an Linux Sysroot".
    - Output under `Builds/<target>/`, which is gitignored.
  - `e2e_local.sh [scenario]`, where scenario is `basic | drop | leave-during-load`:
    - starts the Mac player as a server (`-party-server … -port 7701 -wsPath /g/1`, secret from env);
    - mints 3 tickets with `make_ticket.py`;
    - starts 3 autopilot clients with `ws://127.0.0.1:7701/g/1`;
    - uses playlist `["knockback_arena","knockback_arena"]` (the autopilot leader sets it);
    - exits 0 when the server prints `@@END`, and `@@RESULT` shows 3 human slots whose totals equal `ScoreRules.Award` summed over its `rankings`, checked by a small Python helper in the script.

- [ ] **Step 1:** Implement `ServerRuntime`, `Autopilot` and `PartyBuild`. Build Mac and Pi Server.
- [ ] **Step 2:** Run `e2e_local.sh basic`. Expected: exit 0 within 4 min; the server log shows `@@READY`, then `@@STATE` Lobby → Playing → Final, then `@@END`.
- [ ] **Step 3:** Run `e2e_local.sh leave-during-load`. One client quits at `Loading`. Expected: exit 0, the remaining 2 finish (Final), and there are no exceptions in any log.
- [ ] **Step 4:** On the Pi: rsync the Pi Server build to `~/spike_partyshooter` and run it with an SSH forward. Run the 3 Mac autopilot clients through the forward. Expected: `@@END`, and RSS < 300 MB in `@@STATS`.
- [ ] **Step 5:** Commit: `"Dedicated server runtime, autopilot clients, build menu, local end-to-end test"`.

## M2: room service, menus and lobby

### Task 12: Room service (Python standard library)

**Files:**
- Create in `projects/shooterparty/server/rooms/`: `room_service.py`, `tickets.py`, `codes.py`, `ratelimit.py`, `make_vectors.py`, `make_ticket.py`, `test_room_service.py`, `fake_game_server.py`.

**Interfaces:**
- Produces:
  - **CLI:** `python3 room_service.py --listen 127.0.0.1:7700 --status-listen 127.0.0.1:7709 --server-bin PATH --slots 2 --slot-base-port 7701 --ws-url-template "wss://partyshooter.ezellm.com/g/{slot}" --secret-file FILE --min-players 3`.
  - **HTTP API** exactly as spec §2:
    - create, returning `rejoinToken`;
    - join, with errors 404 / 409 full / 409 in progress;
    - rejoin;
    - `GET /api/v1/status`;
    - every route requires `X-ShooterParty-Version: 1`, otherwise 426 `{"error":"Update your game"}`.
  - **Errors** are JSON `{"error": "<human message>"}`.
  - **Child servers:** started with the env `PARTY_TICKET_SECRET`; the service reads `@@` lines from them.
  - **Rate limits** per `CF-Connecting-IP`, falling back to the peer:
    - create 6 per 10 min;
    - join+rejoin 30 per 10 min;
    - failed lookups 10/min, then 429 for 5 min.
  - **Request size:** body ≤ 2 KB.

- [ ] **Step 1: Write the failing tests** (unittest; `fake_game_server.py` prints READY/STATE/END on command).
  - `test_code_alphabet_and_length`.
  - `test_code_cooldown_10min`.
  - `test_ticket_matches_vectors`, using `Assets/Ezel/ShooterParty/Tests/EditMode/ticket_vectors.json`.
  - `test_create_returns_code_ws_ticket_rejoin`.
  - `test_third_room_503` (with 2 slots).
  - `test_join_unknown_404_message`, expecting "No room with code BCDFG".
  - `test_join_full_409`.
  - `test_join_in_progress_409`.
  - `test_rejoin_issues_ticket_for_same_pid`.
  - `test_version_header_426`.
  - `test_body_over_2kb_413`.
  - `test_failed_lookup_rate_limit_429`.
  - `test_bad_name_400` (same rules as `NameRules`).
  - `test_server_exit_frees_slot`.
  - `test_status_public_has_no_codes`.
  - `test_status_detail_only_on_status_port`.
- [ ] **Step 2:** Run `python3 -m unittest -v test_room_service.py`. Expected: FAIL.
- [ ] **Step 3:** Implement with `http.server.ThreadingHTTPServer`, `hmac`, `secrets` and `subprocess`. The vectors are generated by `make_vectors.py`; check that they equal the committed file.
- [ ] **Step 4:** Run. Expected: all pass. Task 4's C# tests still pass against the same vectors.
- [ ] **Step 5:** Extend `e2e_local.sh` with the room service: clients use `-roomService http://127.0.0.1:7700` with `--ws-url-template "ws://127.0.0.1:{port}/g/{slot}"`. Run `basic`. Expected: exit 0.
- [ ] **Step 6:** Commit: `"Room service: party codes, signed tickets, rejoin, limits, status"`.

### Task 13: RoomServiceClient and the join flow

**Files:**
- Create: `ShooterParty/Scripts/Net/RoomServiceClient.cs`, `ShooterParty/Scripts/Net/JoinFlow.cs`.
- Test: `ShooterParty/Tests/PlayMode/RoomServiceClientTests.cs`.

**Interfaces:**
- Consumes: Tasks 6 and 12.
- Produces:
  - `RoomServiceClient`:
    - `RoomServiceClient(string baseUrl)`, default `https://partyshooter.ezellm.com/api/v1`, overridable with `-roomService`;
    - `IEnumerator Create(string name, bool replaceLeavers, Action<RoomJoin> ok, Action<JoinError> fail)`;
    - `Join(code, name, …)` and `Rejoin(code, token, …)`;
    - `struct RoomJoin { string code, wsUrl, ticket, rejoinToken; }`;
    - `struct JoinError { int status; string message; }`.
    - `message` is the server's `error` text. A network failure maps to "Server unreachable — try Local Play"; 426 maps to "Update your game"; a timeout of 8 s is unreachable.
  - `JoinFlow`, a state machine: `Contacting → Joining → Loading → InLobby | Failed(message)`. It keeps `code`, `name` and `rejoinToken` in memory only.

- [ ] **Step 1: Write the failing test** `MapsErrors`. Start `room_service.py` as a subprocess on a free port (skip if `python3` is missing), then assert:
  - create gives a 5-letter code;
  - join of "BCDFG" gives "No room with code BCDFG";
  - a missing service (wrong port) gives "Server unreachable — try Local Play";
  - an override `-version 0` gives "Update your game".
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement with `UnityWebRequest` and `JsonUtility`, sending header `X-ShooterParty-Version: 1`.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `"Room service client and join flow with readable errors"`.

### Task 14: UI shell — boot, menu, dialogs, settings and pause

**Files:**
- Create in `ShooterParty/Scripts/UI/`: `UiRoot.cs` (screen stack, fades), `MenuScreen.cs`, `NameCodeDialog.cs`, `JoinProgressView.cs`, `Dialog.cs`, `Toasts.cs`, `SettingsView.cs`, `PauseMenu.cs`.
- Create: `ShooterParty/Editor/UiBuilder.cs`. It generates the prefabs and the scenes `SP_Boot` (PartyNet, SfxPlayer, EventSystem with InputSystemUIInputModule, UiRoot canvas 1920×1080 match-height) and `SP_Menu` (arena backdrop, 4 idle critters, slow orbit camera). It also updates `EditorBuildSettings`: SP_Boot, SP_Menu, SP_Lobby, KnockbackArena_Ice.
- Test: `ShooterParty/Tests/PlayMode/MenuFlowTests.cs`.

**Interfaces:**
- Consumes: Tasks 6, 10 and 13, and the existing fonts, sprites and Ease from `UiSetup`.
- Produces:
  - `UiRoot.Show<T>()`, `UiRoot.Toast(string)`, `UiRoot.Dialog(title, body, buttons)`.
  - Settings keys in `PlayerPrefs`:
    - volumes `sp.vol.master`, `sp.vol.music`, `sp.vol.sfx` (0-1);
    - `sp.sens` (0.2-3);
    - `sp.invertY`, `sp.display`, `sp.res`, `sp.vsync`, `sp.netstats`;
    - `sp.lastName`.
  - Menu buttons: Play Online (Create Room / Join Room), Local Play (Host / Join by address), Practice, Settings, Quit.
  - Every wait shows a spinner, a step label and Cancel.
  - Screen changes fade over 0.25 s. Music crossfades through `Sfx.Music` (`MusicLobby` in menu and lobby, `MusicGameplay` in rounds).

- [ ] **Step 1: Write the failing tests.**
  - `PracticeFromMenuReachesLobby`: load SP_Boot, invoke Practice with name "Tester", and within 5 s `SessionManager` is in phase `Lobby` with 4 players (1 human, 3 bots).
  - `BadNameShowsErrorAndKeepsDialog`.
  - `SettingsPersist`: set the music volume to 0.3, reload, read 0.3.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement, then run the builder.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Capture `renders/shooterparty/01_menu.png` with the existing scratch capture approach and look at it. Fix anything that reads badly.
- [ ] **Step 6:** Commit: `"UI shell: boot and menu scenes, dialogs, settings, pause"`.

### Task 15: Lobby

**Files:**
- Create: `ShooterParty/Scripts/UI/LobbyView.cs`, `ShooterParty/Scripts/UI/PlaylistBuilder.cs`, `ShooterParty/Scripts/Session/LobbyStage.cs` (pedestals and podium in `SP_Lobby`).
- Modify: `UiBuilder.cs` to build `SP_Lobby` and the lobby prefabs.
- Test: `ShooterParty/Tests/PlayMode/LobbyTests.cs`.

**Interfaces:**
- Consumes: `SessionManager` (Task 10) and `UiRoot` (Task 14).
- Produces the `LobbyView` bindings:
  - a large code with Copy (`GUIUtility.systemCopyBuffer`), or the host IP list in Local;
  - 4 slots, each with name, ready check, leader star, ping dot and bot tag;
  - playlist panel: presets, plus a builder with add/remove/reorder/shuffle/name;
  - "Replace leavers with bots" toggle (leader);
  - Kick (leader);
  - Ready/Start/Leave.
  - The Start button's disabled tooltip shows the `TryStart` error.
- `LobbyStage`: one critter per pedestal in its colour; it plays `Happy` on ready.

- [ ] **Step 1: Write the failing tests.**
  - `LeaderBuildsPlaylistAndStarts`: in Practice, set the playlist via the builder API to `knockback_arena ×2` named "Duo" and start. The phase goes to `Loading`, and `PlaylistName == "Duo"`.
  - `NonLeaderCannotSeeStart`, using a slot flag.
  - `InvalidPlaylistNameRejectedWithMessage`.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Capture `02_lobby.png` and review it.
- [ ] **Step 6:** Commit: `"Lobby: code, slots on pedestals, playlist presets and builder, leader controls"`.

### Task 16: In-game HUD, intro, results, standings and final

**Files:**
- Modify: `UI/PartyHud.cs`:
  - generic cards (portrait, name, game score, crown icon, connection dot, `statLine`);
  - timer and game name;
  - existing crosshair, cooldown, dash and jump, countdown and banners;
  - a spectate label.
- Create in `ShooterParty/Scripts/UI/`: `IntroCard.cs`, `RoundResultsView.cs`, `StandingsView.cs`, `FinalView.cs`.
- Create: `KnockbackArena/Scripts/UI/KnockbackHudWidget.cs`, which sets each card's `statLine` to "mass 80 %" from `NetworkCritter.Mass`. `MassRing` reads `Mass` too.
- Modify: `UiBuilder.cs`. In `UiSetup.cs`, the HUD prefab moves to the core builder.
- Test: `ShooterParty/Tests/PlayMode/HudTests.cs`. Adapt `HudCardShowsMassDroppingOnHit`.

**Interfaces:**
- Consumes: `SessionManager`, `MiniGameDefinition.hudWidget`, `NetworkCritter.Mass`.
- Produces:
  - `PartyHud.BindSession(SessionManager s)`;
  - `HudCard.SetStatLine(string)`, `HudCard.SetConnection(int rttMs)` (green < 120, amber < 220, red otherwise);
  - views driven only by `SessionManager.Phase`, `PhaseEndsAt` and `PlayerState`;
  - `CrownView`: on every client, it attaches the existing `LeaderCrown` prefab to the critter whose `Slot == SessionManager.CrownSlot`, and detaches it when that critter is eliminated (the card keeps the icon).

- [ ] **Step 1: Write the failing tests.**
  - `IntroShowsHowToPlayAndRoundXofN`, with text from the definition.
  - `CardsShowScoreAndCrownIcon`.
  - `CrownAttachesToLeaderCritter`.
  - `FinalShowsChampionAndBackToLobby`.
  - `HudCardShowsMassDroppingOnHit`: mass 0.65 shows "65%" in amber.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Capture `03_intro.png`, `04_play.png`, `05_results.png`, `06_standings.png` and `07_final.png` from a Practice session, look at each, and fix layout problems.
- [ ] **Step 6:** Commit: `"Session HUD: intro card, cards with crown, results, standings, final podium"`.

### Task 17: Rejoin, toasts and bot replacement

**Files:**
- Modify: `JoinFlow.cs` (auto-rejoin), `SessionManager.cs`, `PartyHud.cs` (spectate) and `Toasts`.
- Modify: `e2e_local.sh` (scenario `drop`).

**Interfaces:**
- Consumes: Tasks 10, 12, 13 and 16.
- Produces:
  - **On an unexpected disconnect online:** the toast "Connection lost — rejoining…". Then `RoomServiceClient.Rejoin` with backoff of 1, 2, 4 and 8 s, for up to 60 s, then `JoinOnline`.
  - **On success:** spectate until the next Intro. On failure: the dialog "Couldn't rejoin" → Menu.
  - **Others see** toasts "<name> joined", "<name> left", "<name> was kicked", "<name> disconnected", "<name> is back" and "<name> replaced by a bot".

- [ ] **Step 1:** Add the `drop` scenario. Client 2 runs `-dropAt 25`, which calls `WssBridge.DropAll()` mid-round on its own bridge. For this local run, clients use the bridge in plain mode (`tls:false`) so the drop path is exercised.
- [ ] **Step 2:** Run `e2e_local.sh drop`. Expected: exit 0. Client 2's log shows `rejoined` and its final total is > 0. The server log shows that slot's `connected` false, then true. No phase waits on the dropped player.
- [ ] **Step 3:** Commit: `"Auto-rejoin with tokens, spectate after rejoin, leaver toasts, bot takeover next round"`.

### Task 18: Network stats overlay

**Files:**
- Create: `ShooterParty/Scripts/Net/NetStats.cs`, `ShooterParty/Scripts/UI/NetStatsOverlay.cs`.

**Interfaces:**
- Produces:
  - `NetStats`: `int RttMs` (`UnityTransport.GetCurrentRtt`), `int JitterMs` (stddev of the last 20 samples), `float UpKbps`/`DownKbps` (from the `WssBridge` counters; shown as "—" on UDP), and `ServerTickMs` from a NetworkVariable the server sets from `@@STATS`.
  - F3 toggles the overlay; `sp.netstats` persists it.

- [ ] **Step 1: Write the failing test** `OverlayShowsRttInPractice`: the overlay text contains "RTT" and a number ≥ 0.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run. Expected: PASS.
- [ ] **Step 5:** Commit: `"F3 network stats overlay and HUD connection dots"`.

## M3: deployment, docs and the final check

### Task 19: Pi deployment and the Cloudflare tunnel

**Requires the owner's approval to open tunnels, and one `cloudflared tunnel login` browser click.**

**Files:**
- Create in `projects/shooterparty/server/`:
  - `systemd/partyshooter-rooms.service` (sandbox values from spec §7);
  - `systemd/cloudflared-partyshooter.service`;
  - `cloudflared/partyshooter.yml.example` (the ingress from spec §7);
  - `deploy.sh`;
  - `partyshooter-status`;
  - `secret.example`.
- `.gitignore` entries: `Builds/`, `*.pem`, `projects/shooterparty/server/**/secret`, `cloudflared/*.json`.

- [ ] **Step 1:** `deploy.sh`:
  - creates the user `partyshooter` (nologin) and `/mnt/apps/partyshooter/{server,rooms}`;
  - creates the secret (`openssl rand -hex 32`) at `/etc/partyshooter/secret`, `root:partyshooter 0640`;
  - rsyncs the Pi Server build and `rooms/`;
  - installs the units and runs `systemctl enable --now partyshooter-rooms`.

  Verify: `curl -s -H 'X-ShooterParty-Version: 1' 127.0.0.1:7700/api/v1/status` on the Pi returns `{"version":1,"rooms":0,...}`.
- [ ] **Step 2:** Tunnel, after approval:
  - `cloudflared tunnel login`, done by the owner;
  - `cloudflared tunnel create partyshooter`;
  - `cloudflared tunnel route dns partyshooter partyshooter.ezellm.com`;
  - install the config and unit, start the service;
  - **delete `~/.cloudflared/cert.pem`**.

  Verify from the Mac:
  - `curl -s -H 'X-ShooterParty-Version: 1' https://partyshooter.ezellm.com/api/v1/status` returns 200 with JSON;
  - `https://partyshooter.ezellm.com/anything` returns 404;
  - no Cloudflare challenge page.
- [ ] **Step 3:** End to end against the Pi. Run 3 Mac autopilot clients with `-roomService https://partyshooter.ezellm.com/api/v1`. Expected:
  - `@@END` in `journalctl -u partyshooter-rooms`;
  - a client-side RTT median within 110-230 ms;
  - server RSS < 300 MB.
- [ ] **Step 4:** Commit: `"Pi deployment: sandboxed room service, separate game tunnel, deploy and status scripts"` (no secrets, IPs or Pi details).

### Task 20: Docs

**Files:**
- Create in `Assets/Ezel/ShooterParty/Docs/`: `ARCHITECTURE.md` (incl. networking), `ADDING_A_MINIGAME.md`, `SERVER_OPS.md`.
- Modify: `projects/game_assets/knockback_arena/blender/v2/README.md`, adding a pointer.

**Contents:**
- `ARCHITECTURE.md`: the diagram, authority table, session flow, where shared art lives today, and the extension seams (`ScoreRules.Award`, `ModifiersFor`, `ShotEffect` hooks).
- `ADDING_A_MINIGAME.md`: step by step (folder, asmdef, definition asset, scene with spawn points, controller hooks, shot effect, bot brain, HUD widget, adding it to the catalog, testing in Practice), with Knockback Arena as the worked example.
- `SERVER_OPS.md`: deploy, status, logs, kill switches, rotating the secret, Bot Fight Mode off, the school Wi-Fi checklist, and the Local play fallback.

- [ ] **Step 1:** Write the docs. Every file path and command in them must exist and run; check them by following `ADDING_A_MINIGAME.md` up to the definition step in scratch, then revert.
- [ ] **Step 2:** Commit: `"Docs: architecture, adding a mini-game, server ops"`.

### Task 21: Final verification

- [ ] **Step 1:** Full test run in scratch: EditMode, PlayMode, and Python unittest. Expected: all pass. Record the counts.
- [ ] **Step 2:** Run `e2e_local.sh` with `basic`, `drop` and `leave-during-load`. Expected: all exit 0.
- [ ] **Step 3:** Build all four targets. Expected: each succeeds. Record the sizes.
- [ ] **Step 4:** Capture a 20 s gameplay video of a Practice session, and one through the real tunnel. Write the Windows/Linux and school Wi-Fi checklist for the owner into `SERVER_OPS.md`.
- [ ] **Step 5:** Run a fresh whole-branch review (one reviewer agent on the most capable model), then fix confirmed findings.
- [ ] **Step 6:** Commit: `"ShooterParty basis: final verification fixes"`.
