using System;
using System.Collections.Generic;
using RCCom.Definitions.Stage;
using RCCom.Managers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            mapSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(mapManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = pointsRoot.GetChild(0).gameObject;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
            Debug.Log($"[StageRouteAuthoring] 테스트 씬 로드 완료: {stage.stageId} / {route.Count} points");
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
            }

            stage.schemaVersion = StageDefinition.CurrentSchemaVersion;
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[StageRouteAuthoring] StageDefinition 캡처 완료: {stage.stageId} / {stage.routePoints.Count} points");
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
            Debug.Log("[StageRouteAuthoring] DefenseScene 전투 배경 렌더러 배선 완료");
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
