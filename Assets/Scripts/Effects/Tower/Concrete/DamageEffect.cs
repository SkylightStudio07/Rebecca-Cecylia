using RCCom.Core;
using RCCom.Data;
using RCCom.Effects.Tower;
using RCCom.Managers;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Tower.Concrete
{
    /// <summary>
    /// 공격 타워 기본 효과: 사거리 내 가장 가까운 적에게 주기적으로 데미지를 입힌다.
    /// 명중 시 짧은 라인 이펙트(AttackFlash)만 띄우고, 그 외 투사체 시각 표현은 다루지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Tower/Effects/Damage Effect")]
    public class DamageEffect : TowerEffectBase
    {
        [SerializeField] private GameObject attackFlashPrefab;
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

            target.TakeDamage(TowerDamageMath.CalculateDamage(ctx.self, data.damage));
            AttackFlash.Spawn(attackFlashPrefab, ctx.self.Position, target.position);
            FakeProjectile.Spawn(fakeProjectilePrefab, ctx.self.Position, target.position);

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayTowerAttack(ctx.self.Position);
            }

            ctx.self.cooldownRemaining = TowerDamageMath.CalculateAttackInterval(ctx.self, data.attackInterval);
        }
    }
}
