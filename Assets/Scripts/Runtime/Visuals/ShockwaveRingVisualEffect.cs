using UnityEngine;

namespace RCCom.Runtime.Visuals
{
    /// <summary>
    /// 위치 기반 원샷 충격파 링(<see cref="ShockwaveRing"/>)의 시각 튜닝값을 담는 데이터 SO.
    ///
    /// 아군 사거리 오라 파동(<see cref="RangePulseVisualEffect"/>)이 색/스트로크/글로우/글로시/
    /// 불투명도/주기를 전부 SO 필드로 노출해 코드를 안 건드리고 에셋만 갈아 끼워 버프·힐·디버프
    /// 색을 구분하는 것과 같은 이유로 이 SO를 둔다 — <see cref="ShockwaveRing"/>도 같은
    /// RangePulseAura.shader를 쓰면서 처음엔 이 값들을 코드/빌더에 하드코딩해뒀었는데("니
    /// 좆대로 구현하냐"는 정당한 지적), 이미 확립된 이 패턴을 그대로 따르지 않은 실수였다.
    /// <see cref="RangePulseVisualEffect"/>를 그대로 상속/재사용하지 않는 이유는 그쪽이
    /// <c>AllyUnitVisualEffectBase</c>(아군 유닛 하나에 상시 붙어 반복 재생되는 오라 전용
    /// IAllyUnitVisualRuntime 훅 계층)에 묶여 있어서다 — 이쪽은 위치 기반 원샷이라 그 계층에
    /// 낄 이유가 없는 순수 데이터 SO로 별도로 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/VFX/Shockwave Ring Visual Effect")]
    public class ShockwaveRingVisualEffect : ScriptableObject
    {
        [SerializeField, ColorUsage(true, true)] private Color color = new(1f, 0.28f, 0.12f, 0.9f);
        [Tooltip("링이 반경 끝까지 확산하는 데 걸리는 시간(초). ShockwaveRing이 0.15~0.2초로 다시 clamp한다.")]
        [SerializeField, Min(0.05f)] private float expandDuration = 0.18f;
        [SerializeField, Range(0.002f, 0.08f)] private float strokeWidth = 0.02f;
        [SerializeField, Range(0f, 2f)] private float glowIntensity = 1.1f;
        [SerializeField, Range(0f, 1f)] private float glossIntensity = 0.3f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.9f;

        public Color Color => color;
        public float ExpandDuration => Mathf.Max(0.05f, expandDuration);
        public float StrokeWidth => Mathf.Clamp(strokeWidth, 0.002f, 0.08f);
        public float GlowIntensity => Mathf.Max(0f, glowIntensity);
        public float GlossIntensity => Mathf.Clamp01(glossIntensity);
        public float Opacity => Mathf.Clamp01(opacity);
    }
}
