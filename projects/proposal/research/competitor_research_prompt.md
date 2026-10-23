# Research prompt: competitors and novelty check for our party game

Copy everything below the line into your research agent.

---

You are a games-market research analyst. We are a 4-person university team pitching a game proposal to a computer-engineering game course. Accuracy matters more than coverage. **If you cannot verify something, say "not found" or mark it unverified. Never fill a gap with a guess.** Every number and every factual claim needs a source URL and a date.

## Our game, in brief (for comparison only, do not research it)

- PC, Unity, third-person 3D, **2–4 players over a local network (LAN)**: one player hosts, the others join. No online play, no internet needed.
- A session is a series of short mini-games. Placement in each round gives points, and points add up on a shared scoreboard.
- Characters are cute, squishy cubes.
- **Mini-game 1, Mass Knockout:** players are on a floating platform. To attack, a cube **spits out a piece of its own body** as a projectile, so **shooting makes you smaller**. A hit also **removes mass from the target**. A lighter cube gets **knocked back further** by the same hit, and falling off the platform is a knockout. Mass is health, ammunition and knockback resistance at once.
- **Mini-game 2, Ice Race:** a race on a slippery ice track floating in the air. Steering changes acceleration, not velocity, so momentum and drift through corners are the skill. If you fall off the edge, you respawn at a checkpoint.
- Possible extension: characters with different stat profiles, for example one starts heavier (more mass, harder to push, slower) and one is faster but lighter.

## Task 1: the closest comparable games (at least 4, ideally 6–8)

Choose the games that are **genuinely most similar** to ours in mechanic, format and audience. Justify each pick in one line. Candidates to consider (you are not limited to these): Fall Guys, Gang Beasts, Party Animals, Stick Fight: The Game, Pummel Party, Ultimate Chicken Horse, Super Smash Bros. Ultimate, Mario Party Jamboree, Duck Game, Boomerang Fu, Rubber Bandits, Stumble Guys, Brawlhalla, Splatoon 3, Agar.io, Human: Fall Flat, Knockout City, Bopl Battle, Spellbreak, Rocket League.

For each chosen game, report:

1. **Links:** official site and Steam, console store or app store page.
2. **Visuals:** 2–3 direct URLs to official screenshots or a press kit (Steam CDN or the publisher's press page), plus a one-line description of the art style.
3. **Theme / setting.**
4. **Core mechanic** in one or two sentences, including how you win and how you lose.
5. **Multiplayer:** player count; local same-screen yes/no; **LAN yes/no (state whether it is official)**; online yes/no; whether it needs an internet connection.
6. **Session structure:** single match, rounds, a mini-game playlist or a board, and whether eliminated players wait out the round.
7. **Financials:** release date, price model and current price, official sales or player counts (with date), revenue if reported, peak concurrent players (SteamDB or SteamCharts), and review score (Steam % and Metacritic). Label estimates (Raijin, VG Insights, Gamalytic) as estimates.
8. **What it does that we do not, and what we do that it does not.** Be concrete.

## Task 2: novelty check (most important)

For each claim below, actively search for **prior art**: shipped games, mods, game-jam games, academic papers. Answer each with **"exists"** (name the games), **"partly exists"** (explain what differs) or **"not found after searching"** (list what you searched). Do not soften the result.

1. **Ammunition is part of your body:** shooting or ejecting material makes your own character smaller or lighter. Check Agar.io (eject mass), Osmos, Tasty Planet, Squarena, the "Bounce" mod on ModDB, slime or blob games, and anything else you find.
2. **Mass is health:** taking hits reduces your size or mass instead of a health bar.
3. **Knockback increases as you lose mass:** compare with Smash Bros.' damage %. Is there any game where knockback is driven by physical mass loss rather than a % counter?
4. **Attacking costs the attacker the same resource that the target loses.** Are there games where your attack resource is also your survival resource (e.g., "health as ammo" designs)? Name them.
5. **All four combined in a 3D party game with a mini-game scoreboard.**
6. **Momentum-based ice racing as a party mini-game.** Which party games already include ice, slippery or drift races?
7. **Character stat archetypes** (heavy and slow vs light and fast) in party brawlers. This is common (Mario Kart weight classes, Smash weights). Confirm and list examples, so we pitch it as a standard framework rather than a novelty.
8. **LAN-first PC party games in 2024–2026:** how many recent party games ship with official LAN? Give examples and a Steam tag count if possible.

## Task 3: market potential (sourced figures only)

- Size and growth of the PC games market and of co-op / social / party games on PC (latest Newzoo, Alinea, or equivalent).
- Evidence for demand for local or in-person multiplayer (ESA Essential Facts 2026, Pew, or similar).
- Typical price range and commercial outcomes for indie party games on Steam, including the median and the risk.
- Note any figure that comes from a vendor report without a disclosed method.

## Output format

1. A summary table of the chosen games: name, link, art style, core mechanic, LAN / local / online, price, sales or players, peak CCU and review score. Every cell carries a source number.
2. One section per game, following the Task 1 fields.
3. The novelty verdicts from Task 2 as a table: claim | verdict | prior-art examples | sources.
4. Market figures from Task 3 as a table: stat | value | date | source.
5. A numbered source list with URLs and access dates.
6. A final "unverified / conflicting" list.
