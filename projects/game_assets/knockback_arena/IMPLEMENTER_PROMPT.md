# Prompt for the implementer agent: build the Knockback Arena assets

Copy everything below the line into the agent.

---

You are building game assets for **Knockback Arena**, a mini-game in a Unity LAN party game. Follow these steps **in order**. Do **not** skip the checks. Do **not** invent gameplay. If a step fails, stop and report the exact error. Never "work around" it silently.

## 0. Facts you must not change

- **Unity:** 6000.3.9f1, URP 17.3, Input System 1.18. Project root: `/Users/ezelbayraktar/Documents/Development/PartyGame`.
- **Asset folder:** put everything under `Assets/Ezel/KnockbackArena/`, with subfolders `Models/`, `Materials/`, `Textures/`, `Sprites/`, `Audio/SFX/`, `Audio/Music/`, `VFX/`, `UI/`, `Prefabs/`, `Scripts/`, `Portraits/`. Create a folder only if it doesn't exist.
- **Blender source files:** save under `projects/game_assets/knockback_arena/blender/`. Reference images are in `projects/game_assets/knockback_arena/refs/`. Open `glossy_lineup.png` and `ko_hit_full.png` and match that look: glossy vinyl toy, soft rounded edges, two tall black oval eyes, small stubby arms, dark brown feet.
- **Colours (exact hex):**

  | Name | Hex |
  |---|---|
  | Orange | `#FF6B1F` |
  | Cyan | `#1FA9EA` |
  | Lime | `#43B832` |
  | Purple | `#8F55F5` |
  | Feet | `#4A2E22` |
  | Eyes | `#16233F` |
  | Eye highlight | `#FFFFFF` |

- **Units:** 1 Blender unit = 1 metre = 1 Unity unit. Y is up in Unity, Z is up in Blender.
- **Game rules:**
  - Shots are free, with only a fire-rate limit, and don't affect the shooter.
  - A hit lowers the target's mass, **not its size**. Never shrink the cube during play.
  - Falling off = out.
  - The arena has no walls and no cover.
  - There are **no powerups**. Do not make any.

## 1. Setup check

1. Confirm Blender MCP responds: run `get_scene_info`. If it fails, stop. The user must install Blender (4.2 LTS or newer) and enable the blender-mcp add-on first.
2. In Blender, set Scene Units: Metric, Unit Scale 1.0, Length: Metres.
3. Delete the default cube, camera and light.

## 2. Character: `CubeCharacter` (most important; take your time)

### 2a. Meshes

Build these as separate objects. All sizes are in metres. All positions are relative to the world origin, which is the point on the ground between the feet.

| Object | Shape | Size | Location (x, y, z Blender) | Notes |
|---|---|---|---|---|
| `Body` | Cube | 1.00 × 0.96 × 0.92 (x, y, z) | bottom face at z = 0.12, centred on x/y → centre (0, 0, 0.58) | Bevel modifier: width 0.22, segments 6, clamp overlap on. Then Subdivision Surface level 1. Apply both. Shade Smooth. Final tri count 2,000–6,000. Quads only before triangulation. |
| `Eye.L` | UV sphere, then scale | 0.085 × 0.05 × 0.17 | (-0.17, -0.47, 0.66) | Sits on the front face (−Y is front). Push it until it barely pokes out of the body surface. Shade Smooth. |
| `Eye.R` | mirror of `Eye.L` | same | (0.17, -0.47, 0.66) | |
| `EyeShine.L` / `.R` | small sphere, Ø 0.03 | | top-front of each eye, offset (+0.015, -0.02, +0.05) | White. Parent to the matching eye. |
| `Arm.L` | Capsule-like (UV sphere scaled) | 0.16 × 0.14 × 0.26 | (-0.56, -0.05, 0.50) | Slightly rotated outward (Y rot −15°). Shade Smooth. |
| `Arm.R` | mirror | | (0.56, -0.05, 0.50) | Y rot +15°. |
| `Foot.L` | Rounded box: cube + bevel 0.05, 3 segments | 0.26 × 0.30 × 0.12 | (-0.22, -0.02, 0.06) | |
| `Foot.R` | mirror | | (0.22, -0.02, 0.06) | |

