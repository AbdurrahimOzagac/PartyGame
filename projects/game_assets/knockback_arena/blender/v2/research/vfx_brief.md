# Knockback Arena: spit projectile and VFX brief

**Labels:**
- **[src]** verified in URP 17.3.0 source. This is the copy shipped in the 6000.3.9f1 editor's BuiltInPackages, and it is identical to Graphics `master` for these files.
- **[doc]** Unity 6000.3 docs.
- **[judgment]** my design call, with no source behind it.

## 1. Projectile: cute, not gross

**Size [maths].** On-screen size in pixels ≈ `d / (2·D·tan(FOV/2)) × screenH`. At 60° vertical FOV and 1080p:

| Radius | 5 m | 10 m | 20 m |
|---|---|---|---|
| 0.13 m (current) | 49 px | 24 px | **12 px** |
| **0.17 m (recommended)** | 64 px | 32 px | **16 px** |

The refs show the blob at roughly 35–40 % of body width. Keep the hitbox separate if balance needs it.

| Aspect | Spec [judgment] |
|---|---|
| Silhouette | Always convex. Stretch max 1.35 length × 0.86 width (`w = 1/√l`). No drips, strands or lumps. |
| Colour | Shooter colour. Core = `lerp(base, white, 0.35)`, via inverse fresnel or vertex gradient. Rim at +10 % saturation. **No pixel below V ≈ 70 %.** |
| Gloss | URP Lit, Smoothness 0.92, plus a **baked white glint**: one additive `sparkle` child, 0.07 m, offset up-left. It reads at 20 m where a real specular won't. |
| Emission | `base × 0.25`. Stops the colour going muddy in shadow and keeps it popping on ice. |
| Wobble | Volume-preserving jiggle at 7 Hz. 12 % at spawn, decaying (τ = 0.15 s) to a steady 4 %. Layered on the speed stretch. |
| Spin | None. It is invisible on a sphere. |
| Trail | Both: a droplet chain (as in the ref) and a 0.15 s soft ribbon (§3). |

**Face vs sparkle.**
- **Tiny face (Kirby / Slime Rancher):** instantly cute. But it is 2–3 px at 20 m, needs camera-facing logic, and makes "absorbing a creature" read oddly.
- **Glint plus 2–3 sparkles per shot:** reads at every distance and says "candy".
- **Recommendation:** no face. Personality already lives in the shooter's ^^ → ^ω^ face. Use stars for hits, and save hearts for the winner so a hostile hit never reads as "love".

**Candy vs snot cues [judgment].**

| Do | Avoid |
|---|---|
| Perfect spheres, separated droplets | Strings, a mouth–blob bridge, hanging drips |
| High gloss, glints, stars, hollow bubbles | Murky translucency, dark edges, matte |
| Saturated, high-value colour, snappy pop and squash | Olive or dark yellow-green, slow ooze, sag |

**Chartreuse #D4F21C** sits at H 68°, S 88 %, V 95 %. In [Kaya 2004](https://www.academia.edu/33758606/), green-yellow was the hue students liked least, and they linked it to sickness and disgust. The risk is the **shading**: chartreuse that darkens in shadow becomes olive. Fix it with emission 0.25, a core lifted toward lemon #F2FF8A, the V ≥ 70 % floor, glints, and no translucency or strands. Keep the team hue for identity.

## 2. Lifecycle timings

| Phase | Spec |
|---|---|
| Wind-up | Existing 0–133 ms cheek puff |
| Spawn pop | Scale 0.5 → **1.15 at 70 ms** → 1.0 at 120 ms (ease-out-back). Start Stretch at 1.3 so it squirts out. Fire V1 on the same frame. |
| Flight | `stretch = 1 + 0.25·clamp01(speed/12)` plus the wobble, aligned to velocity |
| Hit-stop (critter) | **Target only, visual only, 60–90 ms**, scaled by knockback. Sakurai gives projectiles *less* hit-stop and caps it, because in free-for-alls a third player can strike during the freeze ([Source Gaming](https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/)). Shake the model sideways 0.04 m and decay it ("start large, shrink", same source). |
| Absorb (critter) | 0–60 ms: Squash 0 → 1 on the surface. 60–180 ms: shrink to 0 while sinking 0.1 m in. Then the target jiggles: y 0.85 / xz 1.08, damped spring, about 2.5 cycles in 300 ms. This sells the mass loss. |
| Splat (ice) | Existing 0.18 s, then spawn the decal |
| Ice decal | Hold 3 s, then over 1 s fade alpha 1 → 0 and shrink 1 → 0.8. At most 16 alive (ring buffer, §4). |
| Timeout (2.5 s) | Pop: 1 → 1.2 → 0 in 100 ms, plus 3 sparkles. Never just vanish. |

