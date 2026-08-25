using System.Collections.Generic;
using RCCom.Core;
using RCCom.Managers;
using RCCom.Runtime;
using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Overload Attack")]
    public sealed class PlayerOverloadAttackEffect : PlayerPartEffectBase, IPlayerPrimaryAttackEffect
    {
        [Min(1)] public int skillPierceTargets = 3;
        [Range(0.1f, 20f)] public float beamHalfAngleDegrees = 4f;

        [Header("공용 관통 레이저 연출")]
        [SerializeField] private GameObject laserBeamPrefab;
        [SerializeField] private LaserBeamVisualEffect laserBeamVisual;

        public void OnAttack(PlayerAttackContext ctx, EnemyInstance target)
        {
            if (!ctx.self.IsSkillActive)
            {
                ctx.Fire(target);
                return;
            }

            Vector2 direction = target.position - ctx.origin;
            List<EnemyInstance> targets = PierceAttackMath.GetTargetsInBeam(
                ctx.activeEnemies, ctx.origin, direction, ctx.data.attackRange, beamHalfAngleDegrees);
            targets.Sort((left, right) =>
                (left.position - ctx.origin).sqrMagnitude.CompareTo((right.position - ctx.origin).sqrMagnitude));
            int count = Mathf.Min(skillPierceTargets, targets.Count);
            for (int index = 0; index < count; index++)
            {
                ctx.ApplyDamage(targets[index]);
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
