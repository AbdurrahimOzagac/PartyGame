# Blob Creature Animation Spec (30 fps, Unity 6 Generic rig)

**Conventions.** Frame 0 is the first frame. Height H = 1.0 m. Unity axes: +Z forward, +Y up, +X right. Pitch + = lean forward, roll + = lean right. `Sy` is the Body bone's vertical scale and `Sxz` its horizontal scale. Keep volume with **Sxz = 1/sqrt(Sy)**: 0.76→1.147, 0.80→1.118, 0.90→1.054, 0.95→1.026, 1.04→0.981, 1.06→0.971, 1.10→0.953, 1.15→0.933 [1][17]. Looping clips key the last frame identical to frame 0, and Unity clip length = N/30 s. Values marked *(judgment)* are my calls, not sourced numbers.

## 0. Rig and Unity setup (do this first)

```
Root            ground level; the clip root; never moves in XZ (in-place)
├─ Foot_L/R     children of Root, NOT Body, so squash never resizes or lifts feet
└─ COG          ~0.15 m: bob, sway, lean, roll, yaw (locomotion owns it)
   └─ Body      pivot at body base: squash/stretch scale + action lean
      ├─ Eye_L/R      scale for blink/widen; translate for bounce
      ├─ Arm_L/R
      └─ Jiggle_Top   runtime spring only, weighted to the top third
Mesh: blend shapes Pucker, MouthO, Smile, Frown, CheekPuff, EyesHappy(^^), EyesX(><)
```

