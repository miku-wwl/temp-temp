// Static painted ground foliage for Echoes of the Valley (URP 17). Written for this project (Phase 5.2-B4).
// Many clumps share one mesh: every vertex carries its clump's ground point (POSITION) and its corner offset in metres (TEXCOORD1);
// the card is built facing the camera and every fragment takes the depth of the clump's ground point, so a character walking behind a
// clump is hidden by it, in front of it is not, and the terrain never cuts the clump's base. Alpha-to-coverage, no blending, no sorting.
Shader "Echoes/Foliage25D"
{
    Properties
    {
        [MainTexture] _MainTex ("Foliage atlas (straight alpha)", 2D) = "white" {}
        [MainColor] _Tint ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.35
        _DepthPull ("Depth pull toward the camera (m)", Float) = 0.05
        _AmbientWeight ("Ambient weight", Float) = 0.45
        _SunWeight ("Sun weight", Float) = 0.75
        _Exposure ("Exposure", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }

        Pass
        {
            Name "Foliage25D"
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
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half _Cutoff;
                float _DepthPull;
                half _AmbientWeight;
                half _SunWeight;
                half _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 corner : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 anchorVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                float3 positionVS = anchorVS + float3(input.corner, 0);
                float4 positionCS = TransformWViewToHClip(positionVS);
                float4 pulledCS = TransformWViewToHClip(float3(positionVS.xy, anchorVS.z + _DepthPull));
                positionCS.z = pulledCS.z / pulledCS.w * positionCS.w;
                o.positionCS = positionCS;
                o.uv = input.uv;
                o.fog = ComputeFogFactor(positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(tex.a - _Cutoff);
                Light sun = GetMainLight();
                half3 light = SampleSH(half3(0, 1, 0)) * _AmbientWeight + sun.color * _SunWeight;
                half3 colour = MixFog(tex.rgb * _Tint.rgb * light * _Exposure, i.fog);
                return half4(colour, tex.a);
            }
            ENDHLSL
        }
    }
}
