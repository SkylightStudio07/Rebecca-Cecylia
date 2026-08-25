using RCCom.Managers;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace RCCom.EditorTools
{
    /// <summary>
    /// slotTilemap의 TilemapRenderer는 런타임에 항상 꺼져 있다(설치 슬롯은 순수 데이터라 화면에
    /// 그릴 필요가 없다). 그 상태 그대로는 Stage Studio 작업대에서 어떤 셀이 칠해져 있는지 눈으로
    /// 볼 수 없으므로, StageRouteTestScene에서만 별도 Gizmo로 칠해진 셀을 표시한다.
    /// </summary>
    [InitializeOnLoad]
    public static class StageBuildableCellGizmo
    {
        static StageBuildableCellGizmo()
        {
            SceneView.duringSceneGui += DrawBuildableCells;
        }

        private static void DrawBuildableCells(SceneView sceneView)
        {
            if (SceneManager.GetActiveScene().path != StageRouteAuthoringTool.TestScenePath)
            {
                return;
            }

            MapManager mapManager = Object.FindFirstObjectByType<MapManager>();
            if (mapManager == null)
            {
                return;
            }

            var serialized = new SerializedObject(mapManager);
            if (serialized.FindProperty("slotTilemap").objectReferenceValue is not Tilemap tilemap)
            {
                return;
            }

            BoundsInt bounds = tilemap.cellBounds;
            Handles.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell))
                {
                    continue;
                }

                Vector3 center = tilemap.GetCellCenterWorld(cell);
                Vector3 size = tilemap.cellSize;
                Handles.DrawWireCube(center, size);
            }
        }
    }
}
