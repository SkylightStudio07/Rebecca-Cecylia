using System;
using System.Collections.Generic;
using RCCom.Definitions.PlayerPart;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 기존 ShopPanelBackground와 좌측 탭을 보존한 채 Exchange 전용 uGUI 계층을 반복 생성한다.
    /// 목업 단계의 색면 UI라 신규 아트가 도착하면 Image Sprite만 교체해도 데이터 배선은 유지된다.
    /// </summary>
    public static class PlayerPartShopPanelSetup
    {
        private const string CatalogPath = "Assets/Resources/PlayerParts/PlayerPartCatalog.asset";
        private const string LeftPanelSheetPath = "Assets/Art/UI/UnderPanel/LeftPanelSheet.png";
        private const string PlayerSpritePath = "Assets/Art/Player/player-cursor-sprite-no-halo.png";

        private static readonly Color Panel = new(0.005f, 0.025f, 0.045f, 0.96f);
        private static readonly Color PanelSoft = new(0.012f, 0.065f, 0.11f, 0.92f);
        private static readonly Color Cyan = new(0f, 0.66f, 1f, 1f);
        private static readonly Color White = new(0.93f, 0.97f, 1f, 1f);

        [MenuItem("RCCom/UI/Build Player Part Exchange Panel")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exchange UI 생성은 Edit Mode에서만 실행할 수 있습니다.");
            }

            GameObject canvas = RequireSceneObject("Canvas");
            Transform mainMenu = Require(canvas.transform, "MainMenuBackground");
            Transform shop = Require(canvas.transform, "ShopPanelBackground");
            Transform leftFrame = Require(shop, "LeftFrame");
            Transform exchangeTab = Require(leftFrame, "VerticalLayout/ExchangeButton");
            Transform exchangeEntry = Require(mainMenu, "underPanel/ExchangeButton");

            PlayerPartCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayerPartCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"PlayerPartCatalog를 찾지 못했습니다: {CatalogPath}");
            }

            RectTransform root = EnsureRect(shop, "ExchangePanel");
            Stretch(root, new Vector2(170f, 22f), new Vector2(-24f, -22f));
            UnityEngine.UI.Image rootImage = GetOrAdd<UnityEngine.UI.Image>(root.gameObject);
            rootImage.color = Panel;
            rootImage.raycastTarget = true;
            root.SetAsLastSibling();
            leftFrame.SetAsLastSibling();

            TMP_FontAsset font = ResolveFont(shop);
            EnsureText(root, "Title", font, "CRAFT EXCHANGE", 58f,
                new Vector2(-690f, 590f), new Vector2(780f, 80f), TextAlignmentOptions.Left);
            EnsureText(root, "Subtitle", font, "CONFIGURE PLAYER UNIT", 22f,
                new Vector2(-690f, 540f), new Vector2(780f, 42f), TextAlignmentOptions.Left, Cyan);

            RectTransform preview = EnsurePanel(root, "CraftPreviewPanel",
                new Vector2(-335f, 120f), new Vector2(1350f, 760f), PanelSoft);
            UnityEngine.UI.Image craft = EnsureImage(preview, "CraftPreview",
                new Vector2(0f, -35f), new Vector2(700f, 520f));
            craft.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
            craft.preserveAspect = true;
            craft.color = new Color(0.72f, 0.92f, 1f, 1f);

            var slotButtons = new UnityEngine.UI.Button[5];
            var slotBackgrounds = new UnityEngine.UI.Image[5];
            var equippedTexts = new TMP_Text[5];
            string[] slotNames = { "THRUSTER", "TURRET", "BODY", "DRIVER", "SPECIAL" };
            for (int i = 0; i < slotNames.Length; i++)
            {
                float x = -520f + i * 260f;
                UnityEngine.UI.Button button = EnsureButton(preview, $"{slotNames[i]}Button", font,
                    slotNames[i], new Vector2(x, 310f), new Vector2(230f, 108f), PanelSoft);
                slotButtons[i] = button;
                slotBackgrounds[i] = button.GetComponent<UnityEngine.UI.Image>();
                equippedTexts[i] = EnsureText(button.transform, "EquippedText", font, "--", 14f,
                    new Vector2(0f, -30f), new Vector2(200f, 28f), TextAlignmentOptions.Center, Cyan);
            }

            RectTransform cardsRoot = EnsurePanel(root, "PartCarousel",
                new Vector2(-335f, -430f), new Vector2(1350f, 300f), new Color(0f, 0f, 0f, 0.35f));
            var cards = new PlayerPartShopCardView[5];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = BuildCard(cardsRoot, font, i);
            }

            UnityEngine.UI.Button previousPage = EnsureButton(cardsRoot, "PreviousPageButton", font, "‹",
                new Vector2(-630f, 0f), new Vector2(55f, 120f), PanelSoft);
            UnityEngine.UI.Button nextPage = EnsureButton(cardsRoot, "NextPageButton", font, "›",
                new Vector2(630f, 0f), new Vector2(55f, 120f), PanelSoft);

            RectTransform detail = EnsurePanel(root, "SpecificationPanel",
                new Vector2(670f, 0f), new Vector2(620f, 1220f), PanelSoft);
            EnsureText(detail, "Heading", font, "PART SPECIFICATION", 24f,
                new Vector2(0f, 540f), new Vector2(540f, 50f), TextAlignmentOptions.Left, Cyan);
            UnityEngine.UI.Image selectedIcon = EnsureImage(detail, "SelectedIcon",
                new Vector2(-205f, 420f), new Vector2(130f, 130f));
            TMP_Text partName = EnsureText(detail, "PartName", font, "NO PART", 36f,
                new Vector2(45f, 455f), new Vector2(390f, 60f), TextAlignmentOptions.Left);
            TMP_Text grade = EnsureText(detail, "Grade", font, "--", 20f,
                new Vector2(45f, 405f), new Vector2(390f, 36f), TextAlignmentOptions.Left, Cyan);
            TMP_Text description = EnsureText(detail, "Description", font, string.Empty, 19f,
                new Vector2(0f, 275f), new Vector2(540f, 150f), TextAlignmentOptions.TopLeft);
            TMP_Text comparison = EnsureText(detail, "Comparison", font, string.Empty, 21f,
                new Vector2(0f, 65f), new Vector2(540f, 235f), TextAlignmentOptions.TopLeft);
            EnsureText(detail, "CostLabel", font, "COST", 18f,
                new Vector2(-175f, -105f), new Vector2(170f, 36f), TextAlignmentOptions.Left, Cyan);
            TMP_Text cost = EnsureText(detail, "Cost", font, "0", 30f,
                new Vector2(-65f, -105f), new Vector2(140f, 45f), TextAlignmentOptions.Left);
            EnsureText(detail, "OwnedLabel", font, "OWNED", 18f,
                new Vector2(110f, -105f), new Vector2(170f, 36f), TextAlignmentOptions.Left, Cyan);
            TMP_Text owned = EnsureText(detail, "Owned", font, "0", 30f,
                new Vector2(220f, -105f), new Vector2(140f, 45f), TextAlignmentOptions.Left);

            UnityEngine.UI.Button equip = EnsureButton(detail, "EquipButton", font, "EQUIP",
                new Vector2(0f, -225f), new Vector2(540f, 90f), new Color(0f, 0.36f, 0.82f, 0.95f));
            UnityEngine.UI.Button buy = EnsureButton(detail, "BuyButton", font, "BUY",
                new Vector2(-140f, -330f), new Vector2(255f, 72f), Panel);
            UnityEngine.UI.Button sell = EnsureButton(detail, "SellButton", font, "SELL 0",
                new Vector2(140f, -330f), new Vector2(255f, 72f), Panel);
            TMP_Text status = EnsureText(detail, "Status", font, string.Empty, 17f,
                new Vector2(0f, -410f), new Vector2(540f, 52f), TextAlignmentOptions.Center, Cyan);
            UnityEngine.UI.Button back = EnsureButton(detail, "BackButton", font, "BACK",
                new Vector2(0f, -510f), new Vector2(540f, 74f), Panel);

            PlayerPartShopUI exchange = GetOrAdd<PlayerPartShopUI>(root.gameObject);
            SerializedObject exchangeSerialized = new(exchange);
            Assign(exchangeSerialized, "catalog", catalog);
            AssignArray(exchangeSerialized, "slotButtons", slotButtons);
            AssignArray(exchangeSerialized, "slotBackgrounds", slotBackgrounds);
            AssignArray(exchangeSerialized, "slotEquippedTexts", equippedTexts);
            AssignArray(exchangeSerialized, "cardViews", cards);
            Assign(exchangeSerialized, "previousPageButton", previousPage);
            Assign(exchangeSerialized, "nextPageButton", nextPage);
            Assign(exchangeSerialized, "selectedIcon", selectedIcon);
            Assign(exchangeSerialized, "partNameText", partName);
            Assign(exchangeSerialized, "gradeText", grade);
            Assign(exchangeSerialized, "descriptionText", description);
            Assign(exchangeSerialized, "comparisonText", comparison);
            Assign(exchangeSerialized, "costText", cost);
            Assign(exchangeSerialized, "ownedText", owned);
            Assign(exchangeSerialized, "statusText", status);
            Assign(exchangeSerialized, "buyButton", buy);
            Assign(exchangeSerialized, "equipButton", equip);
            Assign(exchangeSerialized, "sellButton", sell);
            Assign(exchangeSerialized, "buyButtonText", RequireText(buy.transform, "Label"));
            Assign(exchangeSerialized, "equipButtonText", RequireText(equip.transform, "Label"));
            Assign(exchangeSerialized, "sellButtonText", RequireText(sell.transform, "Label"));
            exchangeSerialized.ApplyModifiedPropertiesWithoutUndo();

            LobbyShopPanelUI lobby = canvas.GetComponent<LobbyShopPanelUI>();
            if (lobby == null)
            {
                throw new InvalidOperationException("Canvas에 LobbyShopPanelUI가 없습니다. Shop Setup을 먼저 실행하세요.");
            }

            UnityEngine.UI.Button entryButton = GetOrAdd<UnityEngine.UI.Button>(exchangeEntry.gameObject);
            entryButton.targetGraphic = GetOrAdd<UnityEngine.UI.Image>(exchangeEntry.gameObject);
            UnityEngine.UI.Button tabButton = GetOrAdd<UnityEngine.UI.Button>(exchangeTab.gameObject);
            UnityEngine.UI.Image tabImage = GetOrAdd<UnityEngine.UI.Image>(exchangeTab.gameObject);
            tabButton.targetGraphic = tabImage;
            Sprite normal = LoadSheetSprite(1);
            Sprite selected = LoadSheetSprite(5);
            SerializedObject lobbySerialized = new(lobby);
            Assign(lobbySerialized, "exchangeEntryButton", entryButton);
            Assign(lobbySerialized, "shopExchangeTabButton", tabButton);
            Assign(lobbySerialized, "exchangeBackButton", back);
            Assign(lobbySerialized, "exchangePanel", root.gameObject);
            Assign(lobbySerialized, "exchangeController", exchange);
            Assign(lobbySerialized, "shopExchangeTabImage", tabImage);
            Assign(lobbySerialized, "exchangeNormalSprite", normal);
            Assign(lobbySerialized, "exchangeSelectedSprite", selected);
            lobbySerialized.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            EditorUtility.SetDirty(root.gameObject);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerPartShopPanelSetup] Exchange uGUI와 5슬롯 파츠 상점 배선 완료");
        }

        [MenuItem("RCCom/UI/Validate Player Part Exchange Panel")]
        public static void Validate()
        {
            GameObject canvas = RequireSceneObject("Canvas");
            Transform root = Require(Require(canvas.transform, "ShopPanelBackground"), "ExchangePanel");
            PlayerPartShopUI controller = root.GetComponent<PlayerPartShopUI>();
            if (controller == null || root.Find("CraftPreviewPanel") == null ||
                root.Find("PartCarousel") == null || root.Find("SpecificationPanel") == null)
            {
                throw new InvalidOperationException("ExchangePanel 필수 계층 또는 컨트롤러가 비어 있습니다.");
            }

            SerializedObject serialized = new(controller);
            if (serialized.FindProperty("slotButtons").arraySize != 5 ||
                serialized.FindProperty("cardViews").arraySize != 5 ||
                serialized.FindProperty("catalog").objectReferenceValue == null)
            {
                throw new InvalidOperationException("ExchangePanel의 5슬롯·5카드·카탈로그 배선이 올바르지 않습니다.");
            }

            Debug.Log("[PlayerPartShopPanelSetup] Exchange uGUI 배선 검증 통과");
        }

        private static PlayerPartShopCardView BuildCard(Transform parent, TMP_FontAsset font, int index)
        {
            float x = -510f + index * 255f;
            UnityEngine.UI.Button button = EnsureButton(parent, $"PartCard_{index + 1:00}", font, string.Empty,
                new Vector2(x, 0f), new Vector2(235f, 265f), PanelSoft);
            UnityEngine.UI.Image icon = EnsureImage(button.transform, "Icon",
                new Vector2(0f, 35f), new Vector2(150f, 130f));
            TMP_Text name = EnsureText(button.transform, "Name", font, "PART", 17f,
                new Vector2(0f, 112f), new Vector2(215f, 42f), TextAlignmentOptions.Center);
            TMP_Text grade = EnsureText(button.transform, "Grade", font, "COMMON", 14f,
                new Vector2(-65f, -65f), new Vector2(95f, 28f), TextAlignmentOptions.Left, Cyan);
            TMP_Text state = EnsureText(button.transform, "State", font, "OWNED", 14f,
                new Vector2(35f, -65f), new Vector2(110f, 28f), TextAlignmentOptions.Right);
            TMP_Text price = EnsureText(button.transform, "Price", font, "0", 22f,
                new Vector2(0f, -102f), new Vector2(190f, 35f), TextAlignmentOptions.Center);

            PlayerPartShopCardView view = GetOrAdd<PlayerPartShopCardView>(button.gameObject);
            SerializedObject serialized = new(view);
            Assign(serialized, "button", button);
            Assign(serialized, "background", button.GetComponent<UnityEngine.UI.Image>());
            Assign(serialized, "icon", icon);
            Assign(serialized, "nameText", name);
            Assign(serialized, "gradeText", grade);
            Assign(serialized, "stateText", state);
            Assign(serialized, "priceText", price);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static RectTransform EnsurePanel(Transform parent, string name, Vector2 position,
            Vector2 size, Color color)
        {
            RectTransform rect = EnsureRect(parent, name);
            SetRect(rect, position, size);
            UnityEngine.UI.Image image = GetOrAdd<UnityEngine.UI.Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = false;
            UnityEngine.UI.Outline outline = GetOrAdd<UnityEngine.UI.Outline>(rect.gameObject);
            outline.effectColor = new Color(0f, 0.55f, 1f, 0.72f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return rect;
        }

        private static UnityEngine.UI.Button EnsureButton(Transform parent, string name, TMP_FontAsset font,
            string label, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = EnsureRect(parent, name);
            SetRect(rect, position, size);
            UnityEngine.UI.Image image = GetOrAdd<UnityEngine.UI.Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = true;
            UnityEngine.UI.Button button = GetOrAdd<UnityEngine.UI.Button>(rect.gameObject);
            button.targetGraphic = image;
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.55f, 0.87f, 1f, 1f);
            colors.pressedColor = new Color(0.25f, 0.68f, 1f, 1f);
            colors.disabledColor = new Color(0.3f, 0.35f, 0.4f, 0.45f);
            button.colors = colors;
            EnsureText(rect, "Label", font, label, 22f, Vector2.zero,
                new Vector2(size.x - 20f, size.y - 12f), TextAlignmentOptions.Center);
            return button;
        }

        private static UnityEngine.UI.Image EnsureImage(Transform parent, string name,
            Vector2 position, Vector2 size)
        {
            RectTransform rect = EnsureRect(parent, name);
            SetRect(rect, position, size);
            UnityEngine.UI.Image image = GetOrAdd<UnityEngine.UI.Image>(rect.gameObject);
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI EnsureText(Transform parent, string name, TMP_FontAsset font,
            string value, float size, Vector2 position, Vector2 rectSize, TextAlignmentOptions alignment,
            Color? color = null)
        {
            RectTransform rect = EnsureRect(parent, name);
            SetRect(rect, position, rectSize);
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(rect.gameObject);
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color ?? White;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_Text RequireText(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
            return text != null ? text : throw new InvalidOperationException($"{parent.name}/{name} TMP를 찾지 못했습니다.");
        }

        private static TMP_FontAsset ResolveFont(Transform root)
        {
            TMP_Text text = root.GetComponentInChildren<TMP_Text>(true);
            if (text != null && text.font != null) { return text.font; }
            return TMP_Settings.defaultFontAsset;
        }

        private static Sprite LoadSheetSprite(int index)
        {
            string name = $"LeftPanelSheet_{index}";
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(LeftPanelSheetPath))
            {
                if (asset is Sprite sprite && sprite.name == name) { return sprite; }
            }
            throw new InvalidOperationException($"{LeftPanelSheetPath}에서 {name}을 찾지 못했습니다.");
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                RectTransform rect = existing as RectTransform;
                return rect != null ? rect : throw new InvalidOperationException($"{name}이 RectTransform이 아닙니다.");
            }

            GameObject created = new(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            created.transform.SetParent(parent, false);
            return (RectTransform)created.transform;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = min;
            rect.offsetMax = max;
            rect.localScale = Vector3.one;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(target);
        }

        private static void Assign(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) { throw new InvalidOperationException($"{name} 직렬화 필드를 찾지 못했습니다."); }
            property.objectReferenceValue = value;
        }

        private static void AssignArray<T>(SerializedObject serialized, string name, IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) { throw new InvalidOperationException($"{name} 직렬화 배열을 찾지 못했습니다."); }
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static GameObject RequireSceneObject(string name)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.scene.IsValid() && candidate.scene.isLoaded && candidate.name == name) { return candidate; }
            }
            throw new InvalidOperationException($"현재 씬에서 {name}을 찾지 못했습니다.");
        }

        private static Transform Require(Transform parent, string path)
        {
            Transform child = parent.Find(path);
            return child != null ? child : throw new InvalidOperationException($"{parent.name}/{path}를 찾지 못했습니다.");
        }
    }
}
