// Painted stream water for Echoes of the Valley (URP 17). Written for this project (Phase 5.2-B5).
// The stream ribbon (uv.x across the stream 0..1, uv.y along it, 0.25 per metre) drawn in the colours of the painted spring instead of a
// flat blue plane: deeper in the middle, lighter at the banks, banks fading softly into the ground, slow light streaks moving downstream.
// The streaks move with _BaseMap_ST, which WaterFlow scrolls (a creep in the silent valley, full flow once the stream is restored).
// The first metres fade in (_StartFade, in uv.y) so the water leaves the painted spring's rim instead of lying over it.
Shader "Echoes/PaintedWater"
{
    Properties
    {
        [MainTexture] _BaseMap ("Unused (flow phase comes from its scale/offset)", 2D) = "white" {}
        _DeepColor ("Deep", Color) = (0.153, 0.286, 0.302, 1)
        _MidColor ("Middle", Color) = (0.314, 0.557, 0.533, 1)
        _LightColor ("Light / streaks", Color) = (0.706, 0.871, 0.784, 1)
        _Opacity ("Opacity", Range(0,1)) = 0.86
        _BankSoftness ("Bank softness (share of the half width)", Range(0.01,1)) = 0.35
        _StartFade ("Fade in along the stream (uv.y from, to)", Vector) = (0, 0.3, 0, 0)
        _Ripple ("Streak scale across, along", Vector) = (4, 6, 0, 0)
        _GlintStrength ("Streak strength", Range(0,1)) = 0.45
        _AmbientWeight ("Ambient weight", Float) = 0.55
        _SunWeight ("Sun weight", Float) = 0.65
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-30" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "PaintedWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _DeepColor;
                half4 _MidColor;
                half4 _LightColor;
                half _Opacity;
                half _BankSoftness;
                float4 _StartFade;
                float4 _Ripple;
                half _GlintStrength;
                half _AmbientWeight;
                half _SunWeight;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x), u.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float across = abs(i.uv.x * 2 - 1);                                   // 0 in the middle, 1 at a bank
                float along = i.uv.y * _BaseMap_ST.y + _BaseMap_ST.w;                  // scrolls downstream with WaterFlow
                float n1 = ValueNoise(float2(i.uv.x * _Ripple.x, along * _Ripple.y));
                float n2 = ValueNoise(float2(i.uv.x * _Ripple.x * 2.3 + 3.1, along * _Ripple.y * 2.1 - 1.7));
                float n = n1 * 0.65 + n2 * 0.35;
                float deep = saturate((1 - across * across) * 0.85 + (n - 0.5) * 0.3);
                half3 colour = lerp(_LightColor.rgb, _MidColor.rgb, saturate(deep * 1.6));
                colour = lerp(colour, _DeepColor.rgb, saturate(deep * 1.8 - 0.9));
                colour += _LightColor.rgb * smoothstep(0.70, 0.92, n) * _GlintStrength * (0.5 + 0.5 * deep);
                colour = lerp(colour, _LightColor.rgb, smoothstep(0.72, 0.96, across) * 0.30);

                Light sun = GetMainLight();
                half3 light = SampleSH(half3(0, 1, 0)) * _AmbientWeight + sun.color * _SunWeight;
                colour = MixFog(colour * light, i.fog);
                float bank = 1 - smoothstep(1 - _BankSoftness, 1.0, across);
                float start = smoothstep(_StartFade.x, _StartFade.y, i.uv.y);
                return half4(colour, _Opacity * bank * start);
            }
            ENDHLSL
        }
    }
}
