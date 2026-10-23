# Shooter Party: presentation script

CMP406 proposal, 19 October 2026. Script for the 28 slides of the submitted deck.

Full run ≈ 16:30 · short run (skip *optional* lines) ≈ 14:20

| Speaker | Slides | Full | Short |
|---|---|---|---|
| Ezel Bayraktar | 1, 2, 3, 8, 9, 13, 28 | 4:25 | 3:45 |
| Abdurrahim Özağaç | 4, 5, 11, 12, 24, 25 | 4:05 | 3:35 |
| Ardıl Bilal Ülkü | 6, 14, 15, 16, 17, 26, 27 | 4:00 | 3:30 |
| Kürşat Hürkan Aydın | 7, 10, 18, 19, 20, 21, 22, 23 | 4:00 | 3:30 |

Legend: plain text = say it · *(optional)* = cut if short on time · [cue] = stage direction · → = handoff

---

## Opening

### 1. Cover · **Ezel** · ≈0:45

> [cue] All four stand in a row, in first-speaking order: Ezel, Abdurrahim, Ardıl, Kürşat. Wait for silence. Don't start talking until the room is quiet.

Four friends. One button.

> [cue] Pause one beat.

In the first round, that button knocks your friend off the edge of the arena. In the next one, it blasts you up into the sky. On ice, it steers you. Same button, same aim, every time.

This is Shooter Party: a multiplayer party game with one shooting cube, many mini-games, and a shot that does something new in each one.

We're four. I'm Ezel, on the online system and sound. Abdurrahim builds the core prototype and the scoreboard. Kürşat does UI design, sound and music. Ardıl does the menus and the promo video. And each of us designs one mini-game.

> [cue] Each person nods or raises a hand when their name is said.

*(optional) Shooter Party is a working title.*


---

## 01 · The game

### 2. What is Shooter Party? · **Ezel** · ≈0:30

> [cue] Rubric: "intro without technical jargon" (5 pts). Keep this slide plain, with no engine or network words.

In plain words: everyone plays on their own device. One player hosts, the others join.

You control a small shooting cube in your own colour.

Each mini-game changes what your shot does: it pushes, it damages, or it lifts you.

When the session ends, whoever has the most points wins.

*(optional) Short, chaotic rounds with friends. That's the whole idea.*


### 3. How a session runs · **Ezel** · ≈0:45

> [cue] Sweep your hand left to right across the five steps.

A session runs in five steps. Lobby: the host sets the rules and the others join. Pick: the playlist picks the next mini-game. Play. Place: your final placement becomes points. Standings: the scores update, and the leader wears the crown.

Points are a proposal for now: 4, 3, 2, 1 for four players, and the scale grows with more players.

Now the part we like most: the comeback rules. After each mini-game, whoever is last overall gets plus one point. Ties go to the underdog. And the crown on the leader shows everyone who to catch.

*(optional) The host also picks how it ends, after a set number of mini-games or at a target score, and whether the playlist is random or custom.*

**→ Abdurrahim:** "So what does a shot actually do? Abdurrahim built the core of it."


---

## 02 · Gameplay: the relay

### 4. One trigger, different effects · **Abdurrahim** · ≈0:40

One shooting cube. Every mini-game changes what a shot does.

The barrel fires real projectiles. They're physics objects you can see flying, not instant hits.

> [cue] Point at the three pictures in order.

There are three effects. Damage lowers the target's health. Knockback pushes the target away from where the shot came from. Blast explodes, and if you fire it at the ground, it lifts you up.

The controls never change: WASD to move, Shift to dash, jump only where a mini-game allows it, and a third-person shooter camera, because aiming is the heart of the game.

**→ Abdurrahim:** "Next, four mini-games, and each one is presented by the person who designs it. Mine is first."


### 5. Knockback Arena · **Abdurrahim** · ≈0:45

Quick note first: every scene you'll see today is a 3D concept render, not gameplay.

Knockback Arena. Shoot the others off a flat floor.

> [cue] Point at the three arrows on the right, top to bottom.

The key rule is mass, not size. Every hit lowers your mass, but your size stays the same. So the same shot sends you a bit further each time, until one more shot sends you off the edge.

Shooting is free, with a fire-rate limit, and it has no effect on the shooter. Jumping is strong, so it's a pickup, and a lighter cube jumps higher. Dash is on Shift, for example with three charges.

Fall off and you're out. Last cube standing wins.

**→ Ardıl:** "Ardıl, take us up into the sky."


### 6. Blast Parkour · **Ardıl** · ≈0:35

Blast Parkour: an obstacle course in the sky. Shoot, blast and climb.

