# Brief: prompts for gameplay clips (MiniMax H3 in ComfyUI)

We need short video clips for a university game-proposal deck. The clips must show our game's **rules working**, not just its look. So far, generations look great but the motion is arbitrary: characters drift, nothing causes anything, and hits don't produce the right reaction. Please write prompts that get the **cause → effect logic** right.

## The model and its inputs

- **Model:** MiniMax H3, running in ComfyUI with the LightX2V turbo LoRA at 8 steps.
- **Output:** one continuous shot per generation (no cuts) at 1344×768, 24 fps. 124 frames is 5.17 s and 243 frames is 10.13 s. Audio is generated from the prompt.
- **Modes**, pick one per prompt:
  1. **Text to video:** prompt only.
  2. **First + last frames:** we supply a start image and an end image.
  3. **Reference image(s):** up to 9 images, referenced as `<Picture 1>`, `<Picture 2>`, …
  4. **Reference video:** a video referenced as `<Video 1>`. Reference images can be added too.
- **Inputs we can supply:**
  - `character_sheet_jelly.png`: the four characters. Use it as `<Picture 1>`.
  - `motion_truth_spit_sim.mp4`: a plain 3D blockout with physically correct motion and the exact choreography we want. Use it as `<Video 1>`.
  - Any start or end frame you ask for.

## The game

A cute party game for 2–4 friends on one local network. Each round is a short mini-game, and points add up across rounds.

**Characters:** squishy, cute **jelly cubes**. They have glossy, slightly see-through gummy bodies, two black dot eyes and a **mouth**. They have **no arms, legs, guns or barrels**. There is one per player, in coral, cyan, lime and violet. They wobble, squash and stretch like jelly.

**Arena (first mini-game):** a flat, square, floating platform with clean edges and nothing below it. Falling off the edge means you're out.

## The rules a clip must show (in this causal order)

1. **Shooting = spitting a piece of yourself.** A cube opens its mouth and spits a small jelly ball (or a tiny cube) **of its own color**. That ball came out of its body, so **the shooter gets slightly smaller** with each spit.
2. **Recoil:** spitting pushes the shooter **slightly backwards**, away from the shot.
3. **The ball flies in a straight, fast line** to the target. It doesn't curve, wander or home in.
4. **On impact**, the target cube **squashes** (jelly wobble), small jelly chips of the **target's** color splash off it, and **it gets smaller** (it lost mass).
5. **Knockback:** the target **slides straight backwards**, away from the shooter, along the line of the shot, and slows to a stop through friction.
6. **Lighter means you fly further.** Because the target is now lighter, **each later hit pushes it further** than the one before.
7. **Ring-out:** the hit that pushes it past the edge makes it tip over the edge and **fall down** out of the arena.
8. **Everything else stays put.** Cubes that aren't involved stay where they are (idle wobble only). Nothing moves without a cause.

## The exact clip we want (5 s, static or slowly drifting camera)

The side-on, slightly high camera sees the whole platform, with its **right edge** clearly visible.

| time | what happens |
|---|---|
| 0.0 s | Coral (left) faces cyan (centre right). Lime and violet idle in the background. |
| ≈0.55 s | **Spit #1:** coral spits a coral ball, shrinks a little and recoils slightly. |
| ≈0.8 s | **Hit #1:** cyan squashes, splashes cyan chips, shrinks, and slides back about 1 body length. |
| ≈1.85 s | **Spit #2:** coral is a bit smaller again. |
| ≈2.1 s | **Hit #2:** cyan is smaller still and slides further, about 1.5 body lengths, now near the edge. |
| ≈3.15 s | **Spit #3** |
| ≈3.4 s | **Hit #3:** cyan, now visibly small, slides fast to the edge, tips over and **tumbles down** out of frame. |
| 4–5 s | Coral is visibly smaller than at the start. Spent coral balls lie on the platform. |

The attached `motion_truth_spit_sim.mp4` shows exactly this motion.

## What has failed so far

- **Text-to-video:** beautiful, but characters wander randomly, shots don't connect, and no cause and effect is visible.
- **Reference video (restyle the blockout):** this mode **does follow the blockout frame for frame** and restyles it (sky, arena material, character details). The early test only failed because its blockout used an outdated design (barrel cubes, no spitting, no shrinking). Remaining weak points: characters that are small in frame get little detail, and we still need the jelly look, open mouths and visible chips to come through clearly.

## What we'd like from you

- A prompt for **each mode** you think can work, with the exact input images you'd want for First + last frames.
- If the full 3-hit chain is too much for one generation, split it: **one clip = one beat** (one spit → one hit → one slide, or the final hit → fall). Tell us which first and last frames to prepare for each beat.
- Prompt-writing tactics that help the model keep **causal order** and **direction of motion**, and keep **uninvolved objects still**. For example: timestamps, explicit "left to right" wording, or negative instructions.
