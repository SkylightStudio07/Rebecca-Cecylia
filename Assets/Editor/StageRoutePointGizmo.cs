using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>씬 컴포넌트를 남기지 않고 테스트 경로의 의미와 연결 순서를 Scene View에 표시한다.</summary>
    [InitializeOnLoad]
    public static class StageRoutePointGizmo
    {
        static StageRoutePointGizmo()
        {
            SceneView.duringSceneGui += DrawRoute;
        }

        private static void DrawRoute(SceneView sceneView)
        {
            GameObject root = GameObject.Find("StageRouteAuthoring/RoutePoints");
            if (root == null) { return; }
            Transform points = root.transform;
            for (int i = 0; i < points.childCount; i++)
            {
                Transform point = points.GetChild(i);
                bool isStart = i == 0;
                bool isEnd = i == points.childCount - 1;
                Color color = isStart ? Color.green : isEnd ? Color.cyan : Color.yellow;
                Handles.color = color;
                float size = HandleUtility.GetHandleSize(point.position) * (isStart || isEnd ? 0.12f : 0.08f);
                Handles.SphereHandleCap(0, point.position, Quaternion.identity, size, EventType.Repaint);
                string label = isStart
                    ? "ENEMY SPAWN"
                    : isEnd
                        ? "BASE / ALLY DEPLOY"
                        : $"ROUTE {i:00}";
                Handles.Label(point.position + Vector3.up * (size * 1.5f), label);
                if (i + 1 < points.childCount)
                {
                    Handles.DrawLine(point.position, points.GetChild(i + 1).position, 3f);
                }
            }
        }
    }
}
