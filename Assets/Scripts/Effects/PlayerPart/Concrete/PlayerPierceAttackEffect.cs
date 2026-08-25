using System.Collections.Generic;
using RCCom.Core;
using RCCom.Managers;
using RCCom.Runtime;
using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Pierce Attack")]
    public sealed class PlayerPierceAttackEffect : PlayerPartEffectBase, IPlayerPrimaryAttackEffect
    {
        [Min(1)] public int maxTargets = 3;
        [Range(0f, 1f)] public float damageFalloffPerTarget = 0.2f;
        [Range(0.1f, 20f)] public float beamHalfAngleDegrees = 4f;

        [Header("공용 관통 레이저 연출")]
        [SerializeField] private GameObject laserBeamPrefab;
        [SerializeField] private LaserBeamVisualEffect laserBeamVisual;

        public void OnAttack(PlayerAttackContext ctx, EnemyInstance target)
        {
            Vector2 direction = target.position - ctx.origin;
            List<EnemyInstance> targets = PierceAttackMath.GetTargetsInBeam(
                ctx.activeEnemies, ctx.origin, direction, ctx.data.attackRange, beamHalfAngleDegrees);
            targets.Sort((left, right) =>
                (left.position - ctx.origin).sqrMagnitude.CompareTo((right.position - ctx.origin).sqrMagnitude));

            int count = Mathf.Min(Mathf.Max(1, maxTargets), targets.Count);
            for (int index = 0; index < count; index++)
            {
                float multiplier = Mathf.Max(0f, 1f - damageFalloffPerTarget * index);
                ctx.ApplyDamage(targets[index], multiplier);
            }

            Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.right;
            Vector3 beamEnd = ctx.origin + normalizedDirection * ctx.data.attackRange;
            LaserBeamView.Spawn(laserBeamPrefab, ctx.origin, beamEnd, laserBeamVisual);

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayPlayerAttack();
            }
        }
    }
}
