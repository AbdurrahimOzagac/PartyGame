# Knockback Arena: Player Character Design Brief

**My recommendation:** keep the rounded cube and its tall oval eyes, but move the face low, add a goo curl on top that doubles as the ammo gauge, put a player emblem on the back, and ship opaque gummy vinyl rather than real transparency. Of the refs, `podium_full.png` (low face, smile, blush) is the cutest; `glossy_lineup.png` has eyes only and nothing on the back.

Numbers marked † are my own starting values for tuning. Everything else has a source, listed at the end.

## 1. What makes a simple character read as designed and cute

- **Baby schema.** The cue set is a large round head, big eyes, a small nose and mouth, a high forehead and chubby cheeks. In tests, faces altered toward it are rated cuter [1].
  - The strongest single shape cue for cuteness is facial parts sitting relatively low on the face [2].
  - Mickey Mouse drifted the same way over decades, with a bigger head and bigger eyes [3].
  - A cube is already "all head". The levers you have left are a low face, wide-set eyes, a tiny mouth and full cheeks.
- **Face layout (kawaii convention).**
  - Solid oval eyes, set low and further apart than feels right, with one shine each.
  - A small mouth between the eyes and just below them.
  - Blush ellipses near the lower edge of the eyes [4].
- **Catchlights.** Painted shines look fake when they slide with the eye. Drive them from the view or reflection vector so they stay locked to the light [5].
- **Shape language.** Roundness reads friendly [6]. Get appeal by setting straights against curves [7]: a flatter top and back, fuller cheeks and belly. Valve's process is silhouette first, interior shapes second, checked in three-quarter view [8].
- **Neutral base plus motion.** Fall Guys kept the bean "iconic, but at the same time entirely neutral", and gave it a butt and low hips so it wiggles when it runs [9]. Personality comes from motion and one or two add-ons, not from surface detail.

## 2. The back view (what the player stares at)

Stellar Blade's director said: "the player is always facing the back of the character… That's what they see the most of" [10].

Third-person games often put game state on the back:
- Splatoon's ink tank shows remaining ink.
- Dead Space's spine bar shows health. Astroneer's backpack meter copies the idea.
- Journey's scarf shows flight charge [11].

For us:
1. **Charge state on top** (the goo curl), readable without the HUD.
2. **Break the square from behind:** curl on top, arms set slightly behind the side centreline, heels visible.
3. **Animate the rear:** hip-wiggle waddle, spring on the curl.
4. **Big back emblem** (shape plus colour).
5. **Light the back:** aim the key light so the clear-coat streak lands on the upper back from the gameplay camera, or it reads as a flat square.

## 3. Readability with 4 players

- **Valve's TF2 rules [8][12]:**
  - Team colour is read first and silhouette second.
  - Value runs as a "gradient from dark feet to light chest", with the highest contrast where the action is.
  - Silhouettes get rim highlights rather than dark outlines.
  - The rim term is Fresnel (1−N·V)^4 with an up-facing mask, so the rim reads as light from above.
- **Colour must never be the only cue** [13]. Splatoon added Color Lock for exactly this reason [14].
- **I checked your palette** against arena colours sampled from `ko_hit_full.png`. I used colour-blindness simulation matrices (Machado) and a basic colour-difference measure (ΔE76). The script is `colors.py` in the same folder as this brief.

| Check | Your current palette | Tuned palette: Tomato `#FF4F2E`, Teal `#00B3A4`, Chartreuse `#D4F21C`, Violet `#7B2FD0` |
|---|---|---|
| Hardest player-vs-arena pair | Cyan vs ice shadow: ΔE about 21. Cyan is the weakest player on ice. | Every player is at least ΔE 42 from the ice |
| Hardest player-vs-player pair | Orange vs lime: ΔE about 7 under deuteranopia, the most common colour-blindness type | Worst pair about ΔE 33 across normal, deutan, protan and tritan vision |

  Pair the colours with emblem shapes either way.
