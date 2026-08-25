using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// 근접·원거리 아군이 공통으로 사용하는 기본 공격. 사거리·쿨다운은 인스턴스가
    /// 결정하고, 이 효과는 공격 훅에서 실제 피해만 적용해 SO를 상태 없이 유지한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Basic Attack Effect")]
    public class BasicAttackEffect : AllyUnitEffectBase, IAllyUnitPrimaryAttackEffect
    {
        [Tooltip("원거리 유닛(attackRange가 ContactRange보다 큰 경우)에만 재생하는 가짜 투사체. " +
                 "attackRange가 ContactRange 이하로 보정되는 근접 유닛은 지금처럼 연출 없이 즉발 타격을 유지한다.")]
        [SerializeField] private GameObject fakeProjectilePrefab;

        public override void OnAttack(AllyUnitContext ctx, EnemyInstance target)
        {
            if (ctx == null || ctx.self == null || ctx.self.Data == null ||
                ctx.self.IsDead || target == null || !target.IsAlive)
            {
                return;
            }

            target.TakeDamage(ctx.self.Data.attackDamage * ctx.self.CalculateDamageMultiplier(), ctx.self.Position);

            // 승급 보스는 대상별 접촉 거리가 커진다. 고정 ContactRange와 비교하면 보스와 맞닿아
            // 공격하는 유닛도 원거리로 오인하므로 실제 대상 접촉 경계를 기준으로 연출을 고른다.
            if (ctx.self.Data.attackRange > target.GetContactRange(ctx.self))
            {
                FakeProjectile.Spawn(fakeProjectilePrefab, ctx.self.Position, target.position, ctx.self.Data);
            }
        }
    }
}