## 3. VFX package

All textures are white, tinted through `startColor` / `EmitParams`. "A" means alpha blend, "Add" means additive. **Gotcha [judgment]:** additive white disappears on the near-white ice, so impact stars and snow must be **alpha-blended**, at about 0.9 white with a faint tint.

| FX | Life | Count | Size (m) | Speed (m/s) | Colour | Notes |
|---|---|---|---|---|---|---|
| V1 mouth pop | 0.3 s | 6 puffs, 3 droplets, 1 ring, 1 glow | puffs 0.12–0.2, ring 0.15 → 0.5, glow 0.35 | puffs 0.6–1.2, droplets 2–3 | puffs `lerp(shooter, white, 0.6)`, glow Add | Cone 25° on aim. Size 0.6 → 1.2 ease-out. Fade over the last 50 %. Limit Velocity dampen 0.5. |
| V2 impact | 0.6 s | 12 droplets, 1 star, 6 streaks, 8 ice chips, 3 sparkles | droplets 0.05–0.11, star 0.6 → 1.0 (0.1 s) → 0 (0.08 s), chips 0.05–0.1 | droplets 2.5–4.5 (hemisphere on normal), streaks 6, chips 3–5 (cone 35° up) | droplets shooter colour, star and streaks white A, chips #E8F8FF → #A8DDF5 | Droplets: gravityModifier 0.9. Chips: gravity 1.2, spin 360–720 °/s, mesh or sheet. Streaks: stretched `speedline`. |
| V3 hit flash | 140 ms | (none) | (none) | (none) | white emission | Hold 1.0 for 40 ms, ease out over 100 ms (MaterialPropertyBlock), plus the absorb jiggle |
| V4 speed lines | while speed > 6 | 6 per m travelled | width 0.03–0.05 | inheritVelocity 0.25 | white A, alpha 0.7 → 0 | Lifetime 0.2 s, 1 m box. Stretch: velocityScale 0.12, lengthScale 2. Plus foot snow spray at 4 per m. |
| V5 landing | 0.5 s | 4–12, scaled by impact speed | 0.15–0.3 | 1.5–2.5 outward | #FFF → #E6F4FF A | Circle edge r = 0.35. Size 0.6 → 1.3. Soft particles on. Plus 4 Add glints. |
| V6 fall-out | 1.0 s | 18 puffs, 7 stars, 1 ring | puffs 0.4–0.8, stars 0.15–0.25, ring 0.3 → 2.5 (0.25 s) | puffs 3–5, stars 4–6 | pastel critter colour, stars #FFE45C + white | Stars: gravity −0.2, spin 360 °/s. Optional: 3 dizzy stars orbiting during the fall (Local space). |
| V7 confetti | 3.5 s | 150, plus 40 sparkles | 0.06–0.12 | 8–12, cone 20° up | Fixed gradient: 4 player colours, white, gold #FFD23F (about 50 % winner colour) | Gravity 0.35, drag 1.5. Noise 0.6 at frequency 0.5. 3D spin 180–540 °/s. Mesh quads (`_Cull` 0) with a random sheet frame. |
| Trail | per shot | droplets 4 per m (≈ 48 /s) | 0.12 → 0 | inherit 0.1 | shooter colour | Lifetime 0.3 s, gravity 0.3, World space. Ribbon spec in §4. Plus 1 sparkle per 3 m. |
| Dash | 0.3 s | 6 puffs, 6 streaks, 1 ring | ring 0.3 → 1.2 | 2–3, back cone 30° | pastel critter colour | Ring as a HorizontalBillboard at the feet. A 1-sparkle "ding" when a charge refills. |
| Pickup idle | loop | 3 sparkles /s | 0.05–0.1 | 0.2 up | gold + white Add | Bob 0.12 m at 0.8 Hz, spin 90 °/s, breathe ±4 % at 1.6 Hz. Sphere r = 0.4, Local space. |
| Pickup collect | 0.4 s | 12 stars, 1 ring, 6 rising | ring 0.3 → 1.5 (0.2 s) | 3–4 | gold + white | Item scales 1 → 1.3 in 60 ms, then → 0 in 100 ms (ease-in) |
| Snow sparkle | loop | 6 /s, max 40 | 0.03–0.06 | 0 | white Add | Arena box, lifetime 2–3 s, size 0 → 1 → 0 |