- **No ink outline on the body**: it fights the glossy look. Use these instead†:
  - A ground ring in the player colour.
  - A player-coloured silhouette when the character is occluded (URP Render Objects feature).
  - Arrows for players who are off-screen.

## 4. Materials

**Target: opaque gummy vinyl.** True transparency (`character_sheet_jelly.png`) looks great in stills, but a see-through cyan cube on ice is the hardest thing on screen to read, and URP transparency adds sorting and overdraw problems. Fake the jelly with subsurface, wrap lighting and rim instead.

**Blender EEVEE (4.2+), Principled BSDF**

| Setting | Vinyl body | Jelly variant (hero renders only) |
|---|---|---|
| Roughness | 0.30–0.40† | 0.05–0.12† |
| Coat Weight | 1.0 (the manual says 0 or 1) [15] | — |
| Coat Roughness | 0.03–0.08† | — |
| Coat IOR | 1.5 | — |
| Subsurface Weight | 0.15–0.25† | — |
| Subsurface Radius | (1.0, 0.5, 0.25)† | — |
| Subsurface Scale | about 6% of body width† | — |
| Transmission | — | 0.8–1.0† |
| IOR | — | 1.35† |

- EEVEE 4.2 subsurface no longer leaks between objects [16].
- For the jelly variant, set the material's Render Method to Dithered, enable Raytraced Transmission, and turn on raytracing in the render settings. The Blended method does not support raytracing [17].
- **Lighting for both:**
  - One large soft key light.
  - A strong back rim light.
  - An HDRI that contains a softbox.
  
  Vinyl reads through long, unbroken highlight streaks, so the bevels must be smooth.

**Unity 6 URP**
- **Fastest option: Complex Lit with Clear Coat** [18].
  - Smoothness 0.55–0.65†.
  - Clear Coat Mask 1.
  - Clear Coat Smoothness 0.9–0.95†.
  - It costs about twice a base material. That is fine for 4 characters.
- **Better option: Shader Graph Lit with Clear Coat enabled** (it adds Coat Mask and Coat Smoothness blocks), plus an Emission input built from three parts:
  - **Rim:** Fresnel Effect (power 3–5) × saturate(N·up × 0.5 + 0.5) × the lighter player colour × 0.3–0.6† [12].
  - **Wrap lighting (fake softness):** (N·L + w)/(1 + w), with w = 0.3–0.5†. GPU Gems used 0.2 for skin; jelly wants more [19].
  - **Back-light translucency:** pow(saturate(dot(V, −(L + N·0.3))), 4) × 0.3–0.5† [20]. Get the main light through a Custom Function node [21].
  - **Optional:** add a matcap highlight at 0.2–0.4† to guarantee the toy streak. A matcap bakes lighting into an image and ignores scene lights [22], so use it only for the highlight and never for base shading.
- **One shader for all players.** Set the colour per player with a MaterialPropertyBlock.

## 5. Game-ready model

- **Triangle budget†:** 4–6k total (body 2.5–3.5k, arms 2 × 250, feet 2 × 300, curl ~300, face shell ~200). Four players is about 24k, trivial on PC, so skip LODs. A fan-made Fall Guys rig is about 4k [23].
- **Topology.** Build the body as an even quad grid (about 8×8 quads per face before bevelling†), so squash and cheek puff bend smoothly. Add 2–3 concentric loops around the mouth and a radial patch on each cheek. Plan the mouth loops around the expressions you need [24].
- **Parts and rig.**
  - Only the body is skinned, because it carries the blend shapes.
  - Arms, feet and curl are separate rigid meshes parented to bones, so no weight painting is needed.
  - About 8 bones: Root, Body, Arm L/R, Foot L/R, Curl (spring).
- **Squash and stretch.** Scale a "Visual" child transform from code. Keep volume by scaling the two perpendicular axes by 1/√stretch [25]. Don't scale bones that have children: the scale passes down to the children badly in-engine [26].
- **Textures:** no body albedo map (colour comes from the material); one 256² mask (R = emblem, G = blush, B = vertical gradient); one 1024² face atlas shared by all players, with blush tinted by player colour.

