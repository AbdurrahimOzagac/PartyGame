// KA/Ice : stylized ice for Unity 6.3 / URP 17.3 (Forward, Forward+, Deferred via ForwardOnly).
// Compile-verified skeleton from the ice brief (projects/.../blender/v2/research/ice_brief.md), adapted:
// textures are mapped in world XZ (one repeat per _TileSize metres), so kit tiles need no UVs and join
// seamlessly at any rotation; dark seam grooves are drawn on the tile grid (tops only, never on snow).
// Vertex colour (baked in Blender, exported linear): R = height t (0 underside tip, 0.5 bottom of lip, 1 top),
// G = snow mask, B = AO / facet shade, A = sparkle allow.
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
        _TileSize ("Tile size (m)", Float) = 4
        _SeamColor ("Seam groove", Color) = (0.561, 0.690, 0.831, 1)   // #8FB0D4
        _SeamWidth ("Seam width (m)", Range(0, 0.1)) = 0.035
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
            half4 _SeamColor;
            float _TileSize, _SeamWidth;
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
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS);   // kit meshes have no UVs/tangents
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = n.normalWS;
                OUT.uv = 0;   // set per fragment from world position
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
                // planar XZ mapping: tangent frame of a floor is (x, z, y); fine for the near-vertical cliffs too
                half3 viewTS = half3(V.x, V.z, V.y);
                float2 wuv = IN.positionWS.xz / _TileSize;
                IN.uv = float4(wuv * _BaseMap_ST.xy + _BaseMap_ST.zw, wuv * _MaskMap_ST.xy + _MaskMap_ST.zw);
                half top = saturate(N.y * 4.0h - 3.0h);   // 1 on floors, 0 on walls

                // 1. Height gradient: deep -> side -> top (vertex colour R).
                half t = IN.color.r;
                half3 albedo = lerp(lerp(_DeepColor.rgb, _SideColor.rgb, saturate(t * 2.0h)), _TopColor.rgb, saturate(t * 2.0h - 1.0h));
                albedo *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv.xy).rgb * _BaseColor.rgb;

                // 2. Fake inner layer: same mask sampled one "depth" below the surface along the view ray.
                float2 uvDeep = IN.uv.zw - viewTS.xy / max(viewTS.z, 0.2h) * _DepthOffset;
                half inner = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uvDeep).g;
                albedo = lerp(albedo, _SideColor.rgb, inner * _DepthStrength * top);

                // 3. Surface frost / cracks / snow, then baked AO.
                half3 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, IN.uv.zw).rgb;
                albedo = lerp(albedo, _FrostColor.rgb, saturate(max(mask.r * top, IN.color.g)));
                albedo *= IN.color.b;

                // 3b. Seam grooves on the tile grid: floors only, never across the white edge (white = drop)
                float2 g = abs(frac(IN.positionWS.xz / _TileSize + 0.5) - 0.5) * _TileSize;
                half seam = (1.0h - smoothstep(_SeamWidth * 0.4, _SeamWidth, min(g.x, g.y))) * top * (1.0h - IN.color.g);
                albedo = lerp(albedo, _SeamColor.rgb, seam * 0.85h);

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
