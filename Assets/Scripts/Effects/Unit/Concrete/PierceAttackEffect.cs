using System.Collections.Generic;
using RCCom.Core;
using RCCom.Runtime;
using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Effects.Unit.Concrete
{
    /// <summary>
    /// Tower.Concrete.PierceDamageEffect와 동일한 부채꼴 관통(레이저) 판정을 아군 유닛에 이식한
    /// 버전. Unit은 사거리·쿨다운·주 타겟 선정을 AllyUnitInstance가 이미 결정해 OnAttack으로
    /// 넘겨주므로(BasicAttackEffect 참고), Tower 버전과 달리 자체 쿨다운/타겟팅을 두지 않고
    /// "정해진 타겟 방향으로 빔을 쏴서 일직선상의 다른 적도 함께 맞힌다"는 명중 처리만 한다.
    /// BasicAttackEffect/SplashAttackEffect와 배타적으로 한 유닛 Definition에 하나만 붙인다
    /// (여러 개를 같이 붙이면 주 타격이 중복 적용된다).
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Effects/Pierce Attack Effect")]
    public class PierceAttackEffect : AllyUnitEffectBase, IAllyUnitPrimaryAttackEffect
    {
        [SerializeField] private float beamHalfAngleDegrees = 10f;
        [SerializeField] private GameObject laserBeamPrefab;
        [SerializeField] private LaserBeamVisualEffect laserBeamVisual;

        public override void OnAttack(AllyUnitContext ctx, EnemyInstance target)
        {
            if (ctx == null || ctx.self == null || ctx.self.Data == null ||
                ctx.self.IsDead || target == null || !target.IsAlive)
            {
                return;
            }

            Vector2 origin = ctx.self.Position;
            Vector2 beamDirection = (target.position - origin).normalized;
            // 승급 보스와 맞닿는 중심점 거리는 일반 적보다 넓으므로 빔 길이도 대상별 실제
            // 공격 범위를 사용해야 주 대상 직전에 끊기지 않는다.
            float range = ctx.self.GetEffectiveAttackRange(target);
            float damage = ctx.self.Data.attackDamage * ctx.self.CalculateDamageMultiplier();

            List<EnemyInstance> targets = PierceAttackMath.GetTargetsInBeam(
                ctx.activeEnemies, origin, beamDirection, range, beamHalfAngleDegrees);
            foreach (EnemyInstance enemy in targets)
            {
                enemy.TakeDamage(damage, origin);
            }

            Vector3 beamEnd = origin + beamDirection * range;
            LaserBeamView.Spawn(laserBeamPrefab, origin, beamEnd, laserBeamVisual);
        }
    }
}