> [cue] Point at each cube as you name it.

In the render, orange blasts the ground to jump, purple is charging a strong shot, and cyan has just been knocked into the void.

The islands are open, with no walls and no cover, so you're always in someone's line of fire. Hold the trigger for a strong explosion, but you can't move while you charge.

Drop below the course and you're out. First to the top flag wins, and everyone else is ranked by height.

*(optional) If hitting turns out to be too easy, shots fall with gravity, so you have to read the arc. We decide that in playtests.*

**→ Kürşat:** "From the sky to the ice. Kürşat."


### 7. Ice Loop · **Kürşat** · ≈0:35

Ice Loop. Most checkpoints before the timer ends wins, and the ice does not help.

The track loops. Every checkpoint you pass counts plus one, so the count decides, not the lap.

Push a rival off the ice into the water, and they restart from their last checkpoint.

And here the shot changes meaning again: fire forward while you slide, and the recoil pushes you back. Your shot becomes your steering.

*(optional) Ice is the theme of this mini-game only. The track on the right is a top-down draft; its length and checkpoint count aren't set yet.*

**→ Ezel:** "Ezel?"


### 8. Hole Grab · **Ezel** · ≈0:35

Hole Grab. Musical chairs, with holes. If you don't get a hole, the bulldozer gets you.

Holes open in the ground in the player's shape, with the barrel's angle marked, so you have to line up to drop in.

> [cue] Point down the three rounds on the right.

There's always one hole fewer than players. With four of us: three holes, then two, then one. Whoever is inside the last hole wins.

Shots push rivals away from the holes, but once you're inside, nobody can push you out.


### 9. More mini-games & ideas · **Ezel** · ≈0:30

Same cube, same shot, new rules. The playlist is built to grow.

Battle is next in line: a straight shoot-out where the shot does damage. In The Monster, one player is the monster for a round and everyone else plays against them, and the role moves on so everyone gets a turn.

*(optional) On the right are two concept ideas that aren't in the plan yet. Character types: knockback depends on mass, so weight is the natural first difference. And a rage meter that builds up across mini-games and can be spent in a later one.*

**→ Kürşat:** "Kürşat, how do players make the cube their own?"


### 10. Character customisation · **Kürşat** · ≈0:30

Make the cube yours. Cosmetics change the look, never the game.

There are four slots: head, face, ears and extras. You browse the shop and try things on in the lobby before a session.

Nothing in the shop changes mass, speed or the shot. Cosmetics never cover the barrel or the eyes, so you can always read where someone is aiming.

And the crown is not for sale. Only the session leader wears it.

**→ Abdurrahim:** "Abdurrahim, how do we actually build all this?"


---

## 02 · Gameplay & tech

### 11. Technical design · **Abdurrahim** · ≈0:40

> [cue] Read the stack from the bottom up and move your hand up with it.

I'll read this from the bottom up. The engine is Unity in 3D, with its built-in physics. On the network, one player hosts and the others join. We plan to use Unity's own networking tool, and online servers come later.

On top is the shared core every mini-game reuses: moving the cube, the camera, the shot, the playlist, the scoreboard, and the menus and shop.

> [cue] Tap the yellow bar.

And this yellow line is the key. Every mini-game follows the same three steps: set up with the players, play, and report who finished where. The scoreboard only needs that finishing order, so a new mini-game just follows those three steps.

*(optional) That's what lets four people build four mini-games at the same time on one core.*


### 12. How each shot is built in Unity · **Abdurrahim** · ≈0:45

Every shot is a real ball with physics, and we reuse the same set of balls so firing stays smooth. When a ball hits someone, it asks the current mini-game what a hit should do here. The shot is the same everywhere; only that answer changes.

> [cue] Move your hand down the rows.

In Knockback Arena, a hit is an instant kick that also makes the target lighter while its size stays, so the same kick sends it further. Fall into the zone under the arena and you're out.

In Blast Parkour, the shot explodes where it lands and pushes away everyone close to that spot, you too if you're close. That's how a shot at the ground launches you up, and holding longer makes a bigger blast.

In Ice Loop, the floor has almost no grip and every shot kicks you backwards, so you steer by shooting. In Hole Grab, a hole belongs to whoever gets in first, and the shot is just a gentle push.

*(optional) The right column lists the Unity tools behind each of these.*

**→ Ezel:** "That's one machine. Over the network: Ezel."


### 13. One shot over the network · **Ezel** · ≈0:45

Our rule is simple: the host decides, and every screen shows the same result. Physics can come out slightly differently on different computers, and we can't have two players disagreeing about who fell.

> [cue] Walk the four boxes left to right.