Rules:

- **Front is −Y in Blender.** It becomes +Z, Unity forward, after export. The eyes must be on the −Y face.
- Apply all transforms on every object (Ctrl+A → All Transforms) before rigging.
- UV unwrap the Body (Smart UV Project, island margin 0.02). The other parts can use any UVs.

### 2b. Shape keys on the eyes

Do this after applying transforms. Join `Eye.L` and `Eye.R` into one object called `Eyes`. Keep `EyeShine.*` as separate objects parented to `Eyes`. Add these shape keys:

- `Basis`.
- `Happy`: flatten each eye into an upward arc (^ shape). Scale the eye's Z to 0.35 and lift the outer vertices so it curves.
- `Hurt`: squeeze each eye into a short horizontal line, angled inward (> <).

### 2c. Armature: `CubeRig`

Bones (head → tail, in metres, Blender coordinates):

| Bone | Parent | Head | Tail |
|---|---|---|---|
| `Root` | none | (0,0,0) | (0,0,0.12) |
| `Body` | Root | (0,0,0.12) | (0,0,1.04) |
| `Eyes` | Body | (0,-0.47,0.66) | (0,-0.47,0.80) |
| `Arm.L` | Body | (-0.50,-0.05,0.58) | (-0.62,-0.05,0.42) |
| `Arm.R` | Body | (0.50,-0.05,0.58) | (0.62,-0.05,0.42) |
| `Foot.L` | Root | (-0.22,-0.02,0.12) | (-0.22,-0.02,0.0) |
| `Foot.R` | Root | (0.22,-0.02,0.12) | (0.22,-0.02,0.0) |

Skinning. Rigid weights only, no automatic weights:

- `Body` mesh → 100% bone `Body`.
- `Eyes` mesh → 100% bone `Eyes`.
- `Arm.L` mesh → 100% `Arm.L`; `Arm.R` mesh → 100% `Arm.R`.
- `Foot.L` → 100% `Foot.L`; `Foot.R` → 100% `Foot.R`.

To skin each part: add an Armature modifier pointing to `CubeRig`, then a vertex group named exactly like the bone, with all vertices at weight 1.0. `EyeShine.*`: join them into `Eyes`, or skin them to `Eyes` the same way.

**Check A.** In Pose Mode:
1. Scale bone `Body` on Z to 0.7 and X/Y to 1.2. The body must flatten towards the ground, the eyes and arms must move with it, and the feet must not move.
2. Scale `Eyes` on Z to 0.1. The eyes must close (this is the blink).
3. Reset everything.
4. Screenshot and look at it.

If any part detaches, fix the weights before continuing.

### 2d. Materials (Blender, for previews and portraits)

- **`M_Cube_Orange`, `M_Cube_Cyan`, `M_Cube_Lime`, `M_Cube_Purple`** (Body + Arms): Principled BSDF, base colour = the player hex, Roughness 0.25, Coat Weight 0.5, Coat Roughness 0.05, Subsurface Weight 0.05.
- **`M_Feet`**: `#4A2E22`, Roughness 0.45.
- **`M_Eye`**: `#16233F`, Roughness 0.1.
- **`M_EyeShine`**: white, Emission strength 1.

Give the arms the same material slot as the body, so a colour swap changes both.

### 2e. Animations (Blender actions on `CubeRig`)

Make one action per clip. Name each action exactly as below and set Fake User on it. Frame rate is 30 fps.

