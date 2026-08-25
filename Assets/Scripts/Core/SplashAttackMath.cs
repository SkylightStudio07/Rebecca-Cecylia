using System.Collections.Generic;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Core
{
    /// <summary>
    /// "폭발 중심 radius 내 주 타겟 이외의 적"을 찾는 스플래시(범위) 판정. Tower.SplashDamageEffect와
    /// Unit.SplashAttackEffect가 공유한다. 후보 목록을 읽기만 하는 순수 함수라, 호출부가 이미 주
    /// 타겟에게 TakeDamage를 적용한 뒤(콜라이더 비활성화 → OnTriggerExit2D로 원본 목록이 바뀐
    /// 뒤)에 호출해도 안전하다.
    /// </summary>
    public static class SplashAttackMath
    {
        public static List<EnemyInstance> GetSplashTargets(
            IReadOnlyList<EnemyInstance> candidates,
            EnemyInstance primaryTarget,
            Vector2 center,
            float radius)
        {
            var targets = new List<EnemyInstance>();
            if (candidates == null)
            {
                return targets;
            }

            float radiusSqr = radius * radius;
            foreach (EnemyInstance enemy in candidates)
            {
                if (enemy == null || enemy == primaryTarget)
                {
                    continue;
                }

                if ((enemy.position - center).sqrMagnitude <= radiusSqr)
                {
                    targets.Add(enemy);
                }
            }

            return targets;
        }
    }
}