Budget: fewer than 40 particles per burst and fewer than 1,000 alive. Pool everything. [judgment]

**Shake and hit-stop: local only, never desync.**
- **Trauma model:** from Eiserloh's GDC 2016 talk "Juicing Your Cameras With Math" ([Game Developer](https://www.gamedeveloper.com/programming/video-sprucing-up-cameras-with-math)). Shake = trauma², driven by smooth noise. The [Bevy reference](https://bevy.org/examples/camera/2d-screen-shake/) uses exponent 2, decay 0.5 per second, and noise speed 20.
- **Suggested trauma [judgment]:**
  - Hit received: +0.45
  - Own fall-out: +0.6
  - Hit dealt: +0.08
  - Heavy landing: +0.12
  - Spectators: 0
- **Suggested limits:** max offset 0.12 m, roll 2.5°, yaw/pitch 1.5°, noise around 20 Hz, decay 1.5 per second. Add a 0–100 % slider.
- **Shake a child "shake rig" of the camera, and aim from the unshaken parent**, or shake moves where shots go.
- **Never use `Time.timeScale`.** FixedUpdate stops at 0 and physics follows scaled time [doc], so on the host it would slow the authoritative simulation for everyone.
- **Hit-stop instead** freezes the target's visual child and animator (`speed = 0`) on each client, while the Rigidbody and NetworkTransform keep simulating. Lerp the visual back over 50 ms.
- Trigger all of this from the replicated hit event. None of it writes networked state.

## 4. Unity 6 / URP 17.3 facts

**`Universal Render Pipeline/Particles/Unlit` [src]**
- **Properties:** `_BaseMap`, `_BaseColor`, `_Surface` (0 Opaque, 1 Transparent), `_Blend` (0 Alpha, 1 Premultiply, 2 Additive, 3 Multiply), `_Cull`, `_ColorMode` (0 Multiply), `_SoftParticlesEnabled`, `_SoftParticlesNear/FarFadeDistance`, `_QueueOffset`.
- **Colour maths:** output = `tex × _BaseColor × vertexColor` (`MixParticleColor`). The vertex colour carries startColor × colour over lifetime, or the TrailRenderer gradient.
- **Easiest path:** set the floats, then call `BaseShaderGUI.SetMaterialKeywords(mat, null, ParticleGUI.SetMaterialKeywords)`, which is what the shader's `ValidateMaterial` runs. Both classes are public in `Unity.RenderPipelines.Universal.Editor`. **`KnockbackArena.Editor.asmdef` lacks that reference, so add it.**
- **What that sets for Alpha:**
  - `_SrcBlend` 5 (SrcAlpha), `_DstBlend` 10 (OneMinusSrcAlpha), `_SrcBlendAlpha` 1, `_DstBlendAlpha` 10
  - `_ZWrite` 0
  - keyword `_SURFACE_TYPE_TRANSPARENT`
  - `SetOverrideTag("RenderType", "Transparent")`
  - queue 3000 + `_QueueOffset`
  - `SetShaderPassEnabled("DepthOnly", false)`
- **Additive** is the same except `_DstBlend` 1 and `_DstBlendAlpha` 1.
- **Premultiply:** the shader never premultiplies, because the particle shader has no "preserve specular" property. So this mode needs premultiplied texture RGB, and vertex alpha won't darken RGB. **Use Alpha or Additive.**
- **Soft particles:** the helper writes `_SoftParticleFadeParams = (near, 1/(far − near))` plus the `_SOFTPARTICLES_ON` / `_FADING_ON` keywords. They need a Transparent surface **and Depth Texture enabled on the URP asset** ([doc](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/particles-unlit-shader.html)). Use far = 0.25 m for ground puffs. Turn them off for the ice decal.

