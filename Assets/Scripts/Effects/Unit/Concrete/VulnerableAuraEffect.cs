using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// 약화 드론(aurora-debuff-drone) 전용 효과. Tower.Concrete.VulnerableAuraEffect와 동일한
    /// 패턴 — attackRange를 오라 반경으로 재사용해 사거리 내 "적"이 받는 피해를 증가시킨다.
    /// AllyUnitContext.activeEnemies는 Tower와 달리 사거리로 미리 걸러져 있지 않으므로,
    /// TacticalRelayAuraEffect와 동일하게 매 틱 자체적으로 거리 필터링 후 짧은 지속시간의
    /// 취약 상태를 계속 갱신한다 (사거리 이탈 시 자연 만료).
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Vulnerable Aura Effect")]
    public class VulnerableAuraEffect : AllyUnitEffectBase
    {
        [SerializeField, Min(1f)] private float damageTakenMultiplier = 1.3f;
        [SerializeField, Min(0.01f)] private float refreshDuration = 0.5f;

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

                enemy.ApplyVulnerable(damageTakenMultiplier, refreshDuration);
            }
        }
    }
}
