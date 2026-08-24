using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// 감속 드론(aurora-slow-drone) 전용 효과. Tower.Concrete.SlowAuraEffect와 동일한 패턴 —
    /// attackRange를 오라 반경으로 재사용해 사거리 내 "적"의 이동속도를 감소시킨다.
    /// VulnerableAuraEffect(Unit)와 동일하게 매 틱 자체적으로 거리 필터링 후 짧은 지속시간의
    /// 슬로우를 계속 갱신한다 (사거리 이탈 시 자연 만료).
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Slow Aura Effect")]
    public class SlowAuraEffect : AllyUnitEffectBase
    {
        [SerializeField, Range(0f, 1f)] private float speedMultiplier = 0.625f;
        [SerializeField, Min(0.01f)] private float refreshDuration = 0.8f;

        public float SpeedMultiplier => speedMultiplier;

        /// <summary>
        /// 오퍼레이터 강화 적용 전용. 공유 원본 에셋이 아니라 OperatorUpgradeApplier가
        /// Instantiate로 만든 런타임 복제본에만 호출해야 한다. speedMultiplier는 낮을수록 강한
        /// 감속이므로 강화 델타는 음수로 전달된다 (§7.6과 동일한 역방향 지표).
        /// </summary>
        internal void ApplyRuntimeOverride(float speedMultiplier)
        {
            this.speedMultiplier = Mathf.Clamp01(speedMultiplier);
        }

        public override void OnTick(AllyUnitContext ctx)
        {
            if (ctx == null || ctx.self == null || ctx.self.Data == null || ctx.activeEnemies == null)
            {
                return;
            }

            float range = Mathf.Max(0f, ctx.self.Data.attackRange);
            float rangeSquared = range * range;
            foreach (EnemyInstance enemy in ctx.activeEnemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                if ((enemy.position - ctx.self.Position).sqrMagnitude > rangeSquared)
                {
                    continue;
                }

                enemy.ApplySpeedMultiplier(speedMultiplier, refreshDuration);
            }
        }
    }
}
