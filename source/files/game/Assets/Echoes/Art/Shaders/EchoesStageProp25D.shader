// Painted static 2.5D prop with one painting per Resonance stage, for Echoes of the Valley (URP 17). Written for this project (Phase 5.2-B5).
// The card and its depth are those of Echoes/StaticProp25D (every pixel takes the depth of the painting's own ground line: left end,
// front corner and right end of the footprint), so a character in front of the rim is drawn over it and one behind the arch is hidden.
// The paintings of the stages are registered slices of one Texture2DArray; _Stage (0..3, set per renderer by StagePainting25D from the
// valley's own stage blends) cross-fades between the two slices it falls between, so a stage reached in play changes over the same
// seconds as the light, and a loaded save shows its stage at once.
Shader "Echoes/StageProp25D"
{
    Properties
    {
        [NoScaleOffset] _Stages ("Stage paintings (Texture2DArray, straight alpha)", 2DArray) = "" {}
        _StageCount ("Number of stage slices", Float) = 4
        [MainColor] _Tint ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        _DepthPull ("Depth pull toward the camera (m)", Float) = 0.15
        _AmbientWeight ("Ambient weight", Float) = 0.45
        _SunWeight ("Sun weight", Float) = 0.75
        _Exposure ("Exposure", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }

        Pass
        {
            Name "StageProp25D"
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

            TEXTURE2D_ARRAY(_Stages); SAMPLER(sampler_Stages);
            CBUFFER_START(UnityPerMaterial)
                float _StageCount;
                half4 _Tint;
                half _Cutoff;
                float _DepthPull;
                half _AmbientWeight;
                half _SunWeight;
                half _Exposure;
            CBUFFER_END
            // per renderer (MaterialPropertyBlock): StaticProp25D sets the card and the ground line, StagePainting25D the stage
            float4 _FrameRect;   // card in metres around the anchor, view-space x/y: left, bottom, width, height
            float4 _BaseL;       // world xyz of the ground line's left end
            float4 _BaseF;       // world xyz of its front corner
            float4 _BaseR;       // world xyz of its right end
            float4 _BaseU;       // image u of the left end, the front corner and the right end
            float _Stage;        // 0..StageCount-1, fractional while a stage is being reached

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
                float s = clamp(_Stage, 0, _StageCount - 1);
                float s0 = floor(s);
                float s1 = min(s0 + 1, _StageCount - 1);
                half4 a = SAMPLE_TEXTURE2D_ARRAY(_Stages, sampler_Stages, i.uv, s0);
                half4 b = SAMPLE_TEXTURE2D_ARRAY(_Stages, sampler_Stages, i.uv, s1);
                half4 tex = lerp(a, b, (half)(s - s0));   // colour is bled into transparent texels, so a straight blend has no dark rim

                float3 baseWS = BasePoint(i.uv.x);
                float3 toCamera = normalize(_WorldSpaceCameraPos - baseWS);
                float4 baseCS = TransformWorldToHClip(baseWS + toCamera * _DepthPull);
                depth = baseCS.z / baseCS.w;
                clip(tex.a - _Cutoff);

                Light sun = GetMainLight();
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 light = ambient * _AmbientWeight + sun.color * _SunWeight;
                half3 colour = tex.rgb * _Tint.rgb * light * _Exposure;
                colour = MixFog(colour, ComputeFogFactor(baseCS.z));
                return half4(colour, tex.a);
            }
            ENDHLSL
        }
    }
}
