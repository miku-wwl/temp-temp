// Static 2.5D painted prop for Echoes of the Valley (URP 17). Written for this project (Phase 5.2-B4).
// A painted building, tree or ruin is a camera-facing card around its anchor. Its depth is NOT the card's: every pixel takes the depth of
// the point where its image column meets the painting's own ground line (a polyline through three world points: left, front and right
// corner of the footprint, pulled a little toward the camera). So a character in front of a facade is drawn over it, a character behind
// the building is hidden, and the terrain never cuts the painted base. Trees can use one base point (the trunk) and dissolve with a
// dither around the player when the player stands behind them (the global _EchoesFocus point).
Shader "Echoes/StaticProp25D"
{
    Properties
    {
        [MainTexture] _MainTex ("Painting (straight alpha)", 2D) = "white" {}
        [MainColor] _Tint ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        _DepthPull ("Depth pull toward the camera (m)", Float) = 0.15
        _AmbientWeight ("Ambient weight", Float) = 0.45
        _SunWeight ("Sun weight", Float) = 0.75
        _Exposure ("Exposure", Float) = 1.0
        _FadeRadius ("Fade radius around the player (fraction of screen height)", Float) = 0.16
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }

        Pass
        {
            Name "StaticProp25D"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _EchoesFocus;          // xyz = player chest, w = 1 when active (FoliageFadeDriver)
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half _Cutoff;
                float _DepthPull;
                half _AmbientWeight;
                half _SunWeight;
                half _Exposure;
                float _FadeRadius;
            CBUFFER_END
            // per renderer (MaterialPropertyBlock)
            float4 _FrameRect;   // card in metres around the anchor, view-space x/y: left, bottom, width, height
            float4 _BaseL;       // world xyz of the ground line's left end
            float4 _BaseF;       // world xyz of its front corner
            float4 _BaseR;       // world xyz of its right end
            float4 _BaseU;       // image u of the left end, the front corner and the right end
            float _Fade;         // 1 = dissolve around the player when the player is behind (trees)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 anchorVS = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
                float2 corner = _FrameRect.xy + input.uv * _FrameRect.zw;
                o.positionCS = TransformWViewToHClip(anchorVS + float3(corner, 0));
                o.uv = input.uv;
                return o;
            }

            float3 BasePoint(float u)
            {
                float a = saturate((u - _BaseU.x) / max(1e-4, _BaseU.y - _BaseU.x));
                float b = saturate((u - _BaseU.y) / max(1e-4, _BaseU.z - _BaseU.y));
                return u <= _BaseU.y ? lerp(_BaseL.xyz, _BaseF.xyz, a) : lerp(_BaseF.xyz, _BaseR.xyz, b);
            }

            half4 Frag(Varyings i, out float depth : SV_Depth) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float3 baseWS = BasePoint(i.uv.x);
                float3 toCamera = normalize(_WorldSpaceCameraPos - baseWS);
                float4 baseCS = TransformWorldToHClip(baseWS + toCamera * _DepthPull);
                depth = baseCS.z / baseCS.w;

                half alpha = tex.a;
                if (_Fade > 0.5 && _EchoesFocus.w > 0.5)
                {
                    // the player is behind this prop: open a dithered window around her on screen
                    float focusDistance = dot(_EchoesFocus.xyz - _WorldSpaceCameraPos, -UNITY_MATRIX_V[2].xyz);
                    float baseDistance = dot(baseWS - _WorldSpaceCameraPos, -UNITY_MATRIX_V[2].xyz);
                    if (focusDistance > baseDistance - 0.3)
                    {
                        float4 focusCS = TransformWorldToHClip(_EchoesFocus.xyz);
                        float2 focusPx = (focusCS.xy / focusCS.w * float2(0.5, -0.5) + 0.5) * _ScreenParams.xy;
                        #if UNITY_UV_STARTS_AT_TOP
                        #else
                        focusPx.y = _ScreenParams.y - focusPx.y;
                        #endif
                        float r = length(i.positionCS.xy - focusPx) / _ScreenParams.y;
                        float open = 1 - smoothstep(_FadeRadius * 0.55, _FadeRadius, r);
                        float dither = frac(52.9829189 * frac(dot(i.positionCS.xy, float2(0.06711056, 0.00583715))));
                        alpha *= step(open * 0.85, dither);
                    }
                }
                clip(alpha - _Cutoff);

                Light sun = GetMainLight();
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 light = ambient * _AmbientWeight + sun.color * _SunWeight;
                half3 colour = tex.rgb * _Tint.rgb * light * _Exposure;
                colour = MixFog(colour, ComputeFogFactor(baseCS.z));
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
