# Ice Arena — art + tech brief (Knockback Arena, Unity 6000.3.9f1 / URP 17.3)

**Where the numbers come from:**
- **Colours:** sampled from `refs/ko_spit_full.png` and `refs/podium_full.png`.
- **Gameplay:** from the repo scripts.
  - Critter: Rigidbody + **BoxCollider**, run 3.5 m/s, jump 5.5 m/s.
  - Blob: 12 m/s plus 0.8 m/s up, gravity 3 m/s², flat aim.
  - Knockback: ≈ 1.2 m slide per hit.
- **Contrast:** CR = WCAG luminance contrast; L* and ΔE are CIELAB.

**TL;DR**
- **Floor:** pale azure ice **#B8D3EB**, L* 80–86, hue 205–212°, saturation ≤ 0.22.
- **White means edge.** No snow on the play floor.
- **Material:** start on **URP Lit with a baked gradient** (≈ 85–90% of the look). `KA/Ice` (compile-verified) adds blue shadows, twinkle and inner depth.
- **Arena:** **16 × 16 m** (4×4 tiles), with **one floor BoxCollider**.
- **Island and sky:**
  - Underside depth 0.6–0.8× the arena width.
  - Fog from 30 m in **#4A92E2**.
  - Sky is a numpy panorama imported as a Cube.

## 1. Stylized ice

**Palette** (on-screen targets: colour-pick a tonemapped screenshot):

