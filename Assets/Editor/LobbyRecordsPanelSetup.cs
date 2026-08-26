using System;
using RCCom.Definitions.Stage;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// TitleScene의 Records 화면을 uGUI로 조립하고 로비 메뉴에 배선한다.
    /// 스테이지가 늘어나도 같은 화면을 쓰도록 목록은 ScrollRect + 행 프리팹으로 구성한다.
    /// </summary>
    public static class LobbyRecordsPanelSetup
    {
        private const string CatalogPath = "Assets/Data/Stages/StageCatalog.asset";
        private const string ItemPrefabPath = "Assets/Data/Prefabs/StageRecordItemView.prefab";

        private static readonly Color Background = new(0.008f, 0.018f, 0.03f, 0.985f);
        private static readonly Color Panel = new(0.015f, 0.04f, 0.065f, 0.96f);
        private static readonly Color PanelLight = new(0.025f, 0.065f, 0.1f, 0.96f);
        private static readonly Color Cyan = new(0.04f, 0.65f, 1f, 1f);
        private static readonly Color White = new(0.92f, 0.95f, 0.98f, 1f);
        private static readonly Color Muted = new(0.44f, 0.56f, 0.65f, 1f);

        [MenuItem("RCCom/UI/Setup Lobby Records Panel")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Records UI Setup은 Edit Mode에서만 실행할 수 있습니다.");
            }

            GameObject canvas = RequireSceneObject("Canvas");
            Transform mainMenu = Require(canvas.transform, "MainMenuBackground");
            Transform recordsEntry = Require(mainMenu, "CommandMenuPanels/Records");
            TMP_FontAsset font = FindSceneFont(canvas);
            StageRecordItemView itemPrefab = BuildItemPrefab(font);

            RectTransform root = EnsureRect(canvas.transform, "RecordsPanelBackground");
            Stretch(root);
            root.SetAsLastSibling();
            UnityEngine.UI.Image rootImage = EnsureImage(root.gameObject, Background, true);
            rootImage.raycastTarget = true;

            BuildHeader(root, font);
            TMP_Text completionText;
            TMP_Text bestWaveText;
            TMP_Text currentObjectiveText;
            UnityEngine.UI.Image completionFill;
            BuildSummary(root, font, out completionText, out bestWaveText,
                out currentObjectiveText, out completionFill);

            RectTransform archivePanel = EnsurePanel(root, "StageArchivePanel",
                new Vector2(0.315f, 0.135f), new Vector2(0.96f, 0.79f), Panel);
            EnsureText(archivePanel, "SectionLabel", font, "STAGE CLEARANCE LOG",
                28f, FontStyles.Bold, Cyan, TextAlignmentOptions.Left,
                new Vector2(0.035f, 0.88f), new Vector2(0.72f, 0.97f));
            TMP_Text archiveStatusText = EnsureText(archivePanel, "ArchiveStatusText", font,
                "LOCAL PROFILE SYNCHRONIZED  //  000%", 16f, FontStyles.Normal, Muted,
                TextAlignmentOptions.Right, new Vector2(0.58f, 0.89f), new Vector2(0.965f, 0.96f));
            EnsureRule(archivePanel, "HeaderRule", new Vector2(0.035f, 0.865f),
                new Vector2(0.965f, 0.87f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.65f));

            RectTransform content = BuildScrollView(archivePanel);
            UnityEngine.UI.Button backButton = BuildBackButton(root, font);
            UnityEngine.UI.Button entryButton = recordsEntry.GetComponent<UnityEngine.UI.Button>();
            if (entryButton == null) { entryButton = Undo.AddComponent<UnityEngine.UI.Button>(recordsEntry.gameObject); }
            UnityEngine.UI.Image entryImage = recordsEntry.GetComponent<UnityEngine.UI.Image>();
            if (entryImage == null)
            {
                throw new InvalidOperationException("Records 로비 메뉴에 Image가 없습니다.");
            }

            entryButton.targetGraphic = entryImage;
            entryImage.raycastTarget = true;

            LobbyRecordsUI controller = canvas.GetComponent<LobbyRecordsUI>();
            if (controller == null) { controller = Undo.AddComponent<LobbyRecordsUI>(canvas); }
            StageCatalog catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"StageCatalog를 찾지 못했습니다: {CatalogPath}");
            }

            SerializedObject serialized = new(controller);
            Assign(serialized, "mainMenuBackground", mainMenu.gameObject);
            Assign(serialized, "recordsPanelBackground", root.gameObject);
            Assign(serialized, "recordsEntryButton", entryButton);
            Assign(serialized, "backButton", backButton);
            Assign(serialized, "stageCatalog", catalog);
            Assign(serialized, "itemPrefab", itemPrefab);
            Assign(serialized, "itemContent", content);
            Assign(serialized, "completionText", completionText);
            Assign(serialized, "bestWaveText", bestWaveText);
            Assign(serialized, "currentObjectiveText", currentObjectiveText);
            Assign(serialized, "archiveStatusText", archiveStatusText);
            Assign(serialized, "completionFillImage", completionFill);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(true);
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(root.gameObject);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[LobbyRecordsPanelSetup] 로비 Records 화면 생성·배선 완료");
        }

        [MenuItem("RCCom/UI/Validate Lobby Records Panel")]
        public static void Validate()
        {
            GameObject canvas = RequireSceneObject("Canvas");
            LobbyRecordsUI controller = canvas.GetComponent<LobbyRecordsUI>();
            Transform root = canvas.transform.Find("RecordsPanelBackground");
            Transform entry = canvas.transform.Find("MainMenuBackground/CommandMenuPanels/Records");
            if (controller == null || root == null || entry == null ||
                entry.GetComponent<UnityEngine.UI.Button>() == null ||
                root.Find("StageArchivePanel/ScrollView/Viewport/Content") == null ||
                root.Find("BackButton")?.GetComponent<UnityEngine.UI.Button>() == null ||
                AssetDatabase.LoadAssetAtPath<StageRecordItemView>(ItemPrefabPath) == null)
            {
                throw new InvalidOperationException("Lobby Records 화면의 계층 또는 데이터 배선이 비어 있습니다.");
            }

            int eventSystemCount = UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            if (eventSystemCount != 1 || canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                throw new InvalidOperationException("Records UI 입력에 필요한 EventSystem/GraphicRaycaster 구성이 올바르지 않습니다.");
            }

            Debug.Log("[LobbyRecordsPanelSetup] Records 화면 검증 통과");
        }

        private static void BuildHeader(RectTransform root, TMP_FontAsset font)
        {
            RectTransform header = EnsurePanel(root, "Header",
                new Vector2(0.04f, 0.825f), new Vector2(0.96f, 0.955f),
                new Color(0.005f, 0.014f, 0.025f, 0.78f));
            EnsureRule(header, "Accent", new Vector2(0f, 0f), new Vector2(0.006f, 1f), Cyan);
            EnsureText(header, "CompanyText", font, "R&C COMPANY  //  COMMAND SYSTEM",
                16f, FontStyles.Bold, Muted, TextAlignmentOptions.TopLeft,
                new Vector2(0.025f, 0.7f), new Vector2(0.6f, 0.96f));
            EnsureText(header, "TitleText", font, "RECORDS",
                54f, FontStyles.Bold, White, TextAlignmentOptions.Left,
                new Vector2(0.025f, 0.17f), new Vector2(0.35f, 0.72f));
            EnsureText(header, "SubtitleText", font, "BATTLE DATA ARCHIVE",
                24f, FontStyles.Normal, Cyan, TextAlignmentOptions.Left,
                new Vector2(0.27f, 0.2f), new Vector2(0.68f, 0.6f));
            EnsureText(header, "ProtocolText", font, "PROFILE RECORD  /  CHAPTER 01  /  ONLINE",
                16f, FontStyles.Normal, Muted, TextAlignmentOptions.Right,
                new Vector2(0.64f, 0.25f), new Vector2(0.97f, 0.65f));
        }

        private static void BuildSummary(RectTransform root, TMP_FontAsset font,
            out TMP_Text completionText, out TMP_Text bestWaveText,
            out TMP_Text currentObjectiveText, out UnityEngine.UI.Image completionFill)
        {
            RectTransform summary = EnsurePanel(root, "ArchiveSummaryPanel",
                new Vector2(0.04f, 0.135f), new Vector2(0.295f, 0.79f), Panel);
            EnsureText(summary, "SectionLabel", font, "ARCHIVE OVERVIEW",
                26f, FontStyles.Bold, Cyan, TextAlignmentOptions.Left,
                new Vector2(0.07f, 0.88f), new Vector2(0.93f, 0.97f));
            EnsureRule(summary, "HeaderRule", new Vector2(0.07f, 0.855f),
                new Vector2(0.93f, 0.86f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.65f));

            EnsureText(summary, "ChapterLabel", font, "CHAPTER 01",
                18f, FontStyles.Normal, Muted, TextAlignmentOptions.Left,
                new Vector2(0.07f, 0.76f), new Vector2(0.93f, 0.82f));
            EnsureText(summary, "CompletionLabel", font, "STAGE COMPLETION",
                16f, FontStyles.Bold, White, TextAlignmentOptions.Left,
                new Vector2(0.07f, 0.66f), new Vector2(0.93f, 0.72f));
            completionText = EnsureText(summary, "CompletionText", font, "00 / 00",
                42f, FontStyles.Bold, White, TextAlignmentOptions.Left,
                new Vector2(0.07f, 0.565f), new Vector2(0.93f, 0.66f));

            RectTransform bar = EnsurePanel(summary, "CompletionBar",
                new Vector2(0.07f, 0.52f), new Vector2(0.93f, 0.55f),
                new Color(0.09f, 0.13f, 0.17f, 1f));
            completionFill = EnsureImage(EnsureRect(bar, "Fill").gameObject, Cyan, false);
            Stretch(completionFill.rectTransform);
            completionFill.type = UnityEngine.UI.Image.Type.Filled;
            completionFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            completionFill.fillOrigin = 0;
            completionFill.fillAmount = 0f;

            EnsureText(summary, "BestWaveLabel", font, "BEST ENDLESS RECORD",
                16f, FontStyles.Bold, Muted, TextAlignmentOptions.Left,
                new Vector2(0.07f, 0.405f), new Vector2(0.93f, 0.47f));
            bestWaveText = EnsureText(summary, "BestWaveText", font, "WAVE 00",
                36f, FontStyles.Bold, White, TextAlignmentOptions.Left,
                new Vector2(0.07f, 0.325f), new Vector2(0.93f, 0.41f));

            EnsureText(summary, "ObjectiveLabel", font, "CURRENT OBJECTIVE",
                16f, FontStyles.Bold, Muted, TextAlignmentOptions.Left,
                new Vector2(0.07f, 0.21f), new Vector2(0.93f, 0.27f));
            currentObjectiveText = EnsureText(summary, "CurrentObjectiveText", font,
                "NO AVAILABLE OPERATION", 22f, FontStyles.Bold, Cyan,
                TextAlignmentOptions.TopLeft, new Vector2(0.07f, 0.08f), new Vector2(0.93f, 0.205f));
        }

        private static RectTransform BuildScrollView(RectTransform archivePanel)
        {
            RectTransform scrollRoot = EnsureRect(archivePanel, "ScrollView");
            SetAnchors(scrollRoot, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.84f));
            UnityEngine.UI.Image scrollImage = EnsureImage(scrollRoot.gameObject,
                new Color(0f, 0f, 0f, 0.18f), true);
            scrollImage.raycastTarget = true;

            RectTransform viewport = EnsureRect(scrollRoot, "Viewport");
            Stretch(viewport);
            UnityEngine.UI.Image viewportImage = EnsureImage(viewport.gameObject,
                new Color(0f, 0f, 0f, 0.01f), true);
            viewportImage.raycastTarget = true;
            UnityEngine.UI.Mask mask = viewport.GetComponent<UnityEngine.UI.Mask>();
            if (mask == null) { mask = Undo.AddComponent<UnityEngine.UI.Mask>(viewport.gameObject); }
            mask.showMaskGraphic = false;

            RectTransform content = EnsureRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            UnityEngine.UI.VerticalLayoutGroup layout =
                content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (layout == null) { layout = Undo.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(content.gameObject); }
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            UnityEngine.UI.ContentSizeFitter fitter =
                content.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (fitter == null) { fitter = Undo.AddComponent<UnityEngine.UI.ContentSizeFitter>(content.gameObject); }
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            UnityEngine.UI.ScrollRect scroll = scrollRoot.GetComponent<UnityEngine.UI.ScrollRect>();
            if (scroll == null) { scroll = Undo.AddComponent<UnityEngine.UI.ScrollRect>(scrollRoot.gameObject); }
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
            return content;
        }

        private static UnityEngine.UI.Button BuildBackButton(RectTransform root, TMP_FontAsset font)
        {
            RectTransform rect = EnsureRect(root, "BackButton");
            SetAnchors(rect, new Vector2(0.81f, 0.04f), new Vector2(0.96f, 0.105f));
            UnityEngine.UI.Image image = EnsureImage(rect.gameObject, PanelLight, true);
            image.raycastTarget = true;
            UnityEngine.UI.Button button = rect.GetComponent<UnityEngine.UI.Button>();
            if (button == null) { button = Undo.AddComponent<UnityEngine.UI.Button>(rect.gameObject); }
            button.targetGraphic = image;
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.15f, 0.72f, 1f, 1f);
            colors.pressedColor = new Color(0.05f, 0.5f, 0.82f, 1f);
            button.colors = colors;
            EnsureText(rect, "Label", font, "<  BACK", 25f, FontStyles.Bold, White,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            return button;
        }

        private static StageRecordItemView BuildItemPrefab(TMP_FontAsset font)
        {
            StageRecordItemView existing = AssetDatabase.LoadAssetAtPath<StageRecordItemView>(ItemPrefabPath);
            if (existing != null) { return existing; }

            GameObject rootObject = new("StageRecordItem", typeof(RectTransform),
                typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.LayoutElement), typeof(StageRecordItemView));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(1200f, 104f);
            UnityEngine.UI.LayoutElement element = rootObject.GetComponent<UnityEngine.UI.LayoutElement>();
            element.preferredHeight = 104f;
            element.minHeight = 104f;
            element.flexibleWidth = 1f;
            UnityEngine.UI.Image backgroundImage = rootObject.GetComponent<UnityEngine.UI.Image>();
            backgroundImage.color = PanelLight;
            backgroundImage.raycastTarget = false;

            UnityEngine.UI.Image accent = EnsureImage(EnsureRect(root, "StateAccent").gameObject,
                Cyan, false);
            SetAnchors(accent.rectTransform, Vector2.zero, new Vector2(0.007f, 1f));
            TMP_Text stageCode = EnsureText(root, "StageCodeText", font, "1-1", 34f,
                FontStyles.Bold, White, TextAlignmentOptions.Left,
                new Vector2(0.025f, 0.15f), new Vector2(0.115f, 0.88f));
            TMP_Text title = EnsureText(root, "TitleText", font, "FIRST CONTACT", 23f,
                FontStyles.Bold, Cyan, TextAlignmentOptions.Left,
                new Vector2(0.12f, 0.48f), new Vector2(0.34f, 0.88f));
            TMP_Text description = EnsureText(root, "DescriptionText", font,
                "OPERATION ARCHIVE DESCRIPTION", 15f, FontStyles.Normal, Muted,
                TextAlignmentOptions.Left, new Vector2(0.12f, 0.12f), new Vector2(0.67f, 0.5f));
            description.textWrappingMode = TextWrappingModes.NoWrap;
            description.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text recommended = EnsureText(root, "RecommendedLevelText", font, "REC. LV 01",
                17f, FontStyles.Normal, Muted, TextAlignmentOptions.Center,
                new Vector2(0.69f, 0.15f), new Vector2(0.82f, 0.85f));
            TMP_Text state = EnsureText(root, "StateText", font, "AVAILABLE", 20f,
                FontStyles.Bold, Cyan, TextAlignmentOptions.Center,
                new Vector2(0.83f, 0.15f), new Vector2(0.98f, 0.85f));

            SerializedObject serialized = new(rootObject.GetComponent<StageRecordItemView>());
            Assign(serialized, "backgroundImage", backgroundImage);
            Assign(serialized, "stateAccentImage", accent);
            Assign(serialized, "stageCodeText", stageCode);
            Assign(serialized, "titleText", title);
            Assign(serialized, "descriptionText", description);
            Assign(serialized, "recommendedLevelText", recommended);
            Assign(serialized, "stateText", state);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootObject, ItemPrefabPath);
            UnityEngine.Object.DestroyImmediate(rootObject);
            if (prefab == null)
            {
                throw new InvalidOperationException($"기록 행 프리팹 생성 실패: {ItemPrefabPath}");
            }

            return prefab.GetComponent<StageRecordItemView>();
        }

        private static TMP_FontAsset FindSceneFont(GameObject canvas)
        {
            TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null) { return texts[i].font; }
            }

            if (TMP_Settings.defaultFontAsset != null) { return TMP_Settings.defaultFontAsset; }
            throw new InvalidOperationException("Records UI에 사용할 TMP Font Asset을 찾지 못했습니다.");
        }

        private static RectTransform EnsurePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            RectTransform rect = EnsureRect(parent, name);
            SetAnchors(rect, anchorMin, anchorMax);
            EnsureImage(rect.gameObject, color, false);
            return rect;
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                RectTransform existingRect = existing as RectTransform;
                if (existingRect == null)
                {
                    throw new InvalidOperationException($"{name}에 RectTransform이 없습니다.");
                }

                return existingRect;
            }

            GameObject created = new(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            created.transform.SetParent(parent, false);
            return created.GetComponent<RectTransform>();
        }

        private static UnityEngine.UI.Image EnsureImage(GameObject target, Color color, bool raycast)
        {
            UnityEngine.UI.Image image = target.GetComponent<UnityEngine.UI.Image>();
            if (image == null) { image = Undo.AddComponent<UnityEngine.UI.Image>(target); }
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static TMP_Text EnsureText(Transform parent, string name, TMP_FontAsset font,
            string value, float fontSize, FontStyles style, Color color,
            TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = EnsureRect(parent, name);
            SetAnchors(rect, anchorMin, anchorMax);
            TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null) { text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject); }
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void EnsureRule(Transform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Color color)
        {
            RectTransform rule = EnsureRect(parent, name);
            SetAnchors(rule, anchorMin, anchorMax);
            EnsureImage(rule.gameObject, color, false);
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
        }

        private static GameObject RequireSceneObject(string name)
        {
            GameObject target = GameObject.Find(name);
            return target != null ? target : throw new InvalidOperationException($"씬 오브젝트가 없습니다: {name}");
        }

        private static Transform Require(Transform root, string path)
        {
            Transform target = root.Find(path);
            return target != null ? target : throw new InvalidOperationException($"UI 경로가 없습니다: {root.name}/{path}");
        }

        private static void Assign(SerializedObject serialized, string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"직렬화 필드를 찾지 못했습니다: {propertyName}");
            }

            property.objectReferenceValue = value;
        }
    }
}
