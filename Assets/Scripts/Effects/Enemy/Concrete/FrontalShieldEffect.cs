using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Enemy.Concrete
{
    /// <summary>
    /// 이동 방향 기준 앞쪽 반원에서 들어오는 직접 피해를 줄인다. 방향을 알 수 없는 독 틱과
    /// 측면·후면 공격은 그대로 받아 위치 선정으로 대응할 여지를 남긴다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Enemy/Effects/Frontal Shield Effect")]
    public sealed class FrontalShieldEffect : EnemyEffectBase,
        IEnemyIncomingDamageModifier,
        IEnemyFrontShieldVisualEffect
    {
        [Tooltip("정면 직접 피해에 곱할 배율. 0.5는 50% 감소입니다.")]
        [SerializeField, Range(0f, 1f)] private float damageMultiplier = 0.5f;

        [Header("정면 반원 방어막 연출")]
        [SerializeField] private Material material;
        [SerializeField, ColorUsage(true, true)] private Color shieldColor =
            new(0.12f, 0.65f, 1.35f, 0.82f);
        [SerializeField, Min(0.1f)] private float shieldRadius = 2.15f;
        [SerializeField, Range(0.002f, 0.12f)] private float strokeWidth = 0.025f;
        [SerializeField, Range(0f, 2f)] private float glowIntensity = 0.9f;
        [SerializeField, Range(0f, 1f)] private float fillOpacity = 0.12f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.82f;

        public float DamageMultiplier => Mathf.Clamp01(damageMultiplier);
        public Material Material => material;
        public Color ShieldColor => shieldColor;
        public float ShieldRadius => Mathf.Max(0.1f, shieldRadius);
        public float StrokeWidth => Mathf.Clamp(strokeWidth, 0.002f, 0.12f);
        public float GlowIntensity => Mathf.Max(0f, glowIntensity);
        public float FillOpacity => Mathf.Clamp01(fillOpacity);
        public float Opacity => Mathf.Clamp01(opacity);

        public float ModifyIncomingDamage(
            EnemyContext ctx,
            float incomingDamage,
            Vector2? sourcePosition)
        {
            if (ctx == null || ctx.self == null || !sourcePosition.HasValue || incomingDamage <= 0f)
            {
                return incomingDamage;
            }

            Vector2 toSource = sourcePosition.Value - ctx.self.position;
            if (toSource.sqrMagnitude <= 0.0001f)
            {
                return incomingDamage;
            }

            float facingDot = Vector2.Dot(ctx.self.FacingDirection, toSource.normalized);
            return facingDot > 0.0001f
                ? incomingDamage * DamageMultiplier
                : incomingDamage;
        }
    }
}
