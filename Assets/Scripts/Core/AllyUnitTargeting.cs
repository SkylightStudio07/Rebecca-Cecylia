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
        /// 사거리·접촉 판정 공통 허용 오차. 이동은 상대의 접촉 원 경계면에서 정확히 멈추는데,
        /// float 반올림 때문에 멈춘 뒤의 실제 거리가 경계보다 1e-7 단위로 커질 수 있다. 판정에
        /// 오차가 없으면 그 상태에서 사거리 밖으로 취급돼 이동량 0·타깃 null이 영구 반복되는
        /// 교착이 생기므로, 밸런스에 영향이 없는 폭만 더해 경계에 멈춘 상대를 항상 인정한다.
        /// </summary>
        public const float RangeTolerance = 0.001f;

        // --- 경로 누적 거리 캐시 (성능 회귀 방지) ---------------------------------
        // CalculatePathProgress는 O(n)인데 PathProgress 프로퍼티로 노출되어
        // 아래 IsPreferredAlly/IsPreferredEnemy의 진행도 비교문에서 후보 하나당 최대 4회
        // 재평가되고, 그 비교가 아군×적 이중 루프 안에서 매 프레임 돈다. 경로 정점이
        // 9→98개로 늘어나면(웨이포인트 스플라인 베이킹) 이 핫패스가 약 11배 무거워지므로
        // 이번 변경이 만드는 회귀를 상쇄하려면 캐싱이 필수다(조기 최적화가 아니다).
        //
        // 이 캐시는 "같은 리스트 인스턴스가 제자리에서 변형되지 않는다"는 전제 위에 있다 —
        // 유효성 판정이 내용 비교가 아니라 참조(ReferenceEquals) + 길이 비교이기 때문이다.
        // MapManager._waypointPositions는 Awake에서 1회만 생성된 뒤 불변이므로 안전하다.
        private static IReadOnlyList<Vector2> _cachedPath;
        private static float[] _cachedCumulative; // [i] = path[0]..path[i] 누적 거리, [0] = 0
        private static int _cachedCount;

        /// <summary>
        /// 캐시가 유효하도록 보장한다. path가 비었거나 null이면 캐시를 비우고 false를
        /// 반환하므로, 호출부는 기존 조기 반환 경로를 그대로 타야 한다.
        /// </summary>
        private static bool EnsureCumulative(IReadOnlyList<Vector2> path)
        {
            if (path == null || path.Count == 0)
            {
                _cachedPath = null;
                _cachedCumulative = null;
                _cachedCount = 0;
                return false;
            }

            if (ReferenceEquals(path, _cachedPath) && path.Count == _cachedCount)
            {
                return true;
            }

            // 반드시 이 순서(0부터 오름차순, 직전 값에 다음 거리를 더하는 순서)로 누적해야
            // 한다. 부동소수 덧셈은 결합법칙이 성립하지 않으므로 합산 순서가 기존
            // CalculatePathLength/CalculatePathProgress의 순차 누적과 다르면 결과가 미세하게
            // 어긋나고, 그러면 특정 부동소수 값을 단언하는 기존 검증 테스트
            // (AllyUnitCombatVerifier)가 깨진다.
            float[] cumulative = new float[path.Count];
            cumulative[0] = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(path[i - 1], path[i]);
            }

            _cachedPath = path;
            _cachedCumulative = cumulative;
            _cachedCount = path.Count;
            return true;
        }

        /// <summary>
        /// 씬 재로드(Retry, SceneManager.LoadScene)로는 static 캐시가 비워지지 않으므로
        /// GameManager.Awake()가 명시적으로 호출한다(AGENTS.md §5-6). 참조 비교로
        /// 어차피 다음 접근 시 자동 무효화되긴 하지만, 규칙상 명시적 초기화 경로를 둔다.
        /// </summary>
        public static void ResetPathCache()
        {
            _cachedPath = null;
            _cachedCumulative = null;
            _cachedCount = 0;
        }

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

            foreach (EnemyInstance candidate in candidates)
            {
                if (candidate == null || !candidate.IsAlive ||
                    !IsWithinRange(
                        self.Position,
                        candidate.position,
                        self.GetEffectiveAttackRange(candidate)))
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
                    !IsWithinRange(
                        self.Position,
                        candidate.position,
                        candidate.GetContactRange(self)))
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
            if (range <= 0f)
            {
                return false;
            }

            float toleratedRange = range + RangeTolerance;
            return (target - origin).sqrMagnitude <= toleratedRange * toleratedRange;
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

            // 이 시점에서 path.Count >= 2 (위의 조기 반환들이 0/1개 케이스를 걸러냄)이므로
            // EnsureCumulative는 항상 true를 반환하고 _cachedCumulative가 채워져 있다.
            EnsureCumulative(path);
            float totalLength = _cachedCumulative[path.Count - 1];
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

            // 위 EnsureCumulative(path) 호출로 이미 이 path에 대해 캐시가 채워져 있으므로
            // segmentIndex까지의 누적 거리를 O(1)로 조회한다(기존엔 매 호출 O(segmentIndex) 루프).
            float distanceAtSegmentStart = _cachedCumulative[segmentIndex];

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

            // 캐시가 유효하면(또는 이번 호출로 새로 채워지면) 누적 배열의 마지막 원소가
            // 곧 총 길이다. path.Count == 0이면 EnsureCumulative가 캐시를 비우고 false를
            // 반환하므로 아래 기존 루프 경로로 떨어져 0f를 반환한다(기존 동작과 동일).
            if (EnsureCumulative(path))
            {
                return _cachedCumulative[path.Count - 1];
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
