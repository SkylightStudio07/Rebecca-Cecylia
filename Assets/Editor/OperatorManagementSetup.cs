using System;
using RCCom.Definitions.Operator;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 사용자가 먼저 배치한 OperatorManagingSystem의 배경·장식·버튼 이미지를 보존하고,
    /// 반복 생성 가능한 런타임 View와 참조만 Editor API로 조립한다.
    /// </summary>
    public static class OperatorManagementSetup
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string RootName = "OperatorManagingSystem";
        private const string GeneratedRootName = "RuntimeContent";
        private const string CardPrefabPath = "Assets/Data/Prefabs/OperatorManagementCard.prefab";
        private const string CardSheetPath = "Assets/Art/UI/OperatorManaging/OperatorPanels_Managing.png";
        private const string DeploySheetPath = "Assets/Art/UI/OperatorManaging/OperatorManagementDeployButtonSheet.png";
        private const string FontPath = "Assets/Resource/Font/Pretendard-Bold SDF.asset";
        private const string CatalogPath = "Assets/Data/Operators/OperatorCatalog.asset";

        [MenuItem("RCCom/UI/Build Operator Management Screen")]
        public static void Build()
        {
            Scene scene = OpenTitleSceneSafely();
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null) { throw new InvalidOperationException("TitleScene에 Canvas가 없습니다."); }

            Transform root = canvas.transform.Find(RootName);
            if (root == null)
            {
                throw new InvalidOperationException(
                    "사용자가 배치한 OperatorManagingSystem을 찾지 못했습니다. 기존 아트 배치를 임의로 만들지 않습니다.");
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            OperatorCatalog catalog = AssetDatabase.LoadAssetAtPath<OperatorCatalog>(CatalogPath);
            if (font == null || catalog == null) { throw new InvalidOperationException("관리 화면용 글꼴 또는 카탈로그가 없습니다."); }

            OperatorManagementCardView cardPrefab = BuildCardPrefab(font);
            Transform generated = RebuildGeneratedRoot(root);
            ConfigureRoot(root);

            Transform cardContent = CreateContainer("Cards", generated, new Vector2(0.04f, 0.18f),
                new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup layout = cardContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI registered = CreateText("Registered", generated, font, "01 / 01\nREGISTERED", 25f,
                new Vector2(0.82f, 0.84f), new Vector2(0.96f, 0.95f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.TopRight, new Color(0.18f, 0.72f, 1f, 1f));

            Image infoPanel = CreateImage("OperatorInfo", generated, null, new Color(0.01f, 0.025f, 0.045f, 0.9f),
                new Vector2(0.03f, 0.03f), new Vector2(0.39f, 0.19f), Vector2.zero, Vector2.zero);
            CreateAccent(infoPanel.transform);
            TextMeshProUGUI name = CreateText("Name", infoPanel.transform, font, "OPERATOR", 31f,
                new Vector2(0.06f, 0.52f), new Vector2(0.48f, 0.91f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.MidlineLeft, Color.white);
            TextMeshProUGUI affinity = CreateText("Affinity", infoPanel.transform, font, "AFFINITY  000 / 100", 18f,
                new Vector2(0.55f, 0.57f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.MidlineRight, new Color(0.15f, 0.7f, 1f, 1f));
            TextMeshProUGUI description = CreateText("Description", infoPanel.transform, font, string.Empty, 17f,
                new Vector2(0.06f, 0.18f), new Vector2(0.96f, 0.56f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.TopLeft, new Color(0.84f, 0.88f, 0.92f, 1f));
            description.textWrappingMode = TextWrappingModes.Normal;
            TextMeshProUGUI unlock = CreateText("Unlock", infoPanel.transform, font, "LOCAL OPERATOR", 14f,
                new Vector2(0.06f, 0.02f), new Vector2(0.96f, 0.2f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.MidlineLeft, new Color(0.15f, 0.7f, 1f, 1f));

            Button previous = EnsureButton(root, "LeftButton");
            Button next = EnsureButton(root, "RightButton");
            Button deploy = EnsureButton(root, "DeployButton");
            ConfigureDeploySprites(deploy);

            Button back = CreateButton("BackButton", generated, font, "BACK", new Vector2(0.61f, 0.055f),
                new Vector2(0.70f, 0.12f), new Color(0.02f, 0.04f, 0.065f, 0.94f));

            TextMeshProUGUI status = CreateText("Status", generated, font,
                "오퍼레이터를 선택하십시오.", 16f, new Vector2(0.41f, 0.09f), new Vector2(0.59f, 0.14f),
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, new Color(0.78f, 0.86f, 0.92f, 1f));
            Slider progress = CreateProgress(generated);

            OperatorManagementUI controller = root.GetComponent<OperatorManagementUI>();
            if (controller == null) { controller = root.gameObject.AddComponent<OperatorManagementUI>(); }
            CanvasGroup lobbyGroup = canvas.transform.Find("MainMenuBackground")?.GetComponent<CanvasGroup>();
            LobbyOperatorDialogueUI lobbyDialogue = UnityEngine.Object.FindFirstObjectByType<LobbyOperatorDialogueUI>(
                FindObjectsInactive.Include);

            var serialized = new SerializedObject(controller);
            SetReference(serialized, "catalog", catalog);
            SetReference(serialized, "panel", root.gameObject);
            SetReference(serialized, "mainMenuGroup", lobbyGroup);
            SetReference(serialized, "cardContent", cardContent);
            SetReference(serialized, "cardPrefab", cardPrefab);
            SetReference(serialized, "nameText", name);
            SetReference(serialized, "descriptionText", description);
            SetReference(serialized, "unlockText", unlock);
            SetReference(serialized, "affinityText", affinity);
            SetReference(serialized, "registeredText", registered);
            SetReference(serialized, "statusText", status);
            SetReference(serialized, "downloadProgress", progress);
            SetReference(serialized, "previousButton", previous);
            SetReference(serialized, "nextButton", next);
            SetReference(serialized, "deployButton", deploy);
            SetReference(serialized, "backButton", back);
            SetReference(serialized, "lobbyDialogueUI", lobbyDialogue);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            WireOperatorsMenu(canvas.transform, controller);
            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            EditorUtility.SetDirty(root.gameObject);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OperatorManagementSetup] 공용 카드 프리팹과 TitleScene 관리 화면 배치 완료");
        }

        [MenuItem("RCCom/UI/Validate Operator Management Screen")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            OperatorManagementUI controller = UnityEngine.Object.FindFirstObjectByType<OperatorManagementUI>(
                FindObjectsInactive.Include);
            OperatorManagementCardView prefab = AssetDatabase.LoadAssetAtPath<OperatorManagementCardView>(CardPrefabPath);
            Transform operators = GameObject.Find("Canvas/MainMenuBackground/CommandMenuPanels/Operators")?.transform;
            if (controller == null || prefab == null || operators == null ||
                operators.GetComponent<TitleMenuTextButton>() == null)
            {
                throw new InvalidOperationException("오퍼레이터 관리 화면의 필수 배선이 누락되었습니다.");
            }
            if (scene.path != TitleScenePath) { throw new InvalidOperationException("TitleScene 검증에 실패했습니다."); }
            Debug.Log("[OperatorManagementSetup] 관리 화면 프리팹·씬·메뉴 연결 검증 통과");
        }

        private static OperatorManagementCardView BuildCardPrefab(TMP_FontAsset font)
        {
            Sprite normal = LoadSprite(CardSheetPath, "OperatorPanels_Managing_0");
            Sprite hover = LoadSprite(CardSheetPath, "OperatorPanels_Managing_1");
            Sprite locked = LoadSprite(CardSheetPath, "OperatorPanels_Managing_2");

            GameObject root = new("OperatorManagementCard", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(LayoutElement), typeof(OperatorManagementCardView));
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(210f, 560f);
            Image background = root.GetComponent<Image>();
            background.sprite = normal;
            background.preserveAspect = false;
            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            // 호버 강조는 프레임에만 적용한다. 루트 전체를 키우면 초상화와 TMP의 중심도 함께 흔들린다.
            Image frame = CreateImage("Frame", root.transform, normal, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            frame.preserveAspect = false;
            frame.raycastTarget = true;
            background.color = Color.clear;
            background.raycastTarget = false;
            button.targetGraphic = frame;
            LayoutElement element = root.GetComponent<LayoutElement>();
            element.preferredWidth = 210f;
            element.preferredHeight = 560f;

            GameObject portraitViewport = new("PortraitViewport", typeof(RectTransform), typeof(RectMask2D));
            portraitViewport.transform.SetParent(root.transform, false);
            SetRect((RectTransform)portraitViewport.transform, new Vector2(0.09f, 0.24f),
                new Vector2(0.91f, 0.91f), Vector2.zero, Vector2.zero);
            Image portrait = CreateImage("Portrait", portraitViewport.transform, null, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            TextMeshProUGUI index = CreateText("Index", root.transform, font, "01", 24f,
                new Vector2(0.09f, 0.815f), new Vector2(0.34f, 0.875f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.MidlineLeft, new Color(0.18f, 0.72f, 1f, 1f));
            TextMeshProUGUI state = CreateText("State", root.transform, font, "AVAILABLE", 11f,
                new Vector2(0.09f, 0.755f), new Vector2(0.58f, 0.795f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.MidlineLeft, new Color(0.18f, 0.72f, 1f, 1f));
            TextMeshProUGUI name = CreateText("Name", root.transform, font, "OPERATOR", 22f,
                new Vector2(0.18f, 0.19f), new Vector2(0.90f, 0.255f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.MidlineLeft, Color.white);
            TextMeshProUGUI affinity = CreateText("Affinity", root.transform, font, "AFFINITY 000", 11f,
                new Vector2(0.18f, 0.145f), new Vector2(0.90f, 0.19f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.MidlineLeft, new Color(0.18f, 0.72f, 1f, 1f));
            index.overflowMode = TextOverflowModes.Overflow;
            state.overflowMode = TextOverflowModes.Overflow;
            name.overflowMode = TextOverflowModes.Overflow;
            name.enableAutoSizing = true;
            name.fontSizeMin = 12f;
            name.fontSizeMax = 20f;
            affinity.overflowMode = TextOverflowModes.Overflow;

            Image badge = CreateImage("ActiveBadge", root.transform, null, new Color(0.02f, 0.42f, 0.85f, 0.94f),
                new Vector2(0.6f, 0.88f), new Vector2(0.91f, 0.945f), Vector2.zero, Vector2.zero);
            CreateText("Text", badge.transform, font, "ACTIVE", 13f, Vector2.zero, Vector2.one, Vector2.zero,
                Vector2.zero, TextAlignmentOptions.Center, Color.white);

            OperatorManagementCardView view = root.GetComponent<OperatorManagementCardView>();
            var serialized = new SerializedObject(view);
            SetReference(serialized, "button", button);
            SetReference(serialized, "stateImage", frame);
            SetReference(serialized, "portraitImage", portrait);
            SetReference(serialized, "unlockedSprite", normal);
            SetReference(serialized, "hoverSprite", hover);
            SetReference(serialized, "lockedSprite", locked);
            SetReference(serialized, "indexText", index);
            SetReference(serialized, "nameText", name);
            SetReference(serialized, "stateText", state);
            SetReference(serialized, "affinityText", affinity);
            SetReference(serialized, "activeBadge", badge.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null) { throw new InvalidOperationException("관리 카드 프리팹을 저장하지 못했습니다."); }
            return prefab.GetComponent<OperatorManagementCardView>();
        }

        private static Transform RebuildGeneratedRoot(Transform root)
        {
            Transform existing = root.Find(GeneratedRootName);
            if (existing != null) { UnityEngine.Object.DestroyImmediate(existing.gameObject); }
            GameObject generated = new(GeneratedRootName, typeof(RectTransform));
            generated.transform.SetParent(root, false);
            SetRect((RectTransform)generated.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return generated.transform;
        }

        private static void ConfigureRoot(Transform root)
        {
            Image image = root.GetComponent<Image>();
            if (image != null) { image.raycastTarget = false; }
            RectTransform rect = root as RectTransform;
            if (rect != null) { SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); }
        }

        private static Button EnsureButton(Transform root, string childName)
        {
            Transform child = root.Find(childName);
            if (child == null) { throw new InvalidOperationException($"사용자 배치 버튼을 찾지 못했습니다: {childName}"); }
            Button button = child.GetComponent<Button>();
            if (button == null) { button = child.gameObject.AddComponent<Button>(); }
            Image image = child.GetComponent<Image>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            if (image != null) { image.raycastTarget = true; }
            return button;
        }

        private static void ConfigureDeploySprites(Button deploy)
        {
            Transform generatedLabel = deploy.transform.Find("RuntimeLabel");
            if (generatedLabel != null) { UnityEngine.Object.DestroyImmediate(generatedLabel.gameObject); }

            Sprite normal = LoadSprite(DeploySheetPath, "OperatorManagementDeployButtonSheet_0");
            Sprite hover = LoadSprite(DeploySheetPath, "OperatorManagementDeployButtonSheet_1");
            Image image = deploy.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = normal;
                image.preserveAspect = true;
            }
            SpriteState state = deploy.spriteState;
            state.highlightedSprite = hover;
            state.selectedSprite = hover;
            deploy.spriteState = state;
        }

        private static void WireOperatorsMenu(Transform canvas, OperatorManagementUI controller)
        {
            Transform operators = canvas.Find("MainMenuBackground/CommandMenuPanels/Operators");
            if (operators == null) { throw new InvalidOperationException("로비의 Operators 메뉴를 찾지 못했습니다."); }
            TitleMenuTextButton action = operators.GetComponent<TitleMenuTextButton>();
            if (action == null) { action = operators.gameObject.AddComponent<TitleMenuTextButton>(); }
            TitleSceneController title = UnityEngine.Object.FindFirstObjectByType<TitleSceneController>(
                FindObjectsInactive.Include);
            action.Configure(TitleMenuTextButton.MenuAction.ManageOperators, title);
            var serialized = new SerializedObject(action);
            SetReference(serialized, "operatorManagementUI", controller);
            serialized.FindProperty("hoverScale").floatValue = 1.025f;
            serialized.FindProperty("shakeAmount").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
        }

        private static Slider CreateProgress(Transform parent)
        {
            Image background = CreateImage("DownloadProgress", parent, null, new Color(0.04f, 0.08f, 0.12f, 0.9f),
                new Vector2(0.41f, 0.075f), new Vector2(0.59f, 0.087f), Vector2.zero, Vector2.zero);
            Slider slider = background.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.interactable = false;
            Image fill = CreateImage("Fill", background.transform, null, new Color(0.02f, 0.55f, 1f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = background;
            return slider;
        }

        private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            Image image = CreateImage(name, parent, null, color, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.05f, 0.45f, 0.85f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            CreateText("Text", image.transform, font, label, 20f, Vector2.zero, Vector2.one, Vector2.zero,
                Vector2.zero, TextAlignmentOptions.Center, Color.white);
            return button;
        }

        private static void CreateAccent(Transform parent)
        {
            CreateImage("Accent", parent, null, new Color(0.03f, 0.6f, 1f, 1f),
                new Vector2(0f, 0f), new Vector2(0.012f, 1f), Vector2.zero, Vector2.zero);
        }

        private static Transform CreateContainer(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, anchorMin, anchorMax, offsetMin, offsetMax);
            return go.transform;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font,
            string content, float fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin,
            Vector2 offsetMax, TextAlignmentOptions alignment, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, anchorMin, anchorMax, offsetMin, offsetMax);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static Sprite LoadSprite(string path, string name)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && sprite.name == name) { return sprite; }
            }
            throw new InvalidOperationException($"스프라이트를 찾지 못했습니다: {path} / {name}");
        }

        private static void SetReference(SerializedObject serialized, string field, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) { throw new InvalidOperationException($"직렬화 필드를 찾지 못했습니다: {field}"); }
            property.objectReferenceValue = value;
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
