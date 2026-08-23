using RCCom.Data;
using RCCom.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RCCom.Debugging
{
    /// <summary>
    /// 임시 디버그용 스크립트 — MapManager의 그리드/웨이포인트 세팅이 실제로 맞는지
    /// Play 모드에서 눈으로 확인하기 위한 용도. 확인 끝나면 지워도 된다.
    ///
    /// 사용법: 빈 GameObject에 붙이고 Map Manager 필드에 씬의 MapManager를 연결한 뒤 Play.
    /// 마우스 좌클릭한 위치가 건설 가능한 슬롯인지 콘솔에 출력하고, 등록된 웨이포인트를
    /// 순서대로 Scene 뷰에 노란 점+선으로 그려서 경로가 의도한 순서로 이어지는지 보여준다.
    /// </summary>
    public class MapManagerDebugTester : MonoBehaviour
    {
        public MapManager mapManager;
        public TowerKind testKind = TowerKind.Attack;

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Vector3 screenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
            worldPosition.z = 0f;

            if (!mapManager.TryGetSlotCell(worldPosition, out Vector3Int cell))
            {
                Debug.Log($"[MapDebug] 월드 ({worldPosition.x:F2}, {worldPosition.y:F2}) -> 슬롯 타일 없음");
                return;
            }

            bool canBuild = mapManager.CanBuild(testKind, cell);
            Debug.Log($"[MapDebug] 셀 {cell} -> 건설 가능({testKind}): {canBuild}");
        }

        private void OnDrawGizmos()
        {
            // Waypoints는 MapManager.Awake()에서 캐싱되므로 Play 모드에서만 값이 있다.
            if (!Application.isPlaying || mapManager == null || mapManager.Waypoints == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < mapManager.Waypoints.Count; i++)
            {
                Vector2 point = mapManager.Waypoints[i];
                // 정점이 98개(간격 0.5)까지 늘어날 수 있으므로 반경 0.15는 정점 간격보다
                // 커서 인접 구가 서로 겹쳐 경로가 노란 덩어리로 뭉개진다. 0.04로 줄여
                // 정점 간격이 좁아도 각 점이 개별 구로 분리되어 보이게 한다.
                Gizmos.DrawSphere(point, 0.04f);

                if (i > 0)
                {
                    Gizmos.DrawLine(mapManager.Waypoints[i - 1], point);
                }
            }
        }
    }
}
