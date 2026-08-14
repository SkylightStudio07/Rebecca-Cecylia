using System.Collections.Generic;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Core
{
    /// <summary>
    /// 아군 전투에서 전열을 선택하는 상태 없는 유틸리티. 먼저 월드 거리로 후보를
    /// 걸러낸 뒤 경로의 연속 진행도를 비교하므로, 같은 웨이포인트 안에서 이동 중인
    /// 대상도 인덱스 하나로 뭉개지지 않는다.
    /// </summary>
    public static class AllyUnitTargeting
    {
        private const float ProgressEpsilon = 0.0001f;
        private const float DistanceEpsilon = 0.0001f;

        /// <summary>
        /// 아군이 공격할 적을 고른다. 진행도가 높은 적(거점 방향으로 더 전진한 적)을
        /// 우선하고, 진행도가 같을 때만 거리와 열거 순서를 사용한다.
        /// </summary>
        public static EnemyInstance FindBestEnemy(
            AllyUnitInstance self,
            IReadOnlyList<EnemyInstance> candidates)
        {
            if (self == null || self.IsDead || candidates == null)
            {
                return null;
            }

            EnemyInstance best = null;
            float attackRange = self.EffectiveAttackRange;

            foreach (EnemyInstance candidate in candidates)
            {
                if (candidate == null || !candidate.IsAlive ||
                    !IsWithinRange(self.Position, candidate.position, attackRange))
                {
                    continue;
                }

                if (best == null || IsPreferredEnemy(candidate, best, self.Position))
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// 이동 정지 여부를 판단하기 위한 접촉 전열을 고른다. 공격 범위가 짧게 잘못
        /// 설정되어도 접촉 거리 안에 들어온 상대를 놓치지 않도록 공격 후보와 분리한다.
        /// </summary>
        public static EnemyInstance FindBestContactEnemy(
            AllyUnitInstance self,
            IReadOnlyList<EnemyInstance> candidates)
        {
            if (self == null || self.IsDead || candidates == null)
            {
                return null;
            }

            EnemyInstance best = null;
            foreach (EnemyInstance candidate in candidates)
            {
                if (candidate == null || !candidate.IsAlive ||
                    !IsWithinRange(self.Position, candidate.position, self.ContactRange))
                {
                    continue;
                }

                if (best == null || IsPreferredEnemy(candidate, best, self.Position))
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// 적의 후보 비교. 적은 생성점 방향으로 가장 많이 전진한 아군, 즉 진행도가
        /// 낮은 아군을 우선한다. 완전히 같은 후보는 현재/먼저 제시된 대상을 유지한다.
        /// </summary>
        public static bool IsPreferredAlly(
            AllyUnitInstance candidate,
            AllyUnitInstance current,
            Vector2 origin)
        {
            if (candidate == null)
            {
                return false;
            }

            if (current == null)
            {
                return true;
            }

            if (candidate.PathProgress < current.PathProgress - ProgressEpsilon)
            {
                return true;
            }

            if (Mathf.Abs(candidate.PathProgress - current.PathProgress) > ProgressEpsilon)
            {
                return false;
            }

            return IsCloser(candidate.Position, current.Position, origin);
        }

        /// <summary>아군이 공격할 적의 비교 규칙을 공개해 적 후보 계약과 공유한다.</summary>
        public static bool IsPreferredEnemy(
            EnemyInstance candidate,
            EnemyInstance current,
            Vector2 origin)
        {
            if (candidate == null)
            {
                return false;
            }

            if (current == null)
            {
                return true;
            }

            if (candidate.PathProgress > current.PathProgress + ProgressEpsilon)
            {
                return true;
            }

            if (Mathf.Abs(candidate.PathProgress - current.PathProgress) > ProgressEpsilon)
            {
                return false;
            }

            return IsCloser(candidate.position, current.position, origin);
        }

        public static bool IsWithinRange(Vector2 origin, Vector2 target, float range)
        {
            return range > 0f && (target - origin).sqrMagnitude <= range * range;
        }

        /// <summary>
        /// 폴리라인의 누적 길이를 기준으로 위치를 0~1 진행도로 변환한다. 이 메서드는
        /// 결과를 캐시하지 않고 호출 시 계산하므로 공용 후보 목록이나 이전 프레임 상태를
        /// 오염시키지 않는다. 같은 선분 길이 안의 이동량도 진행도에 포함된다.
        /// </summary>
        public static float CalculatePathProgress(IReadOnlyList<Vector2> path, Vector2 position)
        {
            if (path == null || path.Count <= 1)
            {
                return 0f;
            }

            float totalLength = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                totalLength += Vector2.Distance(path[i - 1], path[i]);
            }

            if (totalLength <= DistanceEpsilon)
            {
                return 0f;
            }

            float distanceAtClosestPoint = 0f;
            float distanceAtSegmentStart = 0f;
            float closestSqrDistance = float.PositiveInfinity;

            for (int i = 1; i < path.Count; i++)
            {
                Vector2 start = path[i - 1];
                Vector2 end = path[i];
                Vector2 segment = end - start;
                float segmentLength = segment.magnitude;

                if (segmentLength <= DistanceEpsilon)
                {
                    distanceAtSegmentStart += segmentLength;
                    continue;
                }

                float projection = Mathf.Clamp01(Vector2.Dot(position - start, segment) /
                                                 (segmentLength * segmentLength));
                Vector2 closestPoint = start + segment * projection;
                float sqrDistance = (position - closestPoint).sqrMagnitude;

                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    distanceAtClosestPoint = distanceAtSegmentStart + segmentLength * projection;
                }

                distanceAtSegmentStart += segmentLength;
            }

            return Mathf.Clamp01(distanceAtClosestPoint / totalLength);
        }

        private static bool IsCloser(
            Vector2 candidatePosition,
            Vector2 currentPosition,
            Vector2 origin)
        {
            float candidateDistance = (candidatePosition - origin).sqrMagnitude;
            float currentDistance = (currentPosition - origin).sqrMagnitude;
            return candidateDistance < currentDistance - DistanceEpsilon;
        }
    }
}
