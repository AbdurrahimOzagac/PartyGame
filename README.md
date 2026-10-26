# PartyGame — ShooterParty

ShooterParty is a 3–4 player online party game: cube critters, a spit shot, and a playlist of mini-games played one after another. The session total crowns a champion. It ships with one mini-game, **Knockback Arena**: spit everyone off the ice.

- Engine: **Unity 6000.3.9f1** (URP, Input System, Netcode for GameObjects 2.13, Unity Transport 2.7).
- The ShooterParty code is under `Assets/Ezel/` (`ShooterParty/` = the shared framework, `KnockbackArena/` = the mini-game). Teammates' folders: `Assets/Abd`, `Assets/Ardil`, `Assets/Kha`.
- Design and server: `projects/shooterparty/` (spec, plan, room service, deploy scripts).
- Developer docs: [architecture](Assets/Ezel/ShooterParty/Docs/ARCHITECTURE.md) · [adding a mini-game](Assets/Ezel/ShooterParty/Docs/ADDING_A_MINIGAME.md) · [server operations](Assets/Ezel/ShooterParty/Docs/SERVER_OPS.md).

## Open and play in the editor

1. In Unity Hub: **Add → Add project from disk** → this folder, with editor **6000.3.9f1**.
2. Open `Assets/Ezel/ShooterParty/Scenes/SP_Boot.unity` and press **Play**. The game always starts from `SP_Boot` (it loads the menu).
3. In the menu: **PRACTICE VS BOTS** plays a full session against three bots, with no network needed.

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move (relative to the camera) | WASD | Left stick |
| Look | Mouse | Right stick |
| Spit at the crosshair (hold to keep firing) | Left click | Right trigger |
| Dash | Shift | B or LB |
| Jump (when you hold a spring) | Space | A |
| Pause / settings | Esc | Start |
| Network stats | F3 | — |

The mouse is captured automatically during a round and released in menus, pause and results.

## Build for each OS

In the editor menu **ShooterParty → Build**: **macOS**, **Windows**, **Linux x64**, **Pi Server (Linux Arm64)** or **All**. Each build goes to `Builds/<target>/` (not in git). Each target needs its module (Unity Hub → Installs → 6000.3.9f1 → Add modules): *Windows Build Support (Mono)*, *Linux Build Support (Mono)*, *Mac Build Support (Mono)* (already part of the editor on a Mac), and for the Pi server *Linux Dedicated Server Build Support*. The Arm64 cross-compiler for the Pi comes from packages in `Packages/manifest.json` (`com.unity.toolchain.macos-arm64-linux`, `com.unity.sdk.linux-arm64`), so the Pi server is built on a Mac.

The same builds from the command line, run in the project folder with the editor closed (Unity locks an open project). `-logFile -` prints Unity's log to the terminal. Exit code 0 means the build succeeded; near the end of the log, a `[PartyBuild] … Succeeded` (or `Failed`) line gives the size and time.

macOS (Terminal):
```sh
UNITY=/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -nographics -quit -projectPath . -logFile - -executeMethod ShooterParty.EditorTools.PartyBuild.BatchMac
# the other targets: BatchWindows, BatchLinux, BatchPiServer (Pi: build on a Mac, see below)
```

Windows (PowerShell):
```powershell
$UNITY = "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe"
& $UNITY -batchmode -nographics -quit -projectPath . -logFile - -executeMethod ShooterParty.EditorTools.PartyBuild.BatchWindows
```

Linux (bash):
```sh
UNITY=~/Unity/Hub/Editor/6000.3.9f1/Editor/Unity
"$UNITY" -batchmode -nographics -quit -projectPath . -logFile - -executeMethod ShooterParty.EditorTools.PartyBuild.BatchLinux
```

The paths are Unity Hub's default install locations; if Hub installed the editor elsewhere, use the path shown in Hub → Installs → ⋯ → Show in Finder/Explorer.

The Pi server build (`BatchPiServer`, IL2CPP for Linux Arm64) takes 5–15 minutes and needs about 4 GB of free disk. It fails if the result is not an Arm64 binary.

## Launch a build

Give players the **whole** `Builds/<target>/` folder, not only the executable.

| | Start | First start |
|---|---|---|
| macOS | open `Builds/macOS/ShooterParty.app` | an app copied from another Mac is quarantined: right-click → **Open** once |
| Windows | `Builds\Windows\ShooterParty.exe` | unsigned: SmartScreen → **More info → Run anyway** |
| Linux | `chmod +x Builds/Linux/ShooterParty.x86_64 && ./Builds/Linux/ShooterParty.x86_64` | — |

Ways to play:
- **PLAY ONLINE → CREATE ROOM / JOIN ROOM**: through the server at `partyshooter.ezellm.com`. You get a 5-letter party code; 3–4 players start a session. It works on any network that allows HTTPS (port 443).
- **SAME WI-FI → HOST / JOIN BY ADDRESS**: no server needed; everyone is on the same network, and the host's lobby shows its address (UDP 7777, so allow the firewall prompt on the host).
- **PRACTICE VS BOTS**: alone, offline.

**Clients and the online server must come from the same commit.** Every build carries a fingerprint of its network prefabs; a game built from a different commit than the deployed server gets "Update your game". After changing networked prefabs (or running a *Rebuild* menu), the Pi server has to be redeployed before handing out new builds; see [server operations](Assets/Ezel/ShooterParty/Docs/SERVER_OPS.md).

## Generated assets

Scenes, prefabs and UI are generated from editor code, so change the builders, not the generated files:
- **ShooterParty → Rebuild core assets**: menu, lobby, HUD and session views, boot/menu/lobby scenes, mini-game catalog, network prefab list.
- **Knockback Arena → Rebuild Everything**: critters, VFX, UI widget, audio, the ice arena scene (it runs the core rebuild last).

## Tests

- In the editor: **Window → General → Test Runner**, then run **EditMode** and **PlayMode**.
- From the command line, in the project folder with the editor closed (`$UNITY` as in the build section; on Windows use `& $UNITY` and `$PWD\editmode.xml`):
  ```sh
  "$UNITY" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults "$PWD/editmode.xml" -logFile editmode.log
  "$UNITY" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults "$PWD/playmode.xml" -logFile playmode.log
  ```
  Exit code 0 means every test passed; the XML lists each test.
- Room service (Python 3, standard library only), from the project folder:
  ```sh
  (cd projects/shooterparty/server/rooms && python3 -m unittest test_room_service test_tickets)
  (cd projects/shooterparty/server && python3 -m unittest test_e2e_check)
  ```
- End to end on macOS: a desktop build plays a whole session against a local server (scenarios `basic`, `drop`, `drop-all`, `leave-during-load`, `rooms`; about 1–2 minutes each):
  ```sh
  projects/shooterparty/server/e2e_local.sh basic Builds/macOS/ShooterParty.app/Contents/MacOS/406Party
  ```
  It prints `OK` and exits 0 when the session reached its final with consistent scores. Right after copying in a new build, the first run once stalled at client start-up; running it again passed.
