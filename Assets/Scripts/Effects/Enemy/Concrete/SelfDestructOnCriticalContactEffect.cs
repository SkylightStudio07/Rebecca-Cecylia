using RCCom.Core;
using RCCom.Runtime;

namespace RCCom.Effects.Enemy.Concrete
{
    /// <summary>
    /// 기본 ContactDamageEffect가 Studio의 contactDamage를 적용한 뒤, 플레이어나 최종 거점에
    /// 닿았을 때만 남은 체력과 무관하게 자신을 제거한다. 아군 유닛과의 교전은 기존 접촉 공격을
    /// 유지해야 전열에 막힌 채 아무 행동도 하지 않는 적이 되지 않는다.
    /// </summary>
    public sealed class SelfDestructOnCriticalContactEffect : EnemyEffectBase
    {
        public override void OnDealContactDamage(EnemyContext ctx, IDamageable target)
        {
            if (ctx?.self == null ||
                target is not PlayerController && target is not BaseController)
            {
                return;
            }

            ctx.self.KillImmediately();
        }
    }
}