| Action | Frames | Loop | What happens |
|---|---|---|---|
| `Cube_Idle` | 1–60 | yes | Body bobs: Z scale 1.00 → 0.97 → 1.00, X/Y opposite (1.00 → 1.015). Arms swing ±6°. |
| `Cube_Waddle` | 1–16 | yes | Feet alternate lifting 0.05 m and forward 0.06 m. Body rolls ±4° on Y. Arms swing ±15° opposite to the feet. |
| `Cube_Shoot` | 1–8 | no | Body leans back 6° and squashes on Y-forward (frame 3). Arms jerk forward 20°. Back to rest by frame 8. |
| `Cube_Hit` | 1–10 | no | Body squashes sideways (X 1.2, Z 0.85) at frame 2, overshoots at frame 5 (X 0.92, Z 1.08), rests by frame 10. Arms flail up 40°. |
| `Cube_Tumble` | 1–20 | yes | Arms and feet flail (±35°). Body stretched Z 1.1 / X-Y 0.95. |
| `Cube_Victory` | 1–30 | yes | Hop: Root up 0.25 m at frame 8 (squash before, stretch in air, squash on land at frame 16). Both arms up 120°. |
| `Cube_Defeat` | 1–30 | yes | Body droops (Z 0.9), slow sway ±5°. Arms hang down. |
| `Cube_Wave` | 1–24 | yes | `Arm.R` waves ±35°. Small body bob. |

Use eased (Bezier) keys, not linear. **No root motion:** never move `Root` in X/Y. Only `Cube_Victory` may move `Root` in Z.

**Check B.** Play every action and screenshot one frame of each. Nothing may sink below z = 0 except feet touching the ground.

### 2f. Export `CubeCharacter.fbx`

Select `CubeRig` plus all its meshes. Then **File → Export → FBX**:

- **Path:** `Assets/Ezel/KnockbackArena/Models/CubeCharacter.fbx`
- **Include:** Selected Objects, Object Types: Armature + Mesh.
- **Transform:** Scale 1.0, Apply Scalings: FBX All, Forward: −Z Forward, Up: Y Up, Apply Unit on, Use Space Transform on, Apply Transform (experimental) **on**.
- **Geometry:** Smoothing: Face, Apply Modifiers on, Triangulate off.
- **Armature:** Primary Bone Axis Y, Secondary X, **Add Leaf Bones off**.
- **Animation:** Bake Animation on, All Actions on, NLA Strips off, Force Start/End Keying on, Sampling Rate 1, Simplify 0.

Also save `projects/game_assets/knockback_arena/blender/CubeCharacter.blend`.

### 2g. Portraits (C8)

1. Add a camera in front of the cube, 3/4 view, slightly from above, framing the body with about 10% padding. Use one soft area light plus world colour `#EEF6FF`.
2. Set Film to Transparent and resolution to 512×512.
3. Render 4 PNGs, one per colour material: `Portrait_Orange.png`, `Portrait_Cyan.png`, `Portrait_Lime.png`, `Portrait_Purple.png`.
4. Save them to `Assets/Ezel/KnockbackArena/Portraits/`.
5. Look at each image. The eyes must be visible, nothing may be cut off, and the background must be transparent.

## 3. Shot: `Shot.fbx`

1. UV sphere, Ø 0.28 m, 24 segments × 16 rings, Shade Smooth, origin at the centre.
2. Give it a glossy material (same settings as the body) using a placeholder colour; Unity sets the real colour.
3. Export to `Assets/Ezel/KnockbackArena/Models/Shot.fbx` with the same FBX settings as step 2f (no armature).

## 4. Arena kit (no walls, no cover, open edges)

Build these separate objects, each with its origin at the **top-surface centre** (z = 0 is the walkable top):

