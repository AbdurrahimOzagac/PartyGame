# Adding a mini-game

This guide adds a new mini-game (a race, a parkour, ...) that uses the shared cube critters and the spit shot. You need your own folder, assembly, scene, definition asset and controller. You do not edit core code.

Knockback Arena (`Assets/ShooterParty/MiniGames/KnockbackArena`) is the worked example at every step. Read [ARCHITECTURE.md](ARCHITECTURE.md) sections 3 and 4 first: they explain who decides what and how a session runs.

Below, `MyGame` stands for your mini-game's name.

## What the core does, and what you write

| The core does it for every mini-game | You write it |
|---|---|
| lobby, playlists, intro card, 3-2-1-GO countdown | the rules: a `MiniGameController` subclass |
| spawns one critter per seat at your spawn points, frozen until GO | the scene: world, camera, one `MiniGameScene` marker |
| round timer (`durationSeconds`) that ends the round | when a player is out, scores, the ranking |
| crown on the top in-game score, HUD cards, results, standings, final | optional: a `ShotEffect`, a bot brain, a HUD widget |
| shots: firing, validation, hit detection, knockback delivery | the `MiniGameDefinition` asset that ties it together |
| disconnects (a drop mid-round counts as a fall), rejoin, bot takeover | tests |
| input, camera, audio, network stats | |

The core does **not** detect falls, and a spawned critter never respawns by itself. Your controller decides when a critter is out (`Eliminate`) or put back (`RespawnAt`).

## Step 1. Folder and assemblies

Create `Assets/ShooterParty/MiniGames/MyGame/` with `Scripts/`, `Editor/`, `Tests/`, `Data/`, `Prefabs/`, `Scenes/` and `UI/`. Everything of yours stays in this folder.

Add three assembly definitions. Copy Knockback's and rename them.

| File | Name | References |
|---|---|---|
| `Scripts/MyGame.Runtime.asmdef` | `MyGame.Runtime` | `ShooterParty.Runtime`, `Unity.Netcode.Runtime`, `Unity.InputSystem`, `UnityEngine.UI`, `Unity.TextMeshPro` |
| `Editor/MyGame.Editor.asmdef` | `MyGame.Editor`, Editor platform only | `ShooterParty.Runtime`, `ShooterParty.Editor`, `Unity.Netcode.Runtime`, `MyGame.Runtime` (+ URP and UI assemblies if your builder needs them) |
| `Tests/MyGame.Tests.asmdef` | `MyGame.Tests` | `ShooterParty.Runtime`, `ShooterParty.PlayModeTests`, `Unity.Netcode.Runtime`, `MyGame.Runtime`, `UnityEngine.TestRunner`; Override References on with `nunit.framework.dll`; Auto Referenced off; define constraint `UNITY_INCLUDE_TESTS` |

Knockback's files: `KnockbackArena/Scripts/KnockbackArena.Runtime.asmdef`, `KnockbackArena/Editor/KnockbackArena.Editor.asmdef`, `KnockbackArena/Tests/KnockbackArena.Tests.asmdef`.

Rules:
- Use your own namespace (`namespace MyGame`). Core types are in `ShooterParty` and `ShooterParty.UI`.
- Never reference another mini-game's assembly. If you need something from Knockback's code, ask for it to move into the core.
- Shared art (critter, blob, fonts, sprites, VFX, sounds) stays in `Assets/ShooterParty/MiniGames/KnockbackArena`. Reference it, but do not move or rename it (ARCHITECTURE.md section 7).

## Step 2. The controller

Subclass `MiniGameController` (`ShooterParty/Scripts/MiniGames/MiniGameController.cs`). Seats are identified by **slot** (0-3) everywhere.

**Hooks you override.** All of them run on the server only, except `OnEliminatedClient`.

