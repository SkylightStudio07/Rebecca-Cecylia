using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Effects.Tower;
using RCCom.Managers;
using RCCom.Runtime;
using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Effects.Tower.Concrete
{
    /// <summary>
    /// 카드13 "관통 사격 타워 해금" 전용 신규 공격 타워 효과. 가장 가까운 적 방향으로 가상의
    /// 직선을 긋고, 그 방향에서 beamHalfAngleDegrees 이내(=일직선 상)에 있는 사거리 내 모든
    /// 적에게 동시에 데미지를 입힌다 (DamageEffect가 최근접 1체만 때리는 것과 대비).
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Tower/Effects/Pierce Damage Effect")]
    public class PierceDamageEffect : TowerEffectBase
    {
        [SerializeField] private float beamHalfAngleDegrees = 10f;
        [Tooltip("설계안 §3-② — Inner Core + Outer Glow 2-Layer 빔. 이전엔 임시로 FakeProjectile을 썼다.")]
        [SerializeField] private GameObject laserBeamPrefab;
        [SerializeField] private LaserBeamVisualEffect laserBeamVisual;

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

            EnemyInstance nearest = EnemyTargeting.FindNearestInRange(ctx.activeEnemies, ctx.self.Position, data.attackRange);
            if (nearest == null)
            {
                return;
            }

            Vector2 beamDirection = (nearest.position - ctx.self.Position).normalized;
            float damage = TowerDamageMath.CalculateDamage(ctx.self, data.damage);

            // ctx.activeEnemies는 TowerInstance._enemiesInRange를 그대로 참조한다 — 이 루프 중
            // enemy.TakeDamage()가 즉사시키면 EnemyView.HandleDied()가 콜라이더를 끄면서
            // OnTriggerExit2D가 동기 발생해 같은 프레임에 그 리스트에서 항목이 빠질 수 있다.
            // 원본을 직접 순회하면 "Collection was modified" 예외가 나므로 스냅샷을 떠서 돈다.
            var targets = new List<EnemyInstance>(ctx.activeEnemies);
            foreach (EnemyInstance enemy in targets)
            {
                Vector2 toEnemy = enemy.position - ctx.self.Position;
                if (toEnemy.magnitude > data.attackRange)
                {
                    continue;
                }

                if (Vector2.Angle(beamDirection, toEnemy) <= beamHalfAngleDegrees)
                {
                    enemy.TakeDamage(damage);
                }
            }

            Vector3 beamEnd = ctx.self.Position + beamDirection * data.attackRange;
            LaserBeamView.Spawn(laserBeamPrefab, ctx.self.Position, beamEnd, laserBeamVisual);

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayTowerAttack(ctx.self.Position);
            }

            ctx.self.cooldownRemaining = TowerDamageMath.CalculateAttackInterval(ctx.self, data.attackInterval);
        }
    }
}
