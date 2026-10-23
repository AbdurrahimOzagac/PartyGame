# Ice Rumble — two-mini-game visualization pack

## Start here

Use each mini-game separately in MiniMax H3 Image-to-Video:

**Ice Race**
- Image input: `01_ICE_RACE_reference.png` (only this image).
- Paste prompt: `ICE_RACE_MINIMAX_H3.txt`.
- Goal: continuous fast travel, momentum through one corner, one clean overtake; no falling.

**Mass Knockout**
- Image input: `02_MASS_KNOCKOUT_reference.png` (only this image).
- Paste prompt: `MASS_KNOCKOUT_MINIMAX_H3.txt`.
- Goal: the one existing orange mass chunk hits Cyan, causing a squash/shrink, knockback and clear fall.

## Reference status

The Ice Race anchor is one standalone generated scene. The Mass Knockout anchor was extracted into a standalone 16:9 frame from an earlier generated concept sheet because additional standalone image generations mistakenly produced more racing imagery. Do not misrepresent it as a new independent generation or a recorded game screenshot. Both anchors show concept visuals, not implemented code.

`03_SHARED_CHARACTERS_reference.png` and `04_MASS_THROW_sequence.png` are supporting assets for the deck and design implementation. **Do not** feed them together with the gameplay anchors into H3 unless testing a model workflow specifically designed for multiple reference images: multi-concept cues can cause mixed scenes.

## Reality check

Image-to-video models do not simulate authoritative game physics. These prompts reduce ambiguity, not guarantee correctness. If H3 changes player identity, world geometry or trajectory, build a 5–7s blockout in the engine (even with simple primitives) to drive reference-video generation. Do not claim a generated preview is actual gameplay.

See `VIDEO_TUTORIAL_ANALYSIS.md` for the six video takeaways, `MECHANICS_AND_IMPLEMENTATION.md` for proposed game rules, and `SLIDE_MAKER_HANDOFF.md` to hand the entire package to a presentation agent.
