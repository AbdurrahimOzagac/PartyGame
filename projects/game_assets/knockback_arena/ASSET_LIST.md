# Knockback Arena: asset list

What the mini-game needs to look, sound and feel finished. P0 = needed for a playable, good-looking build. P1 = polish.

## Game rules the assets must respect (from the team design doc)

- Flat ice platform, open edges, **no walls and no cover** (no hiding spots).
- Shooting is **free**, limited only by a fire-rate cooldown. Shots are **real physical projectiles**. Shooting does nothing to the shooter.
- A hit pushes the target back from the shot's direction and **lowers its mass, not its size**. The cube does **not** shrink, so mass must be shown in the UI.
- A cube that falls off **dies**.
- WASD to move, third-person camera.
- No powerups.

## Look

Glossy vinyl-toy style, matching `refs/glossy_lineup.png`, `refs/podium_full.png` and `refs/ko_hit_full.png`.

| Colour | Hex |
|---|---|
| Orange | `#FF6B1F` |
| Cyan | `#1FA9EA` |
| Lime | `#43B832` |
| Purple | `#8F55F5` |
| Feet | `#4A2E22` |
| Eyes | `#16233F` |

## 1. Character, shared by every mini-game

| # | Asset | Pri | Made with | Notes |
|---|---|---|---|---|
| C1 | `CubeCharacter.fbx`: body, 2 eyes, 2 arms, 2 feet + armature | P0 | Blender MCP | Rounded cube, near 1:1:1. One model, four colours via materials. |
| C2 | Body squash/stretch | P0 | Armature (`Body` bone scale) + Unity script | Bounce on land, stretch when flying, squash on hit. Feet stay on the ground. |
| C3 | Resize | P0 | Root scale | Whole character scales uniformly from the ground pivot. |
| C4 | Blink + expressions | P0 | Eye bone scale (blink); eye shape keys `Happy`, `Hurt` | Blink every 2–5 s. `Hurt` on hit, `Happy` on win. |
| C5 | Animation clips | P0 | Blender actions | Idle, Waddle, Shoot, Hit, Tumble, Victory, Defeat, Wave. |
| C6 | 4 materials | P0 | Unity URP | Glossy plastic: smoothness ~0.8. Emission enabled for hit flash. |
| C7 | Blob shadow decal | P1 | Unity URP Decal | Soft dark ellipse under each cube, so height reads clearly when airborne. |
| C8 | 4 portrait PNGs (512×512, transparent) | P0 | Blender render of C1 | For HUD cards and results. Rendered from the model so they always match. |

## 2. Shot

| # | Asset | Pri | Made with | Notes |
|---|---|---|---|---|
| S1 | `Shot.fbx`: glossy blob sphere, 0.28 m | P0 | Blender MCP | Takes the shooter's colour. |
| S2 | Trail | P0 | Unity Trail Renderer | Shooter colour, fades out over 0.15 s. |
| S3 | Velocity stretch | P1 | Unity script | Shot stretches slightly along its velocity. |

## 3. Arena and world

| # | Asset | Pri | Made with | Notes |
|---|---|---|---|---|
| A1 | Arena kit: `Ice_Top_4x4`, `Ice_Edge_4`, `Ice_Corner` | P0 | Blender MCP | Modular tiles, so the arena size stays a design choice. Top is flat and walkable. Edges have icicles underneath. No walls. |
| A2 | `Island_Underside` | P0 | Blender MCP | Big rocky ice chunk under the arena, so it reads as floating. Decorative only, no collider. |
| A3 | Ice textures (albedo, normal, roughness), seamless 2K | P0 | Poly Haven via Blender MCP, or GPT Image | Frosted, slightly cracked ice, tinted pale blue. |
| A4 | Far water / void plane + material | P0 | Unity Shader Graph | Deep blue below the island, where the fall happens. |
| A5 | Skybox: bright sky with soft clouds | P0 | Poly Haven HDRI via Blender MCP | Same daylight as the renders. |
| A6 | 3 background floating islands + 3 ice-crystal clusters | P1 | Blender MCP | Placed far outside the arena: decoration, never cover. |
| A7 | Snow sparkle particles | P1 | Unity particles | Slow, sparse ambient particles. |

## 4. VFX, Unity particle systems using the sprites in 6

| # | Asset | Pri | When |
|---|---|---|---|
| V1 | Muzzle pop puff | P0 | On shoot |
| V2 | Impact splat: shooter-colour droplets + ice chips | P0 | On hit |
| V3 | Hit flash (white emission, 0.08 s) | P0 | On hit |
| V4 | Knockback speed lines | P1 | While pushed fast |
| V5 | Landing snow puff | P1 | On landing |
| V6 | Fall-out splash/poof + stars | P0 | When a cube dies |
| V7 | Winner confetti | P1 | Results |

## 5. UI, Unity UGUI + TextMeshPro

| # | Asset | Pri | Notes |
|---|---|---|---|
| U1 | Font: Fredoka (Google Fonts, OFL), TMP SDF asset | P0 | Rounded, matches the deck. |
| U2 | Player HUD card ×4: portrait, name, **mass meter**, score | P0 | Mass is not visible on the body, so this meter is essential. |
| U3 | World-space mass ring/bar above each cube | P0 | Shows mass at a glance mid-fight. |
| U4 | Crosshair + fire-rate cooldown ring | P0 | |
| U5 | Countdown 3-2-1-GO! | P0 | |
| U6 | "OUT!" banner when a cube falls | P0 | |
| U7 | Round results / podium screen | P0 | |
| U8 | Lobby panel: host / join by IP / ready ×4 | P0 | |
| U9 | Logo (from the deck drafts) | P1 | |

## 6. 2D sprites, generated

`sprites/`: transparent PNGs, white or greyscale so Unity can tint them.

- `puff_soft` (P0)
- `star_4pt` (P0)
- `sparkle` (P0)
- `droplet` (P0)
- `confetti_sheet` 4×2 (P1)
- `speedline` (P1)
- `ring_soft` (P0)
- `ice_chip_sheet` 4×1 (P0)
- UI panel 9-slice, rounded 32 px (P0)
- Mass meter frame + fill (P0)
- Crosshair (P0)

## 7. Audio, Higgsfield Seed Audio (about 0.1 credit each)

| # | Asset | Pri |
|---|---|---|
| SFX1 | Shoot pop ×3 variants | P0 |
| SFX2 | Squishy hit thwack ×3 | P0 |
| SFX3 | Knockback whoosh | P1 |
| SFX4 | Fall "wheee" + distant splash | P0 |
| SFX5 | Landing squish | P1 |
| SFX6 | Ice footsteps ×4 | P1 |
| SFX7 | Countdown beep ×3 + GO horn | P0 |
| SFX8 | KO jingle, round-win fanfare | P0 |
| SFX9 | UI click, hover | P0 |
| MUS1 | Gameplay loop (upbeat, ~120 BPM, 60–90 s) | P0 |
| MUS2 | Lobby loop (chill) | P1 |

## Why the character is built this way

- **Armature:** one armature with rigid-weighted parts (Root, Body, eyes, arms, feet). Scaling the `Body` bone gives clean squash and stretch. Eyes and arms ride along, and feet stay planted.
- **Pivot:** at the ground, so resizing never sinks the cube into the floor.
- **Not AI 3D:** an AI-generated mesh can't do any of this cleanly.
- **Physics vs. visuals:** squash and stretch are visual only. The Rigidbody and collider never change, so the physics and networking stay simple. A deforming collider would desync over the LAN.