| Hook | When | What it must do | Knockback Arena |
|---|---|---|---|
| `OnSetup(IReadOnlyList<int> slots)` | critters are spawned and frozen | reset your per-round state | not used |
| `OnRoundStart()` | after the countdown; `Live` is now true | start round logic (e.g. open a gate) | not used |
| `OnServerTick(float dt)` | every frame while live | detect outs and goals, call `AddScore`/`Eliminate`/`RespawnAt`, and `EndRound()` when the round is decided | falls below `outY` or camping outside the ice for `campSeconds` → `Eliminate`; one left → `EndRound()` |
| `OnHit(in ShotHit hit)` | after the shot effect ran, while live | bookkeeping (who hit whom) | remembers the last hitter for KO credit |
| `OnBotSpawned(NetworkCritter critter)` | a bot's critter was spawned | add your bot brain component | `AddComponent<ArenaBotAI>()` |
| `AttachAutopilot(NetworkCritter critter)` | a self-playing test client takes its critter | default calls `OnBotSpawned`; usually leave it | default |
| `OnEliminatedClient(int slot, Vector3 position)` | **every client** (not on a dedicated server), when a slot is eliminated | fall FX and sound only | splash, sound, camera shake |
| `RankPlayers()` | inside `EndRound()` | return every slot in `Slots`, best first, as `RankEntry` with shared ranks for ties | alive first (KOs, then mass), then fallers, latest first |

**Helpers you call** (server):
- `AddScore(slot, n)`, `SetScore(slot, value)`: the in-game score. It drives the crown and the HUD cards. It is not session points: those come from the ranking.
- `Eliminate(slot)`: the critter disappears for everyone, its player spectates, and the HUD shows "OUT!". It is final for this round.
- `RespawnAt(slot, pose)`: teleports the critter (checkpoints). It works for humans and bots.
- `EndRound()`: ranks, despawns critters and pickups, and hands the ranking to the session. Call it once; calls after the first are ignored.
- `RankBy(ordered, tied)`: builds the "1, 2, 2, 4" ranking from an ordered list.
- Read-only: `Slots`, `AliveSlots`, `CritterOf(slot)`, `ScoreOf(slot)`, `IsAlive(slot)`, `Definition`, `Live`, `EndsAt`. The slot lists and `CritterOf` are filled on the server only. On clients, use `NetworkCritter.All`.

The default `RankPlayers()` ranks by in-game score. A slot you leave out of the ranking gets 0 points. The timer calls `EndRound()` for you at `durationSeconds`.

A minimal controller:

```csharp
using System.Collections.Generic;
using System.Linq;
using ShooterParty;
using UnityEngine;

namespace MyGame
{
    public class MyGameController : MiniGameController
    {
        public float outY = -2.5f;
        readonly List<int> fallOrder = new List<int>();

        protected override void OnSetup(IReadOnlyList<int> slots) => fallOrder.Clear();
        protected override void OnBotSpawned(NetworkCritter critter) => critter.gameObject.AddComponent<MyBotBrain>();

        protected override void OnServerTick(float dt)
        {
            foreach (var slot in AliveSlots.ToList())
                if (CritterOf(slot).transform.position.y < outY) { fallOrder.Add(slot); Eliminate(slot); }
            if (AliveSlots.Count() <= 1) EndRound();
        }

        protected override IReadOnlyList<RankEntry> RankPlayers()
        {
            var alive = AliveSlots.OrderByDescending(ScoreOf).ToList();
            var fallen = Enumerable.Reverse(fallOrder).ToList();          // the last to fall ranks higher
            var rest = Slots.Except(alive).Except(fallen);                // e.g. left mid-round
            return RankBy(alive.Concat(fallen).Concat(rest).ToList(), (a, b) => false);
        }
    }
}
```

**The controller prefab.** Make a GameObject with a `NetworkObject` and your controller, set its Definition field, and save it as `Prefabs/Net_MyGame.prefab`. The session spawns it when your scene has loaded. Do not place it in the scene. Knockback: `KnockbackArena/Prefabs/Net_KnockbackArenaGame.prefab`, built by `ArenaSetup.BuildMiniGame` in `KnockbackArena/Editor/ArenaSetup.cs`.

## Step 3. A shot effect (optional)

