using RCCom.Effects.Tower;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// TacticalRelayAuraEffect(아군 유닛 대상)와 대칭되는 구조로, attackRange를 오라 반경 삼아
    /// 사거리 내 아군 타워의 공격력/공속을 높인다. 타워는 유닛처럼 매 틱 갱신되는 버프 저장소가
    /// 없었으므로 TowerInstance.ApplyTemporaryAura(지속시간 기반 임시 오라)를 새로 얹어 쓴다 —
    /// 스킬 타워의 AuraBuffEffect(콜라이더 출입 이벤트로 상시 등록)와는 별개 파이프라인이며,
    /// TowerDamageMath가 계산 시점에 둘 다 조회해 곱연산으로 합성한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Tower Reinforcement Aura Effect")]
    public class TowerReinforcementAuraEffect : AllyUnitEffectBase
    {
        [SerializeField, Min(1f)] private float damageMultiplier = 1.2f;
        [SerializeField, Min(1f)] private float attackSpeedMultiplier = 1.2f;
        [SerializeField, Min(0.01f)] private float refreshDuration = 0.2f;

        public float DamageMultiplier => damageMultiplier;
        public float AttackSpeedMultiplier => attackSpeedMultiplier;

        /// <summary>
        /// 오퍼레이터 강화 적용 전용. 공유 원본 에셋이 아니라 OperatorUpgradeApplier가
        /// Instantiate로 만든 런타임 복제본에만 호출해야 한다.
        /// </summary>
        internal void ApplyRuntimeOverride(float damageMultiplier, float attackSpeedMultiplier)
        {
            this.damageMultiplier = Mathf.Max(1f, damageMultiplier);
            this.attackSpeedMultiplier = Mathf.Max(1f, attackSpeedMultiplier);
        }

        public override void OnTick(AllyUnitContext ctx)
        {
            if (ctx == null || ctx.self == null || ctx.self.Data == null || ctx.activeTowers == null)
            {
                return;
            }

            float range = Mathf.Max(0f, ctx.self.Data.attackRange);
            float rangeSquared = range * range;
            foreach (TowerInstance tower in ctx.activeTowers)
            {
                if (tower == null)
                {
                    continue;
                }

                if ((tower.Position - ctx.self.Position).sqrMagnitude > rangeSquared)
                {
                    continue;
                }

                tower.ApplyTemporaryAura(
                    ctx.self, this, new SimpleTowerAura(damageMultiplier, attackSpeedMultiplier), refreshDuration);
            }
        }
    }
}