- **In-place locomotion, not root motion.** Code drives position (CharacterController or Rigidbody plus NetworkTransform). Root motion is hard to keep consistent across clients, and speed can't be retuned without re-timing clips [13]. Rig: Generic, Root node = `Root`, Bake Into Pose on all root curves [11].
- **Squash on a dedicated bone.** Keep Blender's Inherit Scale = Full (it composes like Unity Transforms) and give Body's children Body's rest orientation. Non-uniform parent scale shears rotated children, and the other inherit modes don't bake cleanly to FBX [16].
- **Layers** [9][10]:
  - L0 Base, no mask: locomotion blend tree, Airborne, Land, Hit, Victory, Defeat.
  - L1 Action, Override, mask = Body subtree plus mesh. Spit and taunt play here over the run, while COG and feet keep running.
  - L2 Face, Override, mask = eyes plus mesh: random blinks.
  - **Rule:** locomotion clips never key face blend shapes. That avoids relying on whether a transform mask filters blend-shape curves (I didn't verify that).
- **Transitions into actions take 0–1 frames**, not Unity's 0.25 s default. Use 3–4 frames out.
- **Networking.** Call `NetworkAnimator.SetTrigger`, because a plain `Animator.SetTrigger` doesn't reach other clients. Under server authority the owner lags by about one RTT, so the owner plays the spit locally right away [12].

## 1. Idle (60-frame loop, 2.0 s) + Blink (L2)

| F | Pose |
|---|---|
| 0 | Neutral. Sy 1.00. Arms 0°. Eyes Sy 1.0 |
| 30 | Inhale: Sy 1.025 / Sxz 0.988, COG y +0.005 m, arms −3° (out). Eyes follow 4 f late |
| 60 | = F0 (ease in/out on both breaths) |

Add a COG yaw drift of ±2° with peak at F15 and trough at F45, so it doesn't sit in phase with the breath.
**Blink** (7 f, triggered every 2–6 s at random, 20% double blink *(judgment)*): F0 open → F1 eye Sy 0.5 → F2 0.05 → F3 0.05 → F4 0.6 → F5 1.05 (overshoot) → F6 1.0. Close fast, open slower. Never bake blinks into loops, because the repetition shows.

## 2. Run (12-frame loop, 0.4 s, 5 steps/s)

Short legs need high cadence. This sits between Williams' "run" timings of 4 and 6 frames per step at 24 fps [3]. Left contact at F0, right contact at F6. F6–F11 mirror F0–F5: swap feet and arms, and flip the sign of sway, roll and yaw. F12 = F0.

| F | Pose | COG y | COG x (sway) | Pitch / Roll / Yaw | Body Sy | Feet (z, y) | Arms (pitch) |
|---|---|---|---|---|---|---|---|
| 0 | Contact L | −0.015 | −0.020 | 10° / −3° / −4° | 0.97 | L (+0.13, 0) planted; R (−0.15, 0.06) heel kicked up behind | L −25°, R +25° |
| 1 | **Down** | **−0.035** | −0.025 | 12° / −5° / −3° | **0.90** | L (+0.043, 0); R (−0.06, **0.09**), tucking forward | L −30°, R +30°, both rolled out +10° (flap) |
| 2 | Passing | −0.010 | −0.015 | 11° / −3° / −1° | 0.99 | L (−0.043, 0); R (+0.03, 0.09) | ±15° |
| 3 | Push-off / Up | +0.030 | −0.005 | 9° / −1° / 0° | **1.06** | L (−0.13, 0.01), toe-off, toe pitch −30°; R (+0.10, 0.06) | ±5° |
| 4 | Float apex | **+0.045** | +0.005 | 8° / +1° / +2° | 1.03 | L (−0.15, 0.03); R (+0.14, 0.03), toe up +15° | R −10°, L +10° |
| 5 | Pre-contact R | +0.025 | +0.015 | 9° / +2° / +3° | 1.00 | L (−0.16, 0.05); R (+0.14, 0.01) | R −20°, L +20° |

- **Planted-foot Z keys F0→F3 must be linear.** That gives a constant 0.26 m per 0.1 s = **2.6 m/s authored speed**. Set `Animator.speed = v / 2.6`, clamped to 0.85–1.35. Past that range, accept slight sliding: on a cute fast character the eye reads the body bob *(judgment)*.
- Overlap: arms trail the feet by 1 frame. The Eye_L/R y lags COG by 1 frame (−0.01 m at F2, +0.008 m at F5).
- Bob curve: a sharp V at the bottom (F1) and an eased float at the top (F4). That's bouncing-ball spacing, which reads as springy jelly.
- **Looping in Blender:** put a Cycles F-modifier on every curve and turn on Cycle-Aware Keying. Auto handles then smooth across the seam, and you can slide secondary keys in time and they wrap [15]. Bake on FBX export, then enable Loop Time and Loop Pose in Unity [11].

**Walk** (if analog stick speed control is wanted): a 16-frame loop with no flight phase. Bob ±0.015 m. Roll waddle ±6° (more waddle than the run). Contact at F0 and F8, so walk and run share normalized phase and the 1D blend tree (0 / 1.0 / 2.6 m/s) never scissors the feet. A party game can skip Walk and go straight Idle→Run.

## 3. Spit "ptoo!" (22 frames, 0.73 s)

- **Budget:** comfortable controls answer within 100 ms [8]. What matters is the first *visible* response, so frame 0 is already the cheek puff and there are no dead frames.
- **Release:** the projectile leaves at **F4 = 133 ms**. Kirby's star spit releases at 8 frames at 60 fps (also 133 ms) and lasts 30 frames at 60 fps [4]. Mario's fireball (frame 17 at 60 fps) and Fox's blaster (frame 11 at 60 fps) are slower [5][6].
- **Shape of the move:** short anticipation, long cancelable recovery [1][2]. Smash makes recovery interruptible before the clip ends (Mario: frame 50 of 63) [5].

| F | Pose |
|---|---|
| 0 | Inhale starts: CheekPuff 0.5, Pucker 0.3. Body pitch −6°, Sy 1.04. Arms −20°. Eyes squint 0.3 |
| 2 | **Anticipation extreme**: CheekPuff 1.0, Pucker 1.0. Body scale XZ 1.04, Y 1.06 (a balloon inhale, so volume grows on purpose) and pitch −10°. EyesHappy 0.6 (effort squint). Arms −30° |
| 3 | Moving hold: pitch −11°, everything else held |
| **4** | **RELEASE / projectile spawn**: Pucker→MouthO 0.6, CheekPuff 0.3. Pitch +12°. Stretch toward target: Sz 1.12, Sx = Sy 0.945. Arms +35°. Eyes Sy 1.1 (pop) |
| 5 | Lunge extreme: pitch +15°, Sz 1.15 / 0.933, CheekPuff 0, MouthO 0.4 |
| 7 | Recoil: pitch −4°, Sy 0.92 / 1.043. Mouth closes with a "smack" (Pucker 0.2) |
| 9 | Overshoot: pitch +3°, Sy 1.03 |
| 11 | pitch −1.5°, Sy 0.99 |
| 12 | Satisfied: EyesHappy 1.0, Smile 0.7, CheekPuff 0.2 |
| 16 | Moving hold (Sy 1.01, Smile 0.7) |
| 18 | Eyes open with a 1.05 overshoot |
| 22 | Neutral (= Idle F0) |

- **Cancel windows:** movement cancels from F8 (3 f blend out); re-spit from F10, about 3 shots/s *(judgment)*.
- **Timing source:** gameplay spawns the blob on a timer, 4/30 s after input. The F4 Animation Event fires only VFX/SFX ("ptoo" plus a puff particle), because events can double-fire in crossfades and stop when the Animator is culled. The client shows a predicted blob; the server spawns the real one.
- **Long inhale:** make it hold-to-charge, never forced startup.
- **Facing:** if the over-the-shoulder camera keeps the character aimed, snap-turn the root to aim over 2 f on press, or add strafe clips (2D blend F/B/L/R; L/R can be a sideways hop-shuffle).

## 4. Hit by blob, front (20 frames; Hit_Back mirrors pitch)

Hitstop follows Sakurai's approach: freeze the pose, shake the victim side to side when grounded and up/down when airborne, start the shake large and let it decay, keep hitboxes still, and keep it short in free-for-all play [7]. Here that means animator speed 0 for F0–F2 while physics knockback keeps running.

| F | Pose |
|---|---|
| 0 | Impact squash along the hit axis: Sz 0.80, Sx = Sy 1.118. EyesX 1.0, MouthO 0.6. Arms +40° out. Goo splat VFX |
| 0–2 | Hitstop: mesh offset x = +0.02, −0.015, +0.008 m |
| 3 | Rebound: pitch −18°, Sz 1.10 / 0.953. Arms flail +70°. EyesX→0, eyes Sy 1.2 (shock) |
| 5 | pitch −22° (extreme), Sy 1.04 |
| 7 | pitch +6°, Sy 0.93 |
| 9 | pitch −3°, Sy 1.02 |
| 11–17 | Wet-dog shake-off: yaw ±12°, ±9°, ±5°, 0 every 2 f. EyesHappy 0.8, CheekPuff 0.3 |
| 20 | Neutral |

Movement can cancel from F10 and spit from F12. If the knockback impulse is above a threshold, go to Airborne at F3.

## 5. Other clips

- **Airborne panic** (12-frame loop). Arms flap ±50° every 4 f (3 flaps per loop). Feet pedal in air on a 12-f cycle (z ±0.10, y 0.02–0.08). Sy 1.05, optionally stretched along velocity by code. Roll wobble ±6°. Eyes Sy 1.15 with ±0.005 m tremble every 2 f. MouthO 0.8 ±0.1 every 4 f. Every period must divide 12.
- **Land** (10 f, can cancel to locomotion from F2). F0 Sy 0.78 → F1 0.76 (max) → F3 1.08 → F5 0.97 → F7 1.01 → F10 1.0. Eyes drop −0.02 m at F2. Scale the weight by fall speed, e.g. clamp(vy/8, 0.3, 1).
- **Victory** (45-f one-shot, then 30-f loop):
  - F0–4: crouch to Sy 0.85.
  - F5: jump stretch Sy 1.15, COG y +0.30.
  - F5–17: 360° yaw spin, arms +90°.
  - F18: land squash 0.82. F22: overshoot 1.06.
  - F26: arms-up pose, EyesHappy 1.0, Smile 1.0.
  - Loop: two hops (COG y +0.04, Sy 1.04) every 15 f.
- **Defeat** (40-f one-shot, then 60-f loop):
  - F0: eyes widen to 1.15.
  - F8: deflate to Sy 0.86, Sxz 1.03 (deliberate volume loss), pitch +12°, arms −10°, half-lids, Frown.
  - F20: CheekPuff 0.6. F28: sigh out, CheekPuff 0, Sy 0.84.
  - Loop: slow breath Sy 0.84↔0.86.
- **Taunt "Puff & Wiggle"** (36 f; movement can cancel it from F0).
  - F0–6: CheekPuff 1.0, body to 1.06.
  - F6–24: side hop-wiggle with roll ±10° every 6 f, arms flapping.
  - F26: raspberry "pfft" with a bubble VFX (no projectile).
  - F30–36: settle and blink.

## 6. Easing and curves

- **Into an anticipation extreme:** ease out of the start, ease into the extreme.
- **Release frame:** wide spacing, with linear or fast-in tangents into F4. The snap is the cuteness. Then a slow-in to the lunge extreme [1].
- **Overshoot and wobble:** a damped oscillation where each swing is 40–50% of the last, with a 4–6 f period for a 1 m jelly, showing 2–3 visible swings *(judgment)*. Blender's Back and Elastic interpolation can block these in quickly; bake on export.
- **Holds** are always moving holds: 1–2% scale drift or 1° of rotation. Fully frozen frames are reserved for hitstop.
- **Contact frames** (run feet, land F0): linear in, no ease. Bob bottoms are sharp and bob tops are eased.
- **Arcs:** keep arm and eye paths on arcs, and break them on purpose at impacts [1].

## 7. Secondary motion: baked vs runtime

- **Bake anything tied to timing or acting:** spit squash, recoil wobble, the 1–2 f arm-drag and eye-lag offsets above.
- **Runtime springs only for unpredictable forces:** knockback direction, sudden stops and turns, landings. Cooper notes real-time physics rigs handle soft-body drag [1]. In Unity, put Animation Rigging's Damped Transform on `Jiggle_Top` and each eye (Damp Position about 0.3–0.5 *(judgment)*) [14], or use a small critically-damped spring in LateUpdate.
- **Runtime springs are cosmetic and local,** so no network sync. Keep them subtle so they don't double up with the baked squash.

## Sources

1. Cooper, *The 12 principles of animation in video games* (Game Anim excerpt): https://www.gamedeveloper.com/production/the-12-principles-of-animation-in-video-games
2. Game Anim, anticipation/apex/recoil: https://www.gameanim.com/?p=115
3. Survival Kit timing table (Monmouth): https://animation.monmouth.edu/instruct/?p=351
4. SmashWiki, Kirby Inhale / star spit: https://www.ssbwiki.com/Kirby_(SSBU)/Neutral_special
5. SmashWiki, Mario Fireball: https://www.ssbwiki.com/Mario_(SSBU)/Neutral_special
6. SmashWiki, Fox Blaster: https://www.ssbwiki.com/Fox_(SSBU)/Neutral_special
7. Sakurai on hitstop (translation): https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/
8. 100 ms response target (Swink and Card et al., lecture): https://medienwissenschaft.uni-bayreuth.de/wp-content/uploads/GxD-07-Interfaces.pdf ; https://gamedesignskills.com/game-design/game-feel/
9. Unity Animation Layers: https://docs.unity3d.com/6000.0/Documentation/Manual/AnimationLayers.html
10. Unity Avatar Mask: https://docs.unity3d.com/6000.0/Documentation/Manual/class-AvatarMask.html
11. Unity clip import (Loop, Bake Into Pose, Additive ref): https://docs.unity3d.com/6000.0/Documentation/Manual/class-AnimationClip.html
12. NGO NetworkAnimator: https://mp-docs.dl.it.unity3d.com/netcode/2.2.0/components/networkanimator
13. Root motion in multiplayer: https://discussions.unity.com/t/animation-with-root-motion-synchronization-in-multiplayer/90067
14. Damped Transform: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/DampedTransform.html
15. Blender F-curve modifiers (Cycles, cycle-aware keying): https://docs.blender.org/manual/en/latest/editors/graph_editor/fcurves/modifiers.html
16. Blender inherit-scale and shear rationale: https://developer.blender.org/rBfcf2a712eca278f198e8d61845f2078088af1f9a
17. Pixar volume-preserving squash patent: https://patents.google.com/patent/US7221380
18. Rosen, *An Indie Approach to Procedural Animation* (GDC 2014): https://gamedeveloper.com/design/video-an-indie-approach-to-procedural-animation
