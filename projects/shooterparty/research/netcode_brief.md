# ShooterParty: netcode architecture brief

Date: 2026-10-24. Project: Unity 6000.3.9f1, URP 17.3, macOS (Apple Silicon) editor with only Mac Standalone installed.
Tags: **[verified]** means I checked it myself (package contents, registry metadata, source). **[unverified]** means I could not confirm it from a primary source. Everything else is cited inline.

---

## 0. Recommendation

| Decision | Choice |
|---|---|
| Library | **Netcode for GameObjects 2.13.3** (`com.unity.netcode.gameobjects`). Do not take 3.x: 3.0/3.1 require Unity 6000.7 and pull in Netcode for Entities as a hard dependency ([registry](https://packages.unity.com/com.unity.netcode.gameobjects)) **[verified]** |
| Transport | Unity Transport **2.7.4** (`com.unity.transport`). UDP by default. DTLS if you want encryption. WSS on 443 only as a fallback for networks that block UDP |
| Topology | Client-server with **server-authoritative physics**. The same code runs as **Host** (LAN, dev, Relay fallback) and as **Dedicated Server** (Pi). One room per server process |
| Tick | Physics 50 Hz (Unity default `fixedDeltaTime` 0.02) with NGO TickRate 50. NGO recommends matching the tick rate to FixedUpdate ([NGO time](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/networktime-ticks.html)). 30/60 is the cheaper alternative |
| Pi server | Viable: Unity 6.3 officially ships a Linux Arm64 IL2CPP Dedicated Server player (details in §1). Use Raspberry Pi OS Bookworm or Trixie, or Ubuntu 24.04. Try the 16K kernel first, keep `kernel=kernel8.img` as the fallback |
| Fallback | UGS Multiplayer Services 2.4.1 Sessions with Relay (join code, host-based). The same NGO code works, and it is free under 50 CCU |
| Testing | Multiplayer Play Mode 2.0.2 plus Multiplayer Tools 2.2.12 (Network Simulator, Runtime Network Stats Monitor) |

Suggested manifest additions (current versions from the registry, **[verified]** 2026-10-24):
```json
"com.unity.netcode.gameobjects": "2.13.3",
"com.unity.transport": "2.7.4",
"com.unity.multiplayer.tools": "2.2.12",
"com.unity.multiplayer.playmode": "2.0.2",
"com.unity.dedicated-server": "2.0.2",
"com.unity.services.multiplayer": "2.4.1"
```
`com.unity.services.multiplayer` is only needed for the Relay fallback. `com.unity.dedicated-server` adds Multiplayer Roles, which MPPM scenarios use ([MPPM configs](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@2.0/manual/play-mode-scenario/play-mode-scenario-configurations.html)). NGO 2.13.3 declares transport 2.6.0 as its minimum. Services 2.4.1 pulls 2.7.4, so pinning 2.7.4 keeps the two consistent.

---

## 1. Linux ARM64 dedicated server from Unity 6.3

### 1.1 Is it supported?
Yes.
- **Release note** (quoted in the forum): "Player: Added support for building dedicated server players that target Linux Arm64 platforms using IL2CPP." ([forum IN-118136](https://discussions.unity.com/t/in-118136-fail-to-build-linux-server-arm64-conversion-to-bee-architecture-not-implemented-for-linux-architecture-arm64/1685359))
- **Unity 6.3 system requirements.** The Server platform supports "Ubuntu 22.04 (AMD64 or Arm64), Ubuntu 24.04 (AMD64 or Arm64)", with the CPU listed as "x64 … Arm64". The desktop Linux player is x64 only ([6.3 system requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)). Raspberry Pi OS is not on that list, so on Raspberry Pi OS you are outside official support even though the binary is the same.
- **Early bug, fixed.** 6.3 betas failed with `NotImplementedException: Conversion to Bee architecture not implemented for Linux architecture: arm64` (UUM-122045), reproduced on macOS 15 Apple Silicon too. The tracker says "IL2CPP is required to build a Linux Server for ARM" and "addressed in 6000.3.0b10" ([issue tracker](https://issuetracker.unity.com/issues/18979/internal-build-system-error-when-building-a-linux-server-for-arm)). The thread's reporter then built from an x86_64 Windows host and ran the server on a **Raspberry Pi 3 Model B** and in Docker ([forum](https://discussions.unity.com/t/in-118136-fail-to-build-linux-server-arm64-conversion-to-bee-architecture-not-implemented-for-linux-architecture-arm64/1685359)). 6000.3.9f1 is after b10.
- **[verified] Module contents.** I downloaded `UnitySetup-Linux-Server-Support-for-Editor-6000.3.9f1.pkg` (changeset 7a9955a4f2fa, 223 MB) and expanded it without installing. Its `Variations/` folder contains `linuxarm64_server_development_il2cpp` and `linuxarm64_server_nondevelopment_il2cpp`, plus x64 mono/il2cpp server variations. **There is no arm64 Mono variation, so arm64 means IL2CPP.**

### 1.2 What to install
1. In Unity Hub, add the module **"Linux Dedicated Server Build Support"** (id `linux-server`) to 6000.3.9f1 ([dedicated server requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-requirements.html)). The module includes the arm64 IL2CPP server variations listed above. "Linux Build Support (IL2CPP)" is for the desktop client player and should not be needed. **[unverified]** whether the build profile asks for it anyway. Add it if it does.
2. Cross-compile toolchain packages, both 1.1.0 and both targeting 6000.3:
   - `com.unity.toolchain.macos-arm64-linux`: "runs on Arm64 macOS hosts and supports both x86_64 and Arm64 player targets" ([registry](https://packages.unity.com/com.unity.toolchain.macos-arm64-linux))
   - `com.unity.sdk.linux-arm64`: the Arm64 sysroot needed for IL2CPP Linux and Embedded Linux players ([docs](https://docs.unity3d.com/Packages/com.unity.sdk.linux-arm64@1.0/manual/index.html), [registry](https://packages.unity.com/com.unity.sdk.linux-arm64))
   - The manual says Unity auto-installs toolchain packages when you pick a Linux target and the scripting backend is IL2CPP. This is controlled by Project Settings > Toolchain Management. Budget about 2 GB of disk ([Linux IL2CPP cross-compiler](https://docs.unity3d.com/6000.3/Documentation/Manual/linux-il2cpp-crosscompiler.html)). **[unverified]** that auto-install picks the arm64 sysroot. If it doesn't, add both packages by name.
3. Open Build Profiles, choose **Linux Server**, set Architecture to **Arm64**, and set the scripting backend to IL2CPP. From the CLI: `-buildTarget Linux64 -standaloneBuildSubtarget Server` ([dedicated server build](https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-build.html)). **[unverified]** the exact CLI switch for selecting arm64. Use a build script that sets the profile.
4. Burst: Burst 1.8.24 (2025-08-01) "Added Linux/ARM64 support" ([Burst changelog](https://docs.unity3d.com/Packages/com.unity.burst@1.8/changelog/CHANGELOG.html)). UTP 2.7.4 depends on Burst 1.8.24. If Burst can't compile for a target, "Unity doesn't use Burst compilation, but still builds the target without Burst optimizations" ([Burst building projects](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/building-projects.html)). Worst case is slower code, not a broken build.

### 1.3 Runtime requirements on the Pi [verified from binaries]
- `UnityPlayer.so` (arm64 server, non-dev) imports glibc symbols up to **GLIBC_2.35**, and its NEEDED list is only libc, libm, libgcc_s, libanl and ld-linux-aarch64. So you need glibc ≥ 2.35:
  - Raspberry Pi OS **Bookworm** has glibc 2.36: OK ([Debian](https://packages.debian.org/bookworm/libc6))
  - **Trixie** has 2.41: OK ([Debian](https://packages.debian.org/trixie/libc6))
  - **Bullseye** has 2.31: **will not start** ([Debian](https://packages.debian.org/bullseye/libc6))
  - Ubuntu 22.04 (2.35) and 24.04 are the officially supported options.
- Oddity: the arm64 server folder ships `libdecor-0.so.0` and `libdecor-cairo.so` as **x86-64** ELF files. A headless server shouldn't load these Wayland decoration libraries. **[unverified]** whether this causes harmless warnings.

### 1.4 The 16 KB page-size question
Background: the Pi 5 firmware loads `kernel_2712.img` by default "because this image contains optimisations specific to those models (for example, 16K page-size)". Setting `kernel=kernel8.img` in `/boot/firmware/config.txt` loads the common 4K-page kernel ([Raspberry Pi config.txt](https://www.raspberrypi.com/documentation/computers/config_txt.html), [pimylifeup guide](https://pimylifeup.com/raspberry-pi-page-size/)). Software built with a 4K-page assumption can segfault or refuse to load ([pimylifeup guide](https://pimylifeup.com/raspberry-pi-page-size/)). For example, a Qt5 app on Pi 5 crashes on 16K and works on kernel8.img ([Gambas tracker](https://lists.gambas-basic.org/archive/bugtracker/2026-03/0000011.html)).

What I found for Unity:
- **[verified]** Every LOAD segment of the arm64 `LinuxPlayer` and `UnityPlayer.so` is aligned to `2**16` (64 KiB). 64 KiB alignment works on 4K, 16K and 64K kernels, so the classic "ELF alignment 4096 < page size 16384" loader failure seen on Android ([Unity forum, Android](https://discussions.unity.com/t/android-builds-crash-immediately-after-launching-the-app-or-game/1575697)) **does not apply to the engine binaries**.
- **[verified]** `UnityPlayer.so` imports `getpagesize` and `sysconf` and contains the strings `Failed to get _SC_PAGESIZE` and `Baselib_Memory_GetPageSizeInfo`. This suggests the allocator asks for the page size at runtime instead of hard-coding 4096. That is not proof.
- **[unverified]** `GameAssembly.so`, your IL2CPP code, is linked per project by Unity's toolchain. If that toolchain uses LLD, AArch64 defaults to a 64 KiB max page size ([LLD source](https://github.com/llvm/llvm-project/blob/main/lld/ELF/Arch/AArch64.cpp), `defaultMaxPageSize = 65536`). Check after the first build with `readelf -lW GameAssembly.so | grep LOAD`. Every Align value should be ≥ 0x4000.
- I found **no public report of a Unity server on a Pi 5** in either kernel mode. The only hardware report is the Pi 3 one above.

Plan: boot the stock 16K kernel and run the server. If it crashes at startup, in the GC, or on the first allocation, add `kernel=kernel8.img`, reboot, check that `getconf PAGESIZE` prints 4096, and move on. 16K pages should be faster, but I found no benchmark (search returned only anecdotes). For a server using a few hundred MB the difference is negligible, so **switching to 4K is a safe fallback**.

### 1.5 PhysX on ARM
- PhysX runs the same on arm64 (it is the same engine used on mobile and Apple Silicon). Determinism doesn't matter for this design because only the server simulates and clients interpolate.
- Cross-machine bit-exactness is not guaranteed anyway. PhysX "Enhanced Determinism" only promises repeatability on the same machine ([Unity discussions](https://discussions.unity.com/t/state-of-determinism-in-unity/867770), [uninomicon](https://uninomicon.com/physics_determinism)).
- This would only matter with client-side prediction (FishNet or PurrNet), where an ARM server and an x86 or M-series client diverge slightly and trigger more frequent small reconciles. PurrNet's docs say this outright ([PurrNet PredictedRigidbody](https://purrnet.dev/docs/client-side-prediction/built-in-components/predicted-rigidbody-2d-and-3d)).

### 1.6 Server hygiene
- **Cap the frame rate.** The default `targetFrameRate = -1` with vSync off means "rendered unsynchronized as fast as possible" ([API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-targetFrameRate.html)), and a headless server can't vSync. Set `QualitySettings.vSyncCount = 0` and `Application.targetFrameRate = 60` under `#if UNITY_SERVER`, or pass `-framerate` ([DedicatedServer.Arguments.TargetFramerate](https://docs.unity3d.com/ScriptReference/DedicatedServer.Arguments.TargetFramerate.html), [Multiplay best practices](https://docs.unity.com/en-us/multiplay-hosting/guides/game-engine-best-practices)).
- The Dedicated Server target already disables audio, strips GPU-only textures and meshes, and unregisters rendering, UI and input callbacks ([dedicated server optimizations](https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-optimizations.html)). Also turn off Rigidbody interpolation on the server ([optimizing server builds](https://docs.unity3d.com/Packages/com.unity.multiplayer.tools@2.2/manual/porting-to-dgs/optimizing-server-builds.html)).
- Test the arm64 build locally first: Apple Silicon runs arm64 Linux containers natively, and the forum reporter ran the arm64 server in Docker. **[unverified]** the page size of Docker Desktop's VM, so this does not exercise the 16K case.

---

## 2. Netcode library choice

### 2.1 Candidates (versions as of 2026-10-24)

| | NGO 2.13.3 | FishNet 4.7.4R | Mirror 96.11.3 | PurrNet 1.25 beta + PurrDiction 1.4.1 beta |
|---|---|---|---|---|
| Release | 2026-09-21, min Unity 6000.0 ([registry](https://packages.unity.com/com.unity.netcode.gameobjects)) | 16 Oct; "compiles clean on Unity 6.6" ([releases](https://github.com/FirstGearGames/FishNet/releases)); Unity 6 LTS "fully supported" ([compat](https://fish-networking.gitbook.io/docs/overview/readme/features/unity-compatibility)) | 17 Sep 2026 ([releases](https://github.com/MirrorNetworking/Mirror/releases)); Unity 6.3 support **[unverified]** | Daily betas, last 2026-10-23 (OpenUPM); "Requires Unity 2022 or newer", MIT ([GitHub](https://github.com/PurrNet/PurrNet)) |
| Rigidbody prediction | **None.** "Netcode for GameObjects doesn't support full client-side prediction and reconciliation" ([anticipation](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/client-anticipation.html)) | **Yes.** CSP with `PredictionRigidbody`; forces must go through it, and collisions need `NetworkCollision`/`NetworkTrigger` ([PredictionRigidbody](https://fish-networking.gitbook.io/docs/guides/features/prediction/predictionrigidbody)) | `PredictedRigidbody`, but the docs say "purely experimental, we don't recommend using this just yet" and predicted player movement is untested ([Mirror CSP](https://mirror-networking.gitbook.io/docs/manual/general/client-side-prediction)) | `PredictedRigidbody` with policies such as FullPrediction, ServerRelay and PredictedIfOwned ([docs](https://purrnet.dev/docs/client-side-prediction/built-in-components/predicted-rigidbody-2d-and-3d)) |
| Cost | Free | Free core. Lag-comp collider rollback is "Pro" ([docs index](https://fish-networking.gitbook.io/docs/llms.txt)) | Free (MIT) | Free ([pricing](https://purrnet.dev/docs/readme/pricing)) |
| Fit with the team's pitch | The pitch already names NGO. Integrates with Unity tooling (MPPM, Multiplayer Tools, Sessions/Relay) | Third-party; Relay needs a community transport | Third-party | Third-party, still beta |

### 2.2 NGO capabilities that matter here
- **NetworkRigidbody.** It "sets the Rigidbody … into kinematic mode on all non-authoritative instances"; physics runs only on the authority. *Use Rigidbody for Motion* syncs PhysX pose in FixedUpdate ([NetworkRigidbody](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/helper/networkrigidbody.html)).
- **NetworkTransform** ([NetworkTransform](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/helper/networktransform.html)):
  - Server-authoritative by default; owner authority is a setting.
  - Sends on ticks, and "The default TickRate is 30".
  - Interpolators: Legacy, Lerp and SmoothDampening. Lerp is frame-rate independent and works with FixedUpdate.
  - Half-float position takes 2 bytes per axis with delta encoding. Quaternion compression takes "16 bytes reduced down to 4 bytes".
  - Avoid `UseUnreliableDeltas` together with *Use Rigidbody for Motion* and multiple interacting rigidbodies.
  - Use Teleport/SetState for respawn, one per tick.
- **Anticipation.** `AnticipatedNetworkTransform` / `AnticipatedNetworkVariable` let a client show a visual value ahead of the server and `Smooth()` corrections. There is no physics rollback or resimulation ([anticipation](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/client-anticipation.html)). Useful for cosmetic leads, not for real rigidbody prediction.
- **Distributed authority** needs the Unity-hosted DA service via `com.unity.services.multiplayer` and a Unity Cloud project ([DA quickstart](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/learn/distributed-authority-quick-start.html)). It can't run on your Pi. NGO's docs warn "there's typically no single physics simulation governing the interaction of all objects" ([DA concepts](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/terms-concepts/distributed-authority.html)), which is the opposite of what a bump/knockback game needs. Connection approval is "not currently supported" under DA ([connection approval](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/connection-approval.html)). **Reject DA.**
- **NetworkSceneManager** ([using NetworkSceneManager](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/scenemanagement/using-networkscenemanager.html)):
  - Only the server or host loads scenes, and the scenes must be in the build list.
  - Single mode unloads all additive scenes. Only additive scenes can be unloaded.
  - Start a round on `SceneEventType.LoadEventCompleted`, which fires when all clients have loaded.
  - Pattern for the mini-game sequence: a persistent bootstrap scene holds the NetworkManager and session state, and each mini-game loads in Single mode.
- **Connection approval** ([connection approval](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/connection-approval.html)):
  - `ConnectionData` payload set before `StartClient`.
  - Supports `Pending`, `Reason` (sent to the client as a DisconnectReason) and a per-client `PlayerPrefabHash`.
  - The payload is **not encrypted or authenticated**.
  - Put a build/version hash, player name, color and room password in it, and reject version mismatches.

### 2.3 Feel versus amount of code, for a 4-player rigidbody knockback game
- **NGO server-authoritative, recommended.** Every rigidbody (players, blobs) is simulated once on the server.
  - Player-vs-player bumps and blob impulses are symmetric and consistent on every screen. The hit and the reaction arrive in the same snapshot stream, so they always line up visually.
  - Mass changes are a one-liner on the server.
  - Cost: your own movement lags by about one RTT plus buffers (§4). On LAN that is a few ms. Over the internet to a nearby Pi it is roughly 80 to 150 ms. Ice movement is inertial and force-based, which masks much of it **[unverified, judgment]**.
  - Custom code: an input RPC and the cosmetic layer in §4.
- **NGO owner-authoritative players.** Your own movement is instant, but:
  - Knockback must become an RPC to the victim's owner, so the reaction lands about one RTT after the hit is seen.
  - Your dynamic body collides with kinematic remote bodies. Remote players act as infinite-mass walls you can't push, and bumps differ per screen.
  - NGO's docs note mixed-authority collisions depend on latency ([NetworkTransform](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/helper/networktransform.html)).
  - This is the classic source of "it felt wrong" bugs in shove games. Keep it only as an escape hatch.
- **FishNet CSP with PredictionRigidbody.** Best feel over a laggy internet link (instant own movement, server-authoritative), but the most code and debugging:
  - Replicate/reconcile methods.
  - All forces routed through `PredictionRigidbody` and simulated in replicate.
  - `NetworkCollision`/`NetworkTrigger` instead of Unity callbacks.
  - TimeManager physics mode.
  - Tuning prediction interpolation ([TimeManager](https://fish-networking.gitbook.io/docs/guides/features/prediction/configuring-timemanager), [PredictionManager](https://fish-networking.gitbook.io/docs/guides/features/prediction/configuring-predictionmanager)).
  - Predicted player-vs-player collisions are the hardest case for any rollback scheme. A user report shows client jitter from `AddForce` on a predicted ball ([forum](https://discussions.unity.com/t/fishnet-addforce-to-predictionrigidbody-causes-jitter-in-client/1501355)).
  - Switch only if internet playtests at the measured RTT feel bad and the team has time. That realistically means after the 16 Nov demo.
- **Mirror and PurrNet.** Mirror's prediction is self-declared experimental. PurrNet/PurrDiction is promising but beta (daily beta tags). Neither beats NGO's tooling integration for a graded deadline.

### 2.4 NGO pitfall list
1. No built-in rigidbody prediction. Don't try to bolt rollback onto NGO.
2. Non-authority NetworkRigidbody instances are kinematic. With default contact-pair settings, "collision events won't fire when colliding with other networked Rigidbody instances" on clients. Run gameplay collisions on the server only. If clients need collision callbacks for cosmetics, set *Enable All Contact Pairs* ([NetworkRigidbody](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/helper/networkrigidbody.html)).
3. Inbound messages are processed in EarlyUpdate, before FixedUpdate. Queue RPC input and apply it in FixedUpdate rather than touching the Rigidbody inside the RPC ([physics](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/physics.html)).
4. Match tick and physics rates ([time](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/networktime-ticks.html)).
5. Use a single teleport (SetState) per tick for respawns. Don't call `transform.position =` on the server.
6. With owner-auth spawns, spawn as server first, then call `ChangeOwnership` to avoid parenting/offset glitches ([NetworkTransform](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/helper/networktransform.html)).
7. Every NetworkObject prefab must be registered in the NetworkPrefabs list, and client and server must be the same build. Gate this with the approval payload.
8. One NetworkManager means one session per process. Run two rooms as two server processes on two ports **[unverified as a hard limit; standard practice]**.
9. Host mode: the host has zero latency (host advantage), and NetworkTransform interpolation does not apply on the authority.
10. NGO 2.13.3 fixed interpolation against the local clock and frame-rate-dependent lerp smoothing ([registry changelog](https://packages.unity.com/com.unity.netcode.gameobjects)). Don't use an older 2.x.

---

## 3. Transport (Unity Transport 2.7.4)

- **Protocols.**
  - UDP is the default.
  - WebSocket works on desktop players too: "assign the WebSocketNetworkInterface in the non-WebGL clients too", and the server must explicitly use the WebSocket interface. It "can only be used to connect clients and servers that are both using UTP" ([UTP WebSockets](https://docs.unity3d.com/Packages/com.unity.transport@2.7/manual/websockets.html)).
  - In NGO, set `UnityTransport.UseWebSockets` (the default is UDP) ([UnityTransport API](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.Transports.UTP.UnityTransport.html)).
  - A raw UTP server can listen on UDP and WS at once with `MultiNetworkDriver`, up to 4 drivers ([cross-play](https://docs.unity3d.com/Packages/com.unity.transport@2.7/manual/cross-play.html)). **[unverified]** whether NGO's UnityTransport component exposes this. Assume one protocol per server process.
- **Encryption.** `UseEncryption = true` gives DTLS on UDP and TLS (wss) on WebSocket.
  - Server: `SetServerSecrets(certPem, keyPem)`.
  - Client: `SetClientSecrets(serverCommonName, caCertificate = null)`. With no CA, validation uses the OS or browser store. For self-signed setups, pass your root CA ([UnityTransport API](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.Transports.UTP.UnityTransport.html)).
  - UTP's guide shows generating your own root CA plus a server cert with OpenSSL. Its warning: keep server keys out of client builds ([UTP encryption](https://docs.unity3d.com/Packages/com.unity.transport@2.7/manual/client-server-secure.html)).
  - Desktop-only clients can use a self-signed CA embedded in the client. Only browsers need a public-CA certificate ([UTP WebSockets](https://docs.unity3d.com/Packages/com.unity.transport@2.7/manual/websockets.html)).
- **WSS on 443 behind nginx or Cloudflare.** UTP's docs list "your own reverse proxy that performs TLS termination" as an option for "advanced users" ([UTP WebSockets](https://docs.unity3d.com/Packages/com.unity.transport@2.7/manual/websockets.html)).
  - In practice, a user with nginx on 443 (Upgrade headers set) could not get NGO to connect even though websocat and curl worked. The UTP maintainer replied "No need for a reverse proxy at all": load a real cert with `SetServerSecrets` and set `serverCommonName` to the game server's domain ([forum](https://discussions.unity.com/t/issues-setting-up-encrypted-websocket-connection-with-unity-transport/1620104)).
  - **Direct WSS with a Let's Encrypt cert on a DDNS hostname is the lower-risk path. A proxy in front of UTP is [unverified].**
  - Cloudflare proxies WebSockets on all plans, but it closes idle connections and restarts break sockets ([Cloudflare WebSockets](https://developers.cloudflare.com/network/websockets/)).
  - Cloudflare Tunnel public hostnames carry HTTP/WebSocket. Its TCP mode needs cloudflared on each client, and UDP is not a public-hostname service type ([Tunnel routing](https://developers.cloudflare.com/tunnel/routing/)). A tunnel only helps the WSS path.
- **Latency of WS/TCP versus UDP.**
  - With no loss, per-packet latency is essentially the same. You pay a TCP+TLS handshake at connect and about 40 to 60 bytes more header per packet **[unverified, estimate]**.
  - On loss, TCP head-of-line blocking stalls all newer data until the retransmit arrives: at least one RTT, often about two ([Gaffer: UDP vs TCP](https://gafferongames.com/post/udp_vs_tcp/)). Linux's minimum RTO is 200 ms ([end2end list](https://mailman.postel.org/pipermail/end2end-interest/2004-November/004402.html)).
  - On clean home or school links the difference is invisible. On lossy Wi-Fi expect 100 to 200 ms hitches per lost packet. Use UDP as the default and WSS only where UDP is blocked.

---

## 4. Lag and feel model

### 4.1 Reference points
- **Snapshot interpolation buffer.** Size it to survive two lost packets. About 3× the send interval at 2 to 5% loss, plus jitter allowance. Gaffer quotes about 150 ms at 30 snapshots/s and 85 ms at 60/s ([Gaffer: snapshot interpolation](https://gafferongames.com/post/snapshot_interpolation/)).
- **State synchronization** (send state plus input, extrapolate with local physics, snap the simulation and smooth only the render) suits physics-heavy games ([Gaffer: state sync](https://gafferongames.com/post/state_synchronization/)).
- **NGO defaults:** `NetworkTimeSystem` serverBufferSec defaults to 0.05 s and is tunable at runtime on clients ([API](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.NetworkTimeSystem.html)). NetworkTransform Lerp/SmoothDampening suggest starting at 0.5 s max interpolation time and lowering it for precision ([NetworkTransform](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/helper/networktransform.html)).
- **Commercial party games.**
  - Stumble Guys was built on Photon Quantum, a deterministic simulation ([Photon interview](https://mobidictum.com/photon-engine-interview-christof-wegmann/)).
  - Fall Guys' prototype netcode was co-developed with The Multiplayer Group, and they had to write their own ragdoll networking because a third-party one "didn't network properly" (per the [MPG case study](https://www.themultiplayergroup.com/case-studies/fall-guys) as summarized in search; the page didn't render for me). Its authority model is not public; community claims of weak server authority are **[unverified]** ([HN](https://news.ycombinator.com/item?id=26290512)).
  - Rocket League's GDC 2018 talk "It IS Rocket Science!" covers server-authoritative networked physics ([Rocket League](https://www.rocketleague.com/news/rocket-league-at-gdc-2018)). That it predicts all cars and runs at 120 Hz is **[unverified]**: it comes from secondary summaries.
  - Gang Beasts and Party Animals: no public technical write-ups found **[unverified]**.
  - Takeaway: the big titles either buy deterministic rollback (Quantum) or invest heavily in custom physics netcode. Neither fits a 3-month school project. Server authority plus cosmetics does.

### 4.2 Recommended model for Knockback Arena (and the later mini-games)
1. **Server-authoritative simulation.**
   - The server or host runs PhysX at 50 Hz with NGO TickRate 50.
   - Players and arena hazards use NetworkObject + NetworkTransform (server authority, interpolation on, Lerp type, half-float position, quaternion compression) + NetworkRigidbody (*Use Rigidbody for Motion*).
   - Mass, score and state live in server-written NetworkVariables.
   - Falling off the edge is decided on the server only.
2. **Input, not state, from clients.**
   - Every tick, send `(moveX, moveY in camera space, aimYaw, aimPitch, buttons, clientTick)` via an unreliable RPC. Include the last 2 or 3 inputs for loss redundancy.
   - Send fire presses as a reliable RPC, or as a press counter.
   - The server queues input and applies forces in FixedUpdate.
   - Camera yaw and pitch are local and instant. Rotate the character's visual mesh child toward the aim locally (cosmetic), so turning feels zero-latency even though the body translates with delay.
3. **Spit with no perceived delay.**
   - On press, the shooter immediately plays the spit animation, SFX and mouth VFX, and spawns a *cosmetic* blob from the displayed mouth along the local aim.
   - The server spawns the authoritative blob from its own mouth position with the client's aim. Optionally fast-forward it by about min(RTT/2, 100 ms) so it lines up with the shooter's local blob, as FishNet's lag-compensation "Projectiles" guide does ([docs index](https://fish-networking.gitbook.io/docs/llms.txt)).
   - **Prefer blobs as events, not NetworkObjects.** The server broadcasts `BlobSpawned(id, p0, v0, serverTick)`. Each client simulates the ballistic arc in closed form (`p = p0 + v0·t + ½·g·t²`) from the elapsed time. The shooter's cosmetic blob blends into that path over about 0.1 to 0.15 s.
   - The server alone tests hits (SphereCast per physics step on the server copy). This avoids NGO spawn and despawn traffic for short-lived objects.
4. **Knockback is server-confirmed and visually instant.**
   - On hit, the server applies `AddForce(impulse, ForceMode.Impulse)`, changes mass in the same physics step, and sends `HitEvent(victim, point, impulse)` to everyone.
   - Clients play splat VFX, SFX, hit-stop and camera shake on receipt. Because the victim's flight arrives in the same interpolated stream, hit and reaction coincide on every screen.
   - Optional "feel" cheat: when the shooter's cosmetic blob overlaps a remote player's interpolated collider, play a small splat immediately and suppress the duplicate when `HitEvent` arrives. A wrong guess costs only a splat with no knockback.
5. **Tuning and escape hatches.**
   - Start with serverBufferSec 0.05 and interpolation time around 0.1 s.
   - Test with Network Simulator presets: Home Broadband (32 ms / 12 ms / 2%) and the Boss Room baseline of 100 to 150 ms with 5 to 10% loss ([NGO testing](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/tutorials/testing/testing_with_artificial_conditions.html)).
   - If own movement feels too laggy over the internet, step 1 is a cosmetic lead: offset your own body's *visual* by `velocity × (RTT/2 + buffer)`, using `AnticipatedNetworkTransform` + `Smooth()`.
   - Step 2 is owner authority for the local player with knockback by RPC to the owner, accepting the bump asymmetry.
   - Step 3, a port to FishNet CSP, is the only real fix for laggy links.
   - For blast parkour, explosions are server-side `AddExplosionForce` plus a broadcast event, the same pattern.

---

## 5. Bandwidth and CPU estimate

**Per-object size [unverified estimate from documented field sizes].** One NetworkTransform update carries:
- Position as half-float delta: 6 B
- Compressed quaternion: 4 B ([NetworkTransform](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/helper/networktransform.html))
- State flags, object and behaviour ids, tick and message header: about 15 to 20 B

Total is about **28 to 35 B**. Per packet, add IP+UDP (28 B) plus UTP and NGO batch headers (about 20 B), so roughly 50 B per packet. Idle objects below the threshold send nothing. Measure with the Network Profiler or RNSM before trusting these numbers.

| Scenario at 50 Hz | Per client down | Server up (all clients) |
|---|---|---|
| Room of 4, blobs as events (4 bodies) | 4×32+50 ≈ 180 B/tick, about **72 kbps** | 2 rooms × 4 = 8 clients: about **0.6 Mbps** |
| Room of 4, blobs as NetworkObjects (+6) | about 370 B/tick, about 150 kbps | 8 clients: about 1.2 Mbps |
| One room of 8, blobs as events | about 310 B/tick, about 120 kbps | about 1.0 Mbps |
| One room of 8, +12 networked blobs | about 690 B/tick, about 280 kbps | about 2.2 Mbps |

At 30 Hz, multiply by 0.6. Events (spawn and hit, about 40 B each, a few per second) are negligible. Client upload is about 50 Hz × 70 B ≈ **28 kbps** per client, so about 0.25 Mbps into the server for 8 clients. WSS adds about 40 to 60 B per packet, under 0.2 Mbps in total. **Conclusion:** even the worst case is about 2 to 3 Mbps up. The binding constraint is the home line's *upload* speed (the "30 Mbps" is probably the download figure; check upload), not the protocol.

**CPU on a Pi 5 [unverified estimate; no public Unity-on-Pi-5 benchmark found].** A Cortex-A76 at 2.4 GHz, with 4 cores. Per room:
- PhysX for about 5 to 25 bodies plus an arena collider
- NGO serialization for 4 to 8 clients
- Unity player loop overhead

That should total about 1 to 3 ms per 60 Hz frame, which is 5 to 20% of one core. Use one process per room, so two rooms take two cores. Memory is roughly 150 to 400 MB per process. The only hardware data point is the arm64 server running on a much weaker Pi 3 B ([forum](https://discussions.unity.com/t/in-118136-fail-to-build-linux-server-arm64-conversion-to-bee-architecture-not-implemented-for-linux-architecture-arm64/1685359)). Measure with `top` and a development build connected to the Profiler. Use active cooling and wired Ethernet.

---

## 6. Unity Gaming Services alternative (short)

- **Free tiers** ([UGS pricing](https://unity.com/products/gaming-services/pricing)):
  - Relay: 50 average monthly CCU, 3 GiB per CCU, capped at 150 GiB/month.
  - Lobby: 10 GiB/month per regional group.
  - DA: 6,000 connectivity hours and 100 GiB.
  - A class project is far inside these.
- **Multiplay Hosting is gone.** Unity deprecated it as of 2026-04-01 ([status notice](https://status.unity.com/info_notices/362941)). "Online servers come later" therefore means self-hosting (the Pi) or a third-party host.
- **Sessions** (`com.unity.services.multiplayer` 2.4.1) unify Lobby, Matchmaker and Relay ([MPS SDK](https://docs.unity.com/ugs/en-us/manual/mps-sdk/manual)).
  - `new SessionOptions{MaxPlayers=4}.WithRelayNetwork()` makes the creator the host, and the join code is `session.Code`.
  - Relay supports UDP, DTLS and **WSS**.
  - `WithDirectNetwork(ListenIp, PublishIp, Port)` exists for direct IP ([session network](https://docs.unity.com/en-us/mps-sdk/manage-session-network-connection)).
- **As a fallback it is simpler than the Pi:**
  - No port forwarding, CGNAT exposure, domain, certs or ARM build.
  - WSS via Relay gets through firewalls that block UDP.
  - Costs: host advantage, the host's upload carries the room, a Unity Cloud project link and anonymous sign-in.
- **Coexistence.** It is the same NGO game code. Choose at runtime between "Join Pi" (`UnityTransport.SetConnectionData(ip, 7777)`) and "Host/Join code" (Sessions + Relay).
  - A Pi lobby with Relay traffic is possible but buys nothing, because Sessions already gives lobby and join codes.
  - More useful: the Pi dedicated server can itself allocate Relay and `StartServer`. NGO's Relay guide documents `StartServer` instead of `StartHost` ([Relay + NGO](https://docs.unity.com/relay/relay-and-ngo.html)). That gets the Pi through CGNAT or a closed router with no port forward, at the cost of a Relay hop **[unverified end-to-end on a headless arm64 server]**.

---

## 7. Testing

- **Multiplayer Play Mode 2.0.2** (Unity 6.3) ([MPPM 2.0](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@2.0/manual/index.html)):
  - Up to 4 editor players (main plus 3 virtual) on one machine.
  - Play Mode Scenarios can add up to 4 local build instances.
  - Scenario instances get client, server, or client-and-server roles via Dedicated Server package build profiles ([MPPM configs](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@2.0/manual/play-mode-scenario/play-mode-scenario-configurations.html)).
  - Use one server instance plus 3 clients.
  - Limitation: you can't edit GameObjects in virtual instances.
- **ParrelSync** is a community tool that runs full editor clones via symlinks ([coherence docs](https://docs.coherence.io/get-started/build-and-run/testing-locally)). Unity's own samples replaced it with MPPM for Unity 6 ([PR](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.bitesize/pull/218)). Use MPPM. ParrelSync on 6.3 is **[unverified]**.
- **Network Simulator** (Multiplayer Tools 2.2.12) ([Network Simulator](https://docs.unity3d.com/Packages/com.unity.multiplayer.tools@2.2/manual/network-simulator.html)):
  - Works through the UTP simulator pipeline stage.
  - Presets include Home Fiber 10/1/0, Home Cable 25/5/0, Home Broadband 32/12/2% and Mobile 4G 100/20/4%.
  - Supports custom presets, lag-spike triggers (`NetworkSimulator.TriggerLagSpike`), disconnect, and scripted scenarios (Connections Cycle, Random Connections Swap).
  - Note: `UnityTransport.SetDebugSimulatorParameters` is **obsolete and has no effect** in NGO 2.13. Use the Network Simulator ([UnityTransport API](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.Transports.UTP.UnityTransport.html)).
  - **[unverified]** whether the simulator component is active in development player builds.
- **OS-level simulation:**
  - macOS: Network Link Conditioner ([NGO testing](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/tutorials/testing/testing_with_artificial_conditions.html)).
  - On the Pi: `tc qdisc add dev eth0 root netem delay 40ms 10ms loss 2%` ([tc-netem](https://man7.org/linux/man-pages/man8/tc-netem.8.html)). This affects the real deployed path.
- **In-game diagnostics:** the Runtime Network Stats Monitor (Multiplayer Tools) shows RTT and bandwidth. Leave a toggle in for the demo.

---

## 8. Biggest risks (ranked)

1. **The demo network blocks UDP or inbound ports.** School Wi-Fi often allows only 80/443. Mitigation: a WSS:443 build option, or Relay-WSS host mode as the fallback. Test on the actual demo network before 16 Nov.
2. **The home line can't accept inbound traffic.** CGNAT (WAN IP in 100.64.0.0/10, [RFC 6598](https://www.rfc-editor.org/rfc/rfc6598)), no port forward, or a dynamic IP. Mitigation: DDNS, or the Pi allocating Relay as the server (§6).
3. **The 16K-page kernel.** Engine binaries look fine (64 KiB aligned). `GameAssembly.so` and the IL2CPP GC behaviour on 16K are **[unverified]**. Mitigation: `kernel=kernel8.img`.
4. **Raspberry Pi OS isn't an officially supported server OS.** Only Ubuntu 22.04/24.04 Arm64 is. You need glibc ≥ 2.35, so Bullseye fails. Mitigation: Bookworm/Trixie, or Ubuntu 24.04 on the Pi.
5. **Own-movement latency over the internet** with no prediction in NGO. Mitigation: cosmetic layer, measure RTT early, and the escape hatches in §4.2. FishNet CSP is the expensive last resort.
6. **IL2CPP and cross-compile friction.** Long build times, stripping, toolchain auto-install. Mitigation: a scripted build plus a smoke test in an arm64 Docker container on the Mac.
7. **Reverse proxy in front of UTP WSS** is unproven (forum failure, maintainer advises direct certs). Avoid it.
8. **Version drift.** Don't upgrade to NGO 3.x on Unity 6.3. Pin the packages above.