A `ShotEffect` says what a hit does in your game. Subclass it, add a `CreateAssetMenu`, and create the asset in `Data/`. With no effect set, hits still reach `OnHit` but do nothing to the victim.

- `ApplyServer(in ShotHit hit, ShotContext ctx)` runs on the server for each hit on a critter. Use `ctx.Critter(slot)` and `ctx.Knock(victim, impulse, massLossFraction, minMassFraction)`. `Knock` applies the victim's `knockbackTaken` modifier and delivers the push through the victim's owner.
- `OnFireLocal(NetworkCritter shooter)` runs on the shooter's own machine (the server for bots), at fire time: recoil, self-boosts.
- `OnOwnImpactLocal` exists but is not called by `ShotSystem` yet. A blast-jump game needs that wired in the core first.

```csharp
[CreateAssetMenu(menuName = "ShooterParty/Shot Effects/My Game", fileName = "ShotEffect_MyGame")]
public class MyShotEffect : ShotEffect
{
    public float impulse = 2f;
    public override void ApplyServer(in ShotHit hit, ShotContext ctx)
    {
        var victim = ctx.Critter(hit.victimSlot);
        if (!victim) return;
        var dir = hit.velocity; dir.y = 0f;
        ctx.Knock(victim, dir.normalized * impulse, massLossFraction: 0f);
    }
}
```

