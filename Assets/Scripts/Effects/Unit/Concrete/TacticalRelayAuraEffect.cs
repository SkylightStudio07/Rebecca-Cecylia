using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// 전술 중계 드론이 공격 사거리를 오라 반경으로 재사용해 주변 아군의 이동·공격 속도를
    /// 높인다. 매 틱 짧은 버프를 갱신하므로 범위를 벗어난 대상은 별도 이탈 훅 없이 만료된다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Tactical Relay Aura Effect")]
    public class TacticalRelayAuraEffect : AllyUnitEffectBase
    {
        [SerializeField, Min(1f)] private float moveSpeedMultiplier = 1.2f;
        [SerializeField, Min(1f)] private float attackSpeedMultiplier = 1.2f;
        [SerializeField, Min(0.01f)] private float refreshDuration = 0.2f;

        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public float AttackSpeedMultiplier => attackSpeedMultiplier;

        /// <summary>
        /// 오퍼레이터 강화 적용 전용. 공유 원본 에셋이 아니라 OperatorUpgradeApplier가
        /// Instantiate로 만든 런타임 복제본에만 호출해야 한다.
        /// </summary>
        internal void ApplyRuntimeOverride(float moveSpeedMultiplier, float attackSpeedMultiplier)
        {
            this.moveSpeedMultiplier = Mathf.Max(1f, moveSpeedMultiplier);
            this.attackSpeedMultiplier = Mathf.Max(1f, attackSpeedMultiplier);
        }

        public override void OnTick(AllyUnitContext ctx)
        {
            if (ctx == null || ctx.self == null || ctx.self.Data == null || ctx.activeAllies == null)
            {
                return;
            }

            float range = Mathf.Max(0f, ctx.self.Data.attackRange);
            float rangeSquared = range * range;
            foreach (AllyUnitInstance ally in ctx.activeAllies)
            {
                if (ally == null || !ally.IsAlive || ReferenceEquals(ally, ctx.self))
                {
                    continue;
                }

                if ((ally.Position - ctx.self.Position).sqrMagnitude > rangeSquared)
                {
                    continue;
                }

                ally.ApplyStatMultipliers(
                    ctx.self,
                    this,
                    moveSpeedMultiplier,
                    attackSpeedMultiplier,
                    refreshDuration);
            }
        }
    }
}