| Role | Hex | L* |
|---|---|---|
| Top, sunlit (ref #B1CFE7) | **#B8D3EB** | 83 |
| Top, in shadow (ref #4077B4) | **#4F86C2** | 55 |
| Seam groove (never white) | #8FB0D4 | 71 |
| Frost/snow lip (edge, podium, background only) | **#EAF2FA** | 95 |
| Side: under lip → mid (ref #358ABC) → bottom | #7FB6DD → **#3B8CC2** → #1F62AE | 72 / 56 / 41 |
| Underside → tip | **#123E86** → #0C2E6E | 28 / 21 |
| Icicle body → tip | #A9D6F2 → #E6F4FF | 83 / 96 |
| Background crystals (facets #D9F0FF) | #7CC6EE | 77 |
| Sky zenith / mid (ref #0256B9) | #0B4CB0 / #0F5CC4 | 35 / 41 |
| Horizon haze 0–10° (= fog colour) | **#4A92E2** | 59 |
| Void below horizon → nadir | #0A47A0 → #062E78 | 32 / 21 |

- **Starting URP Lit albedo:** top #9FB6CB, frost #CAD1D8, side #3278A7. This assumes a sun of 1.2 at 60° and ambient ≈ 0.35.
- **Tonemapping:** Neutral. ACES shifts saturated hues.

**Shipped games:**
- I found no public breakdown of the ice in Mario Odyssey, BotW/TotK, Pikmin 4, Fall Guys, Overcooked or Astro Bot.
  - Searched: GDC Vault, 80.lv, NERD and the PlayStation Blog.
  - The Astro Bot talk covers water and clouds only ([GDC](https://gdcvault.com/play/1026072/Taming-Technologies-Behind-ASTRO-BOT)).
  - So don't cite those games as technique sources.
- Documented parallels:
  - **Splatoon:** "grey-toned neutral" stages that "could be vandalized with colors" ([Nintendo Everything](https://nintendoeverything.com/?p=361139)).
  - **Fall Guys:** "fairly primitive" levels because "the chaos would come from the other players" ([Creative Review](https://www.creativereview.co.uk/behind-the-scenes-fall-guys-game/)).

**Techniques with public sources:**
- **Fresnel rim:** `pow(1−N·V, 2.5–5)` ([Godot port of G. Aguiar](https://godotshaders.com/shader/spatial-ice-shader/)).
  - Keep strength ≤ 0.35. With the 14° camera pitch the far floor has N·V ≈ 0.16, so a stronger rim turns it white.
- **Inner layer by parallax:** layered BumpOffset ([80.lv](https://80.lv/articles/developing-artistic-ice-in-unreal-engine-4/)), or 24 fading iterations ([80.lv](https://80.lv/articles/how-to-build-cracked-ice-in-material-editor/)).
  - The single-tap version shifts the under texture by view ray × depth ([Godot](https://godotshaders.com/shader/parallax-ice-for-fake-depth-to-plane-meshes/)). One tap is enough on a flat floor.
- **Cracks:** surface lines, or inner extruded planes ([80.lv](https://80.lv/articles/check-out-this-simple-trick-for-deep-ice-cracks-in-3d)).
- **Translucency:** the Frostbite GDC 2011 back-light term ([Frostbite](https://frostbite.com/frostbite/news/approximating-translucency-for-a-fast-cheap-and-convincing-subsurface-scattering-look), [Zucconi](https://www.alanzucconi.com/2017/08/30/fast-subsurface-scattering-1/)).
  - It only pays off on back-lit icicles, crystals and the podium. Skip it on the floor.
- **Sparkle:** two noise taps, one shifted by view direction, then a threshold. Compare the Minions Art foil shader ([Patreon](https://www.patreon.com/posts/51125313)).

**Recipe:**
1. **Blender vertex colour**, generated from bmesh z:
   - R = height t: 1 at the top, 0.5 at the bottom of the lip, 0 at the underside tip.
   - G = snow mask.
   - B = baked AO.
   - A = sparkle allow.
   - Export FBX with **Vertex Colors: Linear**.
2. **Geometry:**
   - 3 cm, 2-segment bevel on tile tops.
   - 8 cm rounded outer rim plus a 15–25 cm frost band, flush with the floor.
   - ±10 cm facets on the sides.
3. **Textures (numpy, tileable):**
   - Top: 2048² at 512 px/m, value variation ±4%, 1–2 px cracks at 15%.
   - Mask: 1024², linear (sRGB off). R = frost, G = inner layer, B = sparkle noise.
   - Rotate tiles in 90° steps for variety.
4. **URP Lit (v1):**
   - Bake vertex colour × detail into the albedo.
   - Smoothness: top 0.6–0.7, frost 0.25, sides 0.5. Normal strength 0.3.
   - Sparkle comes from the planned particles: ≤ 80 visible, 3–6 cm, 0.4–0.8 s, HDR ×2–3.
   - Bloom threshold 1.1.
5. **Upgrade:** set the kit material to `KA/Ice`, which reads the same vertex colours and mask.

## 2. Gameplay readability

**Value ladder (L\*):**

| Layer | L* |
|---|---|
| Void / sky | 32–41 |
| Side | 56–72 |
| Top | 83 |
| Lip | 95 |

- **Critters:** Violet 39, Tomato 59, Teal 66, Chartreuse 91.
- **Anchors:** feet and eyes are 8.8–10:1 against the ice.

**Target the sunlit top at L* 80–86** (relative luminance 0.55–0.68).
- No single ice value separates all four players by luminance.
- Pale ice is the best compromise:
  - Violet (4.3:1) and Tomato (2.1:1) separate by value.
  - Teal is weaker (1.7:1).
  - Chartreuse separates by chroma only.
- L* 70 ice would merge Teal.

| Flag | Numbers | Fix |
|---|---|---|
| **Chartreuse vs ice/snow** | CR 1.22 / 1.12 (ΔE 104 / 96) | No snow on the floor; bloom threshold ≥ 1.1. Yellow vs blue survives protan/deutan colour blindness, not tritan. |
| **Teal vs ice** | ΔE 43, the lowest (32 on cyan ice, 39 vs crystals) | Ice hue ≥ 205°; no crystals in the play area. |
| **Violet vs sky** | CR 1.06, ΔE 44 against ref #0256B9 | The 14° camera puts far players against the horizon. The haze band #4A92E2 gives 2.06:1. |
| Tomato vs side/haze | CR 1.12 / 1.01, ΔE ≥ 116 | Fine; hue carries it. |

**Edge signal:**
- **Value steps:** lip 95 → side 56–72 → void 32 (CR 3.3, then 2.4).
- **White = drop.** Seams are dark grooves (#8FB0D4).
- **Icicles** at irregular 0.4–1.0 m spacing mark the edge from below.
- **Risk:** a raised snow lip snags a sliding BoxCollider. Keep it ≤ 1 cm, or keep it out of the collider.

**Jump shadows:**
- **Realtime shadow:** the jump apex is 1.54 m, and air time is 1.12 s. A 60–65° sun puts the shadow 0.7–0.9 m off the critter at the apex.
- **Blob shadow:** add Mario's shadow "directly under the character regardless of the light source" ([Koizumi](https://www.nintendoworldreport.com/news/14895/taking-mario-into-3d-space)).
  - Use a multiply quad 1 cm above the floor, colour #1D3F7A.
  - Grounded: radius 0.45 m, α 0.45. At the apex: radius 0.35 m, α 0.2.
  - Hide it when the raycast misses, so "no shadow" reads as "off".
- **Ice contrast:** lit vs shadowed ice is 2.5:1.

## 3. Arena scale and layout

**Blob range:** 10.2–11.4 m (muzzle height 0.4–0.6 m).

**Dodge window:**
- A 0.75 m sidestep takes 0.21 s; add 0.25 s reaction.
- So shots are dodgeable beyond ≈ 5.5 m.

**Recommendation: 4×4 tiles = 16 × 16 m** (64 m² per player).
- Centre to edge is 8 m, inside blob range, so there is no safe middle.
- Crossing takes 4.6 s.
- Spawns at (±4, ±4) m: 4 m from two edges, 8 m to the nearest neighbours.

**Knockback (design call):**
- At 1.2 m per hit, a critter needs ≈ 7 hits to go off from the centre.
- For 3–4 hits, use 2–2.5 m per hit, or 3×3 tiles (12 m, ≈ 5 hits).

**Square vs round:**
- **Square:** uses the kit as planned, and the seams act as a distance grid. Corners are two-drop danger zones, which is fine for a party game.
- **Octagon:** equal edge distance, but it needs an extra `Ice_Diag_4` piece. Optional.

**Shrinking (optional; the rules don't require it):**
- Drop the 12 outer tiles, leaving an 8 m core.
- Telegraph it for 3 s: 2 cm shake, tint toward #7FB6DD.
- No static batching if you use this.

**Seams:**
- They are fine visually.
- The physics risk is real. Adjacent colliders create ghost contacts at internal edges, which snag a sliding box ([Unity Physics](https://docs.unity3d.com/Packages/com.unity.physics@1.4/manual/ghost-collisions.html), [issue tracker](https://issuetracker-mig.prd.it.unity3d.com/issues/physics-objects-trip-and-slash-or-jump-when-colliding-with-other-colliders)).
- Use **one BoxCollider, 16 × 1 × 16 m, top at y = 0**. Tiles are visual only.
- When shrinking, swap colliders as the ring drops.

## 4. Floating-island composition

- **`Ice_Edge_4`:** 1.2–1.5 m tall. Icicles 0.3–1.2 m long, on ≈ 30% of the edge.
- **`Island_Underside`:**
  - Inverted faceted cone in 2–3 tiers.
  - Depth **0.6–0.8× arena width** (10–13 m).
  - Tip 15–25% off-centre.
  - The first tier is 90–95% of the floor's width, so the floor overhangs (clean drop silhouette, no ledge).
  - Colour #3B8CC2 → #123E86.
- **Depth layers:**
  - **Near (0–20 m):** arena and underside. Casts and receives shadows.
  - **Mid (35–80 m):** 3 islands at 0.25–0.5× scale and 3 crystal clusters, at y −15 to +10 m. Loose ring, so every spawn sees one element on the horizon. No shadows; fogged.
  - **Far (panorama):** clouds and tiny floes in the 0–10° band, plus the void gradient.
- **Fog:** Linear, from **30 m** (the farthest arena point is ≈ 27 m away) to 160 m, colour **#4A92E2**. URP fog skips the skybox, so match the horizon pixel ([forum](https://discussions.unity.com/t/whats-the-best-way-to-deal-with-fog-and-horizon-in-urp/780400)).
- **Void plane:** optional, for parallax only. 300 m unlit plane, colour #0A47A0, at y = −40 m.

## 5. Budgets (PC LAN)

| Asset | Tris | Texture |
|---|---|---|
| `Ice_Top_4x4` | 200–600 | shared 2048² BC7 (≈ 5.3 MB with mips) |
| `Ice_Edge_4` | 1.5–3k | shared side 1024² |
| `Ice_Corner` | 1–2k | shared |
| `Island_Underside` | 6–12k | vertex colour + side texture |
| Background island ×3 | 3–6k each | vertex colour + 512² |
| Crystal cluster ×3 | 0.5–1.5k each | vertex colour |
| Podium | 2–4k | shared |

- **Totals:**
  - Arena ≈ 60k tris.
  - Environment ≈ 80k tris (cap 150k).
  - Textures < 25 MB, including the sky cube.
- **Draw calls:** ≈ 200 (60 renderers × forward, prepass and 2 cascades).
  - SRP Batcher targets: < 300 batches, < 30 SetPass calls.
- **Lighting: realtime only.**
  - Sun: elevation 60–65°, intensity 1.1–1.3, colour #FFF6E8.
  - Shadows: 2048 map, 2 cascades, 40 m distance, Soft Medium.
  - No lightmaps: the floor is flat, critter shadows are dynamic anyway, and lightmaps would break shrinking.
  - AO is baked into vertex colour B.

## 6. Shader

**Verdict: URP Lit with a baked gradient gets ≈ 85–90% of the look. Use it first.**

`KA/Ice` adds three things Lit can't do:
- **(a) Blue shadow on the ice only.** Lit's shadow colour is albedo × SH ambient, which the critters share. Getting #4F86C2 that way needs ≈ #83BDF4 ambient, which tints Tomato purple.
- **(b) Glints that change with view angle.**
- **(c) Inner depth.**

The shader is also saved as `research_env/KA_Ice.shader`.

**Verified against the shipped URP 17.3.0 source** (the editor's BuiltInPackages, identical to GitHub `6000.3/staging` for these files):
- **Keywords:** the keyword lines match SimpleLit and ComplexLit.
- **Forward+ keyword:** `_CLUSTER_LIGHT_LOOP` replaces `_FORWARD_PLUS` ([6.1 upgrade guide](https://docs.unity3d.com/6000.1/Documentation/Manual/UpgradeGuideUnity61.html)). Keep it: `GetMainLight()` branches on it.
- **Fog:** `#include_with_pragmas Fog.hlsl` picks the right fog keywords.
- **Pass includes:** URP's ShadowCaster, DepthOnly and DepthNormals pass files read `_BaseMap`/`_Cutoff` only under `_ALPHATEST_ON`, so they work with our own cbuffer.
- **Pass tags:** `UniversalForwardOnly` and `DepthNormalsOnly` are drawn in Forward, Forward+ and Deferred.

**Compile test:**
- In a scratch 6000.3.9f1 project: 4 passes × vertex and fragment × 4 keyword sets on Metal, 0 failures.
- SRP Batcher status: **"OK"** with URP active.

**Not verified:** the D3D/Vulkan compile (no Windows module installed), the rendered look, and the GPU Resident Drawer (it falls back to the normal path, which is fine at ≈ 60 renderers).

**Avoid `UsePass "Universal Render Pipeline/Lit/ShadowCaster"`.** It brings in Lit's cbuffer, which triggers "CBuffer inconsistent size inside a SubShader" and turns off the SRP Batcher ([thread](https://discussions.unity.com/t/unitypermaterial-cbuffer-inconsistent-size-inside-a-subshader-914942/914942)).

```hlsl
// KA/Ice : stylized ice for Unity 6.3 / URP 17.3 (Forward, Forward+, Deferred via ForwardOnly).
// Vertex colour (baked in Blender): R = height t (0 underside tip, 0.5 bottom of lip, 1 top), G = snow mask, B = AO, A = sparkle allow.
// _MaskMap (tileable, linear): R = surface frost/cracks, G = inner layer (parallaxed), B = sparkle noise.
Shader "KA/Ice"
{
    Properties
    {
        [MainTexture] _BaseMap ("Detail albedo (RGB, ~0.85-1.0 grey)", 2D) = "white" {}
        [MainColor]   _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _MaskMap ("Mask R frost G inner B sparkle", 2D) = "black" {}
        _TopColor ("Top", Color) = (0.722, 0.827, 0.922, 1)       // #B8D3EB
        _SideColor ("Side", Color) = (0.231, 0.549, 0.761, 1)     // #3B8CC2
        _DeepColor ("Deep / underside", Color) = (0.071, 0.243, 0.525, 1) // #123E86
        _FrostColor ("Frost / snow", Color) = (0.918, 0.949, 0.980, 1)    // #EAF2FA
        _ShadowColor ("Light in shadow (ice only)", Color) = (0.514, 0.741, 0.957, 1) // #83BDF4
        _AmbientStrength ("Scene ambient (SH) add", Range(0, 1)) = 0.15
        _Smoothness ("Smoothness", Range(0, 1)) = 0.65
        _SpecStrength ("Spec strength", Range(0, 2)) = 0.5
        _RimColor ("Fresnel rim", Color) = (0.85, 0.93, 1, 1)
        _RimPower ("Rim power", Range(0.5, 8)) = 4
        _RimStrength ("Rim strength", Range(0, 1)) = 0.3
        _DepthOffset ("Inner layer depth (UV)", Range(0, 0.2)) = 0.03
        _DepthStrength ("Inner layer strength", Range(0, 1)) = 0.35
        _SparkleScale ("Sparkle tiling", Float) = 6
        _SparkleThreshold ("Sparkle threshold", Range(0.5, 0.99)) = 0.85
        _SparkleIntensity ("Sparkle intensity (HDR)", Range(0, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // Shared by every pass: one identical UnityPerMaterial cbuffer = SRP Batcher compatible.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _MaskMap_ST;
            half4 _BaseColor, _TopColor, _SideColor, _DeepColor, _FrostColor, _ShadowColor, _RimColor;
            half _AmbientStrength, _Smoothness, _SpecStrength, _RimPower, _RimStrength;
            half _DepthOffset, _DepthStrength, _SparkleScale, _SparkleThreshold, _SparkleIntensity;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP   // Forward+/Deferred+; main light attenuation path depends on it
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float4 uv          : TEXCOORD0;   // xy base, zw mask
                float3 positionWS  : TEXCOORD1;
                half3  normalWS    : TEXCOORD2;
                half3  tangentWS   : TEXCOORD3;
                half3  bitangentWS : TEXCOORD4;
                half4  color       : TEXCOORD5;
                half   fogFactor   : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = n.normalWS;
                OUT.tangentWS = n.tangentWS;
                OUT.bitangentWS = n.bitangentWS;
                OUT.uv = float4(TRANSFORM_TEX(IN.uv, _BaseMap), TRANSFORM_TEX(IN.uv, _MaskMap));
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half3 viewTS = half3(dot(V, IN.tangentWS), dot(V, IN.bitangentWS), dot(V, N));

                // 1. Height gradient: deep -> side -> top (vertex colour R).
                half t = IN.color.r;
                half3 albedo = lerp(lerp(_DeepColor.rgb, _SideColor.rgb, saturate(t * 2.0h)), _TopColor.rgb, saturate(t * 2.0h - 1.0h));
                albedo *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv.xy).rgb * _BaseColor.rgb;

                // 2. Fake inner layer: same mask sampled one "depth" below the surface along the view ray.
                float2 uvDeep = IN.uv.zw - viewTS.xy / max(viewTS.z, 0.2h) * _DepthOffset;
                half inner = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uvDeep).g;
                albedo = lerp(albedo, _SideColor.rgb, inner * _DepthStrength);

                // 3. Surface frost / cracks / snow, then baked AO.
                half3 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, IN.uv.zw).rgb;
                albedo = lerp(albedo, _FrostColor.rgb, saturate(max(mask.r, IN.color.g)));
                albedo *= IN.color.b;

                // 4. Main light + shadows. Shadowed ice gets an art-directed light colour instead of albedo*ambient.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half lit = saturate(dot(N, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half3 color = albedo * (lerp(_ShadowColor.rgb, mainLight.color, lit) + SampleSH(N) * _AmbientStrength);

                half3 H = SafeNormalize(float3(mainLight.direction) + V);
                color += mainLight.color * (pow(saturate(dot(N, H)), exp2(10.0h * _Smoothness + 1.0h)) * _SpecStrength * lit);

                // 5. Fresnel rim (brightens grazing floor and bevels).
                color += _RimColor.rgb * (pow(1.0h - saturate(dot(N, V)), _RimPower) * _RimStrength);

                // 6. Sparkle: two noise taps, one view-shifted, so glints twinkle as the camera moves. Sunlit only.
                half n1 = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, IN.uv.zw * _SparkleScale).b;
                half n2 = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, IN.uv.zw * (_SparkleScale * 1.7h) + viewTS.xy * 0.2h).b;
                half sparkle = saturate((n1 * n2 - _SparkleThreshold) / (1.0h - _SparkleThreshold));
                color += mainLight.color * (sparkle * _SparkleIntensity * lit * IN.color.a);

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // URP's own pass bodies. They only touch _BaseMap/_BaseColor/_Cutoff under _ALPHATEST_ON, which we never enable.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
```

**Omitted on purpose:** additional lights, lightmaps/Meta pass, SSAO, decals and alpha clip. If you need one, copy its keyword lines from `SimpleLit.shader`.

**Usage:**
- UVs: 0–1 per 4 m tile.
- FBX tangents: Mikktspace.
- `_DepthOffset` 0.03 ≈ 12 cm of apparent depth.

## 7. Sky

**Build:** a numpy equirect panorama, 4096×2048, 16-bit PNG (or 8-bit with ±1 LSB dither).
- **Gradient:** #0B4CB0 → #0F5CC4 → haze #4A92E2 (0–10°) → #0A47A0 → #062E78 at the nadir.
- **Clouds:** fbm, #EAF2FA at 20–40%, in the 5–25° band.
- **Import:** **Texture Shape Cube, Latitude-Longitude**, on **Skybox/Cubemap**. No wrap seam, and mipmaps stay on.

**Alternatives:**
- **Skybox/Panoramic:** disable mipmaps, or the wrap shows a seam ([thread](https://discussions.unity.com/t/the-panoramic-skybox-has-a-seam-line/339527)).
- **Skybox/Procedural:** no. It has no clouds and a pale physical horizon.
- **`sunflowers_puresky`:** photo-real, partly cloudy, 6550 K ([Poly Haven](https://polyhaven.com/a/sunflowers_puresky)). It is too pale for the refs, and photo clouds clash with vinyl toys. Use it only as the reflection cubemap (Environment Reflections → Custom).

**URP 17 gotchas:**
1. **Ambient:** set Environment Lighting to **Gradient** (sky #9CC3EE, equator #B9D3EE, ground #3D6FB0; [docs](https://docs.unity3d.com/Manual/lighting-window.html)). Skybox ambient from this sky would over-blue the critters.
2. **Bake once:** assign **Sun Source**, and press **Generate Lighting** once even with nothing to bake. Otherwise loaded scenes and builds can get the wrong ambient ([issue tracker](https://issuetracker.unity3d.com/issues/ambient-lighting-is-broken-when-loading-scenes-that-contain-a-camera-and-light), [forum](https://discussions.unity.com/t/skybox-ambient-not-working-in-unity-6/1539599)).
3. **Fog:** fog skips the skybox, so its colour must equal the horizon pixel (#4A92E2).
