using System;
using System.Collections.Generic;
using RCCom.Definitions.Stage;
using RCCom.Managers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 공용 DefenseScene의 복사본을 경로 제작 작업대로 사용하고, 최종 좌표만 StageDefinition에
    /// 저장한다. 테스트 씬 자체를 런타임 콘텐츠로 만들지 않아 스테이지별 씬 복제를 막는다.
    /// </summary>
    public static class StageRouteAuthoringTool
    {
        public const string DefenseScenePath = "Assets/Scenes/DefenseScene.unity";
        public const string TestScenePath = "Assets/Scenes/Editor/StageRouteTestScene.unity";
        private const string AuthoringRootName = "StageRouteAuthoring";
        private const string PointsRootName = "RoutePoints";
        private const string AuthoringBackgroundName = "AuthoringBattleBackground";
        private const string RuntimeBackgroundName = "StageBattleBackground";
        private const string DefaultMapBackgroundName = "Square";
        private const string BuildableSlotTilePath = "Assets/Data/Tilemaps/BuildableSlotTile.asset";

        public static void OpenAndLoad(StageDefinition stage)
        {
            EnsureEditMode();
            if (stage == null) { throw new ArgumentNullException(nameof(stage)); }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }

            EnsureTestSceneAsset();
            EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            LoadIntoOpenTestScene(stage);
        }

        public static void LoadIntoOpenTestScene(StageDefinition stage)
        {
            EnsureEditMode();
            if (stage == null) { throw new ArgumentNullException(nameof(stage)); }
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != TestScenePath)
            {
                throw new InvalidOperationException("StageRouteTestScene을 먼저 열어 주세요.");
            }

            MapManager mapManager = FindMapManager(scene);
            if (mapManager == null) { throw new InvalidOperationException("테스트 씬에 MapManager가 없습니다."); }

            GameObject authoringRoot = FindRoot(scene, AuthoringRootName);
            if (authoringRoot == null)
            {
                authoringRoot = new GameObject(AuthoringRootName);
                authoringRoot.tag = "EditorOnly";
            }

            Transform pointsRoot = FindOrCreateChild(authoringRoot.transform, PointsRootName);
            ClearChildren(pointsRoot);
            List<Vector2> route = stage.routePoints != null && stage.routePoints.Count >= 2
                ? new List<Vector2>(stage.routePoints)
                : ReadLegacyRoute();
            if (route.Count < 2)
            {
                throw new InvalidOperationException("기본 DefenseScene에서도 유효한 경로를 찾지 못했습니다.");
            }

            var pointTransforms = new List<Transform>(route.Count);
            for (int i = 0; i < route.Count; i++)
            {
                string pointName = i == 0
                    ? "00_ENEMY_SPAWN"
                    : i == route.Count - 1
                        ? $"{i:00}_BASE_ALLY_DEPLOY"
                        : $"{i:00}_ROUTE";
                var pointObject = new GameObject(pointName);
                pointObject.transform.SetParent(pointsRoot, false);
                pointObject.transform.position = new Vector3(route[i].x, route[i].y, 0f);
                pointTransforms.Add(pointObject.transform);
            }

            Transform obsoleteBackground = authoringRoot.transform.Find(RuntimeBackgroundName);
            if (obsoleteBackground != null)
            {
                UnityEngine.Object.DestroyImmediate(obsoleteBackground.gameObject);
            }
            Transform backgroundTransform = FindOrCreateChild(authoringRoot.transform, AuthoringBackgroundName);
            SpriteRenderer background = backgroundTransform.GetComponent<SpriteRenderer>();
            if (background == null) { background = backgroundTransform.gameObject.AddComponent<SpriteRenderer>(); }
            background.sprite = stage.battleBackground;
            background.enabled = stage.battleBackground != null;
            background.sortingOrder = -100;
            backgroundTransform.position = new Vector3(
                stage.battleBackgroundPosition.x, stage.battleBackgroundPosition.y, 5f);
            backgroundTransform.localScale = new Vector3(
                stage.battleBackgroundScale.x, stage.battleBackgroundScale.y, 1f);

            var mapSerialized = new SerializedObject(mapManager);
            SerializedProperty waypoints = mapSerialized.FindProperty("waypoints");
            waypoints.arraySize = pointTransforms.Count;
            for (int i = 0; i < pointTransforms.Count; i++)
            {
                waypoints.GetArrayElementAtIndex(i).objectReferenceValue = pointTransforms[i];
            }
            mapSerialized.FindProperty("battleBackgroundRenderer").objectReferenceValue = background;
            mapSerialized.FindProperty("pathSmoothness").floatValue = stage.pathSmoothness;
            mapSerialized.FindProperty("maxPointSpacing").floatValue = stage.maxPointSpacing;
            SerializedProperty previewStage = mapSerialized.FindProperty("editorPreviewStage");
            if (previewStage != null) { previewStage.objectReferenceValue = stage; }

            Tilemap slotTilemap = mapSerialized.FindProperty("slotTilemap").objectReferenceValue as Tilemap;
            TileBase slotTile = mapSerialized.FindProperty("buildableSlotTile").objectReferenceValue as TileBase;
            mapSerialized.ApplyModifiedPropertiesWithoutUndo();

            LoadBuildableCells(stage, slotTilemap, slotTile);

            EditorUtility.SetDirty(mapManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = pointsRoot.GetChild(0).gameObject;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
            int loadedCellCount = stage.buildableCells?.Count ?? 0;
            Debug.Log($"[StageRouteAuthoring] 테스트 씬 로드 완료: {stage.stageId} / " +
                      $"{route.Count} points / {loadedCellCount} buildable cells " +
                      $"({(loadedCellCount > 0 ? "stage 저장값" : "빈 상태 — 필요하면 Copy Default Layout을 눌러 시작점으로 쓰세요")})");
        }

        /// <summary>
        /// stage.buildableCells가 있으면 그 좌표로 slotTilemap을 다시 칠한다. 없으면 DefenseScene
        /// 기본 레이아웃을 몰래 채워 넣지 않고 비워 둔다 — 예전에는 여기서 자동으로 옛 기본
        /// 214칸(사인파 도로 모양)을 미리 칠해 뒀는데, 맵 모양이 다른 새 스테이지를 그 위에 이어
        /// 칠하고 Capture하면 사용자가 원치 않은 옛 도로 칸까지 함께 저장돼 버리는 문제가 있었다.
        /// 옛 기본 레이아웃을 시작점으로 쓰고 싶으면 CopyDefaultLayoutIntoTestScene을 직접 눌러야
        /// 한다(명시적 액션으로 분리).
        /// </summary>
        private static void LoadBuildableCells(StageDefinition stage, Tilemap slotTilemap, TileBase slotTile)
        {
            if (slotTilemap == null)
            {
                Debug.LogWarning("[StageRouteAuthoring] slotTilemap 배선이 없어 설치 슬롯을 불러오지 못했습니다.");
                return;
            }

            slotTilemap.ClearAllTiles();

            if (stage.buildableCells == null || stage.buildableCells.Count == 0)
            {
                EditorUtility.SetDirty(slotTilemap);
                return;
            }

            if (slotTile == null)
            {
                Debug.LogWarning("[StageRouteAuthoring] buildableSlotTile 배선이 없어 설치 슬롯을 불러오지 못했습니다. " +
                                  "RCCom/Stages/Setup Runtime Battlefield Background를 먼저 실행하세요.");
                return;
            }

            foreach (Vector3Int cell in stage.buildableCells)
            {
                slotTilemap.SetTile(cell, slotTile);
            }
            EditorUtility.SetDirty(slotTilemap);
        }

        /// <summary>
        /// DefenseScene에 원래 칠해진 기본 설치 슬롯 레이아웃을 현재 열린 Test Scene의 SlotMarkers에
        /// 그대로 복사한다. Load와 달리 사용자가 명시적으로 눌렀을 때만 실행되며, 기존에 칠해진
        /// 내용을 지우고 덮어쓴다 — "이 기본 레이아웃을 시작점으로 다시 쓰고 싶다"는 의도가 분명한
        /// 경우에만 호출해야 한다.
        /// </summary>
        public static void CopyDefaultLayoutIntoTestScene()
        {
            EnsureEditMode();
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != TestScenePath)
            {
                throw new InvalidOperationException("StageRouteTestScene을 먼저 열어 주세요.");
            }

            MapManager mapManager = FindMapManager(scene);
            if (mapManager == null) { throw new InvalidOperationException("테스트 씬에 MapManager가 없습니다."); }

            var mapSerialized = new SerializedObject(mapManager);
            Tilemap slotTilemap = mapSerialized.FindProperty("slotTilemap").objectReferenceValue as Tilemap;
            TileBase slotTile = mapSerialized.FindProperty("buildableSlotTile").objectReferenceValue as TileBase;
            if (slotTilemap == null)
            {
                throw new InvalidOperationException("테스트 씬의 slotTilemap 배선이 없습니다.");
            }
            if (slotTile == null)
            {
                throw new InvalidOperationException("buildableSlotTile 배선이 없습니다. " +
                                                      "RCCom/Stages/Setup Runtime Battlefield Background를 먼저 실행하세요.");
            }

            List<Vector3Int> legacyCells = ReadLegacyBuildableCells();
            slotTilemap.ClearAllTiles();
            foreach (Vector3Int cell in legacyCells)
            {
                slotTilemap.SetTile(cell, slotTile);
            }
            EditorUtility.SetDirty(slotTilemap);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[StageRouteAuthoring] DefenseScene 기본 슬롯 레이아웃 {legacyCells.Count}칸을 테스트 씬에 복사했습니다.");
        }

        private static List<Vector3Int> ReadPaintedCells(Tilemap tilemap)
        {
            tilemap.CompressBounds();
            BoundsInt bounds = tilemap.cellBounds;
            var cells = new List<Vector3Int>();
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (tilemap.HasTile(cell)) { cells.Add(cell); }
            }
            return cells;
        }

        private static List<Vector3Int> ReadLegacyBuildableCells()
        {
            Scene active = SceneManager.GetActiveScene();
            bool alreadyOpen = active.path == DefenseScenePath;
            Scene source = alreadyOpen
                ? active
                : EditorSceneManager.OpenScene(DefenseScenePath, OpenSceneMode.Additive);
            try
            {
                MapManager mapManager = FindMapManager(source);
                if (mapManager == null) { return new List<Vector3Int>(); }
                var serialized = new SerializedObject(mapManager);
                Tilemap tilemap = serialized.FindProperty("slotTilemap").objectReferenceValue as Tilemap;
                return tilemap != null ? ReadPaintedCells(tilemap) : new List<Vector3Int>();
            }
            finally
            {
                if (!alreadyOpen) { EditorSceneManager.CloseScene(source, true); }
            }
        }

        public static void CaptureFromOpenTestScene(StageDefinition stage)
        {
            EnsureEditMode();
            if (stage == null) { throw new ArgumentNullException(nameof(stage)); }
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != TestScenePath)
            {
                throw new InvalidOperationException("StageRouteTestScene을 먼저 열어 주세요.");
            }

            GameObject authoringRoot = FindRoot(scene, AuthoringRootName);
            Transform pointsRoot = authoringRoot != null ? authoringRoot.transform.Find(PointsRootName) : null;
            if (pointsRoot == null || pointsRoot.childCount < 2)
            {
                throw new InvalidOperationException("캡처할 Route Point가 최소 2개 필요합니다.");
            }

            Undo.RecordObject(stage, "Capture Stage Route");
            stage.routePoints ??= new List<Vector2>();
            stage.routePoints.Clear();
            for (int i = 0; i < pointsRoot.childCount; i++)
            {
                Vector3 position = pointsRoot.GetChild(i).position;
                stage.routePoints.Add(new Vector2(position.x, position.y));
            }

            Transform backgroundTransform = authoringRoot.transform.Find(AuthoringBackgroundName);
            SpriteRenderer background = backgroundTransform != null
                ? backgroundTransform.GetComponent<SpriteRenderer>()
                : null;
            if (background != null)
            {
                stage.battleBackground = background.sprite;
                stage.battleBackgroundPosition = backgroundTransform.position;
                stage.battleBackgroundScale = new Vector2(
                    backgroundTransform.localScale.x, backgroundTransform.localScale.y);
            }

            MapManager mapManager = FindMapManager(scene);
            if (mapManager != null)
            {
                var mapSerialized = new SerializedObject(mapManager);
                stage.pathSmoothness = mapSerialized.FindProperty("pathSmoothness").floatValue;
                stage.maxPointSpacing = mapSerialized.FindProperty("maxPointSpacing").floatValue;

                if (mapSerialized.FindProperty("slotTilemap").objectReferenceValue is Tilemap slotTilemap)
                {
                    stage.buildableCells = ReadPaintedCells(slotTilemap);
                }
            }

            stage.schemaVersion = StageDefinition.CurrentSchemaVersion;
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            int cellCount = stage.buildableCells?.Count ?? 0;
            Debug.Log($"[StageRouteAuthoring] StageDefinition 캡처 완료: {stage.stageId} / " +
                      $"{stage.routePoints.Count} points / {cellCount} buildable cells");
        }

        [MenuItem("RCCom/Stages/Setup Runtime Battlefield Background")]
        public static void SetupRuntimeBattlefieldBackground()
        {
            EnsureEditMode();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            string previousPath = SceneManager.GetActiveScene().path;
            Scene scene = EditorSceneManager.OpenScene(DefenseScenePath, OpenSceneMode.Single);
            MapManager mapManager = FindMapManager(scene);
            if (mapManager == null) { throw new InvalidOperationException("DefenseScene에 MapManager가 없습니다."); }

            GameObject backgroundObject = FindRoot(scene, RuntimeBackgroundName);
            if (backgroundObject == null)
            {
                backgroundObject = new GameObject(RuntimeBackgroundName, typeof(SpriteRenderer));
                backgroundObject.transform.position = new Vector3(0f, 0f, 5f);
            }
            SpriteRenderer background = backgroundObject.GetComponent<SpriteRenderer>();
            background.sortingOrder = -100;
            background.enabled = false;

            var serialized = new SerializedObject(mapManager);
            serialized.FindProperty("battleBackgroundRenderer").objectReferenceValue = background;

            // 엔드리스 모드용 기본 맵("Square")은 스테이지 배경이 적용되면 MapManager가 직접 꺼야
            // 하므로 참조가 필요하다. 없으면 경고만 남기고 넘어간다 — 씬 구조가 달라진 상황을
            // 여기서 새로 만들어 강제하지 않는다.
            GameObject defaultMapBackground = FindRoot(scene, DefaultMapBackgroundName);
            SpriteRenderer defaultMapRenderer = defaultMapBackground != null
                ? defaultMapBackground.GetComponent<SpriteRenderer>()
                : null;
            if (defaultMapRenderer != null)
            {
                serialized.FindProperty("defaultMapBackgroundRenderer").objectReferenceValue = defaultMapRenderer;
            }
            else
            {
                Debug.LogWarning($"[StageRouteAuthoring] '{DefaultMapBackgroundName}' 오브젝트를 찾지 못해 " +
                                  "defaultMapBackgroundRenderer 배선을 건너뜁니다.");
            }

            TileBase slotTile = AssetDatabase.LoadAssetAtPath<TileBase>(BuildableSlotTilePath);
            if (slotTile != null)
            {
                serialized.FindProperty("buildableSlotTile").objectReferenceValue = slotTile;
            }
            else
            {
                Debug.LogWarning($"[StageRouteAuthoring] {BuildableSlotTilePath}를 찾지 못해 " +
                                  "buildableSlotTile 배선을 건너뜁니다.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mapManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!string.IsNullOrWhiteSpace(previousPath) && previousPath != DefenseScenePath)
            {
                EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StageRouteAuthoring] DefenseScene 전투 배경·기본 맵·설치 슬롯 타일 배선 완료");
        }

        [MenuItem("RCCom/Stages/Migrate Legacy Route To Missing Stages")]
        public static void MigrateLegacyRouteToMissingStages()
        {
            EnsureEditMode();
            List<Vector2> route = ReadLegacyRoute();
            if (route.Count < 2) { throw new InvalidOperationException("DefenseScene의 기존 경로를 읽지 못했습니다."); }

            int changed = 0;
            foreach (StageDefinition stage in StageCatalogBuilder.LoadDefinitions())
            {
                if (stage.routePoints != null && stage.routePoints.Count >= 2) { continue; }
                Undo.RecordObject(stage, "Migrate Legacy Stage Route");
                stage.routePoints = new List<Vector2>(route);
                stage.pathSmoothness = 1f;
                stage.maxPointSpacing = 0.25f;
                stage.schemaVersion = StageDefinition.CurrentSchemaVersion;
                EditorUtility.SetDirty(stage);
                changed++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[StageRouteAuthoring] 기존 DefenseScene 경로를 {changed}개 StageDefinition에 이관 완료");
        }

        private static void EnsureTestSceneAsset()
        {
            EnsureFolder("Assets/Scenes/Editor");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) != null) { return; }
            if (!AssetDatabase.CopyAsset(DefenseScenePath, TestScenePath))
            {
                throw new InvalidOperationException("StageRouteTestScene 복사본 생성에 실패했습니다.");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static List<Vector2> ReadLegacyRoute()
        {
            Scene active = SceneManager.GetActiveScene();
            bool alreadyOpen = active.path == DefenseScenePath;
            Scene source = alreadyOpen
                ? active
                : EditorSceneManager.OpenScene(DefenseScenePath, OpenSceneMode.Additive);
            try
            {
                MapManager mapManager = FindMapManager(source);
                if (mapManager == null) { return new List<Vector2>(); }
                var serialized = new SerializedObject(mapManager);
                SerializedProperty waypoints = serialized.FindProperty("waypoints");
                var result = new List<Vector2>(waypoints.arraySize);
                for (int i = 0; i < waypoints.arraySize; i++)
                {
                    Transform point = waypoints.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (point != null) { result.Add(point.position); }
                }
                return result;
            }
            finally
            {
                if (!alreadyOpen) { EditorSceneManager.CloseScene(source, true); }
            }
        }

        private static MapManager FindMapManager(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MapManager manager = root.GetComponentInChildren<MapManager>(true);
                if (manager != null) { return manager; }
            }
            return null;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) { return root; }
            }
            return null;
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) { return child; }
            var childObject = new GameObject(name);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) { AssetDatabase.CreateFolder(current, parts[i]); }
                current = next;
            }
        }

        private static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stage Route 제작 도구는 Edit Mode에서만 사용할 수 있습니다.");
            }
        }
    }
}