So when you shoot, your computer tells the host "I fired, aiming here." The host creates the shot, runs the physics and applies this game's effect. Then it sends everyone the same update, and each screen smooths it out so movement looks fluid.

Your own cube reacts instantly on your screen, so it feels responsive, but anything that decides a result happens on the host.

Our main risk is exactly this: keeping the physics smooth for everyone. So we tune how often the host sends updates, and we test online from Demo 1, not at the end.

**→ Ardıl:** "Who plays this, and is there a market for it? Ardıl."


---

## 03 · Market

### 14. Market potential & audience · **Ardıl** · ≈0:35

Who is it for? Groups of friends who want short, chaotic rounds together, online or in the same room, and the streamers and viewers who enjoy watching party-game chaos.

And the market is big. The global games market was about 197 billion dollars in 2025. PC games made 43.6 billion, up 12 percent. 70 percent of US players aged 8 and up play games with other people. And co-op games on Steam made an estimated 4.1 billion in the first half of 2025 alone, a record.

*(optional) The sources are on the last slide.*


### 15. Party games keep selling · **Ardıl** · ≈0:40

And party games keep selling for years. Super Mario Party sold over 21 million. Jamboree sold 5.6 million in 11 weeks, the series' fastest seller.

Small studios can win too. Stick Fight, a small studio's physics shooter, is estimated at around 4.75 million. Pummel Party is estimated at 2 to 5 million owners. Party Animals peaked at 135,834 players in its free playtest.

*(optional) Feather Party: 87 percent positive across 551 reviews.*

> [cue] Point at the yellow box.

Here's the risk, stated plainly: Steam is crowded. An estimated 65.9 percent of 2025 releases earned under a thousand dollars. So a party game needs a one-sentence hook. Ours is one shot that means something new in every mini-game.


### 16. Five competitors compared · **Ardıl** · ≈0:40

> [cue] Rubric: "at least 4 games compared on visuals, theme, mechanic and financials". Point at the four column headers as you name them.

Five competitors, compared on four things: visual style, theme, mechanic, and financials.

Party Animals: soft, furry animals in physics brawls. Gang Beasts: wobbly ragdolls on dangerous city stages. Fall Guys: candy-coloured jellybeans in a TV game-show course, and free-to-play. Pummel Party: a low-poly board game with mini-games, at 14.99. Feather Party: colourful chicks, a board mode plus 22 mini-games, at 8.99.

And our row: glossy toy cubes with a barrel, bright party islands, one shot with a new effect per mini-game, online multiplayer. It's a student project and a free build, and a store price is a later decision.

*(optional) The owner numbers here are SteamDB estimates, not sales.*


### 17. Where we sit among them · **Ardıl** · ≈0:25

> [cue] On this slide the column headers sit a little left of their dots, so "Partly" looks like it belongs to Pummel Party. It's Fall Guys. Point at the dots, not the headers, and say the names out loud.

Everyone has 3D characters and cosmetics. Pummel Party and Feather Party also combine several mini-games into one result, and Fall Guys partly does, because its playlist eliminates players instead of scoring each round.

But look at the last row: it's empty for all five. None of them keeps one shot with a new effect per mini-game. That's our gap. Our column says 'planned', because this is a proposal.

**→ Kürşat:** "That gap is our innovation. Kürşat."


---

## 04 · Innovation (25 pts, the biggest block)

### 18. Innovation & creativity · **Kürşat** · ≈0:45

> [cue] This block is worth 25 points, the most of any criterion. Slow down and look at the audience, not the screen.

This is the heart of our pitch.

> [cue] Pause.

The big idea is one trigger, many effects. The same button, the same barrel and the same aim, in every mini-game. Only what the shot does changes.

> [cue] Point at each of the four pictures as you say its verb.

In Knockback Arena it pushes. In Blast Parkour it lifts you. In Ice Loop it steers you with recoil. In Hole Grab it clears the way to a hole.

So you learn the controls once, and every mini-game still feels new.


### 19. Three ideas that back it up · **Kürşat** · ≈0:35

Three ideas back it up.

One: mini-game variety. Different maps and themes, like the arena, sky islands, the ice track and the holes, all on the same mechanic. Every round feels new, but your skill carries over.

Two: the comeback is built in. Plus one for last place, ties go to the underdog, and the leader wears a crown everyone aims at.

Three: it's your session. Random or custom playlists, repeats and exclusions, and an end by game count or by target score.


### 20. How we differ · **Kürşat** · ≈0:30

Here are three differences from the closest games in the genre.

Pummel Party and Feather Party give each mini-game its own controls and goal. In Shooter Party, one shot stays the shared action in every mini-game.

