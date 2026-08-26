Shader "RCCom/Enemy Visuals/Frontal Shield"
{
    Properties
    {
        [HDR] _Color ("Shield Color", Color) = (0.12, 0.65, 1.35, 0.82)
        _StrokeWidth ("Stroke Width", Range(0.002, 0.12)) = 0.025
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.9
        _FillOpacity ("Fill Opacity", Range(0, 1)) = 0.12
        _Opacity ("Opacity", Range(0, 1)) = 0.82
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
                float _FillOpacity;
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
                clip(centered.x);
                clip(1.0 - radialDistance);

                float stroke = max(_StrokeWidth, 0.002);
                float distanceToArc = abs(radialDistance - 0.92);
                float core = 1.0 - smoothstep(stroke * 0.25, stroke, distanceToArc);
                float glow = 1.0 - smoothstep(stroke, stroke * 5.0, distanceToArc);
                float innerMask = 1.0 - smoothstep(0.82, 0.92, radialDistance);
                float centerFade = smoothstep(0.0, 0.22, centered.x);
                float fill = innerMask * centerFade * _FillOpacity;
                float combined = saturate(core + glow * 0.35 * _GlowIntensity + fill);
                float alpha = combined * _Color.a * _Opacity;
                return half4(_Color.rgb * (1.0 + core * 0.25), alpha);
            }
            ENDHLSL
        }
    }
}