| Object | Size | Notes |
|---|---|---|
| `Ice_Top_4x4` | 4 × 4 m top, 0.6 m thick | Top bevel 0.04. Top face must be perfectly flat (z = 0). Slight random vertex noise only on the sides, max 0.03 m. |
| `Ice_Edge_4` | 4 m long edge strip, 0.6 m thick | Rounded outer top edge (bevel 0.12, 4 segments). 6–10 icicles hanging below, 0.2–0.7 m long. Flat top at z = 0. **No raised lip.** |
| `Ice_Corner` | 0.6 m corner piece matching the edge | Icicles below. |
| `Island_Underside` | ~22 × 22 m wide, 6–9 m deep | Rough rocky-ice cone below the arena. Decorative, no collider. Displace modifier with cloud texture, strength 0.6. |
| `BG_Island_A/B/C` (P1) | 4–10 m, varied | Small floating ice islands for the far background. |
| `Crystal_Cluster_A/B/C` (P1) | 1–3 m tall | 5–9 crystal shards each. Only placed **outside** the arena, never on it. |

Materials and textures:

1. Use Blender MCP Poly Haven tools to search for and download a seamless ice or snow texture set (albedo, normal, roughness) at 2K. Tint it pale blue `#CFE9FB` if needed.
2. Save the textures to `Assets/Ezel/KnockbackArena/Textures/Ice_*.png`.
3. Download a bright daytime sky HDRI with soft clouds at 2K. Save it as `Assets/Ezel/KnockbackArena/Textures/Sky_Day.hdr`.

Export each object as its own FBX into `Assets/Ezel/KnockbackArena/Models/Arena/`. Use the step 2f settings, but with no armature and no animation.

**Check C.** Snap 4 × 4 `Ice_Top_4x4` + edges + corners into a 16 × 16 m arena in Blender. The tiles must snap edge-to-edge with no visible gaps, and the top must be one flat surface. Screenshot it.

## 5. Unity import and setup

### 5a. `CubeCharacter.fbx` import settings

- **Model tab:** Scale Factor 1, Convert Units on, **Bake Axis Conversion on**, Import BlendShapes on, Import Cameras/Lights off.
- **Rig tab:** Animation Type **Generic**, Avatar Definition: Create From This Model, Root node **Root**.
- **Animation tab:** confirm 8 clips exist with the Cube_* names. Tick **Loop Time** on Idle, Waddle, Tumble, Victory, Defeat and Wave.
- **Materials tab:** Material Creation Mode None. You will assign URP materials.

**Check D.** Drag the model into an empty scene at (0,0,0):
- Root rotation must be (0,0,0) and scale (1,1,1).
- The eyes must face +Z (Unity forward).
- The body must be about 1 m tall, standing on y = 0.

If it is lying down or 100× too big, fix the export settings and re-export. **Do not** fix it with a child rotation.

### 5b. URP materials (in `Materials/`)

| Material | Settings |
|---|---|
| `M_Cube_Orange` / `_Cyan` / `_Lime` / `_Purple` | Universal Render Pipeline/Lit. Base colour = the player hex. Metallic 0, **Smoothness 0.82**. Emission enabled with colour black, so the hit-flash script can set it. Receive Shadows on. |
| `M_Feet` | `#4A2E22`, Smoothness 0.5. |
| `M_Eye` | `#16233F`, Smoothness 0.9. |
| `M_EyeShine` | Unlit, white. |
| `M_Ice` | Lit, using the downloaded Ice_* maps. Base tint `#E6F4FF`, Smoothness 0.75. |
| `M_Water` | Lit, `#1E5FD8`, Smoothness 0.95, Alpha 0.85. Use it on a 400 × 400 m plane placed 40 m below the arena. |

For the skybox, create a Skybox/Panoramic material from `Sky_Day.hdr` and set it in Lighting → Environment.

### 5c. Prefab `Prefabs/Cube_Player.prefab`

Hierarchy:

```
Cube_Player                (Rigidbody, BoxCollider, CubeSquash, CubeBlink, CubeHitFlash, Animator)
 └─ CubeCharacter          (the imported model)
```

**Root components:**

- **`Rigidbody`:** mass 1, Interpolate **Interpolate**, Collision Detection **Continuous Dynamic**, Freeze Rotation X and Z.
- **`BoxCollider`:** center (0, 0.55, 0), size (0.95, 1.05, 0.9). It never changes size at runtime.

