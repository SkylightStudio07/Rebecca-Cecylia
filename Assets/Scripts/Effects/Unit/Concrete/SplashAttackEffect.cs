using System.Collections.Generic;
using RCCom.Core;
using RCCom.Runtime;
using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// Tower.Concrete.SplashDamageEffect와 동일한 반경(스플래시) 판정을 아군 유닛에 이식한
    /// 버전. 주 타겟에게 기본 피해를 입히고, splashRadius 내 다른 적에게는 감쇠된 추가 피해를
    /// 동시에 준다. Tower 버전과 달리 사거리·쿨다운·주 타겟 선정은 AllyUnitInstance가 이미
    /// 끝내고 OnAttack으로 타겟을 넘겨준다(BasicAttackEffect 참고). BasicAttackEffect/
    /// PierceAttackEffect와 배타적으로 한 유닛 Definition에 하나만 붙인다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Splash Attack Effect")]
    public class SplashAttackEffect : AllyUnitEffectBase
    {
        [SerializeField] private float splashRadius = 1.5f;
        [SerializeField] private float splashDamageMultiplier = 0.5f;

        [Tooltip("원거리 유닛(attackRange가 ContactRange보다 큰 경우)에만 재생하는 가짜 투사체. " +
                 "BasicAttackEffect와 동일한 근접/원거리 판정 규칙을 따른다.")]
        [SerializeField] private GameObject fakeProjectilePrefab;
        [Tooltip("포물선 낙하 궤적의 정점 높이(월드 단위). 0이면 직선 투사체가 된다.")]
        [SerializeField] private float lobHeight = 0.6f;

        [Header("착탄 연출 — 판정과 무관한 순수 연출")]
        [SerializeField] private GameObject explosionBurstPrefab;
        [SerializeField] private GameObject shockwaveRingPrefab;
        [SerializeField] private ShockwaveRingVisualEffect shockwaveVisual;
        [SerializeField] private GameObject scorchDecalPrefab;

        public override void OnAttack(AllyUnitContext ctx, EnemyInstance target)
        {
            if (ctx == null || ctx.self == null || ctx.self.Data == null ||
                ctx.self.IsDead || target == null || !target.IsAlive)
            {
                return;
            }

            float damage = ctx.self.Data.attackDamage * ctx.self.CalculateDamageMultiplier();
            target.TakeDamage(damage, ctx.self.Position);

            if (ctx.self.Data.attackRange > ctx.self.ContactRange)
            {
                FakeProjectile.Spawn(
                    fakeProjectilePrefab, ctx.self.Position, target.position,
                    ctx.self.Data.projectileSpeed, lobHeight);
            }

            // 착탄 연출 3종은 판정과 완전히 분리된 순수 시각 효과라, 투사체의 실제 비행 시간을
            // 기다리지 않고 이 시점에 바로 재생한다(SplashDamageEffect와 동일한 타이밍).
            ParticleBurst.Spawn(explosionBurstPrefab, target.position);
            ShockwaveRing.Spawn(shockwaveRingPrefab, target.position, splashRadius, shockwaveVisual);
            ScorchDecal.Spawn(scorchDecalPrefab, target.position, splashRadius);

            float splashDamage = damage * splashDamageMultiplier;
            List<EnemyInstance> splashTargets = SplashAttackMath.GetSplashTargets(
                ctx.activeEnemies, target, target.position, splashRadius);
            foreach (EnemyInstance enemy in splashTargets)
            {
                enemy.TakeDamage(splashDamage, target.position);
            }
        }
    }
}
