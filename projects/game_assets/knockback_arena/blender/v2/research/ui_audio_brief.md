# Knockback Arena: UI/HUD and procedural SFX brief
Research only, 2026-10-24. Sizes are px at a 1920×1080 reference. "Ink" = #161A3A.

## 0. Verified facts that change the plan
Checked against the ugui 2.0.0 source inside the local editor (`/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.ugui`), the [TMP_FontAsset API](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/TMPro.TMP_FontAsset.html) and the Unity 6.3 docs.

1. **Fredoka is one variable TTF whose default instance is wght 300 (Light).** I read this from `fvar`; the [METADATA](https://github.com/google/fonts/tree/main/ofl/fredoka) agrees. TMP renders the default instance, so you would get Light. Get static files instead: `curl "https://fonts.googleapis.com/css2?family=Fredoka:wght@500;600;700"` (no User-Agent) returns one static `.ttf` URL per weight. I checked the 700 file: no `fvar`, 327 glyphs.
2. **Fredoka has no ğ, ş, İ, Ğ or Ş, and no arrows or stars.** I checked the cmap; ç, ö, ü and ı are present. Limit names to ASCII and use sprites for icons.
3. **TMP's shaders and `TMP Settings.asset` exist only in `Package Resources/TMP Essential Resources.unitypackage`.** `CreateFontAsset` calls `Shader.Find("TextMeshPro/Mobile/Distance Field")` and reads `TMP_Settings`, so import the essentials first. The project has no `Assets/TextMesh Pro` yet.
4. **Two wiring fixes.** `KnockbackArena.Runtime.asmdef` needs `UnityEngine.UI` and `Unity.TextMeshPro` added. The EventSystem needs `InputSystemUIInputModule`.

## A1. HUD patterns
**Safe margin:** 48 px. For a projector, keep text inside Xbox title-safe: 96 px left/right, 40 px top/bottom ([Xbox](https://devdocs.xbox.com/build/core-features/graphics/overviews/screen-areas)).

**Cards (U2): a top-centre strip of 4 cards, each 300×100 with 16 px gaps (1248 px), y −32.**
- **Why the top:** Smash's bottom strip suits side-on action ([SmashWiki](https://www.ssbwiki.com/Damage_Meter)). Over the shoulder, your own cube sits bottom-centre, and Mario Party-style corners spread the eye too far.
- **Layout:** fixed join order. The local card gets a 4 px white stroke and scale 1.06. Contents: portrait 80², name 28, mass 52, bar 160×12, score 36, and a 44 px crown rotated −12°.

**Mass — recommendation: a Smash-style % number, a thin bar with a drain trail, and a 24 px kettlebell icon.** The number is the read, the bar is the at-a-glance ratio, and the icon labels it without words.
- **Colour shows danger, not player:**
  - white at 70 % and above
  - amber #FFC233 from 40 to 70 %
  - pink #FF3D8B below 40 %, pulsing at 1.5 Hz

  Smash ramps white→yellow→red; red is skipped here because Tomato is a player colour.
- **Hit animation** (unscaled time):
  - **Punch:** 1→1.35 in 60 ms, then back over 180 ms ([easeOutBack](https://easings.net/#easeOutBack)).
  - **Shake:** x = 8·e^(−t/0.06)·sin(2π·30t) px, for 200 ms.
  - **Flash:** white for 60 ms.
  - **Drain trail:** the bar snaps; a white ghost bar holds 300 ms, then eases down over 250 ms.
  - **Delta:** "−8" floats up 40 px and fades over 600 ms. The number rolls to its new value over 250 ms.

**Crosshair and cooldown (U4):**
- **Crosshair:** a white 8 px dot with a 3 px ink outline, plus 4 rounded "bean" ticks (10×6) set 18 px out.
- **Cooldown ring:** 44 px Ø, 6 px stroke, radial fill from the top, clockwise, fill = elapsed/cooldown. The track sits at 25 % alpha.
- **When ready:** pop 1→1.2→1 in 120 ms, then hide; the ring shows only while cooling.
- **Hit confirm:** the ticks flash the target's colour and spread to 24 px for 100 ms.

**Dash and jump (bottom-right, 48 px in):**
- **Dash pips:** 3 pips of 26 Ø, 10 px apart. Full is white with an ink outline; spent is ink at 40 %. A recharging pip fills radially in the player colour, then punches 1.3→1 in 140 ms.
- **Jump slot (72²):** empty is a dashed outline at 35 %. Held, it bobs ±3 px at 1.2 Hz with a "SPACE" hint. On use it flies up 30 px and fades in 200 ms.

**Countdown (U5):**
- **Style:** Fredoka Bold 320 px, white face, ink outline 0.3, hard underlay.
- **Each 1 s number** (the beep fires at 0 ms):
  - 0–220 ms: scale 2.2→1 (easeOutBack), alpha 0→1, rotate −8°→0°
  - 220–800 ms: hold
  - 800–1000 ms: scale →0.6 and alpha →0 (easeInQuad)
- **GO!:** 380 px, chartreuse face. Scale 0→1.25→1 over 350 ms, hold 450 ms, then 1→1.8 with a fade over 250 ms. Unlock input on GO.
- **Clock:** drive it from the server's match time.

**OUT! (U6):**
- **Eliminated player:** a 1920×220 ribbon in their colour, tilted −4°, showing "OUT!" (200 px) and "4th" (56 px). It slides in from the left over 280 ms (easeOutBack), holds 1.4 s, exits right over 220 ms, then the spectator camera takes over. This follows Fall Guys' big centred word.
- **Everyone else:** a 640×72 toast at y −160: "[portrait] TEAL is OUT! · 2 left". Drop in 200 ms, hold 1.6 s, fade 200 ms. The KO jingle plays here.

**Results (U7):** split 60/40.
- **Left:** the real cubes on a 3D podium (2nd–1st–3rd, 0.7/1.0/0.5 m) through a results camera, with 4th squashed on the floor.
- **Right:** 640×110 rows with rank badge, portrait, name, "+N place", a "+1 COMEBACK" chip and a total that counts up over 600 ms. Reveal 4th→1st, 150 ms apart; the crown lands last, with the fanfare.

**Lobby (U8):** a 1400×800 panel.
- **Left:** HOST (chartreuse, ink text). JOIN has an IP field (360×72), a port field and a button. When hosting, show your LAN IP.
- **Right:** 4 slots of 220×320, each with a portrait, a name (ASCII, ≤12 characters) and a pill: EMPTY (dashed), JOINED ("Not ready", dimmed) or READY (✓ sprite plus text). Don't rely on colour alone.
- **Bottom:** a READY toggle, plus START (host only), enabled when at least 2 have joined and all are ready.

**Logo (U9):** "KNOCKBACK" over "ARENA" in Fredoka Bold, tilted −4°, with a white face, 12 px ink outline and a hard shadow. The "O" becomes a tomato cube. Build it in TMP.

## A2. Visual language
- **Panels:** ink at 92 % with a 3 px white stroke at 85 %. Measured contrast: white on ink 16.9:1, ink on ice #DDEFFA 14.3:1, but ink on sky #1F4FA8 only 2.2:1, hence the stroke.
- **Shape:** radius 24 for cards, 16 for buttons, pills for status. Add a hard "sticker" shadow (#0B0E24 at 60 %, offset (0, −6), no blur) and a top gloss (white, 0→12 %).
- **9-slice sprites:** white and tinted per Image: `round_fill_r24` (128², border 32), `round_stroke_r24_w3`, `gloss_top`.
- **Text over 3D:** a TMP outline of 0.2–0.3 in ink plus an underlay (0, −0.5) with dilate 0.2. The atlas needs a 90 pt sample size and padding 12 to fit this.
- **Weights:**
  - 700: countdown, GO, OUT!, mass, scores, logo
  - 600: names, buttons, headers
  - 500: body text, IP field
  - minimum size 24 px
- **Player colours are accents only:** portrait background, an 8 px card band, the ring fill, the ready stroke.
  - **On ink:** Tomato 5.2, Teal 6.4, Chartreuse 13.3, Violet 2.5. Use #B58CFF (6.6) for Violet on ink.
  - **In the scene:** Chartreuse on ice is 1.1 and Violet on sky is 1.2, so the world ring needs a 3 px ink keyline plus a 1 px white rim.
  - **Text on colour fills:** white on Chartreuse is 1.3, so use ink text on Chartreuse and Teal, and white on Tomato and Violet.

## A3. Unity 6 implementation (verified in source and docs)
**`TMP_FontAsset.CreateFontAsset` public overloads:**
- `(string familyName, string styleName, int pointSize = 90)`: OS fonts, DynamicOS
- `(string fontFilePath, int faceIndex, int samplingPointSize, int atlasPadding, GlyphRenderMode renderMode, int atlasWidth, int atlasHeight)`: Dynamic only
- `(Font font)`: 90 pt, padding 9, SDFAA, 1024²
- `(Font font, int samplingPointSize, int atlasPadding, GlyphRenderMode renderMode, int atlasWidth, int atlasHeight, AtlasPopulationMode atlasPopulationMode = Dynamic, bool enableMultiAtlasSupport = true)`

The enum is `AtlasPopulationMode {Static, Dynamic, DynamicOS}`.

**Gotchas from the source:**
- **Sub-assets:** the atlas and material are separate in-memory objects. The editor menu (`TMP_FontAsset_CreationMenu.cs`) calls `CreateAsset`, then `AddObjectToAsset` on each.
- **Order:** `TryAddCharacters` refuses Static assets, and `sourceFontFile` is set only in Dynamic mode. Create Dynamic, add characters, then switch to Static.
- **Keywords:** the runtime path uses the Mobile SDF shader, which needs `OUTLINE_ON`; underlay needs `UNDERLAY_ON`. Save presets as material assets so the `shader_feature` variants ship.

```csharp
var fa = TMP_FontAsset.CreateFontAsset(font, 90, 12, GlyphRenderMode.SDFAA, 1024, 1024,
                                       AtlasPopulationMode.Dynamic, false);
fa.name = "Fredoka-Bold SDF";
AssetDatabase.CreateAsset(fa, path);
fa.atlasTextures[0].name = fa.name + " Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
fa.material.name = fa.name + " Material";      AssetDatabase.AddObjectToAsset(fa.material, fa);
bool ok = fa.TryAddCharacters(ascii32to126 + "çöüıÇÖÜ", out var missing); // false -> 2048 atlas
fa.atlasPopulationMode = AtlasPopulationMode.Static;
EditorUtility.SetDirty(fa); AssetDatabase.SaveAssets();
```

**Static vs Dynamic.** Static bakes a deterministic atlas and needs no TTF at runtime. Dynamic rasterises on demand, and TMP Settings ships `m_ClearDynamicDataOnBuild: 1`, so dynamic atlases are emptied at build. Use Static. I have not checked whether rasterising works under `-nographics`, so leave that flag off.

**Essential Resources.**
- **API:** `TMPro.TMP_PackageResourceImporter.ImportResources(bool importEssentials, bool importExamples, bool interactive)` (editor-only), which wraps `AssetDatabase.ImportPackage`.
- **Menu:** Window ▸ TextMeshPro ▸ Import TMP Essential Resources.
- **Batch mode:** `-quit` exits before `ImportPackage` runs ([forum](https://discussions.unity.com/t/import-package-not-running-when-in-batchmode/1544813)). Use the CLI flag instead, which shows no dialog ([CLI](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)): `Unity -batchmode -projectPath . -importPackage "<…>/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage" -quit`.
- **Then:** commit `Assets/TextMesh Pro/`.

**Shader under URP.** "TextMeshPro/Distance Field" has no LightMode or RenderPipeline tag, uses the Transparent queue and `ZTest [unity_GUIZTestMode]`, and works on both overlay and world-space canvases. The URP Lit/Unlit shadergraphs are only for lit 3D text.

**9-slice import:**
```csharp
var ti = (TextureImporter)AssetImporter.GetAtPath(p);
ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
ti.spritePixelsPerUnit = 100; ti.spriteBorder = new Vector4(32, 32, 32, 32); // L,B,R,T
ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp;
ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
```
The border order is from the [docs](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/TextureImporter-spriteBorder.html). On-screen corner size = border × (canvas PPU / sprite PPU) ÷ `pixelsPerUnitMultiplier`.

**Image types.** `Image.Type` is {Simple, Sliced, Tiled, Filled}. For the ring, use `Filled` with `fillMethod = Radial360`, `fillOrigin = (int)Image.Origin360.Top`, `fillClockwise = true`, and drive `fillAmount`, on an annulus sprite. The drain trail is two stacked Images.

**CanvasScaler.** `ScaleWithScreenSize`, 1920×1080, `MatchWidthOrHeight`, match = 1 (height). This keeps text size stable on 16:10 and 21:9; the layout must also fit 1728 px wide. Set these explicitly: the defaults are 800×600 and match 0.

**Mass ring (U3).** Use one world-space canvas per cube: a 128² rect at scale 0.008, about 1 m above it. Unlike an overlay plus `WorldToScreenPoint`, it needs no projection code, is depth-correct and rebuilds only its own canvas. Hide your own ring.

**Billboarding.** In `LateUpdate`, set `transform.rotation = cam.transform.rotation`; `LookAt` skews rings near the screen edges. Optionally scale by `clamp(dist/8, 1, 2.5)`.

## B. Procedural SFX (numpy, 44.1 kHz, mono, 16-bit)
The system `python3` has no numpy, so use a venv. Write WAVs with stdlib `wave`.

**Building blocks:**
- **Glide:** `ph = 2π·cumsum(f)/sr`, with `f = f0·(f1/f0)**(t/T)`.
- **Band-limited saw:** `Σ sin(k·ph)/k` for k ≤ 18000/f_max.
- **Envelope:** a linear attack, then `exp(−t/τ)`.
- **One-pole low-pass:** `y += a(x−y)`, a = 1−e^(−2πfc/sr). High-pass = x − LP.
- **Pink and brown noise:** `irfft(rfft(white)/√f)` for pink, or `/f` for brown.
- **Static band-pass:** the [RBJ](https://www.w3.org/TR/audio-eq-cookbook) 0 dB-peak BPF. α = sin w0/(2Q), b = [α, 0, −α], a = [1+α, −2cos w0, 1−α].
- **Swept SVF** ([Simper/Cytomic](https://ccrma.stanford.edu/~jatin/chowdsp/chowdsp_utils/classchowdsp_1_1StateVariableFilter.html)):
  - Coefficients: g = tan(πfc/sr), k = 1/Q, a1 = 1/(1+g(g+k)), a2 = g·a1, a3 = g·a2.
  - Per sample: v3 = x−ic2, v1 = a1·ic1 + a2·v3, v2 = ic2 + a2·ic1 + a3·v3, ic1 = 2v1−ic1, ic2 = 2v2−ic2.
  - Outputs: LP = v2, BP = v1.
- **Bubble:** `sin(2π∫f)·e^(−βt)`, with f = f0(1+ξβt) and β = πf0δ. ξ ≈ 0.1 is realistic ([UNC/van den Doel](https://techreports.cs.unc.edu/papers/09-012.pdf)); 0.5–2 is cartoony.
- **sfxr reference points** ([jsfxr](https://github.com/chr15m/jsfxr)): Hz ≈ 3528·(p²+0.001). blipSelect is a square wave at 150–1270 Hz with a high-pass; hitHurt uses a falling ramp.

| SFX | Recipe | Length |
|---|---|---|
| SFX1 pop ×3 | A bubble at f0 = 520 Hz rising to about 1100 Hz in 60 ms (τ 35 ms; FM ratio 2, index 1.5·e^(−t/15 ms)). Add a 3 ms noise burst through a 3.5 kHz BPF at −14 dB, and noise swept 1.8 k→600 Hz (SVF-BP, Q 3) at −16 dB. Variants: 0, +2, −2 semitones. | 120 ms |
| SFX2 thwack ×3 | **Thump:** a sine dropping 180→65 Hz over 80 ms, τ 60 ms, through `tanh(2x)`. **Splat:** noise through an SVF-LP sweeping 4 k→400 Hz over 120 ms, Q 2, τ 50 ms, at −3 dB. **Wobble:** a 230 Hz sine with ±25 % vibrato at 18 Hz, τ 120 ms, at −8 dB. **Plus** 3–5 bubbles (ξ 1) at −14 dB. Variants: ±2 semitones, cutoff ×0.85/1/1.15. | 250 ms |
| SFX3 whoosh | Pink noise through an SVF-BP at Q 1.5, with the centre moving 300→2500→600 Hz in step with the amplitude. Amplitude: a sin² swell over 120 ms, then τ 120 ms. Add a band at 2× the centre, at −9 dB. | 400 ms |
| SFX4a wheee | **Pitch:** a saw rising 520→700 Hz over 150 ms, then falling to 240 Hz over 900 ms, with 6 Hz vibrato growing to 3 %. **Formants:** 3 SVF-BPs gliding /u/→/i/ over 120 ms, (375, 1090, 2800)→(370, 3200, 3730) Hz, Q 5/12/15, gains 0/−6/−12 dB. These are from [adult](https://www.cs.princeton.edu/courses/archive/spr99/cs325/formants.html) /u/ ×1.25 and [child](https://ccrma.stanford.edu/~kglee/m220c/formant.html) /i/. **Distance:** over the last 600 ms, low-pass 8 k→1.5 kHz and fade by −18 dB. | 1.2 s |
| SFX4b splash | A brown-noise burst through a 1.6 kHz low-pass, τ 180 ms. Add 10–15 bubbles (400–1500 Hz, ξ 0.1, δ 0.02) spread over 300 ms. Low-pass the mix at 2 kHz and add a 90 ms echo at 0.3. Play about 0.6 s after the fall. | 700 ms |
| SFX5 landing | A sine dropping 120→55 Hz (τ 50 ms). Add noise through an SVF-BP sweeping 900→300 Hz, Q 4, τ 70 ms, and a suction bubble rising 300→600 Hz at +60 ms, at −10 dB. Scale the volume by impact speed. | 200 ms |
| SFX6 ice ×4 | 6–14 noise grains of 1–3 ms within 30 ms, with density ∝ e^(−t/15 ms). Each grain is high-passed at 2.2–3 kHz with τ 0.8 ms. Add 1–2 "tinks" (3.2–5.5 kHz, τ 8 ms, −16 dB) and a 150 Hz thump (−12 dB). Variants: 4 seeds and 4 high-pass values. | 90 ms |
| SFX7 beep | An 880 Hz sine plus 0.3×1760 Hz plus 0.12×2640 Hz. Attack 3 ms, hold 140 ms, release τ 40 ms. One file, played three times. | 200 ms |
| SFX7 GO | An A-major chord, 440/554.37/659.25/880 Hz: saws detuned ±6 cents, scooping up 1 semitone in 40 ms. A low-pass opens 800→4000 Hz over 60 ms, then settles at 2.5 kHz. Hold 450 ms, release 250 ms. | 750 ms |
| SFX8 KO | A light sad trombone: Bb4 466.16, A4 440, Ab4 415.30 Hz (180 ms each), then G4 392 Hz for 600 ms with ±40 cent vibrato at 5.5 Hz. Each note is a saw through a low-pass "wah" envelope, 600→2400→900 Hz. | 1.2 s |
| SFX8 fanfare | **Marimba** (start ms, note): 0 G4 392 · 110 C5 523.25 · 220 E5 659.25 (100 ms each) · 330 G5 783.99 (240) · 600 E5 (120) · 740 G5 (160) · 920 C6 1046.5 (900). **At 920 ms:** a pad of saws on C4/E4/G4/C5 through a 2.5 kHz low-pass, plus FM sparkle taps on C7 2093 Hz (ratio 3.5, index 2) at 920, 960 and 1000 ms. Fade the last 300 ms. | 2.1 s |
| SFX9 UI | **Hover:** a sine rising 1800→2200 Hz over 25 ms, τ 8 ms. **Click:** modes at 700 Hz (τ 25 ms) and 2800 Hz (τ 8 ms), plus a 1 ms noise tick high-passed at 4 kHz. **Back:** the click down 4 semitones. | 30/60 ms |

**Marimba:** partials ×1/×4/×10 at amplitudes 1/0.35/0.12, with τ 350/90/30 ms, plus a 2 ms noise mallet. 1:4:10 is the design target; [CCRMA](https://ccrma.stanford.edu/CCRMA/Courses/150/percussion.html) measures about 4.0 and 9.2.

**FM bell:** Chowning's 200:280 ratio with index 10·e^(−t/τ) ([UW](https://courses.cs.washington.edu/courses/cse466/10wi/pdfs/FMSynthesisExamples.pdf)). For stingers, use I0 = 6, τI = 0.25 s and amplitude τ = 0.6 s.

**Variants:** fixed seeds; pitch offsets of 0, ±1.5 or +3 semitones; envelope times ×0.92–1.08; layer starts ±3 ms. At runtime, `AudioSource.pitch` is a random 0.96–1.04.

**Avoiding clicks:** start sines at phase 0, fade noise in over 1 ms, use a raised-cosine fade-out (5 ms for short sounds, 20 ms for jingles), high-pass at 25 Hz to remove DC, and end with 10 ms of silence.

**Loudness.** The console mix target is −24 LUFS ([ASWG-R001](https://designingsound.org/2012/07/30/video-games-and-loudness-standards-interview-with-sonys-garry-taylor/)). Bake levels into the files: normalise the RMS of the part above −40 dB to the values below, then cap peaks at −1 dBFS.

| Category | RMS (dBFS) |
|---|---|
| Thwack, KO | −12 |
| Beeps, GO, fanfare | −13 |
| Pop | −15 |
| Wheee, whoosh, landing | −17 |
| Click | −20 |
| Splash | −22 |
| Footsteps | −24 |
| Hover | −28 |

**Unity import.**
- **API:** `forceToMono` and `loadInBackground` are on `AudioImporter`. `loadType`, `compressionFormat` and `preloadAudioData` are on [`AudioImporterSampleSettings`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioImporterSampleSettings.html).
- **Formats:** the [manual](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AudioClip.html) recommends ADPCM for footsteps and impacts, and PCM for short SFX.

| Group | Load type | Format | Mono | Preload |
|---|---|---|---|---|
| Pop, beeps, GO, UI | Decompress On Load | PCM | on | on |
| Thwack, steps, landing, whoosh, splash | Decompress On Load | ADPCM | on | on |
| Wheee, KO, fanfare | Decompress On Load | Vorbis 0.7 | on | on |
| Music | Streaming | Vorbis | — | off, Load In Background on |

**Remaining defaults:** for SFX, Load In Background stays off and the sample rate stays on Preserve.

**Spatial blend:** 1 for world SFX; 0 for UI, countdown and jingles.