**`Animator` on the model:** create the controller `CubeAnimator.controller`.
- States: Idle (default), Waddle, Tumble, Victory, Defeat, Wave.
- Trigger layer (additive or override, weight 1): Shoot, Hit.
- Parameters: `Speed` (float), `Airborne` (bool), `Shoot` (trigger), `Hit` (trigger), `Win` (trigger), `Lose` (trigger).
- Idle ↔ Waddle when `Speed` > 0.1. Any → Tumble when `Airborne`. Use short transitions, about 0.1 s.

Create four prefab variants, `Cube_Player_Orange` and so on, each with its colour material on Body and Arms.

### 5d. Scripts (in `Scripts/`; copy exactly, then compile)

```csharp
// CubeSquash.cs — visual squash/stretch on the "Body" bone. Visual only: never touches the collider.
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CubeSquash : MonoBehaviour
{
    public Transform body;                 // drag the "Body" bone here
    public float stiffness = 180f;
    public float damping = 12f;
    public float maxDeform = 0.35f;
    public float stretchPerSpeed = 0.015f; // stretch per m/s of vertical speed

    Rigidbody rb;
    Vector3 baseScale;
    float deform, deformVel;               // deform > 0 = squash (flatter), < 0 = stretch (taller)

    void Awake() { rb = GetComponent<Rigidbody>(); baseScale = body.localScale; }

    public void Kick(float amount) { deformVel += amount; }   // call with + on hit/landing

    void OnCollisionEnter(Collision c) { Kick(Mathf.Min(c.relativeVelocity.magnitude * 0.6f, 8f)); }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        float target = -Mathf.Min(Mathf.Abs(rb.linearVelocity.y) * stretchPerSpeed, maxDeform);
        deformVel += (stiffness * (target - deform) - damping * deformVel) * dt;
        deform = Mathf.Clamp(deform + deformVel * dt, -maxDeform, maxDeform);
        float up = 1f - deform;
        float side = 1f / Mathf.Sqrt(up);  // keep volume roughly constant
        // Blender Z-up bone axes: after import the Body bone's local Y is up. Verify with Check E.
        body.localScale = Vector3.Scale(baseScale, new Vector3(side, up, side));
    }
}
```

```csharp
// CubeBlink.cs — random blinks by scaling the "Eyes" bone.
using System.Collections;
using UnityEngine;

public class CubeBlink : MonoBehaviour
{
    public Transform eyes;   // drag the "Eyes" bone here
    Vector3 baseScale;

    void Awake() { baseScale = eyes.localScale; }
    void OnEnable() { StartCoroutine(Loop()); }

    IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(2f, 5f));
            for (float t = 0; t < 0.12f; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Sin(t / 0.12f * Mathf.PI) * 0.9f;
                eyes.localScale = new Vector3(baseScale.x, baseScale.y * k, baseScale.z);
                yield return null;
            }
            eyes.localScale = baseScale;
        }
    }
}
```

```csharp
// CubeHitFlash.cs — white flash on hit. Call Flash() from gameplay code.
using System.Collections;
using UnityEngine;

public class CubeHitFlash : MonoBehaviour
{
    public Renderer[] renderers;   // Body and Arm renderers
    static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock block;

    void Awake() { block = new MaterialPropertyBlock(); }

    public void Flash() { StopAllCoroutines(); StartCoroutine(Run()); }

    IEnumerator Run()
    {
        Set(Color.white * 1.5f);
        yield return new WaitForSeconds(0.08f);
        Set(Color.black);
    }

    void Set(Color c)
    {
        foreach (var r in renderers) { r.GetPropertyBlock(block); block.SetColor(Emission, c); r.SetPropertyBlock(block); }
    }
}
```

**Check E.** Enter Play mode with one `Cube_Player` dropped from 3 m onto a plane:
- It must squash on landing and wobble back to normal within about 0.5 s.
- It must blink every few seconds.
- Calling `Flash()` from the inspector context menu, or a test key, must flash it white.

