Shader "RCCom/Enemy Visuals/Persistent Range Aura"
{
    Properties
    {
        [HDR] _Color ("Aura Color", Color) = (0.18, 1.15, 0.35, 0.75)
        _StrokeWidth ("Stroke Width", Range(0.002, 0.08)) = 0.018
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.65
        _Opacity ("Opacity", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _StrokeWidth;
                float _GlowIntensity;
                float _Opacity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = (input.uv - 0.5) * 2.0;
                float radialDistance = length(centered);
                clip(1.0 - radialDistance);

                float stroke = max(_StrokeWidth, 0.002);
                float distanceToRing = abs(radialDistance - 0.94);
                float core = 1.0 - smoothstep(stroke * 0.3, stroke, distanceToRing);
                float glow = 1.0 - smoothstep(stroke, stroke * 5.0, distanceToRing);
                float combined = saturate(core + glow * 0.35 * _GlowIntensity);
                float alpha = combined * _Color.a * _Opacity;
                return half4(_Color.rgb * (1.0 + core * 0.2), alpha);
            }
            ENDHLSL
        }
    }
}
