using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// 근접·원거리 아군이 공통으로 사용하는 기본 공격. 사거리·쿨다운은 인스턴스가
    /// 결정하고, 이 효과는 공격 훅에서 실제 피해만 적용해 SO를 상태 없이 유지한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Basic Attack Effect")]
    public class BasicAttackEffect : AllyUnitEffectBase
    {
        public override void OnAttack(AllyUnitContext ctx, EnemyInstance target)
        {
            if (ctx == null || ctx.self == null || ctx.self.Data == null ||
                ctx.self.IsDead || target == null || !target.IsAlive)
            {
                return;
            }

            target.TakeDamage(ctx.self.Data.attackDamage);
        }
    }
}
