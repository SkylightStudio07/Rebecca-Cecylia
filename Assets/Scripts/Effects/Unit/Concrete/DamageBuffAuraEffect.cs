using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// TacticalRelayAuraEffect(이동/공속)와 짝을 이루는 피해량 전용 오라. attackRange를 오라
    /// 반경으로 재사용해 주변 아군의 공격력을 높인다. 매 틱 짧은 버프를 갱신하므로 범위를
    /// 벗어난 대상은 별도 이탈 훅 없이 만료된다(TacticalRelayAuraEffect와 동일한 패턴).
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Damage Buff Aura Effect")]
    public class DamageBuffAuraEffect : AllyUnitEffectBase
    {
        [SerializeField, Min(1f)] private float damageMultiplier = 1.25f;
        [SerializeField, Min(0.01f)] private float refreshDuration = 0.2f;

        public float DamageMultiplier => damageMultiplier;

        /// <summary>
        /// 오퍼레이터 강화 적용 전용. 공유 원본 에셋이 아니라 OperatorUpgradeApplier가
        /// Instantiate로 만든 런타임 복제본에만 호출해야 한다.
        /// </summary>
        internal void ApplyRuntimeOverride(float damageMultiplier)
        {
            this.damageMultiplier = Mathf.Max(1f, damageMultiplier);
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

                ally.ApplyStatMultipliers(ctx.self, this, 1f, 1f, damageMultiplier, refreshDuration);
            }
        }
    }
}