If the body squashes sideways instead of down, swap the `up` component to the bone axis that points up, then re-test.

### 5e. Shot prefab `Prefabs/Shot.prefab`

- `Shot.fbx` with Sphere Collider radius 0.14.
- Rigidbody: mass 0.2, Interpolate, Continuous Dynamic.
- Trail Renderer: time 0.15, width 0.22 → 0, Sprites-Default material, colour set from code.
- **Do not** add damage or mass logic; gameplay code does that.

### 5f. Arena prefab `Prefabs/Arena_16x16.prefab`

- 4 × 4 `Ice_Top_4x4` + edges + corners.
- One single BoxCollider covering the top, 16 × 0.6 × 16, top at y = 0.
- `Island_Underside` below with no collider.
- Water plane 40 m below.
- A trigger BoxCollider named `FallZone` at y = −8, size 400 × 1 × 400. Gameplay uses it to detect falling out.

## 6. 2D sprites (Higgsfield, GPT Image 2.5)

Each call costs about 1 credit at medium/2K. Run them one at a time:

```
higgsfield generate create gpt_image_2_5 --prompt "<PROMPT>" --aspect_ratio 1:1 --quality medium --resolution 2k --background transparent --wait
```

Download each result:
1. Check that it really has a transparent background.
2. Downscale to 256×256 (sheets: 1024 wide).
3. Save it to `Assets/Ezel/KnockbackArena/Sprites/`.
4. Import it as Sprite (2D and UI), Alpha Is Transparency on.

Every prompt starts with this shared prefix: *"Game VFX sprite, single isolated element centred on a fully transparent background, pure white with soft grey shading so it can be tinted in-engine, clean stylised cartoon style, no text, no border, no shadow on background."*

| File | Prompt (append to prefix) |
|---|---|
| `puff_soft.png` | "a soft round cartoon smoke puff made of 3 overlapping fluffy circles" |
| `star_4pt.png` | "a chunky four-pointed cartoon star with rounded tips" |
| `sparkle.png` | "a small sharp four-point sparkle glint with a soft glow" |
| `droplet.png` | "a glossy liquid droplet, teardrop shape, with a white highlight" |
| `ring_soft.png` | "a thin soft circular shockwave ring, brighter on the outer edge" |
| `ice_chip_sheet.png` (aspect 4:1) | "four small angular ice chips in a row, evenly spaced, frosted crystal look, pale blue tint" |
| `confetti_sheet.png` (aspect 2:1) | "eight small confetti pieces in a 4x2 grid: ribbons, squares and circles, flat white" |
| `speedline.png` (aspect 4:1) | "a single tapered motion speed line, thick at left, fading to a point at right" |

UI elements use the same command. The UI prefix is: *"Game UI element for a bright toy-like party game, flat vector style with soft rounded corners, on a fully transparent background, no text."*

| File | Prompt |
|---|---|
| `ui_panel.png` | "a white rounded rectangle panel with 32 px corner radius and a soft drop shadow, suitable for 9-slice" |
| `ui_mass_frame.png` (4:1) | "a horizontal rounded pill-shaped meter frame, white with a thin light-grey inner border, empty inside" |
| `ui_mass_fill.png` (4:1) | "a horizontal rounded pill-shaped meter fill bar, pure white with a subtle glossy highlight on top" |
| `ui_crosshair.png` | "a minimal crosshair: small centre dot and four short rounded ticks, white with a thin dark outline" |
| `ui_cooldown_ring.png` | "a thick circular progress ring, white, rounded line caps, 85 percent complete" |

Set 9-slice borders on `ui_panel` in the Sprite Editor (about 40 px each side).

## 7. Audio (Higgsfield Seed Audio, about 0.1 credit each)

```
higgsfield generate create seed_audio --prompt "<PROMPT>" --wait
```

