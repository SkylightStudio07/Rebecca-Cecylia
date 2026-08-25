using System;
using System.Collections.Generic;
using RCCom.Definitions.Operator;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>기존 ShopPanelBackground를 재사용해 강화 탭의 임시 uGUI와 배선을 생성한다.</summary>
    public static class OperatorEnhancePanelSetup
    {
        private const string CatalogPath = "Assets/Data/Operators/OperatorCatalog.asset";
        private const string FontPath = "Assets/Resource/Font/Pretendard-Bold SDF.asset";
        private const string LeftPanelSheetPath = "Assets/Art/UI/UnderPanel/LeftPanelSheet.png";
        private const int TrackViewCount = 5;

        [MenuItem("RCCom/UI/Setup Operator Enhance Panel")]
        public static void Setup()
        {
            EnsureEditMode();
            GameObject canvas = RequireRoot("Canvas");
            Transform mainMenu = Require(canvas.transform, "MainMenuBackground").transform;
            Transform shop = Require(canvas.transform, "ShopPanelBackground").transform;
            Transform operatorPanel = Require(shop, "OperatorPanel").transform;
            Transform underPanel = Require(operatorPanel, "UnderPanel").transform;
            GameObject strategistPanel = Require(shop, "StrategistPanel");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            OperatorCatalog catalog = AssetDatabase.LoadAssetAtPath<OperatorCatalog>(CatalogPath);
            if (font == null || catalog == null)
            {
                throw new InvalidOperationException("강화 UI용 TMP 글꼴 또는 OperatorCatalog를 찾지 못했습니다.");
            }

            GameObject enhancePanel = EnsureRect(shop, "EnhancePanel").gameObject;
            CopyRect((RectTransform)strategistPanel.transform, (RectTransform)enhancePanel.transform);
            CopyPanelImage(strategistPanel, enhancePanel);

            TextMeshProUGUI protocolText = EnsureText(enhancePanel.transform, "ProtocolText", font,
                "UPGRADE PROTOCOL", 34, new Color(0.15f, 0.75f, 1f), TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -58f), new Vector2(900f, 50f));
            TextMeshProUGUI panelName = EnsureText(enhancePanel.transform, "OperatorNameText", font,
                "OPERATOR", 48, Color.white, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -132f), new Vector2(900f, 64f));
            TextMeshProUGUI panelAlias = EnsureText(enhancePanel.transform, "AnotherNameText", font,
                string.Empty, 25, new Color(0.76f, 0.82f, 0.86f), TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -190f), new Vector2(900f, 42f));

            var trackViews = new List<OperatorUpgradeTrackView>();
            for (int i = 0; i < TrackViewCount; i++)
            {
                trackViews.Add(BuildTrackView(enhancePanel.transform, font, i));
            }

            GameObject detailPanel = EnsureRect(enhancePanel.transform, "UpgradeDetailPanel").gameObject;
            SetRect((RectTransform)detailPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -835f), new Vector2(920f, 170f));
            Image detailImage = GetOrAdd<Image>(detailPanel);
            detailImage.color = new Color(0.015f, 0.035f, 0.05f, 0.94f);
            detailImage.raycastTarget = false;
            TextMeshProUGUI levelPreview = EnsureText(detailPanel.transform, "LevelPreviewText", font,
                "LV. 00  >  LV. 01", 32, new Color(0.3f, 0.8f, 1f), TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -35f), new Vector2(870f, 45f));
            TextMeshProUGUI description = EnsureText(detailPanel.transform, "DescriptionText", font,
                "강화 트랙을 선택하세요.", 22, new Color(0.82f, 0.87f, 0.9f), TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -92f), new Vector2(870f, 70f));

            TextMeshProUGUI cost = EnsureText(enhancePanel.transform, "CostText", font, "--", 30,
                Color.white, TextAlignmentOptions.Center,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, -1042f), new Vector2(360f, 62f));
            TextMeshProUGUI owned = EnsureText(enhancePanel.transform, "OwnedText", font, "0", 30,
                Color.white, TextAlignmentOptions.Center,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(650f, -1042f), new Vector2(360f, 62f));
            EnsureText(enhancePanel.transform, "CostLabel", font, "COST", 20,
                new Color(0.2f, 0.75f, 1f), TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -1042f), new Vector2(160f, 62f));
            EnsureText(enhancePanel.transform, "OwnedLabel", font, "OWNED", 20,
                new Color(0.2f, 0.75f, 1f), TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(470f, -1042f), new Vector2(160f, 62f));
            TextMeshProUGUI status = EnsureText(enhancePanel.transform, "StatusText", font,
                "강화 데이터를 불러오는 중…", 19, new Color(0.72f, 0.82f, 0.88f),
                TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -1095f), new Vector2(900f, 40f));

            Button enhanceButton = BuildButton(enhancePanel.transform, "EnhanceButton", font, "ENHANCE",
                new Vector2(0f, -1150f), new Vector2(900f, 92f),
                new Color(0.02f, 0.42f, 0.86f, 1f));
            Button enhanceBackButton = BuildButton(enhancePanel.transform, "BackButton", font, "BACK",
                new Vector2(0f, -1260f), new Vector2(900f, 78f),
                new Color(0.015f, 0.06f, 0.09f, 1f));

            OperatorEnhanceShopUI enhanceController = shop.GetComponent<OperatorEnhanceShopUI>();
            if (enhanceController == null) { enhanceController = Undo.AddComponent<OperatorEnhanceShopUI>(shop.gameObject); }
            SerializedObject enhanceSerialized = new(enhanceController);
            Assign(enhanceSerialized, "catalog", catalog);
            Assign(enhanceSerialized, "operatorPortrait", ImageAt(operatorPanel, "OperatorPortrait"));
            Assign(enhanceSerialized, "operatorUpperBodyPortrait", ImageAt(underPanel, "OperatorUpperbodyPortrait"));
            Assign(enhanceSerialized, "operatorUpperBodyPortraitLeft", ImageAt(underPanel, "OperatorUpperbodyPortrait_Left"));
            Assign(enhanceSerialized, "operatorUpperBodyPortraitRight", ImageAt(underPanel, "OperatorUpperbodyPortrait_Right"));
            Assign(enhanceSerialized, "lockSpriteLeft", Require(underPanel, "LockSprite_Left"));
            Assign(enhanceSerialized, "lockSpriteRight", Require(underPanel, "LockSprite_Right"));
            Assign(enhanceSerialized, "operatorName", TextAt(underPanel, "OperatorName"));
            Assign(enhanceSerialized, "operatorNameLeft", TextAt(underPanel, "OperatorName_Left"));
            Assign(enhanceSerialized, "operatorNameRight", TextAt(underPanel, "OperatorName_Right"));
            Assign(enhanceSerialized, "anotherNameText", TextAt(underPanel, "AnotherNameText"));
            Assign(enhanceSerialized, "previousButton", ButtonAt(operatorPanel, "LeftButton"));
            Assign(enhanceSerialized, "nextButton", ButtonAt(operatorPanel, "RightButton"));
            Assign(enhanceSerialized, "panelOperatorName", panelName);
            Assign(enhanceSerialized, "panelAlternateName", panelAlias);
            Assign(enhanceSerialized, "levelPreviewText", levelPreview);
            Assign(enhanceSerialized, "descriptionText", description);
            Assign(enhanceSerialized, "costText", cost);
            Assign(enhanceSerialized, "ownedText", owned);
            Assign(enhanceSerialized, "statusText", status);
            Assign(enhanceSerialized, "enhanceButton", enhanceButton);
            SerializedProperty views = enhanceSerialized.FindProperty("trackViews");
            views.arraySize = trackViews.Count;
            for (int i = 0; i < trackViews.Count; i++)
            {
                views.GetArrayElementAtIndex(i).objectReferenceValue = trackViews[i];
            }
            enhanceSerialized.ApplyModifiedPropertiesWithoutUndo();
            enhanceController.enabled = false;

            LobbyShopPanelUI lobbyController = canvas.GetComponent<LobbyShopPanelUI>();
            if (lobbyController == null)
            {
                throw new InvalidOperationException("Canvas에 LobbyShopPanelUI가 없습니다. 기존 Shop Setup을 먼저 실행하세요.");
            }

            Button lobbyRecruit = EnsureImageButton(mainMenu, "underPanel/RecruitButton");
            Button lobbyEnhance = EnsureImageButton(mainMenu, "underPanel/EnhanceButton");
            Button shopRecruit = ButtonAt(shop, "LeftFrame/VerticalLayout/RecruitButton");
            Button shopEnhance = ButtonAt(shop, "LeftFrame/VerticalLayout/EnhanceButton");
            SerializedObject lobbySerialized = new(lobbyController);
            Assign(lobbySerialized, "recruitEntryButton", lobbyRecruit);
            Assign(lobbySerialized, "enhanceEntryButton", lobbyEnhance);
            Assign(lobbySerialized, "shopRecruitTabButton", shopRecruit);
            Assign(lobbySerialized, "shopEnhanceTabButton", shopEnhance);
            Assign(lobbySerialized, "enhanceBackButton", enhanceBackButton);
            Assign(lobbySerialized, "recruitPanel", strategistPanel);
            Assign(lobbySerialized, "enhancePanel", enhancePanel);
            Assign(lobbySerialized, "recruitController", shop.GetComponent<OperatorRecruitShopUI>());
            Assign(lobbySerialized, "enhanceController", enhanceController);
            Assign(lobbySerialized, "shopRecruitTabImage", shopRecruit.GetComponent<Image>());
            Assign(lobbySerialized, "shopEnhanceTabImage", shopEnhance.GetComponent<Image>());
            Assign(lobbySerialized, "recruitNormalSprite", LoadSheetSprite(0));
            Assign(lobbySerialized, "recruitSelectedSprite", LoadSheetSprite(4));
            Assign(lobbySerialized, "enhanceNormalSprite", LoadSheetSprite(2));
            Assign(lobbySerialized, "enhanceSelectedSprite", LoadSheetSprite(6));
            lobbySerialized.ApplyModifiedPropertiesWithoutUndo();

            enhancePanel.SetActive(false);
            strategistPanel.SetActive(true);
            EditorUtility.SetDirty(enhancePanel);
            EditorUtility.SetDirty(enhanceController);
            EditorUtility.SetDirty(lobbyController);
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OperatorEnhancePanelSetup] Shop 공용 OperatorPanel과 강화 uGUI 배선 완료");
        }

        [MenuItem("RCCom/UI/Validate Operator Enhance Panel")]
        public static void Validate()
        {
            GameObject canvas = RequireRoot("Canvas");
            Transform shop = Require(canvas.transform, "ShopPanelBackground").transform;
            GameObject enhancePanel = Require(shop, "EnhancePanel");
            OperatorEnhanceShopUI enhanceController = shop.GetComponent<OperatorEnhanceShopUI>();
            LobbyShopPanelUI lobbyController = canvas.GetComponent<LobbyShopPanelUI>();
            if (enhanceController == null || lobbyController == null)
            {
                throw new InvalidOperationException("강화 또는 Shop 전환 컨트롤러가 없습니다.");
            }

            SerializedObject enhanceSerialized = new(enhanceController);
            string[] required =
            {
                "catalog", "operatorPortrait", "operatorUpperBodyPortrait",
                "operatorUpperBodyPortraitLeft", "operatorUpperBodyPortraitRight",
                "lockSpriteLeft", "lockSpriteRight", "operatorName", "operatorNameLeft",
                "operatorNameRight", "anotherNameText", "previousButton", "nextButton",
                "panelOperatorName", "panelAlternateName", "levelPreviewText", "descriptionText",
                "costText", "ownedText", "statusText", "enhanceButton"
            };
            foreach (string propertyName in required)
            {
                SerializedProperty property = enhanceSerialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException($"OperatorEnhanceShopUI.{propertyName} 연결이 비어 있습니다.");
                }
            }

            SerializedProperty views = enhanceSerialized.FindProperty("trackViews");
            if (views == null || views.arraySize != TrackViewCount)
            {
                throw new InvalidOperationException($"강화 트랙 행은 {TrackViewCount}개여야 합니다.");
            }
            for (int i = 0; i < views.arraySize; i++)
            {
                if (views.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    throw new InvalidOperationException($"강화 트랙 행 {i + 1} 연결이 비어 있습니다.");
                }
            }

            SerializedObject lobbySerialized = new(lobbyController);
            string[] lobbyRequired =
            {
                "enhanceEntryButton", "shopRecruitTabButton", "shopEnhanceTabButton",
                "enhanceBackButton", "recruitPanel", "enhancePanel", "recruitController",
                "enhanceController", "shopRecruitTabImage", "shopEnhanceTabImage",
                "recruitNormalSprite", "recruitSelectedSprite", "enhanceNormalSprite",
                "enhanceSelectedSprite"
            };
            foreach (string propertyName in lobbyRequired)
            {
                SerializedProperty property = lobbySerialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException($"LobbyShopPanelUI.{propertyName} 연결이 비어 있습니다.");
                }
            }

            if (enhancePanel.activeSelf)
            {
                throw new InvalidOperationException("TitleScene 기본 상태에서 EnhancePanel은 비활성이어야 합니다.");
            }

            Debug.Log("[OperatorEnhancePanelSetup] 강화 uGUI와 탭 전환 배선 검증 통과");
        }

        private static OperatorUpgradeTrackView BuildTrackView(Transform parent, TMP_FontAsset font, int index)
        {
            GameObject row = EnsureRect(parent, $"UpgradeTrack_{index + 1:00}").gameObject;
            SetRect((RectTransform)row.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -270f - index * 105f), new Vector2(920f, 86f));
            Image background = GetOrAdd<Image>(row);
            background.color = new Color(0.025f, 0.07f, 0.1f, 0.94f);
            Button button = GetOrAdd<Button>(row);
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.05f, 0.42f, 0.75f, 1f);
            colors.pressedColor = new Color(0.03f, 0.3f, 0.58f, 1f);
            button.colors = colors;

            TextMeshProUGUI category = EnsureText(row.transform, "CategoryText", font, "CORE", 15,
                new Color(0.2f, 0.75f, 1f), TextAlignmentOptions.Left,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 19f), new Vector2(130f, 28f));
            TextMeshProUGUI name = EnsureText(row.transform, "NameText", font, "TRACK", 23,
                Color.white, TextAlignmentOptions.Left,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -15f), new Vector2(510f, 36f));
            TextMeshProUGUI progress = EnsureText(row.transform, "ProgressText", font, "□□□□□□□□", 21,
                new Color(0.22f, 0.72f, 1f), TextAlignmentOptions.Left,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(540f, -11f), new Vector2(235f, 34f));
            TextMeshProUGUI level = EnsureText(row.transform, "LevelText", font, "LV. 00", 22,
                Color.white, TextAlignmentOptions.Right,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(130f, 44f));

            OperatorUpgradeTrackView view = row.GetComponent<OperatorUpgradeTrackView>();
            if (view == null) { view = Undo.AddComponent<OperatorUpgradeTrackView>(row); }
            SerializedObject serialized = new(view);
            Assign(serialized, "button", button);
            Assign(serialized, "background", background);
            Assign(serialized, "categoryText", category);
            Assign(serialized, "nameText", name);
            Assign(serialized, "levelText", level);
            Assign(serialized, "progressText", progress);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static Button BuildButton(Transform parent, string name, TMP_FontAsset font, string label,
            Vector2 position, Vector2 size, Color color)
        {
            GameObject target = EnsureRect(parent, name).gameObject;
            SetRect((RectTransform)target.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                position, size);
            Image image = GetOrAdd<Image>(target);
            image.color = color;
            image.raycastTarget = true;
            Button button = GetOrAdd<Button>(target);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(
                Mathf.Min(1f, color.r + 0.16f), Mathf.Min(1f, color.g + 0.16f),
                Mathf.Min(1f, color.b + 0.16f), color.a);
            colors.pressedColor = color * 0.8f;
            button.colors = colors;
            EnsureText(target.transform, "Label", font, label, 34, Color.white,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static TextMeshProUGUI EnsureText(Transform parent, string name, TMP_FontAsset font,
            string value, float size, Color color, TextAlignmentOptions alignment,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 rectSize)
        {
            GameObject target = EnsureRect(parent, name).gameObject;
            RectTransform rect = (RectTransform)target.transform;
            if (anchor == Vector2.zero && pivot == Vector2.one && rectSize == Vector2.zero)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                SetRect(rect, anchor, pivot, position, rectSize);
            }

            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(target);
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing as RectTransform ?? throw new InvalidOperationException($"{name}이 RectTransform이 아닙니다.");
            }

            var target = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(target, $"Create {name}");
            target.transform.SetParent(parent, false);
            return (RectTransform)target.transform;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }

        private static void CopyPanelImage(GameObject source, GameObject target)
        {
            Image sourceImage = source.GetComponent<Image>();
            Image targetImage = GetOrAdd<Image>(target);
            if (sourceImage != null)
            {
                targetImage.sprite = sourceImage.sprite;
                targetImage.material = sourceImage.material;
                targetImage.color = sourceImage.color;
                targetImage.type = sourceImage.type;
                targetImage.preserveAspect = sourceImage.preserveAspect;
            }
            targetImage.raycastTarget = true;
        }

        private static Sprite LoadSheetSprite(int index)
        {
            string expected = $"LeftPanelSheet_{index}";
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(LeftPanelSheetPath))
            {
                if (asset is Sprite sprite && sprite.name == expected) { return sprite; }
            }
            throw new InvalidOperationException($"{expected}를 찾지 못했습니다.");
        }

        private static GameObject RequireRoot(string name)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name) { return root; }
            }
            throw new InvalidOperationException($"루트 오브젝트를 찾지 못했습니다: {name}");
        }

        private static GameObject Require(Transform root, string path)
        {
            Transform target = root.Find(path);
            return target != null ? target.gameObject : throw new InvalidOperationException($"오브젝트를 찾지 못했습니다: {root.name}/{path}");
        }

        private static Image ImageAt(Transform root, string path)
        {
            return RequireComponent<Image>(Require(root, path));
        }

        private static TMP_Text TextAt(Transform root, string path)
        {
            return RequireComponent<TMP_Text>(Require(root, path));
        }

        private static Button ButtonAt(Transform root, string path)
        {
            return RequireComponent<Button>(Require(root, path));
        }

        private static Button EnsureImageButton(Transform root, string path)
        {
            GameObject target = Require(root, path);
            Image image = RequireComponent<Image>(target);
            Button button = GetOrAdd<Button>(target);
            button.targetGraphic = image;
            image.raycastTarget = true;
            return button;
        }

        private static T RequireComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : throw new InvalidOperationException($"{target.name}에 {typeof(T).Name}이 없습니다.");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private static void Assign(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) { throw new InvalidOperationException($"직렬화 필드를 찾지 못했습니다: {propertyName}"); }
            property.objectReferenceValue = value;
        }

        private static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("강화 UI Setup은 Edit Mode에서만 실행할 수 있습니다.");
            }
        }
    }
}
