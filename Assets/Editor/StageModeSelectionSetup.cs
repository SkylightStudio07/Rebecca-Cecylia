using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Stage;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 오퍼레이터 선택 뒤의 모드 선택·CH1 스테이지 맵 UGUI와 샘플 스테이지 데이터를 반복 생성하고 배선한다.
    /// </summary>
    public static class StageModeSelectionSetup
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string ModeSystemName = "ModeSelectionSystem";
        private const string StageSystemName = "StageSelectionSystem";
        private const string CatalogPath = "Assets/Data/Stages/StageCatalog.asset";
        private const string StageDefinitionFolder = "Assets/Data/Stages/CH1";
        private const string NodePrefabPath = "Assets/Data/Prefabs/StageNode.prefab";
        private const string KoreanFontPath = "Assets/Resource/Font/Pretendard-Bold SDF.asset";
        private const string TitleFontPath = "Assets/Resource/Font/PlayfairDisplay-ExtraBold SDF.asset";
        private const string ModeButtonSpriteSheetPath =
            "Assets/Art/UI/Stage Selection/StageSelectionUISpriteSheet.png";
        private const string StageNodeSpriteSheetPath =
            "Assets/Art/UI/Stage Selection/StageSelectionsSmallPanel.png";
        private const string GeneratedLabel = "RCCom.GeneratedStageModeUI";

        [MenuItem("RCCom/Stages/Build Mode and Chapter UI")]
        public static void Build()
        {
            Scene scene = OpenTitleSceneSafely();
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null) { throw new InvalidOperationException("TitleScene에 Canvas가 없습니다."); }

            TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            if (koreanFont == null || titleFont == null)
            {
                throw new InvalidOperationException("스테이지 UI용 TMP 글꼴을 찾지 못했습니다.");
            }

            StageCatalog catalog = BuildCatalog();
            StageNodeView nodePrefab = BuildNodePrefab(koreanFont);
            CanvasGroup mainMenuGroup = GetOrAddCanvasGroup(canvas.transform.Find("MainMenuBackground")?.gameObject);
            OperatorSelectionUI operatorSelectionUI = UnityEngine.Object.FindFirstObjectByType<OperatorSelectionUI>(
                FindObjectsInactive.Include);
            if (operatorSelectionUI == null)
            {
                throw new InvalidOperationException("TitleScene의 OperatorSelectionUI를 찾지 못했습니다.");
            }

            ModeSelectionUI modeController = BuildModePanel(canvas.transform, mainMenuGroup, operatorSelectionUI,
                titleFont, koreanFont, out GameObject modePanel, out Button stageModeButton,
                out Button endlessButton, out Button modeBackButton, out TextMeshProUGUI modeOperatorText,
                out TextMeshProUGUI modeStatusText);

            StageSelectionUI stageController = BuildStagePanel(canvas.transform, mainMenuGroup, modeController,
                catalog, nodePrefab, titleFont, koreanFont, out GameObject stagePanel,
                out ScrollRect nodeScrollRect, out Transform nodeContent, out Button previousNodeButton,
                out Button nextNodeButton, out TextMeshProUGUI chapterText, out TextMeshProUGUI selectedTitle,
                out TextMeshProUGUI selectedSubtitle, out TextMeshProUGUI selectedDescription,
                out TextMeshProUGUI recommendedLevel, out Image descriptionBackground,
                out TextMeshProUGUI stageStatus, out Button startStageButton, out Button stageBackButton);

            var modeSerialized = new SerializedObject(modeController);
            SetReference(modeSerialized, "panel", modePanel);
            SetReference(modeSerialized, "mainMenuGroup", mainMenuGroup);
            SetReference(modeSerialized, "operatorSelectionUI", operatorSelectionUI);
            SetReference(modeSerialized, "stageSelectionUI", stageController);
            SetReference(modeSerialized, "operatorNameText", modeOperatorText);
            SetReference(modeSerialized, "statusText", modeStatusText);
            SetReference(modeSerialized, "stageButton", stageModeButton);
            SetReference(modeSerialized, "endlessButton", endlessButton);
            SetReference(modeSerialized, "backButton", modeBackButton);
            modeSerialized.ApplyModifiedPropertiesWithoutUndo();

            var stageSerialized = new SerializedObject(stageController);
            SetReference(stageSerialized, "catalog", catalog);
            SetReference(stageSerialized, "panel", stagePanel);
            SetReference(stageSerialized, "mainMenuGroup", mainMenuGroup);
            SetReference(stageSerialized, "modeSelectionUI", modeController);
            SetReference(stageSerialized, "nodeScrollRect", nodeScrollRect);
            SetReference(stageSerialized, "nodeContent", nodeContent);
            SetReference(stageSerialized, "nodePrefab", nodePrefab);
            SetReference(stageSerialized, "previousNodeButton", previousNodeButton);
            SetReference(stageSerialized, "nextNodeButton", nextNodeButton);
            SetReference(stageSerialized, "chapterText", chapterText);
            SetReference(stageSerialized, "selectedTitleText", selectedTitle);
            SetReference(stageSerialized, "selectedSubtitleText", selectedSubtitle);
            SetReference(stageSerialized, "selectedDescriptionText", selectedDescription);
            SetReference(stageSerialized, "recommendedLevelText", recommendedLevel);
            SetReference(stageSerialized, "descriptionBackgroundImage", descriptionBackground);
            SetReference(stageSerialized, "statusText", stageStatus);
            SetReference(stageSerialized, "startStageButton", startStageButton);
            SetReference(stageSerialized, "backButton", stageBackButton);
            stageSerialized.ApplyModifiedPropertiesWithoutUndo();

            BindButton(stageModeButton, modeController.SelectStageMode);
            BindButton(endlessButton, modeController.SelectEndlessMode);
            BindButton(modeBackButton, modeController.Back);
            BindButton(startStageButton, stageController.StartSelectedStage);
            BindButton(stageBackButton, stageController.Close);
            BindButton(previousNodeButton, stageController.ScrollPrevious);
            BindButton(nextNodeButton, stageController.ScrollNext);

            var operatorSerialized = new SerializedObject(operatorSelectionUI);
            SetReference(operatorSerialized, "modeSelectionUI", modeController);
            operatorSerialized.ApplyModifiedPropertiesWithoutUndo();

            modePanel.SetActive(false);
            stagePanel.SetActive(false);
            EditorUtility.SetDirty(modeController);
            EditorUtility.SetDirty(stageController);
            EditorUtility.SetDirty(operatorSelectionUI);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StageModeSelectionSetup] 모드 선택·CH1 스테이지 맵 UGUI 배치 완료");
        }

        [MenuItem("RCCom/Stages/Validate Mode and Chapter UI")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            ModeSelectionUI mode = UnityEngine.Object.FindFirstObjectByType<ModeSelectionUI>(
                FindObjectsInactive.Include);
            StageSelectionUI stage = UnityEngine.Object.FindFirstObjectByType<StageSelectionUI>(
                FindObjectsInactive.Include);
            OperatorSelectionUI operatorSelection = UnityEngine.Object.FindFirstObjectByType<OperatorSelectionUI>(
                FindObjectsInactive.Include);
            StageCatalog catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
            StageNodeView prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NodePrefabPath)
                ?.GetComponent<StageNodeView>();

            if (scene.path != TitleScenePath || mode == null || stage == null || operatorSelection == null ||
                catalog == null || catalog.entries == null || catalog.entries.Count == 0 || prefab == null)
            {
                throw new InvalidOperationException("모드·CH1 스테이지 UI 필수 구성이 누락되었습니다.");
            }

            foreach (StageCatalogEntry entry in catalog.entries)
            {
                if (entry == null || entry.stageDefinition == null || !entry.stageDefinition.IsPlayable)
                {
                    throw new InvalidOperationException($"스테이지 전투 Definition 연결이 누락되었습니다: {entry?.stageId}");
                }
            }

            var operatorSerialized = new SerializedObject(operatorSelection);
            if (operatorSerialized.FindProperty("modeSelectionUI").objectReferenceValue != mode)
            {
                throw new InvalidOperationException("OperatorSelectionUI의 모드 선택 UI 연결이 누락되었습니다.");
            }

            var modeSerialized = new SerializedObject(mode);
            if (modeSerialized.FindProperty("stageSelectionUI").objectReferenceValue != stage ||
                modeSerialized.FindProperty("stageButton").objectReferenceValue == null ||
                modeSerialized.FindProperty("endlessButton").objectReferenceValue == null)
            {
                throw new InvalidOperationException("ModeSelectionUI 참조 배선이 올바르지 않습니다.");
            }

            ValidateSpriteButton((Button)modeSerialized.FindProperty("backButton").objectReferenceValue,
                "StageSelectionUISpriteSheet_0", "StageSelectionUISpriteSheet_2");
            ValidateSpriteButton((Button)modeSerialized.FindProperty("stageButton").objectReferenceValue,
                "StageSelectionUISpriteSheet_5", "StageSelectionUISpriteSheet_4");
            ValidateSpriteButton((Button)modeSerialized.FindProperty("endlessButton").objectReferenceValue,
                "StageSelectionUISpriteSheet_6", "StageSelectionUISpriteSheet_3");

            var stageSerialized = new SerializedObject(stage);
            if (stageSerialized.FindProperty("catalog").objectReferenceValue != catalog ||
                stageSerialized.FindProperty("nodePrefab").objectReferenceValue != prefab ||
                stageSerialized.FindProperty("nodeScrollRect").objectReferenceValue == null ||
                stageSerialized.FindProperty("previousNodeButton").objectReferenceValue == null ||
                stageSerialized.FindProperty("nextNodeButton").objectReferenceValue == null ||
                stageSerialized.FindProperty("startStageButton").objectReferenceValue == null ||
                stageSerialized.FindProperty("recommendedLevelText").objectReferenceValue == null ||
                stageSerialized.FindProperty("descriptionBackgroundImage").objectReferenceValue == null)
            {
                throw new InvalidOperationException("StageSelectionUI 참조 배선이 올바르지 않습니다.");
            }

            var nodeSerialized = new SerializedObject(prefab);
            if (nodeSerialized.FindProperty("background").objectReferenceValue == null ||
                nodeSerialized.FindProperty("backgroundRect").objectReferenceValue == null ||
                nodeSerialized.FindProperty("availableSprite").objectReferenceValue !=
                LoadSprite(StageNodeSpriteSheetPath, "StageSelectionsSmallPanel_0") ||
                nodeSerialized.FindProperty("selectedSprite").objectReferenceValue !=
                LoadSprite(StageNodeSpriteSheetPath, "StageSelectionsSmallPanel_1") ||
                nodeSerialized.FindProperty("lockedSprite").objectReferenceValue !=
                LoadSprite(StageNodeSpriteSheetPath, "StageSelectionsSmallPanel_2"))
            {
                throw new InvalidOperationException("StageNode의 일반·선택·잠금 스프라이트 배선이 올바르지 않습니다.");
            }

            Debug.Log("[StageModeSelectionSetup] 모드 선택·CH1 스테이지 맵 UI 검증 통과");
        }

        private static ModeSelectionUI BuildModePanel(Transform canvas, CanvasGroup mainMenuGroup,
            OperatorSelectionUI operatorSelectionUI, TMP_FontAsset titleFont, TMP_FontAsset koreanFont,
            out GameObject panel, out Button stageButton, out Button endlessButton, out Button backButton,
            out TextMeshProUGUI operatorText, out TextMeshProUGUI statusText)
        {
            GameObject system = GetOrCreateSystem(ModeSystemName, canvas, typeof(ModeSelectionUI));
            ClearChildren(system.transform);
            panel = CreateImageObject("Panel", system.transform, new Color(0.015f, 0.03f, 0.055f, 0.97f));
            Stretch(panel.transform);
            CreateAccent(panel.transform, new Vector2(0.07f, 0.16f), new Vector2(0.078f, 0.84f));
            CreateText("Title", panel.transform, "SELECT OPERATION MODE", titleFont, 46f,
                new Vector2(0.11f, 0.68f), new Vector2(0.7f, 0.82f), TextAlignmentOptions.Left);
            operatorText = CreateText("Operator", panel.transform, "OPERATOR LOADOUT READY", koreanFont, 21f,
                new Vector2(0.11f, 0.59f), new Vector2(0.62f, 0.66f), TextAlignmentOptions.Left);
            operatorText.color = new Color(0.18f, 0.72f, 1f, 1f);
            statusText = CreateText("Status", panel.transform, "작전 모드를 선택하십시오.", koreanFont, 20f,
                new Vector2(0.11f, 0.14f), new Vector2(0.68f, 0.22f), TextAlignmentOptions.Left);
            statusText.color = new Color(0.75f, 0.82f, 0.9f, 1f);

            stageButton = CreateButton("StageButton", panel.transform, "STAGE MODE\nCHAPTER CAMPAIGN", koreanFont,
                new Vector2(0.12f, 0.34f), new Vector2(0.47f, 0.54f), new Color(0.03f, 0.25f, 0.44f, 0.97f));
            endlessButton = CreateButton("EndlessButton", panel.transform, "ENDLESS MODE\nLEGACY PROCEDURAL WAVES",
                koreanFont, new Vector2(0.53f, 0.34f), new Vector2(0.88f, 0.54f),
                new Color(0.1f, 0.13f, 0.18f, 0.97f));
            backButton = CreateButton("BackButton", panel.transform, "BACK", koreanFont,
                new Vector2(0.78f, 0.09f), new Vector2(0.9f, 0.16f), new Color(0.05f, 0.07f, 0.1f, 0.96f));

            ConfigureSpriteButton(backButton, "StageSelectionUISpriteSheet_0", "StageSelectionUISpriteSheet_2");
            ConfigureSpriteButton(stageButton, "StageSelectionUISpriteSheet_5", "StageSelectionUISpriteSheet_4");
            ConfigureSpriteButton(endlessButton, "StageSelectionUISpriteSheet_6", "StageSelectionUISpriteSheet_3");
            return system.GetComponent<ModeSelectionUI>();
        }

        private static StageSelectionUI BuildStagePanel(Transform canvas, CanvasGroup mainMenuGroup,
            ModeSelectionUI modeController, StageCatalog catalog, StageNodeView nodePrefab,
            TMP_FontAsset titleFont, TMP_FontAsset koreanFont, out GameObject panel, out ScrollRect nodeScrollRect,
            out Transform nodeContent, out Button previousNodeButton, out Button nextNodeButton,
            out TextMeshProUGUI chapterText, out TextMeshProUGUI selectedTitle,
            out TextMeshProUGUI selectedSubtitle, out TextMeshProUGUI selectedDescription,
            out TextMeshProUGUI recommendedLevel, out Image descriptionBackground,
            out TextMeshProUGUI statusText, out Button startButton, out Button backButton)
        {
            GameObject system = GetOrCreateSystem(StageSystemName, canvas, typeof(StageSelectionUI));
            ClearChildren(system.transform);
            panel = CreateImageObject("Panel", system.transform, new Color(0.025f, 0.045f, 0.07f, 0.98f));
            Stretch(panel.transform);
            CreateAccent(panel.transform, new Vector2(0.06f, 0.12f), new Vector2(0.067f, 0.87f));
            CreateText("Title", panel.transform, "STAGE OPERATION", titleFont, 43f,
                new Vector2(0.1f, 0.83f), new Vector2(0.65f, 0.94f), TextAlignmentOptions.Left);
            chapterText = CreateText("Chapter", panel.transform, "CHAPTER 01  /  CH1", koreanFont, 20f,
                new Vector2(0.1f, 0.76f), new Vector2(0.65f, 0.82f), TextAlignmentOptions.Left);
            chapterText.color = new Color(0.18f, 0.72f, 1f, 1f);

            GameObject nodeArea = CreateImageObject("StageMap", panel.transform, new Color(0.01f, 0.025f, 0.045f, 0.82f));
            SetRect((RectTransform)nodeArea.transform, new Vector2(0.16f, 0.42f), new Vector2(0.84f, 0.73f));
            nodeArea.AddComponent<RectMask2D>();
            nodeScrollRect = nodeArea.AddComponent<ScrollRect>();
            nodeScrollRect.viewport = (RectTransform)nodeArea.transform;
            nodeScrollRect.horizontal = true;
            nodeScrollRect.vertical = false;
            nodeScrollRect.movementType = ScrollRect.MovementType.Clamped;
            nodeScrollRect.inertia = true;
            nodeScrollRect.scrollSensitivity = 35f;
            nodeContent = CreateRectObject("NodeContent", nodeArea.transform).transform;
            RectTransform nodeContentRect = (RectTransform)nodeContent;
            nodeContentRect.anchorMin = new Vector2(0f, 0f);
            nodeContentRect.anchorMax = new Vector2(0f, 1f);
            nodeContentRect.pivot = new Vector2(0f, 0.5f);
            nodeContentRect.anchoredPosition = Vector2.zero;
            nodeContentRect.sizeDelta = Vector2.zero;
            nodeScrollRect.content = nodeContentRect;
            HorizontalLayoutGroup layout = nodeContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(20, 20, 34, 34);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter contentSizeFitter = nodeContent.gameObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            previousNodeButton = CreateButton("PreviousNodeButton", panel.transform, "<", koreanFont,
                new Vector2(0.105f, 0.515f), new Vector2(0.15f, 0.635f),
                new Color(0.025f, 0.09f, 0.15f, 0.96f));
            nextNodeButton = CreateButton("NextNodeButton", panel.transform, ">", koreanFont,
                new Vector2(0.85f, 0.515f), new Vector2(0.895f, 0.635f),
                new Color(0.025f, 0.09f, 0.15f, 0.96f));

            GameObject detail = CreateImageObject("Detail", panel.transform, new Color(0.01f, 0.02f, 0.035f, 0.94f));
            SetRect((RectTransform)detail.transform, new Vector2(0.08f, 0.12f), new Vector2(0.6f, 0.36f));
            GameObject backgroundObject = CreateImageObject("DescriptionBackground", detail.transform, Color.white);
            Stretch(backgroundObject.transform);
            descriptionBackground = backgroundObject.GetComponent<Image>();
            descriptionBackground.color = new Color(1f, 1f, 1f, 0.32f);
            descriptionBackground.raycastTarget = false;
            descriptionBackground.enabled = false;
            selectedTitle = CreateText("SelectedTitle", detail.transform, "1-1", koreanFont, 32f,
                new Vector2(0.06f, 0.67f), new Vector2(0.5f, 0.9f), TextAlignmentOptions.Left);
            selectedSubtitle = CreateText("SelectedSubtitle", detail.transform, "FIRST CONTACT", koreanFont, 15f,
                new Vector2(0.06f, 0.52f), new Vector2(0.5f, 0.66f), TextAlignmentOptions.Left);
            selectedSubtitle.color = new Color(0.18f, 0.72f, 1f, 1f);
            selectedDescription = CreateText("SelectedDescription", detail.transform,
                "스테이지 설명", koreanFont, 17f, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.48f),
                TextAlignmentOptions.TopLeft);
            recommendedLevel = CreateText("RecommendedLevel", detail.transform, "RECOMMENDED LV.  1", koreanFont,
                14f, new Vector2(0.56f, 0.7f), new Vector2(0.94f, 0.88f), TextAlignmentOptions.Right);
            recommendedLevel.color = new Color(0.18f, 0.72f, 1f, 1f);
            statusText = CreateText("Status", panel.transform, "스테이지를 선택하면 작전 정보가 표시됩니다.", koreanFont,
                17f, new Vector2(0.64f, 0.31f), new Vector2(0.92f, 0.37f), TextAlignmentOptions.Left);
            statusText.color = new Color(0.75f, 0.82f, 0.9f, 1f);
            startButton = CreateButton("StartStageButton", panel.transform, "DEPLOY\nSTAGE BATTLE", koreanFont,
                new Vector2(0.64f, 0.18f), new Vector2(0.82f, 0.29f), new Color(0.03f, 0.28f, 0.5f, 0.97f));
            startButton.interactable = false;
            backButton = CreateButton("BackButton", panel.transform, "BACK", koreanFont,
                new Vector2(0.84f, 0.18f), new Vector2(0.94f, 0.29f), new Color(0.05f, 0.07f, 0.1f, 0.96f));
            return system.GetComponent<StageSelectionUI>();
        }

        private static StageCatalog BuildCatalog()
        {
            EnemyRoster enemyRoster = FindEnemyRoster();
            BuildStageDefinition("ch1-01", "1-1", "FIRST CONTACT",
                "미확인 신호를 추적해 첫 방어선을 확보하십시오.", enemyRoster, 0);
            BuildStageDefinition("ch1-02", "1-2", "FALLEN ROUTE",
                "적의 이동 경로가 바뀌었습니다. 방어선을 재배치하십시오.", enemyRoster, 1);
            BuildStageDefinition("ch1-03", "1-3", "DEEP SIGNAL",
                "도시 심부에서 더 강한 신호가 감지됩니다.", enemyRoster, 2);
            BuildStageDefinition("ch1-04", "1-4", "CROSSING",
                "교차 지점에 적 증원이 집결하고 있습니다.", enemyRoster, 3);
            BuildStageDefinition("ch1-05", "1-5", "LAST CHECKPOINT",
                "챕터 1의 마지막 방어선을 지키십시오.", enemyRoster, 4);
            BuildStageDefinition("ch1-06", "1-6", "BREACH POINT",
                "방어선 외곽의 돌파 지점을 봉쇄하십시오.", enemyRoster, 5);
            BuildStageDefinition("ch1-07", "1-7", "SIGNAL CORE",
                "적 신호의 핵심부를 제압하고 작전을 종결하십시오.", enemyRoster, 6);
            return StageCatalogBuilder.BuildCatalog();
        }

        private static EnemyRoster FindEnemyRoster()
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyRoster");
            foreach (string guid in guids)
            {
                EnemyRoster roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>(AssetDatabase.GUIDToAssetPath(guid));
                if (roster != null && roster.enemies != null && roster.enemies.Count > 0)
                {
                    return roster;
                }
            }

            throw new InvalidOperationException("스테이지 샘플을 만들 EnemyRoster를 찾지 못했습니다.");
        }

        private static StageDefinition BuildStageDefinition(string id, string displayName, string subtitle,
            string description, EnemyRoster enemyRoster, int difficulty)
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Stages");
            EnsureFolder(StageDefinitionFolder);
            string path = $"{StageDefinitionFolder}/{id}.asset";
            StageDefinition definition = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<StageDefinition>();
                AssetDatabase.CreateAsset(definition, path);
                AssetDatabase.SetLabels(definition, new[] { GeneratedLabel });
            }
            else
            {
                // Studio에서 편집한 제작 원본을 UI 재생성 때문에 덮어쓰지 않는다.
                return definition;
            }

            definition.schemaVersion = StageDefinition.CurrentSchemaVersion;
            definition.stageId = id;
            definition.chapterId = "ch1";
            definition.displayName = displayName;
            definition.subtitle = subtitle;
            definition.recommendedLevel = 1 + difficulty * 4;
            definition.order = difficulty;
            definition.requiredBestWave = difficulty;
            definition.description = description;
            definition.waves = new List<StageWaveDefinition>();

            for (int waveIndex = 0; waveIndex < 3; waveIndex++)
            {
                StageWaveDefinition wave = new StageWaveDefinition
                {
                    displayName = $"WAVE {waveIndex + 1:00}",
                    buildPhaseDuration = waveIndex == 0 ? 2f : 1.5f,
                    healthMultiplier = 1f + difficulty * 0.04f + waveIndex * 0.08f
                };
                AddSpawn(wave, enemyRoster, 0, 3 + difficulty + waveIndex, 0.65f,
                    waveIndex == 0 ? 0f : 0.5f);
                if (waveIndex >= 1) { AddSpawn(wave, enemyRoster, 1, 1 + difficulty / 2, 0.9f, 1.2f); }
                if (waveIndex >= 2) { AddSpawn(wave, enemyRoster, 2, difficulty >= 2 ? 1 : 0, 1.2f, 1.5f); }
                definition.waves.Add(wave);
            }

            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void AddSpawn(StageWaveDefinition wave, EnemyRoster roster, int enemyIndex,
            int count, float interval, float initialDelay)
        {
            if (count <= 0 || roster.enemies == null || enemyIndex < 0 || enemyIndex >= roster.enemies.Count)
            {
                return;
            }

            wave.spawns.Add(new StageEnemySpawn
            {
                enemy = roster.enemies[enemyIndex],
                count = count,
                interval = interval,
                initialDelay = initialDelay
            });
        }

        private static StageNodeView BuildNodePrefab(TMP_FontAsset font)
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Prefabs");
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(NodePrefabPath);
            if (existing != null && Array.IndexOf(AssetDatabase.GetLabels(existing), GeneratedLabel) < 0)
            {
                throw new InvalidOperationException($"자동 생성물이 아닌 StageNode 프리팹을 덮어쓸 수 없습니다: {NodePrefabPath}");
            }

            Sprite availableSprite = LoadSprite(StageNodeSpriteSheetPath, "StageSelectionsSmallPanel_0");
            Sprite selectedSprite = LoadSprite(StageNodeSpriteSheetPath, "StageSelectionsSmallPanel_1");
            Sprite lockedSprite = LoadSprite(StageNodeSpriteSheetPath, "StageSelectionsSmallPanel_2");

            GameObject root = new GameObject("StageNode", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(LayoutElement), typeof(StageNodeView));
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(220f, 260f);
            Image hitArea = root.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0.001f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;
            LayoutElement layoutElement = root.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 220f;
            layoutElement.preferredHeight = 260f;

            GameObject visual = CreateImageObject("PanelVisual", root.transform, Color.white);
            RectTransform visualRect = (RectTransform)visual.transform;
            visualRect.anchorMin = visualRect.anchorMax = visualRect.pivot = new Vector2(0.5f, 0.5f);
            visualRect.anchoredPosition = Vector2.zero;
            visualRect.sizeDelta = new Vector2(188f, 253f);
            Image background = visual.GetComponent<Image>();
            background.sprite = availableSprite;
            background.preserveAspect = true;
            background.raycastTarget = false;

            TextMeshProUGUI title = CreateText("Title", root.transform, "1-1", font, 27f,
                new Vector2(0.13f, 0.5f), new Vector2(0.86f, 0.64f), TextAlignmentOptions.Left);
            TextMeshProUGUI subtitle = CreateText("Subtitle", root.transform, "FIRST CONTACT", font, 14f,
                new Vector2(0.13f, 0.4f), new Vector2(0.86f, 0.5f), TextAlignmentOptions.Left);
            subtitle.color = new Color(0.18f, 0.72f, 1f, 1f);

            StageNodeView view = root.GetComponent<StageNodeView>();
            var serialized = new SerializedObject(view);
            SetReference(serialized, "button", button);
            SetReference(serialized, "background", background);
            SetReference(serialized, "backgroundRect", visualRect);
            SetReference(serialized, "availableSprite", availableSprite);
            SetReference(serialized, "selectedSprite", selectedSprite);
            SetReference(serialized, "lockedSprite", lockedSprite);
            SetReference(serialized, "titleText", title);
            SetReference(serialized, "subtitleText", subtitle);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, NodePrefabPath);
            AssetDatabase.SetLabels(prefab, new[] { GeneratedLabel });
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<StageNodeView>();
        }

        private static GameObject GetOrCreateSystem(string name, Transform canvas, Type componentType)
        {
            Transform existing = canvas.Find(name);
            if (existing != null)
            {
                if (existing.GetComponent(componentType) == null) { existing.gameObject.AddComponent(componentType); }
                return existing.gameObject;
            }

            GameObject created = new GameObject(name, typeof(RectTransform), componentType);
            created.transform.SetParent(canvas, false);
            Stretch(created.transform);
            return created;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }

        private static GameObject CreateImageObject(string name, Transform parent, Color color)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            created.transform.SetParent(parent, false);
            Image image = created.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return created;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string content,
            TMP_FontAsset font, float fontSize, Vector2 anchorMin, Vector2 anchorMax,
            TextAlignmentOptions alignment)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            created.transform.SetParent(parent, false);
            SetRect((RectTransform)created.transform, anchorMin, anchorMax);
            TextMeshProUGUI text = created.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject created = CreateImageObject(name, parent, color);
            SetRect((RectTransform)created.transform, anchorMin, anchorMax);
            Button button = created.AddComponent<Button>();
            button.targetGraphic = created.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;
            CreateText("Label", created.transform, label, font, 20f, Vector2.zero, Vector2.one,
                TextAlignmentOptions.Center);
            return button;
        }

        private static void ConfigureSpriteButton(Button button, string normalSpriteName, string hoverSpriteName)
        {
            Sprite normalSprite = LoadSprite(normalSpriteName);
            Sprite hoverSprite = LoadSprite(hoverSpriteName);
            Image image = button.targetGraphic as Image;
            if (image == null)
            {
                throw new InvalidOperationException($"버튼 Image를 찾지 못했습니다: {button.name}");
            }

            image.sprite = normalSprite;
            image.color = Color.white;
            image.preserveAspect = true;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = hoverSprite,
                pressedSprite = hoverSprite,
                // EventSystem의 기본 선택은 키보드 포커스일 뿐 호버가 아니므로 Normal을 유지한다.
                selectedSprite = normalSprite,
                disabledSprite = normalSprite
            };

            UIHoverScale hoverScale = button.gameObject.AddComponent<UIHoverScale>();
            var hoverScaleSerialized = new SerializedObject(hoverScale);
            float scaleCompensation = Mathf.Max(
                hoverSprite.rect.width / normalSprite.rect.width,
                hoverSprite.rect.height / normalSprite.rect.height);
            hoverScaleSerialized.FindProperty("hoverScale").floatValue = scaleCompensation;
            hoverScaleSerialized.ApplyModifiedPropertiesWithoutUndo();

            // 버튼 문구가 스프라이트에 포함되어 있으므로 기존 TMP가 겹쳐 보이지 않게 한다.
            Transform label = button.transform.Find("Label");
            if (label != null) { label.gameObject.SetActive(false); }
        }

        private static Sprite LoadSprite(string spriteName)
        {
            return LoadSprite(ModeButtonSpriteSheetPath, spriteName);
        }

        private static Sprite LoadSprite(string assetPath, string spriteName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            throw new InvalidOperationException($"스프라이트를 찾지 못했습니다: {assetPath} / {spriteName}");
        }

        private static void ValidateSpriteButton(Button button, string normalSpriteName, string hoverSpriteName)
        {
            if (button == null || button.transition != Selectable.Transition.SpriteSwap ||
                button.image == null || button.image.sprite != LoadSprite(normalSpriteName) ||
                button.spriteState.highlightedSprite != LoadSprite(hoverSpriteName) ||
                button.spriteState.selectedSprite != LoadSprite(normalSpriteName) ||
                button.GetComponent<UIHoverScale>() == null)
            {
                throw new InvalidOperationException($"모드 선택 버튼 스프라이트 배선이 올바르지 않습니다: {normalSpriteName}");
            }
        }

        private static void CreateAccent(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject accent = CreateImageObject("Accent", parent, new Color(0.02f, 0.6f, 1f, 1f));
            SetRect((RectTransform)accent.transform, anchorMin, anchorMax);
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            if (target == null) { throw new InvalidOperationException("MainMenuBackground를 찾지 못했습니다."); }
            if (!target.TryGetComponent(out CanvasGroup group)) { group = target.AddComponent<CanvasGroup>(); }
            return group;
        }

        private static void SetReference(SerializedObject serialized, string field, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) { throw new InvalidOperationException($"직렬화 필드를 찾지 못했습니다: {field}"); }
            property.objectReferenceValue = value;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(Transform target)
        {
            SetRect((RectTransform)target, Vector2.zero, Vector2.one);
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

        private static Scene OpenTitleSceneSafely()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == TitleScenePath) { return active; }
            if (active.isDirty) { throw new InvalidOperationException("저장되지 않은 다른 씬이 열려 있습니다."); }
            return EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        }
    }
}
