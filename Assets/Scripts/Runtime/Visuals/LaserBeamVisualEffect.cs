using UnityEngine;

namespace RCCom.Runtime.Visuals
{
    /// <summary>
    /// 관통 사격 타워의 2-Layer 빔(<see cref="LaserBeamView"/>, 설계안 §3-②) 시각 튜닝값.
    /// <see cref="ShockwaveRingVisualEffect"/>와 같은 이유로 데이터 SO로 분리한다 — 색/폭/글로우/
    /// 노이즈/지속시간을 코드가 아니라 이 에셋에서 조정할 수 있어야 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/VFX/Laser Beam Visual Effect")]
    public class LaserBeamVisualEffect : ScriptableObject
    {
        [Header("Inner Core (얇고 밝은 백색)")]
        [SerializeField, ColorUsage(true, true)] private Color innerColor = new(2.4f, 2.4f, 2.7f, 1f);
        [SerializeField, Min(0.01f)] private float innerWidth = 0.06f;
        [SerializeField, Range(0f, 3f)] private float innerGlowIntensity = 1.4f;

        [Header("Outer Glow (넓고 반투명, 테마색)")]
        [SerializeField, ColorUsage(true, true)] private Color outerColor = new(0.75f, 0.3f, 1.35f, 0.55f);
        [SerializeField, Min(0.01f)] private float outerWidth = 0.28f;
        [SerializeField, Range(0f, 3f)] private float outerGlowIntensity = 1.1f;

        [Header("공통 (두 레이어에 동일 적용)")]
        [SerializeField, Range(0.01f, 1f)] private float softEdge = 0.4f;
        [SerializeField, Min(0f)] private float noiseScale = 6f;
        [SerializeField, Min(0f)] private float scrollSpeed = 3.5f;
        [Tooltip("빔이 표시되는 총 시간(초). 발사 직후 폭 팬치 애니메이션 + 잔여시간 알파 페이드에 쓰인다.")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.22f;
        [Tooltip("발사 직후 폭 배율(150% = 1.5) — pinchDuration에 걸쳐 100%로 줄어든다.")]
        [SerializeField, Range(1f, 3f)] private float pinchStartMultiplier = 1.5f;
        [SerializeField, Min(0.01f)] private float pinchDuration = 0.08f;

        public Color InnerColor => innerColor;
        public float InnerWidth => Mathf.Max(0.01f, innerWidth);
        public float InnerGlowIntensity => Mathf.Max(0f, innerGlowIntensity);

        public Color OuterColor => outerColor;
        public float OuterWidth => Mathf.Max(0.01f, outerWidth);
        public float OuterGlowIntensity => Mathf.Max(0f, outerGlowIntensity);

        public float SoftEdge => Mathf.Clamp(softEdge, 0.01f, 1f);
        public float NoiseScale => Mathf.Max(0f, noiseScale);
        public float ScrollSpeed => Mathf.Max(0f, scrollSpeed);
        public float Lifetime => Mathf.Max(0.05f, lifetime);
        public float PinchStartMultiplier => Mathf.Clamp(pinchStartMultiplier, 1f, 3f);
        public float PinchDuration => Mathf.Max(0.01f, pinchDuration);
    }
}