1. Save results as WAV/OGG into `Audio/SFX/` or `Audio/Music/`.
2. Trim silence. Normalise SFX to about −3 dBFS peak.
3. Import settings:
   - SFX: Load Type Decompress On Load, Force To Mono on.
   - Music: Streaming.

| File | Prompt |
|---|---|
| `sfx_shoot_01..03` | "short cartoon pop, a soft rubbery blob being fired from a toy, bright and playful, 0.2 seconds, no music" (run 3 times) |
| `sfx_hit_01..03` | "squishy cartoon thwack, a rubber toy hit by a jelly ball, punchy, 0.3 seconds, no music" (3 times) |
| `sfx_whoosh` | "fast cartoon whoosh of a small object flying past, 0.4 seconds" |
| `sfx_fall` | "small cartoon character falling away with a comic high-pitched 'wheee' fading into the distance, then a faint far splash, 2 seconds, no words" |
| `sfx_land` | "soft squishy landing of a rubber toy on ice, 0.25 seconds" |
| `sfx_step_01..04` | "tiny footstep on hard ice, light plastic tap, 0.1 seconds" (4 times) |
| `sfx_count_beep` | "clean bright game countdown beep, 0.2 seconds" |
| `sfx_go` | "short triumphant game start horn, cheerful, 0.8 seconds" |
| `sfx_ko_jingle` | "short comic descending jingle when a player is knocked out, xylophone and slide whistle, 1.5 seconds" |
| `sfx_win_fanfare` | "short cheerful victory fanfare for a party game, brass and xylophone, 3 seconds" |
| `sfx_ui_click` / `sfx_ui_hover` | "soft bubbly UI click, 0.1 seconds" / "very soft UI hover tick, 0.05 seconds" |
| `mus_gameplay_loop` | "upbeat playful party game music loop, 120 BPM, marimba, plucked bass, light drums, bright and bouncy, seamless loop, no vocals, 60 seconds" |
| `mus_lobby_loop` | "relaxed cheerful lobby music loop, 95 BPM, soft synth pads and marimba, seamless loop, no vocals, 45 seconds" |

## 8. VFX (Unity Particle Systems in `VFX/`; one prefab each)

Use the sprites from step 6 with Additive or Alpha Blended URP particle materials. Each effect plays once and destroys itself, except the ambient one. Colour comes from code via `ParticleSystem.MainModule.startColor`.

| Prefab | Recipe |
|---|---|
| `VFX_MuzzlePop` | 6 `puff_soft` particles, 0.25 s, speed 1–2, size 0.2→0.5, fade out |
| `VFX_HitSplat` | 12 `droplet` in a 50° cone back toward the shooter, plus 8 `ice_chip_sheet` chips with gravity 1, plus 1 `ring_soft` 0→1.2 m in 0.2 s |
| `VFX_LandPuff` | 8 `puff_soft` in a ring along the ground, 0.4 s |
| `VFX_FallOut` | 1 big `puff_soft` burst + 6 `star_4pt` spinning upward, 0.8 s |
| `VFX_SpeedLines` | Looping, stretched-billboard `speedline`. Enable from code while horizontal speed > 8 m/s |
| `VFX_Confetti` | 120 `confetti_sheet` (texture sheet 4×2), gravity 0.3, rotation over lifetime, 3 s |
| `VFX_SnowAmbient` | Looping, 30 `sparkle` slowly drifting in a 30 × 10 × 30 m box |

## 9. Final check and report

Build a test scene, `Scenes/MK_AssetTest.unity`, containing:
- `Arena_16x16` and the skybox;
- four `Cube_Player` variants on the corners;
- one `Shot` fired by a test script;
- every VFX prefab placed in a row.

Take screenshots from the game camera. Report back:
1. A list of every created file path.
2. Every check (A–E) marked passed or failed.
3. The screenshots.
4. Anything you could not do, and why.

**Do not** report success for a check you did not actually run.