Many racing and party games help the player who's behind with random items, so catching up comes down to luck. Ours is a fixed rule everyone can see: plus one point and the tie-break.

*(optional) And many party games let you pick mini-games. We add an end by score or count, and favourites that can repeat.*


### 21. One character, one shot (statement slide) · **Kürşat** · ≈0:20

> [cue] Dark slide. Say it slowly and stop after each sentence. Don't add anything.

Most party games are a playlist of unrelated mini-games.

Shooter Party keeps one character and one shot across the whole session, and lets each mini-game bend what that shot means.

So players carry their skill from round to round.


---

## 05 · Drafts & prototype

### 22. Logo drafts · **Kürşat** · ≈0:20

Now, what we've already made. Here are three logo directions for one identity: A, cube letters, for the title screen and the video end card; B, stacked, for the store capsule and the lobby; C, the icon, for the taskbar and the build icon.

*(optional) The tile colours match the character palette.*

We'll pick one after your feedback.


### 23. Art direction & 3D blockouts · **Kürşat** · ≈0:25

Our art direction is glossy toy: rounded cubes in bright colours, a barrel on every character, and party settings like grass, sky and water. Ice only shows up in Ice Loop.

Every mini-game was blocked out in 3D first, to check the camera angle, the scale and whether you can read the shots, before we build it in Unity.

**→ Abdurrahim:** "And Abdurrahim has already tested the core rule."


### 24. Prototype & level drafts · **Abdurrahim** · ≈0:40

Our early prototype asked one question: does knockback read on screen?

> [cue] Point along Before, Shot 1, Shot 2, Shot 3.

We built a small physics test of the knockback rule in Three.js. Before, shot one, shot two, shot three. The target flies further each time, and you can see the rule without any UI.

It's the reference for the first Unity test of Knockback Arena: same scene, same rule.

*(optional) Below are level drafts for all four mini-games. Sizes, spawn points and checkpoint counts aren't set yet.*


---

## 06 · Development plan

### 25. Development plan · **Abdurrahim** · ≈0:35

Twelve weeks, four presentations, and one playable step for each.

Today is the proposal. Demo 1 on 16 November: the core prototype, host and join, and the first mini-games playable. Demo 2 on 14 December: all four mini-games and a full session with scoreboard and menus. The final on 11 January: UI, sound, cosmetics, polish and the promo video.

Each of us builds one mini-game and its shot, and owns one shared system. That's the table.

*(optional) Battle is next in line once the four are fun.*

**→ Ardıl:** "Ardıl, week by week."


### 26. Gantt chart: 12 weeks · **Ardıl** · ≈0:30

Each column is one week, and week 1 starts today. The bars are coloured by owner, the same colours as on our cover.

Weeks 1 to 4: the core prototype and online host-and-join come first, along with menus and UI drafts. Everyone designs their mini-game, then blocks it out, ending with the Demo 1 build.

Weeks 5 to 8: the scoreboard and comeback, networked shots, finishing each mini-game with its assets, then the first full-session playtest for Demo 2.

Weeks 9 to 12: the cosmetics shop, sound, balance and polish, Battle as a stretch goal, online tests, the promo video and the final hand-in.

*(optional) The full 26-task breakdown is in the written pitch.*


### 27. Risks and how we handle them · **Ardıl** · ≈0:35

We see three risks, and each one has a plan.

Networked physics: players could disagree on a push or a fall. Plan: the host simulates, and clients only render and interpolate.

Balance: hits that are too easy, jumping that's too strong, a runaway leader. Plan: fire-rate limits, jump as a power-up, the comeback rules, and lots of playtests.

Scope: many mini-games for four people. Plan: one shared core first, small mini-games, and extras only after the core game works.

**→ Ezel:** "Ezel, bring it home."


---

## Close

### 28. Shooter Party in short · **Ezel** · ≈0:35

Shooter Party, in short.

First: one shooting cube, and every mini-game changes what a shot does.

Second: a comeback built in. Plus one for last place, ties to the underdog, and a crown on the leader.

Third: multiplayer sessions you shape, with your playlist, your end rule and your look.

> [cue] All four step forward together.

One cube. One shot. A new meaning every round. Thank you, and we'd love your questions.

*(optional) The sources for every figure are listed here and in the written pitch.*


---

## Before you go up