**ParticleSystem**
- **Colour:** Colour over Lifetime **multiplies** startColor ([manual](https://docs.unity3d.com/2021.2/Documentation/Manual/PartSysColorOverLifeModule.html)). Keep gradients white with alpha only. One pooled system can serve all players: `ps.Emit(new ParticleSystem.EmitParams{position = p, applyShapeToPosition = true, startColor = c}, n)` [doc]. For confetti, use `MinMaxGradient{mode = RandomColor}` over a `GradientMode.Fixed` gradient (8 colour keys maximum).
- **Stretched billboards:** `renderMode = Stretch`, plus `velocityScale` and `lengthScale` (0 makes them invisible), `cameraVelocityScale = 0` (otherwise the moving camera smears them), and `freeformStretching = false` [doc]. **Unverified:** which texture axis follows velocity. Render an arrow test once, and `np.rot90` the PNG if needed.
- **Texture sheet 4×2:** `numTilesX = 4`, `numTilesY = 2`, `frameOverTime = 0`, `startFrame = new MinMaxCurve(0f, 0.999f)`. Script values are **normalised 0–1**. The inspector multiplies them by the tile count (`m_RemapValue` in [UVModuleUI.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/ParticleSystemEditor/ParticleSystemModules/UVModuleUI.cs)), so the docs' 0–7 slider example misleads.
- **Mesh particles:** `renderMode = Mesh`, `SetMeshes(...)`, `meshDistribution = UniformRandom`, `startRotation3D = true`. Rotations are in **radians** [doc].
- **Sub-emitters:** `AddSubEmitter(ps, Collision, InheritColor)`. Collision, Death and Manual allow **only one burst** [doc]. Prefer plain child systems under one root, which play together. Use sub-emitters only for droplet → micro-splat.
- **Space and scale:** World space for trails, puffs, splats and speed lines. Local for orbiting stars and pickup sparkles. `scalingMode = Local` ignores parent scale [doc], which suits FX parented to the growing blob.
- **Stopping:** `stopAction` fires when the system "stops and all particles have died" [doc]. Destroy for one-shots, Disable or Callback for pools. Set `loop = false`, and `playOnAwake = false` when pooled.
- **Configuring:** `duration` can only be set while the system is stopped [doc], so call `Stop(true, StopEmittingAndClear)` first. A fresh `AddComponent<ParticleSystem>()` has a cone shape, rate 10, looping on and no material. Override all of these [from memory].
- **Decals:** `ringBufferMode = PauseUntilReplaced`, `maxParticles = 16`, `renderMode = HorizontalBillboard`, placed 0.01 m above the ice [doc]. No URP Decal feature needed.

**TrailRenderer [doc]**
- **Settings:** `time = 0.15`, `minVertexDistance = 0.1`, `widthCurve` (0, 1) → (1, 0) × 0.22, `colorGradient` alpha 0.6 → 0, `alignment = View`, `textureMode = Stretch`, `numCapVertices = 4`, shadows off, `autodestruct = false`.
- **Pooling:** **`Clear()` after teleporting a pooled shot**, or it streaks from the old position. Set `emitting = false` on impact. Use the Particles/Unlit Alpha material.

## 5. Sprite sheets (numpy SDF)

- **Format:** white RGB, with the shape in **straight (not premultiplied) alpha**, because URP's Alpha mode is `SrcAlpha, OneMinusSrcAlpha` [src].
- **Shading:** grey shading in RGB survives the multiply tint.
- **Bleed:** fill RGB everywhere, so mips don't fringe.
- **Anti-aliasing:** `alpha = clip(0.5 − sdf_px/1.5, 0, 1)`, or render at 4× and downsample.
- **Import:** sRGB on, Alpha Is Transparency on, mipmaps on, Clamp, Bilinear, no compression.

| Texture | Size | Shape |
|---|---|---|
| `puff_soft` | 128² | 5–7 merged circles, edge smoothstep over 12 % of radius, RGB 1.0 → 0.85 shading toward the bottom-right |
| `star_4pt` | 128² | Concave 4-point star (arms 0.48, waist 0.12), 1.5 px AA |
| `sparkle` | 64² | Long and short thin cross plus a Gaussian core (σ = 8 %). For Add. |
| `droplet` | 64² | Crisp circle r = 0.45 with body RGB 0.85, plus a white highlight dot (r = 0.12) at the top-left |
| `confetti_sheet` | 512×256 | 4×2 cells: rectangle, square, circle, triangle, star, heart, squiggle, diamond. 8 px padding. |
| `speedline` | 32×256 | Gaussian width (σ = 25 %), tapered ends, brighter head |
| `ring_soft` | 256² | r = 0.4, thickness 0.06, Gaussian falloff, inner edge softer |
| `ice_chip_sheet` | 256×64 | 4 convex 4–6-gons, two-tone facets (1.0 / 0.75), 1 px white edge |
| *add* `splat_decal` | 256² | Round blob plus 5–7 round satellite dots and a highlight (cute, not spiky) |
| *add* `splash_star` | 256² | 8–10 rounded spikes of varying length, for the ref's white burst |

**Not checked:**
- Which stretched-billboard texture axis follows velocity.
- Whether a sub-emitter child self-plays with its parent.
- The defaults of a new ParticleSystem.

**Not found online:** Splatoon art-book and Fall Guys VFX notes. All cute-vs-gross guidance is judgment, except the cited colour study.
