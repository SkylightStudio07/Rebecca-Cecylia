using RCCom.Data;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// 피트크루 로버가 attackRange를 오라 반경으로 재사용해 주변 아군을 초당 지속 수리(힐)한다.
    /// 정지 상태로 교전 중인 아군(Engaging)에게는 피트스톱 보너스 배율을 적용해
    /// 차단선 유지력을 극대화한다. TacticalRelayAuraEffect와 동일하게 SO는 상태를 갖지 않고
    /// 매 틱 AllyUnitContext 매개변수만으로 계산한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Pit Crew Repair Aura Effect")]
    public class PitCrewRepairAuraEffect : AllyUnitEffectBase
    {
        [Header("수리량 설정")]
        [Tooltip("초당 기본 수리(회복)량")]
        [SerializeField, Min(0f)] private float repairPerSecond = 5f;

        [Header("피트스톱 기믹")]
        [Tooltip("정지하여 교전 중(Engaging)인 아군에게 적용할 수리량 배율")]
        [SerializeField, Min(1f)] private float pitStopMultiplier = 2.0f;

        public float RepairPerSecond => repairPerSecond;
        public float PitStopMultiplier => pitStopMultiplier;

        /// <summary>
        /// 오퍼레이터 강화 적용 전용. 공유 원본 에셋이 아니라 OperatorUpgradeApplier가
        /// Instantiate로 만든 런타임 복제본에만 호출해야 한다.
        /// </summary>
        internal void ApplyRuntimeOverride(float repairPerSecond, float pitStopMultiplier)
        {
            this.repairPerSecond = Mathf.Max(0f, repairPerSecond);
            this.pitStopMultiplier = Mathf.Max(1f, pitStopMultiplier);
        }

        public override void OnTick(AllyUnitContext ctx)
        {
            if (ctx == null || ctx.self == null || ctx.self.Data == null || ctx.activeAllies == null ||
                ctx.deltaTime <= 0f || repairPerSecond <= 0f)
            {
                return;
            }

            float range = Mathf.Max(0f, ctx.self.Data.attackRange);
            float rangeSquared = range * range;
            float baseHeal = repairPerSecond * ctx.deltaTime;

            foreach (AllyUnitInstance ally in ctx.activeAllies)
            {
                // 생존 검증 및 자가 수리 제외. TacticalRelayAuraEffect와 동일한 자가 제외 규칙을 따른다.
                if (ally == null || !ally.IsAlive || ReferenceEquals(ally, ctx.self))
                {
                    continue;
                }

                // 이미 최대 체력인 대상은 거리 계산 전에 걸러 불필요한 연산을 피한다.
                if (ally.Data != null && ally.CurrentHealth >= ally.Data.maxHealth)
                {
                    continue;
                }

                if ((ally.Position - ctx.self.Position).sqrMagnitude > rangeSquared)
                {
                    continue;
                }

                // 피트스톱 보너스: 정지한 채 접촉 전열에서 교전 중인 상태에만 배율을 적용한다.
                float multiplier = ally.State == AllyUnitState.Engaging ? pitStopMultiplier : 1f;
                ally.Heal(baseHeal * multiplier);
            }
        }
    }
}
