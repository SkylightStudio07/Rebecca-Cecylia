using System.Collections.Generic;
using RCCom.Core;
using RCCom.Managers;
using RCCom.Runtime;
using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Splash Attack")]
    public sealed class PlayerSplashAttackEffect : PlayerPartEffectBase, IPlayerPrimaryAttackEffect
    {
        [Min(0f)] public float radius = 1.2f;
        [Min(0f)] public float splashDamageMultiplier = 0.4f;

        [Header("공용 스플래시 착탄 연출")]
        [SerializeField] private GameObject fakeProjectilePrefab;
        [SerializeField, Min(0f)] private float lobHeight = 0.6f;
        [SerializeField] private GameObject explosionBurstPrefab;
        [SerializeField] private GameObject shockwaveRingPrefab;
        [SerializeField] private ShockwaveRingVisualEffect shockwaveVisual;
        [SerializeField] private GameObject scorchDecalPrefab;

        public void OnAttack(PlayerAttackContext ctx, EnemyInstance target)
        {
            Vector2 impactPosition = target.position;
            ctx.ApplyDamage(target);
            FakeProjectile.Spawn(
                fakeProjectilePrefab,
                ctx.origin,
                impactPosition,
                ctx.data.projectileSpeed,
                lobHeight);
            ParticleBurst.Spawn(explosionBurstPrefab, impactPosition);
            ShockwaveRing.Spawn(shockwaveRingPrefab, impactPosition, radius, shockwaveVisual);
            ScorchDecal.Spawn(scorchDecalPrefab, impactPosition, radius);

            List<EnemyInstance> splashTargets = SplashAttackMath.GetSplashTargets(
                ctx.activeEnemies, target, impactPosition, radius);
            foreach (EnemyInstance splashTarget in splashTargets)
            {
                ctx.ApplyDamage(splashTarget, splashDamageMultiplier, impactPosition);
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayPlayerAttack();
            }
        }
    }
}
