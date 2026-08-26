using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Enemy.Concrete
{
    /// <summary>
    /// 자신의 attackRange 안에 있는 다른 적의 최대 체력을 지속적으로 높인다. Effect SO는
    /// 설정만 보유하고 실제 제공자별 만료 상태는 버프를 받는 EnemyInstance가 소유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Enemy/Effects/Max Health Aura Effect")]
    public sealed class MaxHealthAuraEffect : EnemyEffectBase, IEnemyRangeAuraVisualEffect
    {
        [Tooltip("범위 안 적에게 적용할 최대 체력 배율. 1.1은 10% 증가입니다.")]
        [SerializeField, Min(0f)] private float healthMultiplier = 1.1f;

        [Tooltip("매 틱 갱신이 끊긴 뒤 버프가 사라지기까지의 짧은 유예 시간")]
        [SerializeField, Min(0.01f)] private float refreshDuration = 0.2f;

        [Tooltip("활성화하면 방어 유닛 자신도 최대 체력 오라를 받습니다.")]
        [SerializeField] private bool includeSelf;

        [Header("지속 범위 연출")]
        [SerializeField] private Material material;
        [SerializeField, ColorUsage(true, true)] private Color auraColor =
            new(0.18f, 1.15f, 0.35f, 0.75f);
        [SerializeField, Range(0.002f, 0.08f)] private float strokeWidth = 0.018f;
        [SerializeField, Range(0f, 2f)] private float glowIntensity = 0.65f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.75f;

        public float HealthMultiplier => Mathf.Max(0f, healthMultiplier);
        public float RefreshDuration => Mathf.Max(0.01f, refreshDuration);
        public bool IncludeSelf => includeSelf;
        public Material Material => material;
        public Color AuraColor => auraColor;
        public float StrokeWidth => Mathf.Clamp(strokeWidth, 0.002f, 0.08f);
        public float GlowIntensity => Mathf.Max(0f, glowIntensity);
        public float Opacity => Mathf.Clamp01(opacity);

        public override void OnTick(EnemyContext ctx)
        {
            if (ctx == null || ctx.self == null || ctx.activeEnemies == null)
            {
                return;
            }

            float range = Mathf.Max(0f, ctx.self.Data.attackRange);
            float rangeSquared = range * range;
            for (int i = 0; i < ctx.activeEnemies.Count; i++)
            {
                EnemyInstance target = ctx.activeEnemies[i];
                if (target == null || !target.IsAlive || (!includeSelf && target == ctx.self))
                {
                    continue;
                }

                if ((target.position - ctx.self.position).sqrMagnitude > rangeSquared)
                {
                    continue;
                }

                target.ApplyMaxHealthAura(
                    ctx.self,
                    this,
                    HealthMultiplier,
                    RefreshDuration);
            }
        }
    }
}
