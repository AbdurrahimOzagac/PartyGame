// KA/Goo : candy-goo spit blob for Unity 6.3 / URP 17.3. Built on the same compile-verified skeleton as KA/Ice.
// Reads as glossy jelly candy, never snot (VFX brief): core lifted toward white, saturated rim, a little
// self-emission, a brightness floor (no pixel below ~70 % of the base colour), and a baked cartoon glint
// fixed in view space so it reads at 20 m where real speculars vanish.
Shader "KA/Goo"
{
    Properties
    {
        [MainTexture] _BaseMap ("Unused (shadow pass)", 2D) = "white" {}
        [MainColor]   _BaseColor ("Goo colour", Color) = (1, 0.31, 0.18, 1)
        _CoreColor ("Core lift toward", Color) = (1, 1, 1, 1)
        _CoreLift ("Core lift", Range(0, 1)) = 0.35
        _RimStrength ("Saturated rim", Range(0, 2)) = 0.6
        _Emission ("Self emission", Range(0, 1)) = 0.25
        _Floor ("Brightness floor", Range(0, 1)) = 0.7
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _GlintDir ("Glint direction (view space)", Vector) = (-0.45, 0.55, 0.7, 0)
        _GlintSize ("Glint size", Range(0.001, 0.2)) = 0.045
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _CoreColor, _GlintDir;
            half _CoreLift, _RimStrength, _Emission, _Floor, _Smoothness, _GlintSize;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
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
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
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
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = GetVertexNormalInputs(IN.normalOS).normalWS;
                OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                half3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half nv = saturate(dot(N, V));
                half3 baseC = _BaseColor.rgb;

                // candy core: the centre facing the camera lifts toward white, the rim stays saturated
                half3 albedo = lerp(baseC, _CoreColor.rgb, _CoreLift * nv * nv);

                Light L = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half wrap = saturate((dot(N, L.direction) + 0.5h) / 1.5h);          // soft wrap: jelly, not plastic
                half lit = wrap * lerp(0.55h, 1.0h, L.shadowAttenuation);
                half3 color = albedo * (L.color * lit + SampleSH(N));

                half3 H = SafeNormalize(float3(L.direction) + V);
                color += L.color * pow(saturate(dot(N, H)), exp2(10.0h * _Smoothness + 1.0h)) * L.shadowAttenuation;

                half3 sat = baseC * baseC / max(max(baseC.r, max(baseC.g, baseC.b)), 0.01h);   // deeper, more saturated rim
                color += sat * pow(1.0h - nv, 3.0h) * _RimStrength;
                color += baseC * _Emission;
                color = max(color, albedo * _Floor);                                   // never muddy in shadow

                // view-space cartoon glint (+ a tiny second one), so it reads as glossy candy at any range
                half3 nVS = normalize(mul((float3x3)UNITY_MATRIX_V, N));
                half3 gd = normalize(_GlintDir.xyz);
                half g1 = smoothstep(1.0h - _GlintSize, 1.0h - _GlintSize * 0.55h, dot(nVS, gd));
                half g2 = smoothstep(1.0h - _GlintSize * 0.3h, 1.0h - _GlintSize * 0.12h, dot(nVS, normalize(gd + half3(0.32h, -0.22h, 0))));
                color = lerp(color, half3(1, 1, 1), saturate(g1 + g2 * 0.9h));

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

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
