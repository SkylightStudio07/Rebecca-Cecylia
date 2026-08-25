using System.Collections.Generic;
using RCCom.Data;
using UnityEngine;

namespace RCCom.Runtime
{
    public sealed class PlayerAttackContext
    {
        public PlayerController self;
        public PlayerData data;
        public IReadOnlyList<EnemyInstance> activeEnemies;
        public Vector2 origin;

        public void Fire(EnemyInstance target, float damageMultiplier = 1f, float delay = 0f)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            self.QueueAttackShot(target, data.attackDamage * damageMultiplier, delay);
        }

        /// <summary>
        /// 레이저·연쇄·스플래시처럼 투사체와 판정 모양이 일치하지 않는 공격의 피해 전용 경로.
        /// 시각 효과는 각 Effect가 공용 렌더러를 한 번만 호출하고, 여기서는 피해와 넉백 원점만
        /// 처리해야 스플래시 대상마다 플레이어발 투사체가 생기는 잘못된 표현을 막을 수 있다.
        /// </summary>
        public void ApplyDamage(
            EnemyInstance target,
            float damageMultiplier = 1f,
            Vector2? sourcePosition = null)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            target.TakeDamage(data.attackDamage * damageMultiplier, sourcePosition ?? origin);
        }
    }
}
