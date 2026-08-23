using System;
using System.Collections.Generic;
using RCCom.Core;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 웨이포인트 경로 스플라인 베이킹(PathSmoothing)과 경로 진행도 캐시
    /// (AllyUnitTargeting)를 씬·프리팹·프로젝트 에셋 없이 검증한다. AllyUnitCombatVerifier와
    /// 같은 방식으로 메모리 상의 좌표 배열만 다루므로 데이터 배선 상태와 무관하게 반복 실행할 수 있다.
    /// </summary>
    public static class PathSmoothingVerifier
    {
        /// <summary>DefenseScene에 실제로 배치된 웨이포인트 9개 좌표 (회귀 기준값).</summary>
        private static readonly Vector2[] SceneWaypoints =
        {
            new(-16.62f, -3.63f), new(-12.81f, 1.11f), new(-8.8f, 4.65f),
            new(-4.29f, 0.17f), new(-0.29f, -4.13f), new(3.63f, 0.61f),
            new(7.49f, 5.11f), new(12.16f, 0.04f), new(15.47f, -2.78f),
        };

        [MenuItem("RCCom/Map/Verify Path Smoothing")]
        public static void Verify()
        {
            VerifyBackwardCompatibility();
            VerifyEndpointsPreserved();
            VerifyControlPointsOnPath();
            VerifyStraightLinePreserved();
            VerifyPointSpacing();
            VerifyNoOvershoot();
            VerifyLengthGrowth();
            VerifyDegenerateInputs();
            VerifyProgressCacheEquivalence();

            Debug.Log("[PathSmoothingVerifier] 경로 스플라인/진행도 캐시 9개 시나리오 검증 통과");
        }

        /// <summary>
        /// smoothness가 0이면 정점 개수·좌표가 원본과 완전히 같아야 한다. 이게 직선형 맵의
        /// 하위호환 보증인데, 좌표만 같고 정점 수가 늘어나면 안 되는 이유가 따로 있다 —
        /// EnemyInstance.MoveAlongPath()는 정점에 도달한 프레임의 잔여 이동량을 버리므로
        /// 정점 밀도 자체가 적 이동 속도에 영향을 준다.
        /// </summary>
        private static void VerifyBackwardCompatibility()
        {
            Vector2[] baked = PathSmoothing.GenerateSmoothPath(SceneWaypoints, 0f, 0.5f);

            Assert(baked.Length == SceneWaypoints.Length,
                $"smoothness=0인데 정점 수가 달라졌습니다 (원본 {SceneWaypoints.Length}, 결과 {baked.Length}).");
            for (int i = 0; i < baked.Length; i++)
            {
                Assert(baked[i] == SceneWaypoints[i], $"smoothness=0인데 정점 {i}의 좌표가 달라졌습니다.");
            }
        }

        /// <summary>
        /// path[0]은 적 스폰 좌표, path[^1]은 아군 스폰/거점 좌표로 그대로 쓰이므로
        /// 곡선을 최대로 걸어도 두 끝점은 부동소수 오차 없이 정확히 보존되어야 한다.
        /// </summary>
        private static void VerifyEndpointsPreserved()
        {
            Vector2[] baked = PathSmoothing.GenerateSmoothPath(SceneWaypoints, 1f, 0.5f);

            Assert(baked[0] == SceneWaypoints[0], "곡선 베이킹 후 시작점이 원본과 다릅니다.");
            Assert(baked[baked.Length - 1] == SceneWaypoints[SceneWaypoints.Length - 1],
                "곡선 베이킹 후 끝점이 원본과 다릅니다.");
        }

        /// <summary>
        /// Catmull-Rom을 고른 이유가 "모든 제어점을 정확히 통과한다"이므로, 디자이너가
        /// 도로 중앙에 찍은 웨이포인트가 결과에 그대로 들어 있는지 확인한다.
        /// </summary>
        private static void VerifyControlPointsOnPath()
        {
            Vector2[] baked = PathSmoothing.GenerateSmoothPath(SceneWaypoints, 1f, 0.5f);

            foreach (Vector2 controlPoint in SceneWaypoints)
            {
                bool found = false;
                foreach (Vector2 point in baked)
                {
                    if (point == controlPoint)
                    {
                        found = true;
                        break;
                    }
                }

                Assert(found, $"원본 제어점 {controlPoint}이(가) 베이킹 결과에 정확히 존재하지 않습니다.");
            }
        }

        /// <summary>
        /// 일직선상의 제어점은 smoothness를 최대로 걸어도 직선을 유지해야 한다.
        /// 여기서 휘면 팬텀 제어점 반사나 크넛 파라미터화가 잘못된 것이다.
        /// </summary>
        private static void VerifyStraightLinePreserved()
        {
            var line = new List<Vector2>
            {
                new(0f, 0f), new(2f, 0f), new(5f, 0f), new(9f, 0f), new(14f, 0f),
            };

            Vector2[] baked = PathSmoothing.GenerateSmoothPath(line, 1f, 0.5f);

            foreach (Vector2 point in baked)
            {
                Assert(Mathf.Abs(point.y) < 0.001f,
                    $"일직선 제어점인데 곡선이 직선을 벗어났습니다 (y={point.y}).");
            }
        }

        /// <summary>
        /// 분할 수는 현(chord) 길이 기준으로 정하는데 점은 그보다 긴 곡선 위에 놓이고,
        /// 구심 파라미터화는 호 길이에 대해 균일하지도 않다. 따라서 실제 정점 간격은
        /// maxPointSpacing을 다소 넘어선다 — 실측상 씬 경로 기준 약 1.19배이고 간격 값을
        /// 0.3~1.0으로 바꿔도 1.18~1.21배로 일정하다. 여유를 조금 둔 1.25배를 상한으로 잡는다
        /// (엄밀히 maxPointSpacing 이하를 요구하면 정상 동작에서도 실패한다).
        /// </summary>
        private static void VerifyPointSpacing()
        {
            foreach (float spacing in new[] { 0.3f, 0.5f, 0.75f, 1f })
            {
                Vector2[] baked = PathSmoothing.GenerateSmoothPath(SceneWaypoints, 1f, spacing);
                for (int i = 1; i < baked.Length; i++)
                {
                    float gap = Vector2.Distance(baked[i - 1], baked[i]);
                    Assert(gap <= spacing * 1.25f,
                        $"정점 간격이 상한의 1.25배를 넘었습니다 (spacing {spacing}, 실제 {gap}).");
                }
            }
        }

        /// <summary>
        /// 구심(α=0.5) 파라미터화를 쓰는 이유가 급커브에서의 오버슈트/루프 방지이므로,
        /// 곡선이 제어점들의 AABB를 크게 벗어나지 않는지 확인한다. 씬 경로 기준 실측
        /// 이탈은 0이지만, 다른 맵을 넣어도 재앙적으로 튀지 않는지 보기 위해 1.5로 여유를 둔다.
        /// </summary>
        private static void VerifyNoOvershoot()
        {
            Vector2 min = SceneWaypoints[0];
            Vector2 max = SceneWaypoints[0];
            foreach (Vector2 point in SceneWaypoints)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            Vector2[] baked = PathSmoothing.GenerateSmoothPath(SceneWaypoints, 1f, 0.5f);
            foreach (Vector2 point in baked)
            {
                float overshootX = Mathf.Max(0f, Mathf.Max(min.x - point.x, point.x - max.x));
                float overshootY = Mathf.Max(0f, Mathf.Max(min.y - point.y, point.y - max.y));
                Assert(overshootX < 1.5f && overshootY < 1.5f,
                    $"곡선이 제어점 AABB를 과도하게 벗어났습니다 ({point}).");
            }
        }

        /// <summary>
        /// 곡선은 직선보다 길어지는데, 경로 총 길이는 아군 최종 대기점과 진행도 정규화의
        /// 분모라서 과도하게 늘어나면 밸런스가 흔들린다. 씬 경로 실측은 1.017배다.
        /// </summary>
        private static void VerifyLengthGrowth()
        {
            float linearLength = AllyUnitTargeting.CalculatePathLength(SceneWaypoints);
            AllyUnitTargeting.ResetPathCache();

            Vector2[] baked = PathSmoothing.GenerateSmoothPath(SceneWaypoints, 1f, 0.5f);
            float curvedLength = AllyUnitTargeting.CalculatePathLength(baked);
            AllyUnitTargeting.ResetPathCache();

            float ratio = curvedLength / linearLength;
            Assert(ratio >= 1f && ratio <= 1.15f,
                $"곡선 경로 길이 증가율이 허용 범위를 벗어났습니다 ({ratio}배).");
        }

        /// <summary>
        /// 인스펙터 실수(빈 배열, 웨이포인트 1개, 동일 좌표 중복 배치)에서 예외로 죽지 않는지
        /// 확인한다. 중복 제어점은 크넛 간격이 0이 되어 0 나눗셈이 날 수 있는 지점이다.
        /// </summary>
        private static void VerifyDegenerateInputs()
        {
            Assert(PathSmoothing.GenerateSmoothPath(null, 1f, 0.5f).Length == 0,
                "null 입력에서 빈 배열이 반환되지 않았습니다.");
            Assert(PathSmoothing.GenerateSmoothPath(Array.Empty<Vector2>(), 1f, 0.5f).Length == 0,
                "빈 입력에서 빈 배열이 반환되지 않았습니다.");
            Assert(PathSmoothing.GenerateSmoothPath(new[] { new Vector2(3f, 4f) }, 1f, 0.5f).Length == 1,
                "제어점 1개 입력이 그대로 반환되지 않았습니다.");

            Vector2[] two = PathSmoothing.GenerateSmoothPath(
                new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) }, 1f, 0.5f);
            Assert(two[0] == new Vector2(0f, 0f) && two[two.Length - 1] == new Vector2(10f, 0f),
                "제어점 2개 입력에서 끝점이 보존되지 않았습니다.");
            foreach (Vector2 point in two)
            {
                Assert(Mathf.Abs(point.y) < 0.0001f, "제어점 2개(직선) 입력인데 결과가 휘었습니다.");
            }

            // 동일 좌표가 연속으로 들어오면 PathSmoothing이 경고 로그를 남기므로, 이 검증만
            // 에디터에서 의미가 있다(순수 C# 하니스에서는 Unity 로깅을 실행할 수 없다).
            Vector2[] duplicated = PathSmoothing.GenerateSmoothPath(
                new[] { new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(5f, 0f) }, 1f, 0.5f);
            Assert(duplicated.Length >= 2 && duplicated[duplicated.Length - 1] == new Vector2(5f, 0f),
                "중복 제어점 입력에서 끝점이 보존되지 않았습니다.");
        }

        /// <summary>
        /// 진행도 캐시는 순수 최적화라 값이 캐시 도입 전과 비트 단위로 같아야 한다. 캐시가
        /// 비어 있을 때와 적중했을 때, 그리고 서로 다른 경로를 번갈아 조회해 캐시가 계속
        /// 재구축되는 상황에서도 동일한지 확인한다.
        /// </summary>
        private static void VerifyProgressCacheEquivalence()
        {
            Vector2[] curved = PathSmoothing.GenerateSmoothPath(SceneWaypoints, 0.6f, 0.5f);
            var paths = new List<IReadOnlyList<Vector2>> { SceneWaypoints, curved };

            foreach (IReadOnlyList<Vector2> path in paths)
            {
                for (int index = -1; index <= path.Count; index++)
                {
                    Vector2 position = path[Mathf.Clamp(index, 0, path.Count - 1)] + new Vector2(0.3f, -0.2f);

                    AllyUnitTargeting.ResetPathCache();
                    float cold = AllyUnitTargeting.CalculatePathProgress(path, index, position, true);
                    float warm = AllyUnitTargeting.CalculatePathProgress(path, index, position, true);

                    // 캐시를 다른 경로로 오염시킨 뒤 다시 조회해도 값이 같아야 한다.
                    AllyUnitTargeting.CalculatePathProgress(paths[0] == path ? paths[1] : paths[0], 1, Vector2.zero, true);
                    float afterThrash = AllyUnitTargeting.CalculatePathProgress(path, index, position, true);

                    Assert(cold.Equals(warm) && warm.Equals(afterThrash),
                        $"진행도가 캐시 상태에 따라 달라졌습니다 (index {index}: {cold} / {warm} / {afterThrash}).");
                }
            }

            AllyUnitTargeting.ResetPathCache();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
