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
        /// 인스턴스가 현재 향하는 실제 선분과 그 선분 안의 보간값으로 진행도를 계산한다.
        /// 가장 가까운 선분을 추측하지 않는 이유는 경로가 교차하거나 되감기는 지형에서
        /// 위치만으로 선분을 고르면 진행도가 다른 구간으로 순간 이동할 수 있기 때문이다.
        /// </summary>
        public static float CalculatePathProgress(
            IReadOnlyList<Vector2> path,
            int nextWaypointIndex,
            Vector2 position,
            bool movingForward)
        {
            if (path == null || path.Count == 0)
            {
                return 0f;
            }

            if (path.Count == 1)
            {
                return movingForward ? 0f : 1f;
            }

            float totalLength = CalculatePathLength(path);
            if (totalLength <= DistanceEpsilon)
            {
                return movingForward ? 0f : 1f;
            }

            if (movingForward && nextWaypointIndex >= path.Count)
            {
                return 1f;
            }

            if (!movingForward && nextWaypointIndex < 0)
            {
                return 0f;
            }

            int segmentIndex = movingForward ? nextWaypointIndex - 1 : nextWaypointIndex;
            segmentIndex = Mathf.Clamp(segmentIndex, 0, path.Count - 2);

            float distanceAtSegmentStart = 0f;
            for (int i = 1; i <= segmentIndex; i++)
            {
                distanceAtSegmentStart += Vector2.Distance(path[i - 1], path[i]);
            }

            Vector2 start = path[segmentIndex];
            Vector2 end = path[segmentIndex + 1];
            Vector2 segment = end - start;
            float segmentLength = segment.magnitude;
            if (segmentLength <= DistanceEpsilon)
            {
                return Mathf.Clamp01(distanceAtSegmentStart / totalLength);
            }

            float interpolation = Mathf.Clamp01(Vector2.Dot(position - start, segment) /
                                                 (segmentLength * segmentLength));
            float distanceAlongPath = distanceAtSegmentStart + segmentLength * interpolation;
            return Mathf.Clamp01(distanceAlongPath / totalLength);
        }

        public static float CalculatePathLength(IReadOnlyList<Vector2> path)
        {
            if (path == null)
            {
                return 0f;
            }

            float totalLength = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                totalLength += Vector2.Distance(path[i - 1], path[i]);
            }

            return totalLength;
        }

        public static Vector2 GetPointAtDistance(
            IReadOnlyList<Vector2> path,
            float distanceFromStart)
        {
            if (path == null || path.Count == 0)
            {
                return Vector2.zero;
            }

            if (path.Count == 1)
            {
                return path[0];
            }

            float remainingDistance = Mathf.Max(0f, distanceFromStart);

            for (int i = 1; i < path.Count; i++)
            {
                Vector2 start = path[i - 1];
                Vector2 end = path[i];
                float segmentLength = Vector2.Distance(start, end);

                if (segmentLength <= DistanceEpsilon)
                {
                    continue;
                }

                if (remainingDistance <= segmentLength)
                {
                    return Vector2.Lerp(start, end, remainingDistance / segmentLength);
                }

                remainingDistance -= segmentLength;
            }

            return path[path.Count - 1];
        }

        /// <summary>
        /// 한 번의 직선 이동이 접촉 원에 처음 들어가는 지점까지 허용할 거리다.
        /// 시작점이 이미 원 안이면 0, 선분이 원과 만나지 않으면 무한대를 반환한다.
        /// </summary>
        public static float DistanceBeforeContact(
            Vector2 start,
            Vector2 end,
            Vector2 contactCenter,
            float contactRange)
        {
            if (contactRange <= 0f)
            {
                return float.PositiveInfinity;
            }

            Vector2 movement = end - start;
            float movementLength = movement.magnitude;
            if (movementLength <= DistanceEpsilon)
            {
                return float.PositiveInfinity;
            }

            Vector2 offset = start - contactCenter;
            float radiusSquared = contactRange * contactRange;
            if (offset.sqrMagnitude <= radiusSquared)
            {
                return 0f;
            }

            Vector2 direction = movement / movementLength;
            float projection = Vector2.Dot(offset, direction);
            float discriminant = projection * projection - (offset.sqrMagnitude - radiusSquared);
            if (discriminant < 0f)
            {
                return float.PositiveInfinity;
            }

            float firstIntersection = -projection - Mathf.Sqrt(discriminant);
            return firstIntersection >= 0f && firstIntersection <= movementLength
                ? firstIntersection
                : float.PositiveInfinity;
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