Knockback: `KnockbackArena/Scripts/KnockbackShotEffect.cs` (push along the blob's path, 10 % mass loss per hit), asset `KnockbackArena/Data/ShotEffect_Knockback.asset`.

## Step 4. A bot brain

Practice fills free seats with bots, and online rooms can replace leavers with bots. A bot brain is a plain `MonoBehaviour` that you add in `OnBotSpawned`.

- Drive the critter only through `CritterMotor` (`Move`, `Dash`, `Jump`, `FaceTowards`) and `CritterSpit.TryFire(aimDir)`. `TryFire` applies the cooldown and sends the shot through `ShotSystem`.
- Stand still unless `MiniGameController.Current.Live.Value` is true. Critters are frozen during the intro and countdown.
- The same brain also drives a test client's own critter (`AttachAutopilot`), so it must work on an owner client too. Do not call server-only APIs from it.

```csharp
[RequireComponent(typeof(CritterMotor), typeof(CritterSpit))]
public class MyBotBrain : MonoBehaviour
{
    CritterMotor motor; CritterSpit spit;
    void Awake() { motor = GetComponent<CritterMotor>(); spit = GetComponent<CritterSpit>(); }
    void Update()
    {
        var game = MiniGameController.Current;
        if (!game || !game.Live.Value) { motor.Move(Vector3.zero); return; }
        var toCentre = -transform.position; toCentre.y = 0f;
        motor.Move(Vector3.ClampMagnitude(toCentre, 1f));
        spit.TryFire(transform.forward);
    }
}
```

Knockback: `KnockbackArena/Scripts/ArenaBotAI.cs` (targets the nearest rival, strafes, avoids the edge, dashes back, detours for pickups).

## Step 5. The critter and its config

- `critterPrefab`: use `Assets/ShooterParty/MiniGames/KnockbackArena/Prefabs/CubeCritter_Net.prefab`, or a prefab variant of it saved in your `Prefabs/` folder if you need extra parts. Keep the `NetworkObject` on the root.
- `critter` (`CritterConfig`): `jump` (`Off`, `Pickup` = only after a pickup, `On`), `dashCharges`, `shotSpeed`, `shotGravity`, `shotCooldown`. `chargeTime` is reserved and does nothing yet. `CritterConfig.Default` is Knockback's tuning.
- Pickups: a `NetworkObject` prefab that the server spawns at the scene's pickup spots. Knockback's `JumpPickup` (`KnockbackArena/Scripts/JumpPickup.cs`) shows the pattern: the server detects the trigger, flips a `NetworkVariable`, and grants charges with `NetworkCritter.ServerAddJumpCharge`.

## Step 6. The scene

Create `Scenes/MyGame.unity` with:
- the world, with colliders;
- lights and post FX;
- a camera tagged MainCamera with `Camera`, `AudioListener`, `OverShoulderCamera` and `CameraShake`;
- one GameObject with `MiniGameScene`:
  - `spawnPoints`: 4 transforms. The i-th seat of the round gets point i. Critters spawn facing the world origin, so centre the play area on (0, 0, 0);
  - `pickupSpots` and `pickupPrefab`: optional.

Do not put in the scene:
- any `NetworkObject` (the server spawns everything);
- a UI canvas, `EventSystem` or `SfxPlayer` (`SP_Boot` brings them).

Knockback builds its scene from code in `ArenaSetup.BuildScene` (`KnockbackArena/Editor/ArenaSetup.cs`): sun, sky and fog, the ice prefabs, the camera rig, the post-FX volume, 4 spawn points and 2 pickup spots. A hand-made scene works just as well. The ice kit in `KnockbackArena/Prefabs/Arena/Kit/` has modular floes, edges and corners for new layouts. Add your own floor collider.

## Step 7. A HUD widget (optional)

The core HUD (`PartyHud`) shows a card per seat. Your widget adds game-specific information.

- Make a prefab with a `RectTransform` and your script, and save it in `UI/`.
- `PartyHud` instantiates it under its widget slot when a round of your game starts, and destroys it when another game's round starts.
- Get the HUD with `GetComponentInParent<PartyHud>(true)`, and the card for a seat with `hud.CardFor(slot)`.
- Write to the card with `SetStatLine(text, colour)`, `SetStatMeter(0..1)` and `PunchStat("-10")`.
- Only read network state (`NetworkCritter.All`, `Mass`, `Eliminated`, ...). A widget never changes the game.
- Clean up any world-space objects you create in `OnDestroy`.

```csharp
using ShooterParty;
using ShooterParty.UI;   // PartyHud, HudCard
using UnityEngine;

public class MyHudWidget : MonoBehaviour
{
    PartyHud hud;
    void Awake() => hud = GetComponentInParent<PartyHud>(true);
    void Update()
    {
        if (!hud) return;
        foreach (var c in NetworkCritter.All)
        {
            if (!c || !c.IsSpawned) continue;
            var card = hud.CardFor(c.Slot.Value);
            if (card) card.SetStatLine(Mathf.RoundToInt(c.MassFraction * 100f) + "%", Color.white);
        }
    }
}
```

Knockback: `KnockbackArena/Scripts/UI/KnockbackHudWidget.cs` (mass line, meter, hit punch) and `MassRing.cs` (a ring over every other critter). The prefabs `KnockbackArena/UI/HUD_KnockbackWidget.prefab` and `MassRing.prefab` are built by `UiSetup.BuildAll` in `KnockbackArena/Editor/UiSetup.cs`.

## Step 8. The definition asset

Create it with **Assets > Create > ShooterParty > Mini-game** in `Data/`, or from your editor builder.

| Field | Meaning | Knockback Arena |
|---|---|---|
| `id` | stable key stored in playlists: lowercase, no commas, at most 29 characters (it travels as a `FixedString32Bytes`) | `knockback_arena` |
| `displayName` | name in the lobby, intro card and HUD; keep it short (the "+ GAME" buttons share one row) | `Knockback Arena` |
| `tagline` | one line on the intro card | `Spit everyone off the ice.` |
| `howToPlay` | up to 3 short lines on the intro card | 3 lines |
| `controlsHint` | one line of controls on the intro card | see the asset |
| `icon` | sprite on the intro card | `UI/Sprites/UI_IconSpring.png` |
| `sceneName` | the scene's **name** (not its path); it must be in the build settings | `KnockbackArena_Ice` |
| `controllerPrefab` | the prefab from step 2 | `Prefabs/Net_KnockbackArenaGame.prefab` |
| `critterPrefab` | `CubeCritter_Net` or your variant | `Prefabs/CubeCritter_Net.prefab` |
| `durationSeconds` | the timer ends the round here | `120` |
| `placementPoints` | session points by rank, read from the top | `4, 3, 2, 1` |
| `critter` | `CritterConfig` (step 5) | `CritterConfig.Default` |
| `shotEffect` | the asset from step 3, or empty | `Data/ShotEffect_Knockback.asset` |
| `hudWidget` | the prefab from step 7, or empty | `UI/HUD_KnockbackWidget.prefab` |
| `minPlayers`, `maxPlayers`, `supportsBots` | stored, but the core does not read them yet: room size and bots are session settings | `3`, `4`, `true` |

Knockback creates this asset in code: `ArenaSetup.BuildMiniGame` writes `KnockbackArena/Data/KnockbackArena.asset`.

## Step 9. Register it

1. **Networked prefabs register themselves.** The core's prefab list (`ShooterParty/Data/NetPrefabs.asset`) is rebuilt from every prefab under `Assets/ShooterParty` whose root has a `NetworkObject`. Your controller, critter variant and pickups only need to live under `Assets/ShooterParty`.
2. **Add the scene to the build settings.** NGO can only load listed scenes. From your builder, call `ShooterPartySetup.EnsureInBuildSettings("Assets/ShooterParty/MiniGames/MyGame/Scenes/MyGame.unity")`, or add it by hand in the build profile's scene list.
3. **Run ShooterParty > Rebuild core assets** (`ShooterPartySetup.BuildAll`). It:
   - rebuilds the catalog with every `MiniGameDefinition` under `Assets/ShooterParty`, and warns if one has no scene, controller or critter prefab;
   - rebuilds the presets, which cycle through all games, so Quick/Classic/Marathon now include yours;
   - rebuilds `UI_Root.prefab`, which gets a "+ MY GAME" button in the playlist builder;
   - rebuilds `NetPrefabs.asset`, `SP_Menu`, `SP_Lobby` and `SP_Boot`.
4. Check the Console for `[ShooterParty] mini-games: ...` and `[ShooterParty] network prefabs: ...` with your entries.
5. Commit the regenerated core files (`MiniGameCatalog.asset`, `NetPrefabs.asset`, `UI_Root.prefab`, the `SP_*` scenes) together with your folder. If two branches both regenerated them, do not merge these files by hand: take either version, then run Rebuild core assets again after the merge.

Tip: write one editor menu item that builds your definition, prefabs and scene, then calls `ShooterPartySetup.BuildAll()` last. Knockback's is **Knockback Arena > Rebuild Everything (critters, VFX, UI, audio, ice arena scene)** (`ArenaSetup.RebuildEverything`).

## Step 10. Play it in Practice

1. Open `Assets/ShooterParty/Core/Scenes/SP_Boot.unity` and press Play. Pressing Play in your own scene does not start a session.
2. In the menu, choose **PRACTICE VS BOTS** and enter a name. You land in the lobby with 3 bots.
3. In the playlist panel, choose **CUSTOM**, add your game with "+ MY GAME", then **USE PLAYLIST** and **START**.

Check:
- the intro card shows your texts;
- critters stand at your spawn points and stay frozen until GO;
- bots move and shoot;
- scores update the cards and the crown;
- the round ends by your rule or by the timer;
- the results show the right points;
- the Final and the return to the lobby work.

To test with several humans, use the SAME WI-FI buttons: one instance chooses HOST, the others JOIN BY ADDRESS with `127.0.0.1` (same machine) or the host's LAN address. A Local room needs 3 humans to start. Multiplayer Play Mode (`com.unity.multiplayer.playmode`) is installed for running extra editor players. Before a demo, also play one online session (see [SERVER_OPS.md](SERVER_OPS.md)).

## Step 11. Write a PlayMode test

Put tests in `Assets/ShooterParty/MiniGames/MyGame/Tests/` and wrap them in `#if UNITY_EDITOR`, because `NetTestRig` loads prefabs through the AssetDatabase. There are two useful kinds:

**Controller test with a fake session** (like `KnockbackArena/Tests/KnockbackArenaGameTests.cs`): fast and deterministic. It uses the real prefab list, so run Rebuild core assets first.

```csharp
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterParty;
using ShooterParty.Tests;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyGame.Tests
{
    public class MyGameTests
    {
        class FakeHost : ISessionHost
        {
            public List<SlotInfo> slots = new List<SlotInfo>();
            public List<RankEntry> ranking;
            public IReadOnlyList<SlotInfo> RoundSlots => slots;
            public int Epoch => 1;
            public float TimeScale => 1f;
            public void ReportScores(IReadOnlyList<(int slot, int score)> scores, int crownSlot) { }
            public void ReportRound(IReadOnlyList<RankEntry> r) => ranking = r.ToList();
        }

        GameObject world;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MiniGameController.Host = null;
            if (world) Object.Destroy(world);
            yield return NetTestRig.Teardown();
        }

        [UnityTest]
        public IEnumerator LastCritterStandingRanksFirst()
        {
            NetTestRig.StartNetworked();   // Practice host with the real prefab list and ShotSystem
            world = new GameObject("TestWorld");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(world.transform);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(16f, 1f, 16f);
            var scene = world.AddComponent<MiniGameScene>();
            scene.spawnPoints = new[] { -3f, 0f, 3f }.Select(x =>
            {
                var t = new GameObject("Spawn").transform;
                t.SetParent(world.transform);
                t.position = new Vector3(x, 0.05f, 2f);
                return t;
            }).ToArray();

            var host = new FakeHost();
            for (int i = 0; i < 3; i++)
                host.slots.Add(new SlotInfo { slot = i, colourIndex = i, isBot = true,
                                              ownerClientId = NetworkManager.ServerClientId, mods = PlayerModifiers.One });
            MiniGameController.Host = host;
            var game = Object.Instantiate(NetTestRig.Load("Assets/ShooterParty/MiniGames/MyGame/Prefabs/Net_MyGame.prefab"))
                             .GetComponent<MyGameController>();
            game.NetworkObject.Spawn(destroyWithScene: true);
            game.ServerSetupRound();
            yield return null;
            foreach (var brain in Object.FindObjectsByType<MyBotBrain>(FindObjectsSortMode.None)) Object.Destroy(brain);
            game.ServerStartRound();
            foreach (var slot in new[] { 1, 2 })
                game.CritterOf(slot).GetComponent<Rigidbody>().position = new Vector3(0f, -5f, 0f);

            for (float t = 0f; t < 3f && host.ranking == null; t += Time.deltaTime) yield return null;
            Assert.IsNotNull(host.ranking, "the round should have ended");
            Assert.AreEqual(1, host.ranking.First(e => e.slot == 0).rank);
        }
    }
}
#endif
```

**Full session test** (like `KnockbackArena/Tests/PracticeSessionTests.cs`): it runs your game through the real session with bots and real scene loads.
1. Set `SessionFlow.TimeScaleForTests = 0.1f`.
2. Call `NetTestRig.StartSession()`.
3. Wait for 4 seats, then call `SessionManager.Instance.RequestPlaylist("Test", new[] { "my_game" })` and `RequestStart()`.
4. Wait for `SessionPhase.Final`, then assert on `SessionManager.Instance.ServerFlow.Rankings`.
5. Reset the time scale to 1 in the teardown.

Run them from the Test Runner (PlayMode tab), or from a shell as in ARCHITECTURE.md section 9 with `-assemblyNames MyGame.Tests`.

## Done when

- [ ] Everything lives in `Assets/ShooterParty/MiniGames/MyGame/`, and no `ShooterParty` file was edited by hand (only regenerated).
- [ ] The definition has an id, a scene, a controller and a critter prefab, and the scene is in the build settings.
- [ ] Rebuild core assets lists your game and its prefabs, and the lobby shows "+ MY GAME".
- [ ] A Practice round plays to the results, and a playlist with your game reaches the Final.
- [ ] Your controller returns every slot from `RankPlayers()` and calls `EndRound()` when the round is decided.
- [ ] No client RPC decides a result (ARCHITECTURE.md section 10).
- [ ] Your PlayMode tests and the existing ones pass.
