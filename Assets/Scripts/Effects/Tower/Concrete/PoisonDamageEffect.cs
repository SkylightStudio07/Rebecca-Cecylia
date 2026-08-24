using RCCom.Core;
using RCCom.Data;
using RCCom.Effects.Tower;
using RCCom.Managers;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Tower.Concrete
{
    /// <summary>
    /// 공격 타워 신규 효과("맹독"): 명중 시 즉발 피해에 더해, 지속시간 동안 매초 고정 피해를
    /// 추가로 입힌다(DoT). 계속 사거리 안에서 재적중하면 EnemyInstance.ApplyPoison이 지속시간을
    /// 갱신한다 — SlowAuraEffect의 갱신 방식과 동일한 철학.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Tower/Effects/Poison Damage Effect")]
    public class PoisonDamageEffect : TowerEffectBase
    {
        [SerializeField] private float poisonDamagePerSecond = 3f;
        [SerializeField] private float poisonDuration = 3f;
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
            target.ApplyPoison(poisonDamagePerSecond, poisonDuration);
            FakeProjectile.Spawn(fakeProjectilePrefab, ctx.self.Position, target.position);

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayTowerAttack(ctx.self.Position);
            }

            ctx.self.cooldownRemaining = TowerDamageMath.CalculateAttackInterval(ctx.self, data.attackInterval);
        }
    }
}
