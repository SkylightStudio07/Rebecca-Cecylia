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

        [Header("착탄 연출 (설계안 §3-③) — 판정과 무관한 순수 연출")]
        [Tooltip("포물선 낙하 궤적의 정점 높이(월드 단위). 0이면 다른 공격 효과와 같은 직선 투사체가 된다.")]
        [SerializeField] private float lobHeight = 0.6f;
        [Tooltip("착탄 순간 재생할 큰 파편 버스트(ParticleBurst) — FakeProjectile 자체의 작은 히트 스파크와 별개로, splashRadius 규모에 맞는 더 큰 버스트.")]
        [SerializeField] private GameObject explosionBurstPrefab;
        [Tooltip("착탄 순간 splashRadius까지 확산하는 충격파 링(ShockwaveRing).")]
        [SerializeField] private GameObject shockwaveRingPrefab;
        [Tooltip("착탄 지점에 잠깐 남는 바닥 그을림 자국(ScorchDecal).")]
        [SerializeField] private GameObject scorchDecalPrefab;

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
            FakeProjectile.Spawn(fakeProjectilePrefab, ctx.self.Position, target.position, 0f, lobHeight);

            // 착탄 연출 3종은 판정과 완전히 분리된 순수 시각 효과라 투사체의 실제 비행 시간
            // (0.05~0.2초)을 기다리지 않고 이 시점에 바로 재생한다 — AttackFlash 시절부터 이
            // 효과 클래스가 유지해온 "명중 즉시 연출" 타이밍과 동일하다.
            ParticleBurst.Spawn(explosionBurstPrefab, target.position);
            ShockwaveRing.Spawn(shockwaveRingPrefab, target.position, splashRadius);
            ScorchDecal.Spawn(scorchDecalPrefab, target.position, splashRadius);

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
