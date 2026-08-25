using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// Tower.Concrete.PoisonDamageEffect와 동일한 맹독(DoT) 처리를 아군 유닛에 이식한 버전.
    /// 명중 시 즉발 피해에 더해 poisonDuration 동안 매초 poisonDamagePerSecond를 추가로 입힌다.
    /// 계속 사거리 안에서 재적중하면 EnemyInstance.ApplyPoison이 지속시간을 갱신한다(SlowAuraEffect와
    /// 동일한 철학). BasicAttackEffect/PierceAttackEffect/SplashAttackEffect와 배타적으로 한 유닛
    /// Definition에 하나만 붙인다 — 이 효과 자체가 주 타격(즉발 피해)까지 겸한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Poison Attack Effect")]
    public class PoisonAttackEffect : AllyUnitEffectBase, IAllyUnitPrimaryAttackEffect
    {
        [SerializeField] private float poisonDamagePerSecond = 3f;
        [SerializeField] private float poisonDuration = 3f;

        [Tooltip("원거리 유닛(attackRange가 ContactRange보다 큰 경우)에만 재생하는 가짜 투사체. " +
                 "BasicAttackEffect와 동일한 근접/원거리 판정 규칙을 따른다.")]
        [SerializeField] private GameObject fakeProjectilePrefab;

        public override void OnAttack(AllyUnitContext ctx, EnemyInstance target)
        {
            if (ctx == null || ctx.self == null || ctx.self.Data == null ||
                ctx.self.IsDead || target == null || !target.IsAlive)
            {
                return;
            }

            target.TakeDamage(ctx.self.Data.attackDamage * ctx.self.CalculateDamageMultiplier(), ctx.self.Position);
            target.ApplyPoison(poisonDamagePerSecond, poisonDuration);

            if (ctx.self.Data.attackRange > target.GetContactRange(ctx.self))
            {
                FakeProjectile.Spawn(fakeProjectilePrefab, ctx.self.Position, target.position, ctx.self.Data);
            }
        }
    }
}
