using System;
using RCCom.UI;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Unit;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>TitleScene의 로비 리크루트 진입과 Shop 복귀 버튼을 반복 가능하게 배선한다.</summary>
    public static class LobbyShopPanelSetup
    {
        private const string RecruitNormalPath = "Assets/Art/UI/UnderPanel/RecruitOperatorButton.png";
        private const string RecruitHoverPath = "Assets/Art/UI/UnderPanel/RecruitOperatorButton_hover.png";
        private const string RecruitAlreadyPurchasedPath =
            "Assets/Art/UI/UnderPanel/RecruitOperatorButton_AlreadyPurchased.png";
        private const string BackNormalPath = "Assets/Art/UI/UnderPanel/RightFrameV2Back.png";
        private const string BackHoverPath = "Assets/Art/UI/UnderPanel/RightFrameV2Back_hover.png";
        private const string LeftPanelSheetPath = "Assets/Art/UI/UnderPanel/LeftPanelSheet.png";
        private const string CatalogPath = "Assets/Data/Operators/OperatorCatalog.asset";
        private const string UnitPreviewPrefabPath =
            "Assets/Data/Prefabs/OperatorRosterPreviewItem.prefab";
        private const int ShopUnitSlotCount = 2;

        [MenuItem("RCCom/UI/Setup Lobby Shop Navigation")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Shop UI Setup은 Edit Mode에서만 실행할 수 있습니다.");
            }

            GameObject canvas = RequireSceneObject("Canvas");
            GameObject mainMenu = RequireChild(canvas.transform, "MainMenuBackground");
            GameObject shopPanel = RequireChild(canvas.transform, "ShopPanelBackground");
            GameObject entryObject = RequirePath(mainMenu.transform, "underPanel/RecruitButton");
            GameObject recruitObject = RequirePath(shopPanel.transform, "StrategistPanel/RecruitOperatorButton");
            GameObject backObject = RequirePath(shopPanel.transform, "StrategistPanel/BackButton");

            Button entryButton = GetOrAddButton(entryObject);
            Button recruitButton = ConfigureSpriteSwap(recruitObject, RecruitNormalPath, RecruitHoverPath);
            Button backButton = ConfigureSpriteSwap(backObject, BackNormalPath, BackHoverPath);
            ConfigureShopLeftNavigation(shopPanel.transform);

            LobbyShopPanelUI controller = canvas.GetComponent<LobbyShopPanelUI>();
            if (controller == null) { controller = Undo.AddComponent<LobbyShopPanelUI>(canvas); }

            SerializedObject serialized = new(controller);
            Assign(serialized, "mainMenuBackground", mainMenu);
            Assign(serialized, "shopPanelBackground", shopPanel);
            Assign(serialized, "recruitEntryButton", entryButton);
            Assign(serialized, "recruitOperatorButton", recruitButton);
            Assign(serialized, "backButton", backButton);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            OperatorRecruitShopUI shopController = shopPanel.GetComponent<OperatorRecruitShopUI>();
            if (shopController == null) { shopController = Undo.AddComponent<OperatorRecruitShopUI>(shopPanel); }

            OperatorCatalog catalog = AssetDatabase.LoadAssetAtPath<OperatorCatalog>(CatalogPath);
            if (catalog == null) { throw new InvalidOperationException($"OperatorCatalog를 찾지 못했습니다: {CatalogPath}"); }

            SerializedObject shopSerialized = new(shopController);
            Assign(shopSerialized, "catalog", catalog);
            Assign(shopSerialized, "acquisitionUI",
                UnityEngine.Object.FindFirstObjectByType<OperatorAcquisitionUI>(FindObjectsInactive.Include));
            Assign(shopSerialized, "operatorPortrait",
                RequireComponent<Image>(RequirePath(shopPanel.transform, "OperatorPanel/OperatorPortrait")));
            Assign(shopSerialized, "operatorUpperBodyPortrait",
                RequireComponent<Image>(RequirePath(shopPanel.transform,
                    "OperatorPanel/UnderPanel/OperatorUpperbodyPortrait")));
            Assign(shopSerialized, "operatorName",
                RequireComponent<TMP_Text>(RequirePath(shopPanel.transform,
                    "OperatorPanel/UnderPanel/OperatorName")));
            Assign(shopSerialized, "anotherNameText",
                RequireComponent<TMP_Text>(RequirePath(shopPanel.transform,
                    "OperatorPanel/UnderPanel/AnotherNameText")));
            Assign(shopSerialized, "priceText",
                RequireComponent<TMP_Text>(RequirePath(shopPanel.transform,
                    "StrategistPanel/PriceText")));
            Assign(shopSerialized, "rightOperatorNameText",
                RequireComponent<TMP_Text>(RequirePath(shopPanel.transform,
                    "StrategistPanel/OperatorNameText - RightPanel")));
            Assign(shopSerialized, "operatorDialogue",
                RequireComponent<TMP_Text>(RequirePath(shopPanel.transform,
                    "StrategistPanel/OperatorDialouge")));
            OperatorRosterPreviewItem[] unitItems = BuildUnitPreviewSlots(
                RequirePath(shopPanel.transform, "StrategistPanel").transform, catalog);
            SerializedProperty unitItemsProperty = shopSerialized.FindProperty("unitPreviewItems");
            if (unitItemsProperty == null)
            {
                throw new InvalidOperationException("unitPreviewItems 직렬화 필드를 찾지 못했습니다.");
            }

            unitItemsProperty.arraySize = unitItems.Length;
            for (int i = 0; i < unitItems.Length; i++)
            {
                unitItemsProperty.GetArrayElementAtIndex(i).objectReferenceValue = unitItems[i];
            }
            Assign(shopSerialized, "recruitOperatorButton", recruitButton);
            Assign(shopSerialized, "recruitOperatorButtonImage", recruitObject.GetComponent<Image>());
            Assign(shopSerialized, "recruitNormalSprite", LoadFullFrameSprite(RecruitNormalPath));
            Assign(shopSerialized, "recruitHoverSprite", LoadFullFrameSprite(RecruitHoverPath));
            Assign(shopSerialized, "recruitAlreadyPurchasedSprite",
                LoadFullFrameSprite(RecruitAlreadyPurchasedPath));
            shopSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(entryObject);
            EditorUtility.SetDirty(recruitObject);
            EditorUtility.SetDirty(backObject);
            EditorUtility.SetDirty(shopController);
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LobbyShopPanelSetup] 로비 리크루트 진입과 Shop 복귀 버튼 배선 완료");
        }

        [MenuItem("RCCom/UI/Setup Shop Left Navigation")]
        public static void SetupShopLeftNavigation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Shop 좌측 메뉴 Setup은 Edit Mode에서만 실행할 수 있습니다.");
            }

            GameObject canvas = RequireSceneObject("Canvas");
            GameObject shopPanel = RequireChild(canvas.transform, "ShopPanelBackground");
            ConfigureShopLeftNavigation(shopPanel.transform);
            EditorUtility.SetDirty(shopPanel);
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateShopLeftNavigation();
            Debug.Log("[LobbyShopPanelSetup] ShopPanelBackground 좌측 메뉴 배선 완료");
        }

        [MenuItem("RCCom/UI/Validate Lobby Shop Navigation")]
        public static void Validate()
        {
            GameObject canvas = RequireSceneObject("Canvas");
            LobbyShopPanelUI controller = canvas.GetComponent<LobbyShopPanelUI>();
            if (controller == null)
            {
                throw new InvalidOperationException("Canvas에 LobbyShopPanelUI가 없습니다.");
            }

            GameObject shopPanel = RequireChild(canvas.transform, "ShopPanelBackground");
            OperatorRecruitShopUI shopController = shopPanel.GetComponent<OperatorRecruitShopUI>();
            if (shopController == null)
            {
                throw new InvalidOperationException("ShopPanelBackground에 OperatorRecruitShopUI가 없습니다.");
            }

            SerializedObject shopSerialized = new(shopController);
            string[] requiredProperties =
            {
                "catalog", "operatorPortrait", "operatorUpperBodyPortrait", "operatorName",
                "anotherNameText", "priceText", "rightOperatorNameText", "operatorDialogue",
                "recruitOperatorButton", "recruitOperatorButtonImage", "recruitNormalSprite",
                "recruitHoverSprite", "recruitAlreadyPurchasedSprite",
            };
            foreach (string propertyName in requiredProperties)
            {
                SerializedProperty property = shopSerialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException($"OperatorRecruitShopUI.{propertyName} 연결이 비어 있습니다.");
                }
            }

            SerializedProperty unitItems = shopSerialized.FindProperty("unitPreviewItems");
            if (unitItems == null || unitItems.arraySize != ShopUnitSlotCount)
            {
                throw new InvalidOperationException($"상점 고유 유닛 슬롯은 {ShopUnitSlotCount}개여야 합니다.");
            }

            for (int i = 0; i < unitItems.arraySize; i++)
            {
                if (unitItems.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    throw new InvalidOperationException($"상점 고유 유닛 슬롯 {i + 1} 연결이 비어 있습니다.");
                }
            }

            OperatorCatalog catalog = AssetDatabase.LoadAssetAtPath<OperatorCatalog>(CatalogPath);
            OperatorCatalogEntry previewEntry = FindFirstPurchaseEntry(catalog);
            if (previewEntry == null || previewEntry.unitPreviews == null ||
                previewEntry.unitPreviews.Count < ShopUnitSlotCount)
            {
                throw new InvalidOperationException(
                    $"첫 구매형 오퍼레이터에 표시 가능한 고유 유닛이 {ShopUnitSlotCount}개 미만입니다.");
            }

            Transform unitContainer = RequirePath(shopPanel.transform,
                "StrategistPanel/OperatorUnitPreviewSlots").transform;
            for (int i = 0; i < ShopUnitSlotCount; i++)
            {
                GameObject slot = RequirePath(unitContainer, $"UnitPreview_{i + 1:00}");
                AllyUnitCatalogEntry preview = previewEntry.unitPreviews[i];
                TextMeshProUGUI name = RequireComponent<TextMeshProUGUI>(RequirePath(slot.transform, "Name"));
                TextMeshProUGUI cost = RequireComponent<TextMeshProUGUI>(RequirePath(slot.transform, "Cost"));
                Image icon = RequireComponent<Image>(RequirePath(slot.transform, "Icon"));
                if (!slot.activeSelf || name.text != preview.displayName ||
                    cost.text != $"CP {preview.deployCost}" || icon.sprite != preview.previewIcon)
                {
                    throw new InvalidOperationException($"상점 고유 유닛 슬롯 {i + 1} 표시값이 카탈로그와 다릅니다.");
                }
            }

            ValidateSpriteSwap(RequirePath(shopPanel.transform, "StrategistPanel/RecruitOperatorButton"),
                RecruitNormalPath, RecruitHoverPath);
            if (AssetDatabase.GetAssetPath(
                    shopSerialized.FindProperty("recruitAlreadyPurchasedSprite").objectReferenceValue) !=
                RecruitAlreadyPurchasedPath)
            {
                throw new InvalidOperationException(
                    "RecruitOperatorButton 구매 완료 스프라이트 연결이 올바르지 않습니다.");
            }
            ValidateSpriteSwap(RequirePath(shopPanel.transform, "StrategistPanel/BackButton"),
                BackNormalPath, BackHoverPath);
            ValidateShopLeftNavigation(shopPanel.transform);
            Debug.Log("[LobbyShopPanelSetup] Shop 이동·호버 배선 검증 통과");
        }

        [MenuItem("RCCom/UI/Validate Shop Left Navigation")]
        public static void ValidateShopLeftNavigation()
        {
            GameObject canvas = RequireSceneObject("Canvas");
            GameObject shopPanel = RequireChild(canvas.transform, "ShopPanelBackground");
            ValidateShopLeftNavigation(shopPanel.transform);
            Debug.Log("[LobbyShopPanelSetup] ShopPanelBackground 좌측 메뉴 검증 통과");
        }

        private static void ConfigureShopLeftNavigation(Transform shopPanel)
        {
            Transform layout = RequirePath(shopPanel, "LeftFrame/VerticalLayout").transform;
            ConfigureSelectedSheetButton(RequirePath(layout, "RecruitButton"), 4);
            ConfigureSheetSpriteSwap(RequirePath(layout, "ExchangeButton"), 1, 5);
            ConfigureSheetSpriteSwap(RequirePath(layout, "EnhanceButton"), 2, 6);
            ConfigureSheetSpriteSwap(RequirePath(layout, "MaterialButton"), 3, 7);
        }

        private static void ValidateShopLeftNavigation(Transform shopPanel)
        {
            Transform layout = RequirePath(shopPanel, "LeftFrame/VerticalLayout").transform;
            ValidateSelectedSheetButton(RequirePath(layout, "RecruitButton"), 4);
            ValidateSheetSpriteSwap(RequirePath(layout, "ExchangeButton"), 1, 5);
            ValidateSheetSpriteSwap(RequirePath(layout, "EnhanceButton"), 2, 6);
            ValidateSheetSpriteSwap(RequirePath(layout, "MaterialButton"), 3, 7);
        }

        private static Button ConfigureSheetSpriteSwap(GameObject target, int normalIndex, int hoverIndex)
        {
            Image image = RequireComponent<Image>(target);
            Sprite normal = LoadSheetSprite(normalIndex);
            Sprite hover = LoadSheetSprite(hoverIndex);
            image.sprite = normal;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.useSpriteMesh = false;
            image.raycastTarget = true;

            Button button = GetOrAddButton(target);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.highlightedSprite = hover;
            state.pressedSprite = hover;
            // 클릭 포커스가 남아도 현재 페이지가 아닌 버튼은 일반 상태로 돌아가야 한다.
            state.selectedSprite = normal;
            state.disabledSprite = normal;
            button.spriteState = state;
            EditorUtility.SetDirty(target);
            return button;
        }

        private static Button ConfigureSelectedSheetButton(GameObject target, int selectedIndex)
        {
            Image image = RequireComponent<Image>(target);
            image.sprite = LoadSheetSprite(selectedIndex);
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.useSpriteMesh = false;
            image.raycastTarget = true;

            Button button = GetOrAddButton(target);
            button.targetGraphic = image;
            // Recruit는 현재 페이지 탭이라 포인터 위치와 무관하게 선택 외형을 유지한다.
            button.transition = Selectable.Transition.None;
            EditorUtility.SetDirty(target);
            return button;
        }

        private static Sprite LoadSheetSprite(int index)
        {
            string expectedName = $"LeftPanelSheet_{index}";
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(LeftPanelSheetPath))
            {
                if (asset is Sprite sprite && sprite.name == expectedName) { return sprite; }
            }

            throw new InvalidOperationException(
                $"{LeftPanelSheetPath}에서 {expectedName} 스프라이트를 찾지 못했습니다.");
        }

        private static void ValidateSheetSpriteSwap(GameObject target, int normalIndex, int hoverIndex)
        {
            Image image = RequireComponent<Image>(target);
            Button button = RequireComponent<Button>(target);
            Sprite normal = LoadSheetSprite(normalIndex);
            Sprite hover = LoadSheetSprite(hoverIndex);
            SpriteState state = button.spriteState;
            if (image.sprite != normal || button.transition != Selectable.Transition.SpriteSwap ||
                state.highlightedSprite != hover || state.pressedSprite != hover ||
                state.selectedSprite != normal || state.disabledSprite != normal)
            {
                throw new InvalidOperationException(
                    $"{target.name}의 LeftPanelSheet Normal/Hover 배선이 올바르지 않습니다.");
            }
        }

        private static void ValidateSelectedSheetButton(GameObject target, int selectedIndex)
        {
            Image image = RequireComponent<Image>(target);
            Button button = RequireComponent<Button>(target);
            if (image.sprite != LoadSheetSprite(selectedIndex) ||
                button.transition != Selectable.Transition.None)
            {
                throw new InvalidOperationException($"{target.name}의 현재 탭 선택 외형이 올바르지 않습니다.");
            }
        }

        private static Button ConfigureSpriteSwap(GameObject target, string normalPath, string hoverPath)
        {
            Image image = target.GetComponent<Image>();
            if (image == null) { throw new InvalidOperationException($"{target.name}에 Image가 없습니다."); }

            Sprite previous = image.sprite;
            Rect previousRect = previous != null ? previous.rect : default;
            Vector2 previousTextureSize = previous != null
                ? new Vector2(previous.texture.width, previous.texture.height)
                : Vector2.zero;

            Sprite normal = LoadFullFrameSprite(normalPath);
            Sprite hover = LoadFullFrameSprite(hoverPath);
            if (normal == null || hover == null)
            {
                throw new InvalidOperationException($"버튼 스프라이트를 찾지 못했습니다: {normalPath}, {hoverPath}");
            }

            PreserveCurrentVisualBounds(image.rectTransform, previousRect, previousTextureSize);
            image.sprite = normal;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.useSpriteMesh = false;
            image.raycastTarget = true;
            Button button = GetOrAddButton(target);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.highlightedSprite = hover;
            state.selectedSprite = hover;
            state.pressedSprite = hover;
            button.spriteState = state;
            return button;
        }

        private static Sprite LoadFullFrameSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Sprite TextureImporter를 찾지 못했습니다: {path}");
            }

            // 자동 슬라이스는 발광 여백까지 서로 다른 크기로 잘라 Normal/Hover의 본체가
            // 같은 RectTransform 안에서 축소·이동해 보이게 한다. 동일한 원본 캔버스를 쓰는
            // 버튼 상태는 전체 프레임 Single Sprite로 읽어야 픽셀 좌표가 고정된다.
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void PreserveCurrentVisualBounds(RectTransform rect, Rect previousSpriteRect,
            Vector2 previousTextureSize)
        {
            if (previousSpriteRect.width <= 0f || previousSpriteRect.height <= 0f ||
                previousTextureSize.x <= 0f || previousTextureSize.y <= 0f)
            {
                return;
            }

            Vector2 currentSize = rect.rect.size;
            Vector2 unitsPerPixel = new(
                currentSize.x / previousSpriteRect.width,
                currentSize.y / previousSpriteRect.height);
            Vector2 fullSize = new(
                previousTextureSize.x * unitsPerPixel.x,
                previousTextureSize.y * unitsPerPixel.y);
            Vector2 sourceCenterOffset = new(
                previousTextureSize.x * 0.5f - previousSpriteRect.center.x,
                previousTextureSize.y * 0.5f - previousSpriteRect.center.y);

            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullSize.x);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fullSize.y);
            rect.anchoredPosition += Vector2.Scale(sourceCenterOffset, unitsPerPixel);
        }

        private static void ValidateSpriteSwap(GameObject target, string normalPath, string hoverPath)
        {
            Image image = target.GetComponent<Image>();
            Button button = target.GetComponent<Button>();
            Sprite normal = AssetDatabase.LoadAssetAtPath<Sprite>(normalPath);
            Sprite hover = AssetDatabase.LoadAssetAtPath<Sprite>(hoverPath);
            if (image == null || button == null || image.sprite != normal ||
                button.transition != Selectable.Transition.SpriteSwap ||
                button.spriteState.highlightedSprite != hover ||
                normal == null || hover == null ||
                normal.rect.size != hover.rect.size)
            {
                throw new InvalidOperationException($"{target.name}의 Normal/Hover SpriteSwap 배선이 올바르지 않습니다.");
            }
        }

        private static Button GetOrAddButton(GameObject target)
        {
            Button existing = target.GetComponent<Button>();
            return existing != null ? existing : Undo.AddComponent<Button>(target);
        }

        private static T RequireComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException($"{target.name}에 {typeof(T).Name}이 없습니다.");
        }

        private static OperatorRosterPreviewItem[] BuildUnitPreviewSlots(
            Transform strategistPanel,
            OperatorCatalog catalog)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPreviewPrefabPath);
            if (prefab == null || prefab.GetComponent<OperatorRosterPreviewItem>() == null)
            {
                throw new InvalidOperationException($"유닛 미리보기 프리팹을 찾지 못했습니다: {UnitPreviewPrefabPath}");
            }

            Transform container = strategistPanel.Find("OperatorUnitPreviewSlots");
            if (container == null)
            {
                GameObject created = new("OperatorUnitPreviewSlots", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(created, "Create Recruit Shop Unit Slots");
                created.transform.SetParent(strategistPanel, false);
                container = created.transform;
            }

            RectTransform containerRect = (RectTransform)container;
            SetRect(containerRect, new Vector2(-8f, 70f), new Vector2(840f, 150f));

            var items = new OperatorRosterPreviewItem[ShopUnitSlotCount];
            OperatorCatalogEntry previewEntry = FindFirstPurchaseEntry(catalog);
            for (int i = 0; i < ShopUnitSlotCount; i++)
            {
                string slotName = $"UnitPreview_{i + 1:00}";
                Transform existing = container.Find(slotName);
                GameObject slot;
                if (existing != null)
                {
                    slot = existing.gameObject;
                }
                else
                {
                    slot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                    if (slot == null)
                    {
                        throw new InvalidOperationException($"{slotName} 인스턴스를 만들지 못했습니다.");
                    }

                    Undo.RegisterCreatedObjectUndo(slot, "Create Recruit Shop Unit Preview");
                    slot.name = slotName;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(slot))
                {
                    // 선택 화면용 원본 프리팹의 간결한 데이터 바인딩 컴포넌트만 재사용하고,
                    // 상점 전용 크기·윤곽 오버라이드가 프리팹 갱신으로 되돌아가지 않게 분리한다.
                    PrefabUtility.UnpackPrefabInstance(slot,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }

                ConfigureUnitPreviewSlot(slot, i);
                OperatorRosterPreviewItem item = RequireComponent<OperatorRosterPreviewItem>(slot);
                items[i] = item;

                bool hasPreview = previewEntry != null && previewEntry.unitPreviews != null &&
                    i < previewEntry.unitPreviews.Count && previewEntry.unitPreviews[i] != null;
                slot.SetActive(hasPreview);
                if (hasPreview)
                {
                    item.Setup(previewEntry.unitPreviews[i]);
                }
            }

            EditorUtility.SetDirty(container.gameObject);
            return items;
        }

        private static void ConfigureUnitPreviewSlot(GameObject slot, int index)
        {
            RectTransform rect = RequireComponent<RectTransform>(slot);
            SetRect(rect, new Vector2(index == 0 ? -210f : 210f, 0f), new Vector2(390f, 120f));
            rect.localScale = Vector3.one;

            Image background = RequireComponent<Image>(slot);
            background.color = new Color(0.015f, 0.07f, 0.12f, 0.78f);
            background.raycastTarget = false;

            Outline outline = slot.GetComponent<Outline>();
            if (outline == null) { outline = Undo.AddComponent<Outline>(slot); }
            outline.effectColor = new Color(0f, 0.58f, 1f, 0.58f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;

            Image icon = RequireComponent<Image>(RequirePath(slot.transform, "Icon"));
            SetRect(icon.rectTransform, new Vector2(-132f, 0f), new Vector2(100f, 100f));
            icon.preserveAspect = true;

            TextMeshProUGUI name = RequireComponent<TextMeshProUGUI>(RequirePath(slot.transform, "Name"));
            SetRect(name.rectTransform, new Vector2(42f, 20f), new Vector2(230f, 42f));
            name.fontSize = 22f;

            TextMeshProUGUI cost = RequireComponent<TextMeshProUGUI>(RequirePath(slot.transform, "Cost"));
            SetRect(cost.rectTransform, new Vector2(62f, -28f), new Vector2(190f, 34f));
            cost.fontSize = 19f;

            EditorUtility.SetDirty(slot);
        }

        private static OperatorCatalogEntry FindFirstPurchaseEntry(OperatorCatalog catalog)
        {
            if (catalog == null || catalog.entries == null)
            {
                return null;
            }

            return catalog.entries.Find(entry =>
                entry != null && entry.unlockType == OperatorUnlockType.CommodityPurchase);
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Assign(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) { throw new InvalidOperationException($"{propertyName} 직렬화 필드를 찾지 못했습니다."); }
            property.objectReferenceValue = value;
        }

        private static GameObject RequireSceneObject(string name)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.scene.IsValid() && candidate.scene.isLoaded && candidate.name == name)
                {
                    return candidate;
                }
            }
            throw new InvalidOperationException($"현재 씬에서 {name}을 찾지 못했습니다.");
        }

        private static GameObject RequireChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child != null ? child.gameObject : throw new InvalidOperationException($"{parent.name}/{name}을 찾지 못했습니다.");
        }

        private static GameObject RequirePath(Transform parent, string path)
        {
            Transform child = parent.Find(path);
            return child != null ? child.gameObject : throw new InvalidOperationException($"{parent.name}/{path}를 찾지 못했습니다.");
        }
    }
}
