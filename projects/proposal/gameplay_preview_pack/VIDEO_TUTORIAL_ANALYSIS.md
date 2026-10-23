# Lessons from the six uploaded animation/VFX tutorials

These six source clips teach game feel / stylized impact design. They are **not** actual racing footage or evidence that physics are implemented. Observations below come from the visuals and on-screen captions.

1. **How to Make Hits Punchy** (`2f771ce172384d4f90e3cb765d142a32.mp4`, 34.3 s): transient contact flash, sound cue, sparse particles, displacement, and 1–3 frames of freeze or shake make a hit readable. **Apply:** single contact flash + Cyan knockback. **Do not apply:** violent camera shake and an overloaded effect stack.
2. **How to Animate Lightning** (`4e25bc5cb4274520a33314f42c2cb6c0.mp4`, 57 s): exaggerated, tapered directionality, accent branches, progressive reveal, rapid disappearance. **Apply:** if needed, a tiny directional streak at the projectile's contact point. **Do not apply:** actual lightning effects; they imply another ability.
3. **How to Make a Magic Attack** (`5973c93760ed4fdd9a81fd5bcb668cc5.mp4`, 57 s): coherent attack stages: charge, visible core, trailing particles, hit burst, reaction and aftermath. **Apply:** material chunk is visible from the start, travels as one object and disappears only on contact. **Do not apply:** magical glow or lingering aura that would contradict physically thrown mass.
4. **How to Make Gunfire Punchy** (`66fa67019f8a46cda36c93548b40be9e.mp4`, 56.2 s): extremely quick shot cue, readable bullet path, distinct hit reaction, recoil and finality. **Apply:** restrained release anticipation and precise projectile-to-target direction. **Do not apply:** guns, muzzle flashes, shell casings, blood or realistic bullet effects.
5. **How to Make Anime Hits** (`69bd2f758dbb4298b7f9ae926543a490.mp4`, 28.3 s): impact emphasis through held pose, strong silhouettes and concentrated burst lines. **Apply:** optional 2-frame hit-stop on the decisive knockout hit. **Do not apply:** dramatic full-screen inversion, speed-line screens or hard cinematic cuts.
6. **VFX — Cartoon Rules** (`fc33c530da4f48daabd119a46af6fc16.mp4`, 51.3 s): anticipation, squash/stretch, follow-through, staggered/layered movement, impact timing and curved action arcs. **Apply:** Orange follows a smooth drifting curve in Ice Race; body chunk and Cyan respond with follow-through in Mass Knockout. **Do not apply:** overly elastic bending that implies expensive jelly simulation.

## Crucial distinction

**Game feel** is feedback added on top of player agency. It does NOT replace deterministic world movement. Ice Race needs a continuous forward trajectory and a visually coherent bend; do not ask a video generator to independently improvise four racers. Mass Knockout needs one projectile source, one target, and an unambiguous collision normal followed by knockback and a fall.

## Prompting design decisions

- One image = one mini-game. Never provide a combined concept board as the principal frame for image-to-video.
- Keep the opening shot compatible with the input reference. The current Mass Knockout screenshot begins mid-throw, so its clip continues that throw instead of describing an earlier windup as the first frame.
- Treat other players as background actors with strictly limited motion during the focal event.
- Don't require exact numerical HUD changes, which generative video can garble; composite HUD in editing if needed.
- Favor a 5–7 second action and no cuts; if continuity breaks, use a deterministic Unity graybox animation as video conditioning rather than adding more adjectives to the prompt.
