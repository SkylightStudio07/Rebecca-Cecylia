using System;
using RCCom.Definitions.Stage;
using RCCom.Managers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 1-2 제작 데이터를 초안으로 복제해 1-8을 만든다. 최초 생성 뒤에는 기존 1-8을 절대
    /// 덮어쓰지 않아 Stage Studio에서 사람이 수정한 경로·설치 셀을 보존한다.
    /// </summary>
    public static class StageEightSetup
    {
        public const string SourceStagePath = "Assets/Data/Stages/CH1/ch1-02.asset";
        public const string TargetStagePath = "Assets/Data/Stages/CH1/ch1-08.asset";
        public const string BackgroundPath = "Assets/Art/Backgrounds/Stages/stage 1-8.png";
        public const string AuthoringScenePath = "Assets/Scenes/Editor/_정현stage1-8.unity";

        [MenuItem("RCCom/Stages/Create 1-8 From 1-2")]
        public static void CreateStageEight()
        {
            StageDefinition source = AssetDatabase.LoadAssetAtPath<StageDefinition>(SourceStagePath);
            if (source == null)
            {
                throw new InvalidOperationException($"복제 원본 1-2를 찾지 못했습니다: {SourceStagePath}");
            }

            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            if (background == null)
            {
                throw new InvalidOperationException($"1-8 배경 Sprite를 찾지 못했습니다: {BackgroundPath}");
            }

            StageDefinition target = AssetDatabase.LoadAssetAtPath<StageDefinition>(TargetStagePath);
            bool created = false;
            if (target == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(TargetStagePath) != null)
                {
                    throw new InvalidOperationException(
                        $"1-8 경로에 StageDefinition이 아닌 에셋이 있습니다: {TargetStagePath}");
                }

                if (!AssetDatabase.CopyAsset(SourceStagePath, TargetStagePath))
                {
                    throw new InvalidOperationException($"1-2를 1-8로 복제하지 못했습니다: {TargetStagePath}");
                }
                AssetDatabase.ImportAsset(TargetStagePath, ImportAssetOptions.ForceSynchronousImport);
                target = AssetDatabase.LoadAssetAtPath<StageDefinition>(TargetStagePath);
                if (target == null)
                {
                    throw new InvalidOperationException("복제된 1-8 StageDefinition을 다시 불러오지 못했습니다.");
                }

                target.schemaVersion = StageDefinition.CurrentSchemaVersion;
                target.stageId = "ch1-08";
                target.chapterId = "ch1";
                target.displayName = "1-8";
                target.subtitle = "STAGE 1-8";
                target.description = "1-8 작전 구역입니다.";
                target.recommendedLevel = 29;
                target.order = 7;
                target.requiredBestWave = 7;
                target.remoteContent = false;
                target.descriptionBackground = background;
                target.battleBackground = background;
                target.battleBackgroundScale = CalculateFittedBackgroundScale(source, background);
                EditorUtility.SetDirty(target);
                created = true;
                Debug.Log("[StageEightSetup] 1-2 경로·설치 셀·웨이브를 복제해 1-8 초안을 생성했습니다.");
            }
            else
            {
                // 이후 Studio 편집분은 자동화 재실행보다 우선한다. Catalog만 다시 맞춘다.
                Debug.Log("[StageEightSetup] 기존 1-8을 발견해 덮어쓰지 않았습니다.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            StageCatalogBuilder.BuildCatalog();
            VerifyInternal(created);
        }

        [MenuItem("RCCom/Stages/Fit 1-8 Background To 1-2 Footprint")]
        public static void FitStageEightBackground()
        {
            StageDefinition source = AssetDatabase.LoadAssetAtPath<StageDefinition>(SourceStagePath);
            StageDefinition target = AssetDatabase.LoadAssetAtPath<StageDefinition>(TargetStagePath);
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            if (source == null || target == null || background == null)
            {
                throw new InvalidOperationException("1-8 배경 크기 보정에 필요한 에셋이 없습니다.");
            }

            target.battleBackgroundPosition = source.battleBackgroundPosition;
            target.battleBackgroundScale = CalculateFittedBackgroundScale(source, background);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            StageCatalogBuilder.BuildCatalog();
            VerifyInternal(false);
            Debug.Log("[StageEightSetup] 1-8 배경이 1-2와 같은 월드 영역을 채우도록 배율을 보정했습니다.");
        }

        [MenuItem("RCCom/Stages/Fit 1-8 Authoring Scene Background")]
        public static void FitStageEightAuthoringSceneBackground()
        {
            StageDefinition target = AssetDatabase.LoadAssetAtPath<StageDefinition>(TargetStagePath);
            if (target == null || target.battleBackground == null)
            {
                throw new InvalidOperationException("1-8 Definition 또는 전투 배경이 없습니다.");
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(AuthoringScenePath) == null)
            {
                throw new InvalidOperationException($"1-8 제작 보조 씬을 찾지 못했습니다: {AuthoringScenePath}");
            }

            Scene scene = EditorSceneManager.OpenScene(AuthoringScenePath, OpenSceneMode.Single);
            Transform backgroundTransform = FindTransform(scene, "AuthoringBattleBackground");
            if (backgroundTransform == null)
            {
                throw new InvalidOperationException("1-8 제작 보조 씬에 AuthoringBattleBackground가 없습니다.");
            }

            SpriteRenderer background = backgroundTransform.GetComponent<SpriteRenderer>();
            if (background == null)
            {
                throw new InvalidOperationException("AuthoringBattleBackground에 SpriteRenderer가 없습니다.");
            }

            background.sprite = target.battleBackground;
            background.enabled = true;
            background.sortingOrder = -100;
            backgroundTransform.position = new Vector3(
                target.battleBackgroundPosition.x, target.battleBackgroundPosition.y, backgroundTransform.position.z);
            backgroundTransform.localScale = new Vector3(
                target.battleBackgroundScale.x, target.battleBackgroundScale.y, 1f);

            MapManager mapManager = FindComponentInScene<MapManager>(scene);
            if (mapManager != null)
            {
                var serialized = new SerializedObject(mapManager);
                SerializedProperty previewStage = serialized.FindProperty("editorPreviewStage");
                if (previewStage != null) { previewStage.objectReferenceValue = target; }
                serialized.FindProperty("battleBackgroundRenderer").objectReferenceValue = background;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mapManager);
            }

            EditorUtility.SetDirty(background);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("1-8 제작 보조 씬 저장에 실패했습니다.");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (background.sprite != target.battleBackground ||
                backgroundTransform.localScale != new Vector3(
                    target.battleBackgroundScale.x, target.battleBackgroundScale.y, 1f))
            {
                throw new InvalidOperationException("1-8 제작 보조 씬 배경 배선 검증에 실패했습니다.");
            }

            Debug.Log($"[StageEightSetup] PASS — 1-8 제작 보조 씬 배경·배율 적용 " +
                      $"({target.battleBackgroundScale.x:F3}, {target.battleBackgroundScale.y:F3})");
        }

        [MenuItem("RCCom/Verify/Stage 1-8 Setup")]
        public static void Verify()
        {
            VerifyInternal(false);
        }

        private static void VerifyInternal(bool requireSourceDraftMatch)
        {
            StageDefinition source = AssetDatabase.LoadAssetAtPath<StageDefinition>(SourceStagePath);
            StageDefinition target = AssetDatabase.LoadAssetAtPath<StageDefinition>(TargetStagePath);
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            StageCatalog catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageCatalogBuilder.CatalogPath);
            StageCatalogEntry entry = catalog != null ? catalog.FindById("ch1-08") : null;

            if (source == null || target == null || background == null || entry == null)
            {
                throw new InvalidOperationException("1-8 Definition·배경·Catalog 배선 중 누락된 항목이 있습니다.");
            }
            if (!string.Equals(target.stageId, "ch1-08", StringComparison.Ordinal) ||
                target.order != 7 || target.requiredBestWave != 7 || target.battleBackground != background ||
                target.descriptionBackground != background)
            {
                throw new InvalidOperationException("1-8 식별 정보 또는 배경 연결이 올바르지 않습니다.");
            }
            if (requireSourceDraftMatch &&
                (target.battleBackgroundPosition != source.battleBackgroundPosition ||
                 !BackgroundFootprintsMatch(source, target) ||
                 !ListsMatch(target.routePoints, source.routePoints) ||
                 !ListsMatch(target.buildableCells, source.buildableCells) ||
                 target.waves == null || source.waves == null || target.waves.Count != source.waves.Count))
            {
                throw new InvalidOperationException("1-8에 1-2의 전장 초안이 정확히 복제되지 않았습니다.");
            }
            if (entry.stageDefinition != target || !entry.HasBattleData || entry.order != 7)
            {
                throw new InvalidOperationException("Stage Catalog의 1-8 로컬 참조가 올바르지 않습니다.");
            }

            string draftMessage = requireSourceDraftMatch ? ", 1-2 전장 초안 복제" : string.Empty;
            Debug.Log($"[StageEightSetup] PASS — 1-8 배경{draftMessage}, Catalog 등록 확인");
        }

        private static Vector2 CalculateFittedBackgroundScale(StageDefinition source, Sprite targetBackground)
        {
            if (source.battleBackground == null || targetBackground == null)
            {
                return source.battleBackgroundScale;
            }

            Vector2 sourceSize = source.battleBackground.bounds.size;
            Vector2 targetSize = targetBackground.bounds.size;
            float scaleX = targetSize.x > 0f
                ? source.battleBackgroundScale.x * sourceSize.x / targetSize.x
                : source.battleBackgroundScale.x;
            float scaleY = targetSize.y > 0f
                ? source.battleBackgroundScale.y * sourceSize.y / targetSize.y
                : source.battleBackgroundScale.y;
            return new Vector2(scaleX, scaleY);
        }

        private static bool BackgroundFootprintsMatch(StageDefinition source, StageDefinition target)
        {
            if (source.battleBackground == null || target.battleBackground == null)
            {
                return source.battleBackground == target.battleBackground;
            }

            Vector2 sourceFootprint = Vector2.Scale(
                source.battleBackground.bounds.size, source.battleBackgroundScale);
            Vector2 targetFootprint = Vector2.Scale(
                target.battleBackground.bounds.size, target.battleBackgroundScale);
            return Mathf.Approximately(sourceFootprint.x, targetFootprint.x) &&
                   Mathf.Approximately(sourceFootprint.y, targetFootprint.y);
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in transforms)
                {
                    if (candidate.name == objectName) { return candidate; }
                }
            }
            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) { return component; }
            }
            return null;
        }

        private static bool ListsMatch<T>(System.Collections.Generic.IReadOnlyList<T> left,
            System.Collections.Generic.IReadOnlyList<T> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!Equals(left[i], right[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
