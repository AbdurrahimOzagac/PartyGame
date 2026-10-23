# Shooter Party: talking points (v10, 28 slides)

Short subjects to talk through, not a transcript. Numbers come from the slides.

## Introduction
**1. Cover**
- Shooter Party: working title, 4 people, CMP406
- One line: one shooting cube, many mini-games, and a shot that does something new in each
- All images are concept renders, not gameplay

## The game
**2. Game description**
- Each player on their own device; one hosts, the others join over the network
- Session loop: lobby → pick → play → place → standings, repeat
- Host sets the rules: ends after N mini-games or at a target score
- Most points wins

**3. Points and comeback**
- Placement → points (4/3/2/1 for four players, a proposal; scale grows with player count)
- Three comeback rules: +1 for last place overall, ties go to the one further behind, crown on the leader
- Why: session stays open, everyone sees who to catch

## Gameplay
**4. Core mechanics**
- Small cube character, barrel fires real physics projectiles (not hitscan)
- One trigger, three effects: damage, knockback, blast (blast at the ground lifts you)

**5. Shot in each mini-game**
- Same input, five meanings: Arena pushes, Parkour blasts, Ice Loop momentum, Hole Grab pushes away from holes, Battle damages
- Controls: WASD, jump only where allowed, Shift dash (cooldown or power-up charges), third-person camera

## Mini-games
**6. Knockback Arena**
- Shoot others off a flat floor
- Mass, not size: each hit removes mass, so the same shot flies further
- Jump is a pickup, dash on Shift; fall off = out, ranked by elimination order

**7. Blast Parkour**
- Obstacle course in the sky, open islands, no cover
- Hold to charge a strong shot (can't move while charging); shoot the ground to climb
- Falls out below the course; first to the flag wins, others ranked by height
- Fallback if hitting is too easy: gravity on shots, decided in playtests

**8. Ice Loop**
- Most checkpoints before the timer ends wins; the count decides, not the lap
- Slippery ice; pushed into water = restart from last checkpoint
- Firing forward pushes you back, so recoil steers you
- Ice is the theme of this mini-game only

**9. Hole Grab**
- Musical chairs with holes, shaped like the player with a barrel slot
- One hole fewer than players each round (4 players: 3, 2, 1)
- Bulldozer crushes whoever is left; players in a hole can't be pushed out

**10. More mini-games and ideas**
- Playlist is built to grow: same cube, same shot, new maps
- Battle (damage) is the stretch goal; The Monster: one player vs the rest, role rotates
- Concept ideas, not in the plan: character types (weight first), rage meter

**11. Customisation**
- Four slots: head, face, ears, extras; shop with try-on in the lobby
- Cosmetics never change mass, speed or the shot, never cover barrel or eyes
- The crown is not for sale

## Technology
**12. Technical design**
- Each mini-game implements one small contract: set up, run, report placement
- Shared layer: controller, camera, shot system, session/playlist, scoreboard, menus/shop
- Unity 3D, rigidbody physics; one player hosts, Netcode is the candidate

**13. Network sequence**
- Host decides everything that affects a result, so every screen agrees
- Own movement predicted locally; clients interpolate snapshots
- Main risk: smooth physics on every client. Mitigation: tune snapshot rate, test with real players early

## Market
**14. Market and audience**
- Audience: friend groups wanting short chaotic rounds, plus streamers and viewers
- $197B global games market 2025, PC $43.6B (+12%), 70% of US players 8+ play with others
- Honest risk: Steam is crowded (65.9% of 2025 releases under $1,000). Our hook: one shot, new meaning every mini-game

**15. Party games keep selling**
- Super Mario Party 21M+, Jamboree 5.6M in 11 weeks: mass-market and still growing
- Small studios can win: Stick Fight ~4.75M (estimate), Pummel Party still selling
- Party Animals: 135,834 playtest players; Feather Party 87% positive

**16. Competitors**
- Party Animals, Gang Beasts (physics brawlers), Fall Guys (playlist), Pummel Party (board + mini-games), Feather Party (22 mini-games, elemental physics)
- Owner numbers are third-party estimates, labelled as such

**17. Where we sit**
- All five have 3D characters and cosmetics
- None keeps one shot whose effect changes per mini-game
- Our column is the plan, not shipped software

## Innovation
**18. Six pillars**
- One trigger, many effects → controls learned once
- Shooting at the heart, variety on one mechanic, built-in comeback, your session your rules, mass not size

**19. How we differ**
- Others: each mini-game has its own controls. Us: one character and one shot carry skill between rounds
- Comeback is a fixed visible rule, not random items
- Smash uses damage %, we use physical mass

## Drafts
**20. Art direction and blockouts**
- Glossy toy: rounded cubes, bright colours, grass/sky/water, ice only in Ice Loop
- 3D blockouts check camera, scale, shot readability before Unity

**21. Level drafts**
- Top-down: Arena (flat square, open edges, no walls) and Ice Loop (looping track, checkpoints)
- Dimensions, spawns, checkpoint count not set yet

## Plan
**22. Development plan**
- 12 weeks, four checkpoints: Proposal 19 Oct, Demo 1 16 Nov, Demo 2 14 Dec, Final 11 Jan
- Risks: networked physics (host simulation), balance (fire-rate, jump as power-up, comeback), scope (shared core first, small mini-games)

**23. Everyone designs a mini-game**
- Abdurrahim: Knockback Arena + core prototype, scoreboard
- Ardıl: Blast Parkour + menus, promo video
- Kürşat: Ice Loop + UI design, sound/music
- Ezel: Hole Grab + online system, sound/music
- Assignments are a proposal

**24. Gantt: Demo 1 (weeks 1–4)**
- Core prototype, online research, menu/lobby, UI style
- Week 1 everyone designs their mini-game and shot; weeks 3–4 blockouts; Demo 1 build

**25. Gantt: Demo 2 (weeks 5–8)**
- Scoreboard/points/comeback, networked shots, finish own mini-games and assets, session menu, in-game UI
- Week 8: first full-session playtest + Demo 2 build

**26. Gantt: Final (weeks 9–12)**
- Cosmetics shop, sound, balance, polish; Battle as stretch goal
- Online tests, promo video, final deck. Final date may still move

## Close
**27. In short**
- One cube, one shot, new meaning each mini-game
- Comeback built in
- Sessions you shape: playlist, end rule, look
- Questions?

**28. Sources (appendix)**
- Only if asked; owner counts are third-party estimates
