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

            // 근접 유닛은 EffectiveAttackRange가 ContactRange로 보정돼 attackRange보다 커지므로
            // (AllyUnitInstance.EffectiveAttackRange), attackRange가 ContactRange를 실제로 넘는
            // 유닛만 "진짜 원거리"로 보고 투사체를 띄운다. 근접 유닛은 현행(연출 없는 즉발 타격)을
            // 그대로 유지한다 — {{user}} 확인.
            if (ctx.self.Data.attackRange > ctx.self.ContactRange)
            {
                FakeProjectile.Spawn(fakeProjectilePrefab, ctx.self.Position, target.position, ctx.self.Data);
            }
        }
    }
}
