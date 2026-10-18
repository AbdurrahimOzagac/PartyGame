# ICE RUMBLE — provisional two-mini-game party-game concept

## Product scope

2–4 players on one LAN. One host provides authoritative game state. Players join over a local IP address. Short mini-games rotate; placement points contribute to a shared scoreboard and overall winner. No dedicated server, matchmaking, persistent accounts or content pipeline required for the term project.

## Shared game rules and assets

- The same 4 rounded cube characters: Orange, Cyan, Green and Purple; soft corners, eyes, tiny arms/feet, colored flat-plastic appearance.
- Shared movement input, camera family, collision, player names/colors, LAN spawn/despawn, countdown, score table, round transition and result display.
- Different physics tuning per mini-game: slipperier driving in Race, more responsive grounded movement in Knockout.
- Round placement awards: 1st 4 pts, 2nd 3 pts, 3rd 2 pts, 4th 1 pt. A suggested session is two rounds of each mode, then overall standings. These are **proposed** rules to playtest.

## Mini-game A — ICE RACE

**Fantasy:** a simple top-down/raised-third-person ice race where the challenge is managing momentum on turns.

**Mechanic:** input affects acceleration rather than setting velocity instantly. Low lateral friction means a character can point into a turn while still moving outward. Players race around a single easy-to-model ribbon of icy ground with obvious drop-offs. Respawn at last activated checkpoint if they fall. Win by reaching finish first; use progress for unfinished participants at timeout.

**Prototype controls:** WASD/left stick = accelerate and steer; optional Space/trigger = brake (tune before deciding). No shooting. Players have the same acceleration and grip.

**Prototype map:** 1 continuous track composed of 5–8 simple box/extruded segments and 2 turns, painted arrows, 2 trigger checkpoints. No moving platforms, complex ice fracture, physics puzzles or snow storms.

**Skill expression:** pre-position before a turn; release acceleration; take a tighter apex; manage velocity to avoid falling. **Failure:** carry too much lateral velocity over an exposed edge and respawn at checkpoint.

**Baseline tuning examples (not promises):** speed cap 6–8 units/s, a moderate acceleration and low friction; increase grip only while braking. Avoid fake controls that rotate the character instantly through a hairpin.

**Video preview:** Orange executes a deliberate inside-line overtake on Cyan through one broad turn. Other players continue forward. No random fall is shown in the main racing clip. The ongoing race is clearer and more exciting than a random knockout.

## Mini-game B — MASS KNOCKOUT

**Fantasy:** four compact blobs duel on one flat raised platform by physically throwing away pieces of their own bodies. No guns or cannons.

**Mechanic:** each primary attack detaches one same-colored mass chunk from the attacker's body; the attacker shrinks as the chunk leaves. A hit applies knockback, causes the target to lose some mass and makes it easier to displace next time. A missed throw still costs the attacker mass. Falling off scores a knockout for the last hitter. After a short delay, the victim respawns; round ends on timer and the players are ranked by knockouts (ties: hits).

**Prototype controls:** WASD = move; mouse/stick = aim; click/trigger = single mass throw; short cooldown. No weapon model. A throw is a sphere projectile emitted from the cube's front/hand; characters uniformly scale rather than deforming with fluid simulation.

**Illustrative balance values to playtest:** initial scale 1.0; every throw costs 0.08 of initial scale; successful hit reduces target's scale by 0.12; minimum scale 0.65; knockback scales from ~1.0 to ~1.7 as target gets smaller; throw cooldown ~0.8 s; respawn after 2 seconds. Consider slow regeneration or a small central mass pickup **only if** playtests show attacking is excessively punished.

**Important design risk:** if each throw shrinks the attacker, standing back and waiting can become optimal. Reward knockouts, make rounds short, and tune projectile speed/damage after playtesting. The current rule is arcade-style mass reduction, not a physically mass-conserving simulation. If mass conservation is required, add recoverable fragments later—not in the MVP.

**Prototype arena:** one static rectangular or square platform, completely exposed perimeter and a blue void, no guardrails, moving elements or decorative obstacles. Falling below a Y-threshold triggers a knockout.

**Video preview:** start with Orange's own colored chunk already launched; follow it to Cyan, show Cyan squash/shrink, persistent physical knockback, and the fall below the visible rim. Green and Purple are inactive onlookers during the focal shot so cause-effect remains readable.

## Minimal technical plan

1. Local character prefab, camera and shared movement; collision layer and fall detector.
2. LAN host/client authority over round state, transforms, projectiles and score.
3. Ice Race input smoothing, ribbon track, checkpoint triggers, respawn, finish/progress.
4. Mass Knockout body-size state, projectile spawning/hits, impulse, knockouts, respawns.
5. Lobby, rotating mini-game state machine, per-round results, overall winner.
6. Visual polish restricted to small skid marks, projectile trails, hit flash and UI overlays.

## Deliverability and honesty

These are concept reference images and **AI-generated gameplay preview instructions**, not screenshots or recordings of an implemented build. University slides must label the footage accordingly.
