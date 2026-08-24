Shader "RCCom/Unit Visuals/Range Pulse Aura"
{
    Properties
    {
        [HDR] _Color ("Aura Color", Color) = (0.15, 0.85, 1.2, 0.8)
        _Progress ("Progress", Range(0, 1)) = 0
        _StrokeWidth ("Stroke Width", Range(0.002, 0.08)) = 0.012
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.8
        _GlossIntensity ("Gloss Intensity", Range(0, 1)) = 0.35
        _Opacity ("Opacity", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "RangePulseAura"
            Blend SrcAlpha One
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Progress;
                float _StrokeWidth;
                float _GlowIntensity;
                float _GlossIntensity;
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
                float distanceToRing = abs(radialDistance - _Progress);
                float core = 1.0 - smoothstep(stroke * 0.3, stroke, distanceToRing);
                float glow = 1.0 - smoothstep(stroke, stroke * 5.0, distanceToRing);

                // 시작점과 최대 반경에서 자연스럽게 사라져 파동이 갑자기 켜지고 꺼지는 인상을 줄인다.
                float lifeFade = smoothstep(0.015, 0.11, _Progress) *
                                 (1.0 - smoothstep(0.86, 1.0, _Progress));

                // 좌상단에 좁은 흰 반사를 더해 평면적인 네온 선보다 약간 glossy하게 보이게 한다.
                float2 ringNormal = normalize(centered + float2(0.0001, 0.0001));
                float2 lightDirection = normalize(float2(-0.55, 0.83));
                float gloss = pow(saturate(dot(ringNormal, lightDirection) * 0.5 + 0.5), 12.0);
                float glossAmount = saturate(_GlossIntensity * (0.2 + gloss * 0.8));
                float3 glossyColor = lerp(_Color.rgb, float3(1.0, 1.0, 1.0), glossAmount);

                float combined = saturate(core + glow * 0.35 * _GlowIntensity);
                float alpha = combined * _Color.a * _Opacity * lifeFade;
                float intensity = 1.0 + core * 0.2 + glow * 0.15 * _GlowIntensity;
                return half4(glossyColor * intensity, alpha);
            }
            ENDHLSL
        }
    }
}
