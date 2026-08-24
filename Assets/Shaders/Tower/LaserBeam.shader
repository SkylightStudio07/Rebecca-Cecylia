Shader "RCCom/Tower Visuals/Laser Beam"
{
    // 관통 사격 타워(PierceDamageEffect)의 2-Layer 빔(설계안 §3-②). Inner Core/Outer Glow
    // 두 레이어 모두 이 셰이더 하나를 공유하고, LaserBeamView가 MaterialPropertyBlock으로
    // 레이어별 색/글로우를 다르게 먹인다 — RangePulseAura.shader와 같은 구조(URP HLSLPROGRAM +
    // Core.hlsl + CBUFFER_START(UnityPerMaterial))를 그대로 따른다.
    //
    // LineRenderer가 만드는 스트립의 기본 UV: x=선을 따라 0→1, y=폭 방향 0→1(0.5가 중심선).
    // 폭 방향 페이드(SoftEdge)만 셰이더가 담당하고, "150%→100% 팬치" 폭 애니메이션은 셰이더가
    // 아니라 LaserBeamView가 LineRenderer.widthMultiplier로 절차적으로 구동한다(설계안 §3-②:
    // "AttackFlash.lifetime과 동일한 잔여시간 필드로 절차적 계산").
    Properties
    {
        [HDR] _Color ("Beam Color", Color) = (1, 1, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 1.2
        _SoftEdge ("Soft Edge", Range(0.01, 1)) = 0.4
        _NoiseScale ("Noise Scale", Float) = 6
        _ScrollSpeed ("Scroll Speed", Float) = 3.5
        _LifeFade ("Life Fade", Range(0, 1)) = 1
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
            Name "LaserBeam"
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
                float _GlowIntensity;
                float _SoftEdge;
                float _NoiseScale;
                float _ScrollSpeed;
                float _LifeFade;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // 텍스처 없는 해시 기반 1D 노이즈 — 새 텍스처 에셋을 만들지 않고 UV 스크롤만으로
            // "에너지가 흐르는" 인상을 준다(설계안 §0-2: 지오메트리/셰이더 중심, RangePulseAura와
            // 같은 절차적 접근).
            float Hash(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            float FlowNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float a = Hash(i);
                float b = Hash(i + 1.0);
                return lerp(a, b, smoothstep(0.0, 1.0, f));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 중심선(0.5)에서 폭 방향으로 멀어질수록 부드럽게 사라진다.
                float distFromCenter = abs(input.uv.y - 0.5) * 2.0;
                float edge = 1.0 - smoothstep(1.0 - max(_SoftEdge, 0.01), 1.0, distFromCenter);

                float scrollUv = input.uv.x * max(_NoiseScale, 0.01) - _Time.y * _ScrollSpeed;
                float noise = FlowNoise(scrollUv) * 0.6 + FlowNoise(scrollUv * 2.13 + 5.0) * 0.4;
                float energy = lerp(0.6, 1.0, noise);

                float alpha = edge * _Color.a * energy * _LifeFade;
                float3 rgb = _Color.rgb * _GlowIntensity * energy;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
