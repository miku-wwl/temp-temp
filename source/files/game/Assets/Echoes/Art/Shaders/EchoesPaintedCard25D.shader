// Painted camera-facing cards for Echoes of the Valley (URP 17). Written for this project (Phase 5.2-B5).
// Trees, garden plants and flowers: every vertex carries its card's ground point (POSITION, object space), its corner offset in metres
// (TEXCOORD1, view-space x/y) and a tint (COLOR.rgb; COLOR.a = 1 lets the card open around the player). The card stands in a plane
// facing the camera through its ground point, so all its fragments take that point's depth: a character in front of the trunk is drawn
// over the tree, a character behind it is hidden, the terrain never cuts the painted roots. Many cards share one mesh (one draw call);
// a card under a moving or scaling object (a plant that pops up when a stage is reached) grows with the object's uniform scale.
// The part of a card below its ground point (painted roots, the foot of a clump) is pulled towards the camera as far as the ground in
// front of the ground point is (cot(pitch) per metre below), so the terrain does not cut it off along a straight line.
// Where the player stands behind a card that may open, an ordered dither opens a soft window around her (never the whole card).
Shader "Echoes/PaintedCard25D"
{
    Properties
    {
        [MainTexture] _MainTex ("Painting atlas (straight alpha)", 2D) = "white" {}
        [MainColor] _Tint ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.45
        _DepthPull ("Depth pull toward the camera (m)", Float) = 0.15
        _AmbientWeight ("Ambient weight", Float) = 0.45
        _SunWeight ("Sun weight", Float) = 0.75
        _Exposure ("Exposure", Float) = 1.0
        _FadeRadius ("Opening radius around the player (fraction of screen height)", Float) = 0.15
        _FadeStrength ("Opening at its centre (1 = every pixel)", Range(0,1)) = 0.85
        _GroundFollow ("Below the ground point: depth follows the ground (x cot pitch)", Float) = 1.15
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }

        Pass
        {
            Name "PaintedCard25D"
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
                half _FadeStrength;
                float _GroundFollow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 corner : TEXCOORD1; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float fog : TEXCOORD1;
                float behind : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 baseWS = TransformObjectToWorld(input.positionOS.xyz);
                float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 anchorVS = TransformWorldToView(baseWS);
                float3 positionVS = anchorVS + float3(input.corner * scale, 0);
                float4 positionCS = TransformWViewToHClip(positionVS);
                float3 forward = -UNITY_MATRIX_V[2].xyz;
                float sinPitch = saturate(-forward.y);
                float cotPitch = sqrt(1 - sinPitch * sinPitch) / max(sinPitch, 0.3);
                float below = max(0, -input.corner.y * scale);
                float pull = _DepthPull + below * cotPitch * _GroundFollow;
                float4 pulledCS = TransformWViewToHClip(float3(positionVS.xy, anchorVS.z + pull));   // view space looks down -z
                positionCS.z = pulledCS.z / pulledCS.w * positionCS.w;
                o.positionCS = positionCS;
                o.uv = input.uv;
                o.color = input.color;
                o.fog = ComputeFogFactor(positionCS.z);
                float behind = 0;
                if (input.color.a > 0.5 && _EchoesFocus.w > 0.5)
                {
                    float focusDistance = dot(_EchoesFocus.xyz - _WorldSpaceCameraPos, forward);
                    float baseDistance = dot(baseWS - _WorldSpaceCameraPos, forward);
                    behind = focusDistance > baseDistance - 0.3 ? 1 : 0;
                }
                o.behind = behind;
                return o;
            }

            // 4 x 4 ordered dither threshold in (0, 1): a fine regular screen instead of noise
            static const float BayerMatrix[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            float Bayer4(uint2 p) { return (BayerMatrix[(p.y & 3) * 4 + (p.x & 3)] + 0.5) / 16.0; }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half alpha = tex.a;
                if (i.behind > 0.5)
                {
                    float4 focusCS = TransformWorldToHClip(_EchoesFocus.xyz);
                    float2 focusPx = (focusCS.xy / focusCS.w * float2(0.5, -0.5) + 0.5) * _ScreenParams.xy;
                    #if UNITY_UV_STARTS_AT_TOP
                    #else
                    focusPx.y = _ScreenParams.y - focusPx.y;
                    #endif
                    // an upright ellipse round the chest, as tall as the caretaker (her head and feet are 0.7 m and 1.05 m from the focus):
                    // fully open over her whole figure, fading out softly to the ellipse's edge
                    float2 d = (i.positionCS.xy - focusPx) / _ScreenParams.y;
                    float r = length(float2(d.x, d.y * 0.62));
                    float open = (1 - smoothstep(_FadeRadius * 0.55, _FadeRadius, r)) * _FadeStrength;
                    alpha *= step(open, Bayer4(uint2(i.positionCS.xy)));
                }
                clip(alpha - _Cutoff);

                Light sun = GetMainLight();
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 light = ambient * _AmbientWeight + sun.color * _SunWeight;
                half3 colour = tex.rgb * i.color.rgb * _Tint.rgb * light * _Exposure;
                colour = MixFog(colour, i.fog);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
