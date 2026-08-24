using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Effects.UnitVisual.Concrete
{
    /// <summary>
    /// attackRange를 최대 반경으로 삼는 공용 오라 파동 설정. 버프·힐·디버프는 별도 클래스가
    /// 아니라 서로 다른 색과 주기를 가진 이 SO 에셋을 조립해 구분한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Visual Effects/Range Pulse Visual Effect")]
    public class RangePulseVisualEffect : AllyUnitVisualEffectBase
    {
        [SerializeField] private Material material;
        [SerializeField, ColorUsage(true, true)] private Color auraColor =
            new(0.15f, 0.85f, 1.2f, 0.8f);
        [SerializeField, Min(0.05f)] private float pulseDuration = 0.85f;
        [SerializeField, Min(0f)] private float pulseInterval = 0.3f;
        [SerializeField, Range(0.002f, 0.08f)] private float strokeWidth = 0.012f;
        [SerializeField, Range(0f, 2f)] private float glowIntensity = 0.8f;
        [SerializeField, Range(0f, 1f)] private float glossIntensity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.8f;

        public Material Material => material;
        public Color AuraColor => auraColor;
        public float PulseDuration => Mathf.Max(0.05f, pulseDuration);
        public float PulseInterval => Mathf.Max(0f, pulseInterval);
        public float StrokeWidth => Mathf.Clamp(strokeWidth, 0.002f, 0.08f);
        public float GlowIntensity => Mathf.Max(0f, glowIntensity);
        public float GlossIntensity => Mathf.Clamp01(glossIntensity);
        public float Opacity => Mathf.Clamp01(opacity);

        public override IAllyUnitVisualRuntime CreateRuntime(AllyUnitVisualContext ctx)
        {
            if (ctx == null || ctx.view == null || ctx.instance == null || material == null)
            {
                return null;
            }

            return new RangePulseVisualRuntime(this, ctx);
        }
    }
}
