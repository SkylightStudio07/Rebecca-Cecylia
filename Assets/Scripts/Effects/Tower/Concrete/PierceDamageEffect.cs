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

            // PierceAttackMath.GetTargetsInBeam은 후보 목록을 읽기만 하는 순수 함수이므로, 아래
            // 루프에서 enemy.TakeDamage()가 즉사시켜 ctx.activeEnemies(TowerInstance._enemiesInRange)
            // 원본이 바뀌어도(콜라이더 비활성화 → OnTriggerExit2D) 이미 별도로 담아둔 targets
            // 리스트는 영향을 받지 않는다.
            List<EnemyInstance> targets = PierceAttackMath.GetTargetsInBeam(
                ctx.activeEnemies, ctx.self.Position, beamDirection, data.attackRange, beamHalfAngleDegrees);
            foreach (EnemyInstance enemy in targets)
            {
                enemy.TakeDamage(damage, ctx.self.Position);
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
