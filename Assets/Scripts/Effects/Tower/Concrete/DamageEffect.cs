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
    /// 명중 연출은 FakeProjectile(가짜 투사체)이 전담한다 — 예전 AttackFlash 라인 이펙트는
    /// 투사체 도입 후 시각적으로 중첩되기만 해서 제거했다(전투 VFX 강화 1단계 후속 정리).
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Tower/Effects/Damage Effect")]
    public class DamageEffect : TowerEffectBase
    {
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

            target.TakeDamage(TowerDamageMath.CalculateDamage(ctx.self, data.damage), ctx.self.Position);
            FakeProjectile.Spawn(fakeProjectilePrefab, ctx.self.Position, target.position);

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayTowerAttack(ctx.self.Position);
            }

            ctx.self.cooldownRemaining = TowerDamageMath.CalculateAttackInterval(ctx.self, data.attackInterval);
        }
    }
}