- Stand in first-speaking order: Ezel, Abdurrahim, Ardıl, Kürşat. The relay on slides 4 to 8 then runs left to right and comes back to Ezel.
- Whoever is speaking holds the clicker. Handing it over at a handoff line makes the transition visible.
- Don't read the slide. Use this script to rehearse, then glance at the slide and speak to the room.
- Grey lines are optional. If the teacher signals time, drop them, and the script still flows.
- Say numbers exactly as the slide shows them. If you blank, say "about" and the rounded number. Never make one up.
- "Concept render", "proposal" and "decided in playtests" are strengths. They show you know what's done and what isn't.
- In Q&A, the owner of the topic answers (see the list). If you don't know, say what we'd test and when.
- The submitted PPTX is 28 full-slide images with your speaker notes attached. There are no animations, so one click is one slide.

## Q&A prep

**Does it have to be a cube?** (Abdurrahim)
No. It doesn't have to be a cube, but we keep the proportions close to 1:1:1 and give every character a visible barrel, so everyone can read where a player is aiming.

**Why real projectiles instead of instant hits?** (Abdurrahim)
All our effects are physical: knockback, mass loss, blast and recoil depend on where and from which direction the shot lands. A real projectile also gives the others something they can see coming. If hitting is too easy, we can make shots fall with gravity, as on the Blast Parkour slide.

**Doesn't mass loss snowball? Whoever gets hit first just loses.** (Abdurrahim)
That's the balance risk on slide 27. We have three levers: a fire-rate limit, jump as a pickup, and the comeback rules across the whole session. We'll set the exact values in playtests rather than guess numbers today.

**Is the prototype in Unity?** (Abdurrahim)
Not yet. The knockback test is a small Three.js physics test. Its job was to check that the rule reads on screen, and the first Unity test of Knockback Arena reproduces the same scene and rule.

**Online or local network for the demos?** (Ezel)
One of us hosts and the others join as clients, which is what Demo 1 shows. Online servers are a later step. We start testing the network from Demo 1, and the online tests are in weeks 11 and 12.

**What about lag and players seeing different results?** (Ezel)
The host is the authority. It runs the physics and decides every push, fall and score, and the clients only render and interpolate the snapshot. Your own movement is predicted locally so it feels instant. We tune the snapshot rate and test early.

**Why Unity Netcode?** (Ezel)
It's Unity's own networking package, and NetworkTransform and NetworkRigidbody fit our model where the host runs the physics. It's still a candidate: researching it is my job in weeks 1 to 3, and if it doesn't hold up we'll report that at Demo 1.

**What happens if the host leaves?** (Ezel)
We haven't designed that yet, and moving the host to another player isn't in the plan. We'd rather say that than promise it.

**How many players?** (Ezel)
We design and test for four. The rules already scale: placement points and the number of holes grow with the player count. We haven't fixed a maximum yet.

**Won't +1 for last place feel unfair to the leader?** (Ezel)
It's small next to 4-3-2-1, and everyone can see it, so it's a nudge and not a reset. The values are a proposal, and the full-session playtest in week 8 is where we tune them.

**How is this different from Pummel Party or Feather Party?** (Kürşat)
Both give each mini-game its own controls and goal. We keep one character and one shot for the whole session, and each mini-game changes what that shot does, so your skill carries over from round to round. Our comeback is also a fixed rule everyone can see, not random items.

**Are those images gameplay?** (Kürşat)
No. They're 3D concept renders and blockouts made for this pitch. We use them to check the camera, scale and how readable the shots are before building in Unity.

**Is four mini-games plus networking plus a shop too much for 12 weeks?** (Ardıl)
That's why the shared core comes first. Every mini-game plugs into one contract, each of us owns one small mini-game, and Battle and extras only come after the core game works. Demo 2 is our checkpoint for all four mini-games and a full session.

**How would it make money?** (Ardıl)
Right now it's a student project with a free build, and a store price is a later decision. Cosmetics never change gameplay, so they could never become pay-to-win.

## Rubric map

| Pts | Criterion | Slides | Make sure |
|---|---|---|---|
| 5 | Introduction without technical jargon | 1–2 | Ezel: no engine or network words on slides 1–2. |
| 10 | Mechanics & how they'll be built | 3–13 | On 12–13 the slide shows the Unity names. Point at them, but say what they do in plain words. |
| 10 | Market: 4+ games on visuals, theme, mechanic, financials | 14–17 | Ardıl: say the four column names out loud on 16. |
| 25 | Innovation & what sets it apart | 17–21 | Kürşat: the slowest, calmest delivery of the day. Pause on 21. |
| 10 | Development plan | 25–27 | Dates, owners, risks, each with a plan. |
| 15 | Drafts & prototype | 5–8, 22–24 | Say "already made" and "concept render"; invite logo feedback. |
| 15 | Presentation: time, command, energy | all | Clean handoffs, eyes on the room, finish on time. |
| 10 | Proposal document quality | PDF | Already submitted. |