## 6. Face and expressions

| Method | Pros | Cons |
|---|---|---|
| Texture atlas + UV offset | Cheapest, and handles any shape (><, ^^, @@) | Expressions snap with no blending [27] |
| Geometry eyes | Real 3D shines, easy blink | Can't make ^^ or >< |
| Blend shapes | Smooth, and add real volume | More authoring work |

Animal Crossing–style rigs use texture faces with normal maps for 3D shading [28].

**Recommended hybrid:**
- Eyes and mouth are two UV-offset quads on a thin face shell. The eye cells get a dome normal map and the view-locked catchlight.
- The body gets 3 blend shapes: CheekPuff, Pucker and MouthOpen.
- Each expression is one eye cell, one mouth cell and a set of blend-shape weights.
- The atlas holds 8 eye cells and 8 mouth cells, in 4×2 grids of 256² cells.

| State | Eyes | Mouth | Body (all †) |
|---|---|---|---|
| Idle | ovals; blink every 2–6 s (120 ms); look toward aim (UV shift ±8%) | tiny smile | breathe ±2% |
| Aim | flat-topped "determined" | flat line | lean 5° |
| Charge | slight squint | closed, CheekPuff 0→1 | +6% size, curl swells |
| Spit | wide | "O", Pucker 1 | recoil squash Y 0.85 |
| Hit | > < | wavy | squash, blush flash |
| Stunned | @ @ (rotate the cell's UVs) | wavy | wobble |
| Falling | 1.3× wide | big open "D" | arms up, stretch 1.1 |
| Win | ^ ^ | big grin | hop, blush 2× |
| Lose | drooped | small frown | slump |
| KO cam | x x | open | — |

## 7. Recommended spec

All values are †. W = body width, H = body height, D = body depth. Heights are measured from the bottom of the body.

**Body**
- W:H:D = 1 : 0.94 : 0.92.
- Bevel radius 0.22 W.
- Faces bulge 3–5%.
- Top 3% narrower than the base.
- Top 10% lighter than the bottom (the TF2-style value gradient).

**Eyes**
- Tall ovals, 0.20 H tall and 0.11 W wide.
- Centres at 0.45 H, which is below the midline.
- Centre-to-centre spacing 0.38 W, so the gap between them is about 2.5 eye widths.
- One catchlight at 30% of eye width in the upper-outer area, plus an optional second one at 12%.

**Mouth and blush**
- Mouth centre at 0.29 H. Width 0.12 W at rest, 0.16 W for the "O".
- Blush ellipses 0.16 W × 0.08 H, centred at 0.30 H, below and outside each eye. 35% opacity, player colour shifted toward pink.

**Limbs**
- Arms: 0.25 H long and 0.14 W thick, attached at 0.45 H, set 0.05 D behind the side centre, splayed 20°.
- Feet: 0.30 W × 0.12 H × 0.38 D each. Toes reach 0.06 D past the front. Colour is warm near-black `#2B2622`, not pure black.

**Personality hooks**
- **Front: the cheeks.** They puff up before every spit, and the mouth snaps to an "O". Opponents can read this as a warning before each shot.
- **Top, visible from front and back: the goo curl.**
  - A drip 0.14 H tall, set 0.08 W off-centre, with its tip curling forward.
  - It sits on a spring bone.
  - While charging, it swells to 1.4× and brightens, so it doubles as the ammo gauge.
- **Back: the player emblem.**
  - One shape per player: dot, ring, double stripe or chevron.
  - 0.40 W across, centred at 0.55 H on the back.
  - Body colour lightened by 40%.

**Materials**
- **Blender:** use the vinyl column of the EEVEE table in section 4.
- **URP:** Shader Graph Lit with Clear Coat. Smoothness 0.6, Coat Mask 1, Coat Smoothness 0.92. Rim power 4 × 0.45. Wrap 0.4. Translucency 0.35.
- **Eyes:** black `#0D0D12`, smoothness 0.95.

## Sources

1. Glocker et al. 2009, baby schema — https://repository.upenn.edu/cog_neuro_pubs/11
2. Komori et al. 2022, Frontiers in Psychology — https://www.frontiersin.org/journals/psychology/articles/10.3389/fpsyg.2022.979341/full
3. Gould, "A Biological Homage to Mickey Mouse" (summary) — https://whyevolutionistrue.com/2010/08/11/why-are-animals-cute/
4. Kawaii face construction — https://www.fontbundles.net/design-school/affinity-designer-tutorials/how-to-create-kawaii-faces-in-affinity-designer
5. 80.lv, non-photorealistic eyes — https://80.lv/articles/a-neat-technique-for-setting-up-non-photorealistic-eyes
6. Studio Liddell, design fundamentals — https://studioliddell.com/news/the-ultimate-guide-to-character-design-fundamentals
7. Creative Bloq, simplifying figures — https://www.creativebloq.com/how-to/simplfy-figures-for-animation
8. Valve, GDC 2008 TF2 slides — https://cdn.steamstatic.com/apps/valve/2008/GDC2008_StylizationWithAPurpose_TF2.pdf
9. MCV on Fall Guys — https://mcvuk.com/?p=212598 ; Fall Guys low-hip rig (Pessina) — https://www.ggrecon.com/articles/fall-guys-developers-reveal-what-makes-their-jelly-beans-so-lovable (search-indexed; returned 404 when fetched)
10. Stellar Blade director — https://80.lv/articles/stellar-blade-s-game-director-on-the-importance-of-creating-visually-appealing-characters
11. Splatoon ink tank — https://www.mariowiki.com/Inklings ; Dead Space and Astroneer — https://giantbomb.com/users/3929/articles/markers-iii-diegetic-ui-since-dead-space ; Journey — https://kotaku.com/journey-the-kotaku-review-5889425
12. Valve, Illustrative Rendering in TF2 — https://www.cs.princeton.edu/courses/archive/fall07/cos597B/papers/mitchell-team-fortress.pdf
13. Xbox Accessibility Guideline 103 — https://devdocs.xbox.com/gaming/accessibility/xbox-accessibility-guidelines/103
14. Splatoon Color Lock — https://splatoonwiki.org/wiki/Color_lock
15. Blender manual, Principled BSDF — https://docs.blender.org/manual/en/4.2/render/shader_nodes/shader/principled.html
16. EEVEE 4.2 release notes — https://developer.blender.org/docs/release_notes/4.2/eevee
17. EEVEE 4.2 migration notes — https://developer.blender.org/docs/release_notes/4.2/eevee_migration/
18. URP Complex Lit — https://docs.unity3d.com/6/Documentation/Manual/urp/shader-complex-lit.html
19. GPU Gems, chapter 16 — https://developer.nvidia.com/gpugems/gpugems/part-iii-materials/chapter-16-real-time-approximations-subsurface-scattering
20. Fast translucency (DICE) — https://www.alanzucconi.com/2017/08/30/fast-subsurface-scattering-1/
21. URP custom lighting in Shader Graph — https://nedmakesgames.medium.com/creating-custom-lighting-in-unitys-shader-graph-with-universal-render-pipeline-5ad442c27276
22. Matcap (Redshift docs) — https://help.maxon.net/c4d/en-us/Content/_REDSHIFT_/html/Texture%2BMatcap.html
23. Fan-made Fall Guys rig — https://miguelparts.gumroad.com/l/riggedfallguy
24. Gnomon, topology for animated characters — https://www.thegnomonworkshop.com/workshops/topology-for-animated-characters
25. Squash/stretch volume rule — https://gamedev.net/forums/topic/703514-dynamic-bouncy-bones-demo-tech-breakdown/
26. Game-rig bone scaling — https://www.blendernation.com/2021/03/12/avoiding-squash-and-stretch-issues-in-game-rigs/
27. UV-offset face atlas — https://discussions.unity.com/t/flat-facial-animation-on-3d-character/432055
28. 80.lv, Animal Crossing villager rig — https://80.lv/articles/custom-animal-crossing-villager-3d-rig-built-in-blender
