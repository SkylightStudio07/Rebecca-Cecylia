using RCCom.Core;
using RCCom.Data;
using RCCom.Effects.Tower;
using RCCom.Managers;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Tower.Concrete
{
    /// <summary>
    /// 공격 타워 신규 효과("폭발탄"): 가장 가까운 적에게 기본 데미지를 입히고, 그 주변
    /// splashRadius 내 다른 적들에게는 splashDamageMultiplier만큼 감쇠된 추가 피해를 동시에
    /// 입힌다. DamageEffect와 파이프라인(쿨다운/타겟팅/데미지 계산)은 동일하고 명중 처리만 다르다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Tower/Effects/Splash Damage Effect")]
    public class SplashDamageEffect : TowerEffectBase
    {
        [SerializeField] private float splashRadius = 1.5f;
        [SerializeField] private float splashDamageMultiplier = 0.5f;
        [SerializeField] private GameObject fakeProjectilePrefab;

        public override void OnTick(TowerContext ctx)
        {
            if (ctx.self.Data is not AttackTowerData data)
            {
                return;
            }

            ctx.self.cooldownRemaining -= ctx.deltaTime;
            if (ctx.self.cooldownRemaining > 0f)
            {
                return;
            }

            EnemyInstance target = EnemyTargeting.FindNearestInRange(ctx.activeEnemies, ctx.self.Position, data.attackRange);
            if (target == null)
            {
                return;
            }

            float damage = TowerDamageMath.CalculateDamage(ctx.self, data.damage);
            target.TakeDamage(damage);
            FakeProjectile.Spawn(fakeProjectilePrefab, ctx.self.Position, target.position);

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayTowerAttack(ctx.self.Position);
            }

            float splashDamage = damage * splashDamageMultiplier;
            float splashRadiusSqr = splashRadius * splashRadius;

            foreach (EnemyInstance enemy in ctx.activeEnemies)
            {
                if (enemy == target)
                {
                    continue;
                }

                if ((enemy.position - target.position).sqrMagnitude <= splashRadiusSqr)
                {
                    enemy.TakeDamage(splashDamage);
                }
            }

            ctx.self.cooldownRemaining = TowerDamageMath.CalculateAttackInterval(ctx.self, data.attackInterval);
        }
    }
}
