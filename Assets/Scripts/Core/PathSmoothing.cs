using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Core
{
    /// <summary>
    /// 웨이포인트 제어점 배열을 구심(centripetal) Catmull-Rom 곡선으로 베이킹하는
    /// 상태 없는 순수 정적 유틸리티. MapManager.Awake()가 1회만 호출해 결과를 캐싱하고,
    /// 이후 전투 로직(EnemyInstance/AllyUnitInstance 이동, AllyUnitTargeting 진행도 계산)은
    /// 베이킹된 정점 배열만 소비한다 — 즉 곡선 수식 자체가 런타임 이동 코드에 섞이지 않는다.
    ///
    /// 왜 Bézier/B-spline이 아니라 Catmull-Rom인가: 이 두 곡선은 일반적으로 제어점을
    /// 정확히 통과하지 않고 "근처를 지나가는" 근사 곡선이라, 디자이너가 도로 중앙에
    /// 정확히 찍어둔 웨이포인트의 의도(스폰 지점, 코너 정점 등)가 어긋난다. Catmull-Rom은
    /// 정의상 모든 제어점을 정확히 통과하므로 이 의도를 보존한다.
    /// </summary>
    public static class PathSmoothing
    {
        /// <summary>
        /// 구심(centripetal) 파라미터화 지수. 0(uniform)은 급커브에서 궤적이 튀거나
        /// 자체 교차(루프)가 생기고, 1(chordal)은 반대로 코너가 과도하게 무뎌진다.
        /// 0.5는 Catmull-Rom 스플라인이 자체 교차를 일으키지 않음이 수학적으로 증명된
        /// 유일한 값이라 임의로 바꾸지 않는다.
        /// </summary>
        private const float Alpha = 0.5f;

        /// <summary>크넛 간격이 0에 수렴할 때(중복 제어점 등) 0 나눗셈을 막는 하한.</summary>
        private const float KnotEpsilon = 1e-5f;

        /// <summary>이 거리 미만이면 직전에 추가한 점과 같은 점으로 취급해 결과에서 생략한다.</summary>
        private const float DuplicateEpsilon = 1e-4f;

        /// <summary>구간 하나가 과도하게 길어도(예: 데이터 오류) 정점 폭주를 막는 구간별 분할 상한.</summary>
        private const int MaxSubdivisionsPerSegment = 64;

        /// <summary>결과 배열 전체 길이에 대한 안전장치. 이 이상은 절대 만들지 않는다.</summary>
        private const int MaxTotalPoints = 2048;

        /// <summary>
        /// 원본 웨이포인트 제어점을 구심 Catmull-Rom 곡선으로 베이킹한다.
        /// smoothness가 0이면 세분화 없이 원본 배열을 그대로 복사해 반환한다 — 정점 밀도
        /// 자체가 EnemyInstance.MoveAlongPath()의 프레임당 이동 손실량에 영향을 주므로
        /// (정점 도달 프레임에 남은 이동량을 버림), "곡선을 적용하지 않는다"는 것이
        /// "정점 개수도 늘리지 않는다"를 반드시 포함해야 완전한 하위호환이 된다.
        /// </summary>
        public static Vector2[] GenerateSmoothPath(
            IReadOnlyList<Vector2> controlPoints,
            float smoothness,
            float maxPointSpacing)
        {
            if (controlPoints == null || controlPoints.Count == 0)
            {
                return Array.Empty<Vector2>();
            }

            if (controlPoints.Count == 1)
            {
                return new[] { controlPoints[0] };
            }

            if (smoothness <= 0f)
            {
                // 세분화조차 하지 않는다 — "곡선 0%"를 Lerp 블렌딩으로만 표현하면 좌표는
                // 원본 직선과 같아도 정점 수가 늘어나 위 요약의 속도 손실이 그대로 생긴다.
                Vector2[] copy = new Vector2[controlPoints.Count];
                for (int i = 0; i < controlPoints.Count; i++)
                {
                    copy[i] = controlPoints[i];
                }

                return copy;
            }

            smoothness = Mathf.Clamp01(smoothness);
            maxPointSpacing = Mathf.Max(0.25f, maxPointSpacing);

            int n = controlPoints.Count;

            // 양 끝을 반사해 팬 컨트롤 포인트를 만든다. Catmull-Rom은 각 구간을 평가할 때
            // 앞뒤 이웃 제어점이 필요한데 양 끝 구간은 바깥쪽 이웃이 없어서, 끝점을 기준으로
            // 대칭 반사한 가상의 점을 만들어 동일한 4점 공식을 예외 없이 적용할 수 있게 한다.
            Vector2 phantomStart = 2f * controlPoints[0] - controlPoints[1];
            Vector2 phantomEnd = 2f * controlPoints[n - 1] - controlPoints[n - 2];

            Vector2[] extended = new Vector2[n + 2];
            extended[0] = phantomStart;
            for (int i = 0; i < n; i++)
            {
                extended[i + 1] = controlPoints[i];
            }

            extended[n + 1] = phantomEnd;

            List<Vector2> result = new List<Vector2>(Mathf.Min(MaxTotalPoints, n * 8));
            result.Add(controlPoints[0]);

            bool overflowed = false;

            // extended 인덱스 기준으로 seg = 1 .. n-1 구간이 원본 선분 controlPoints[seg-1] -> controlPoints[seg] 다.
            for (int seg = 1; seg < n && !overflowed; seg++)
            {
                Vector2 p0 = extended[seg - 1];
                Vector2 p1 = extended[seg];
                Vector2 p2 = extended[seg + 1];
                Vector2 p3 = extended[seg + 2];

                // 구심 파라미터화 크넛 값. 지수가 Alpha(=0.5)라서 세그먼트 길이가 제각각이어도
                // 곡선이 급커브에서 오버슈트하지 않는다(§ Alpha 주석 참조).
                float t0 = 0f;
                float t1 = t0 + Mathf.Max(KnotEpsilon, Mathf.Pow(Vector2.Distance(p0, p1), Alpha));
                float t2 = t1 + Mathf.Max(KnotEpsilon, Mathf.Pow(Vector2.Distance(p1, p2), Alpha));
                float t3 = t2 + Mathf.Max(KnotEpsilon, Mathf.Pow(Vector2.Distance(p2, p3), Alpha));

                float chord = Vector2.Distance(p1, p2);

                // 구간마다 개별로 분할 수를 정한다 — 고정 개수가 아니라 거리 기반이라,
                // 웨이포인트 간격이 제각각인 맵에서도 정점 밀도(및 그로 인한 적 이동 속도
                // 손실률)가 맵과 무관하게 일정해진다.
                int subdivisions = Mathf.Clamp(
                    Mathf.CeilToInt(chord / maxPointSpacing),
                    1,
                    MaxSubdivisionsPerSegment);

                for (int j = 1; j <= subdivisions; j++)
                {
                    bool isSegmentEnd = j == subdivisions;
                    Vector2 point;

                    if (isSegmentEnd)
                    {
                        // 부동소수 계산 오차로 원본 웨이포인트에서 어긋나면 안 된다 — 특히
                        // path[0]/path[^1]이 스폰 좌표로 그대로 쓰이므로 끝점 정확도가 중요하다.
                        point = p2;
                    }
                    else
                    {
                        float u = (float)j / subdivisions;
                        float t = Mathf.Lerp(t1, t2, u);

                        Vector2 curvePoint = EvaluateBarryGoldman(p0, p1, p2, p3, t0, t1, t2, t3, t);
                        Vector2 linearPoint = Vector2.Lerp(p1, p2, u);
                        point = Vector2.Lerp(linearPoint, curvePoint, smoothness);
                    }

                    if (!isSegmentEnd && result.Count > 0)
                    {
                        float distToPrev = Vector2.Distance(result[result.Count - 1], point);
                        if (distToPrev < DuplicateEpsilon)
                        {
                            continue;
                        }
                    }
                    else if (isSegmentEnd && result.Count > 0)
                    {
                        // 원본 제어점은 중복 제거 대상에서 예외다 — 끝점이 보존되어야 한다.
                        float distToPrev = Vector2.Distance(result[result.Count - 1], point);
                        if (distToPrev < DuplicateEpsilon)
                        {
                            Debug.LogWarning(
                                "[PathSmoothing] 동일 좌표의 웨이포인트가 연속으로 배치되어 중복점이 감지됨 (데이터 확인 필요).");
                        }
                    }

                    if (result.Count >= MaxTotalPoints)
                    {
                        Debug.LogError(
                            $"[PathSmoothing] 결과 정점이 상한({MaxTotalPoints})에 도달해 베이킹을 중단함. " +
                            "MapManager의 maxPointSpacing 값을 키워라.");
                        overflowed = true;
                        break;
                    }

                    result.Add(point);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Barry–Goldman 피라미드 알고리즘. 4점 윈도우(P0..P3)와 그 크넛(t0..t3)에 대해
        /// t ∈ [t1, t2] 구간의 곡선 위 점을 계산한다. 모든 분모는 KnotEpsilon으로 방어해
        /// 크넛이 겹치는 퇴화 케이스(중복 제어점)에서도 0 나눗셈이 나지 않게 한다.
        /// </summary>
        private static Vector2 EvaluateBarryGoldman(
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
            float t0, float t1, float t2, float t3,
            float t)
        {
            Vector2 a1 = (t1 - t) / Mathf.Max(KnotEpsilon, t1 - t0) * p0 + (t - t0) / Mathf.Max(KnotEpsilon, t1 - t0) * p1;
            Vector2 a2 = (t2 - t) / Mathf.Max(KnotEpsilon, t2 - t1) * p1 + (t - t1) / Mathf.Max(KnotEpsilon, t2 - t1) * p2;
            Vector2 a3 = (t3 - t) / Mathf.Max(KnotEpsilon, t3 - t2) * p2 + (t - t2) / Mathf.Max(KnotEpsilon, t3 - t2) * p3;

            Vector2 b1 = (t2 - t) / Mathf.Max(KnotEpsilon, t2 - t0) * a1 + (t - t0) / Mathf.Max(KnotEpsilon, t2 - t0) * a2;
            Vector2 b2 = (t3 - t) / Mathf.Max(KnotEpsilon, t3 - t1) * a2 + (t - t1) / Mathf.Max(KnotEpsilon, t3 - t1) * a3;

            Vector2 c = (t2 - t) / Mathf.Max(KnotEpsilon, t2 - t1) * b1 + (t - t1) / Mathf.Max(KnotEpsilon, t2 - t1) * b2;

            return c;
        }
    }
}
