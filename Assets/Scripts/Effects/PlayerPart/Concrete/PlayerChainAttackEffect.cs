using System.Collections.Generic;
using RCCom.Managers;
using RCCom.Runtime;
using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Chain Attack")]
    public sealed class PlayerChainAttackEffect : PlayerPartEffectBase, IPlayerPrimaryAttackEffect
    {
        [Min(0)] public int jumpCount = 2;
        [Range(0f, 1f)] public float damageFalloffPerJump = 0.3f;

        [Header("공용 연쇄 레이저 연출")]
        [SerializeField] private GameObject laserBeamPrefab;
        [SerializeField] private LaserBeamVisualEffect laserBeamVisual;

        public void OnAttack(PlayerAttackContext ctx, EnemyInstance target)
        {
            var hit = new HashSet<EnemyInstance>();
            EnemyInstance current = target;
            Vector2 segmentOrigin = ctx.origin;
            int totalHits = Mathf.Max(0, jumpCount) + 1;
            for (int index = 0; index < totalHits && current != null; index++)
            {
                hit.Add(current);
                Vector2 hitPosition = current.position;
                ctx.ApplyDamage(
                    current,
                    Mathf.Pow(1f - damageFalloffPerJump, index),
                    segmentOrigin);
                // 첫 구간은 플레이어→첫 적, 이후는 직전 적→다음 적으로 이어 같은 레이저
                // 프리팹 여러 개가 한 프레임에 연결된 전기 사슬을 그린다.
                LaserBeamView.Spawn(laserBeamPrefab, segmentOrigin, hitPosition, laserBeamVisual);
                segmentOrigin = hitPosition;
                current = FindNearestUnhit(ctx.activeEnemies, hitPosition, hit);
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayPlayerAttack();
            }
        }

        private static EnemyInstance FindNearestUnhit(
            IReadOnlyList<EnemyInstance> enemies,
            Vector2 origin,
            HashSet<EnemyInstance> hit)
        {
            EnemyInstance nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (EnemyInstance enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive || hit.Contains(enemy))
                {
                    continue;
                }

                float distance = (enemy.position - origin).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = enemy;
                }
            }

            return nearest;
        }
    }
}
