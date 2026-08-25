using System.Collections.Generic;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Core
{
    /// <summary>
    /// "타겟 방향으로 부채꼴을 그어, 사거리 내에서 그 방향과 beamHalfAngleDegrees 이내(=일직선
    /// 상)에 있는 모든 적을 동시에 맞춘다"는 관통(레이저) 판정. Tower.PierceDamageEffect와
    /// Unit.PierceAttackEffect가 공유한다. 실제 콜라이더 레이캐스트가 아니라 벡터 각도 비교
    /// 방식이다. 후보 목록을 읽기만 하고 아무것도 변경하지 않는 순수 함수라, 호출부가 반환된
    /// 목록에 피해를 적용하는 동안 원본 후보 목록이 바뀌어도(예: TakeDamage로 인한 즉사 →
    /// 콜라이더 비활성화 → OnTriggerExit2D) 안전하다.
    /// </summary>
    public static class PierceAttackMath
    {
        public static List<EnemyInstance> GetTargetsInBeam(
            IReadOnlyList<EnemyInstance> candidates,
            Vector2 origin,
            Vector2 beamDirection,
            float range,
            float beamHalfAngleDegrees)
        {
            var targets = new List<EnemyInstance>();
            if (candidates == null)
            {
                return targets;
            }

            foreach (EnemyInstance enemy in candidates)
            {
                if (enemy == null)
                {
                    continue;
                }

                Vector2 toEnemy = enemy.position - origin;
                if (toEnemy.magnitude > range)
                {
                    continue;
                }

                if (Vector2.Angle(beamDirection, toEnemy) <= beamHalfAngleDegrees)
                {
                    targets.Add(enemy);
                }
            }

            return targets;
        }
    }
}
