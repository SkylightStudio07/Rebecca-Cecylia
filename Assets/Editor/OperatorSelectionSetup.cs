using System;
using RCCom.Definitions.Operator;
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
    /// TitleScene에 해상도 독립적인 오퍼레이터 선택 패널을 만들고 기존 New Game 버튼을
    /// 연결한다. 재실행 시 도구 소유 오브젝트를 갱신하므로 수작업 반복이 필요 없다.
    /// </summary>
    public static class OperatorSelectionSetup
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string SystemName = "OperatorSelectionSystem";
        private const string PanelName = "OperatorSelectionPanel";
        private const string CardPrefabPath = "Assets/Data/Prefabs/OperatorSelectionCard.prefab";
        private const string RosterItemPrefabPath = "Assets/Data/Prefabs/OperatorRosterPreviewItem.prefab";
        private const string OverlaySpritePath =
            "Assets/Art/UI/Sprites/OperatorSelection/Operator-Selection-Overlay-v2.png";
        private const string DimmerSpritePath =
            "Assets/Art/UI/Sprites/OperatorSelection/Operator-Selection-Center-Dimmer.png";
        private const string GeneratedLabel = "RCCom.GeneratedOperatorSelectionUI";
        private const string KoreanFontPath = "Assets/Resource/Font/Pretendard-Bold SDF.asset";
        private const string TitleFontPath = "Assets/Resource/Font/PlayfairDisplay-ExtraBold SDF.asset";

        [MenuItem("RCCom/Operators/Build Catalog And Title Selection UI")]
        public static void BuildAll()
        {
            OperatorCatalogBuilder.BuildAll();
            BuildTitleSelectionUI();
        }

        public static void BuildTitleSelectionUI()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != TitleScenePath)
            {
                if (activeScene.isDirty)
                {
                    throw new InvalidOperationException("저장되지 않은 다른 씬이 열려 있어 TitleScene을 열 수 없습니다.");
                }

                activeScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            }

            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                throw new InvalidOperationException("TitleScene에 Canvas가 없습니다.");
            }

            OperatorCatalog catalog = AssetDatabase.LoadAssetAtPath<OperatorCatalog>(OperatorCatalogBuilder.CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException("OperatorCatalog가 없습니다.");
            }

            TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            if (koreanFont == null || titleFont == null)
            {
                throw new InvalidOperationException("선택 UI용 TMP 글꼴을 찾지 못했습니다.");
            }

            OperatorSelectionCard cardPrefab = BuildCardPrefab(koreanFont);
            OperatorRosterPreviewItem rosterItemPrefab = BuildRosterItemPrefab(koreanFont);
            Sprite overlaySprite = AssetDatabase.LoadAssetAtPath<Sprite>(OverlaySpritePath);
            if (overlaySprite == null)
            {
                throw new InvalidOperationException($"선택 UI 오버레이 스프라이트를 찾지 못했습니다: {OverlaySpritePath}");
            }

            Sprite dimmerSprite = BuildCenterDimmerSprite();

            Transform existingSystem = canvas.transform.Find(SystemName);
            GameObject systemObject = existingSystem != null
                ? existingSystem.gameObject
                : CreateRectObject(SystemName, canvas.transform);
            OperatorSelectionUI controller = systemObject.GetComponent<OperatorSelectionUI>();
            if (controller == null)
            {
                controller = systemObject.AddComponent<OperatorSelectionUI>();
            }

            RectTransform systemRect = (RectTransform)systemObject.transform;
            Stretch(systemRect);

            Transform existingPanel = canvas.transform.Find(PanelName);
            GameObject panel = existingPanel != null
                ? existingPanel.gameObject
                : CreateImageObject(PanelName, canvas.transform, Color.clear);
            RectTransform panelRect = (RectTransform)panel.transform;
            Stretch(panelRect);
            panel.transform.SetAsLastSibling();

            Image panelImage = panel.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panel.AddComponent<Image>();
            }
            panelImage.color = Color.clear;
            panelImage.raycastTarget = false;

            ClearChildren(panel.transform);

            Image dimmer = CreateImageObject("CenterDimmer", panel.transform, Color.white).GetComponent<Image>();
            Stretch(dimmer.rectTransform);
            dimmer.sprite = dimmerSprite;
            dimmer.raycastTarget = true;

            Image overlay = CreateImageObject("OverlayArtwork", panel.transform, Color.white).GetComponent<Image>();
            Stretch(overlay.rectTransform);
            overlay.sprite = overlaySprite;
            overlay.preserveAspect = true;
            overlay.raycastTarget = false;

            GameObject cardArea = CreateRectObject("CardArea", panel.transform);
            SetRect((RectTransform)cardArea.transform, new Vector2(0f, 160f), new Vector2(880f, 280f));
            GameObject cardContent = CreateRectObject("CardContent", cardArea.transform);
            Stretch((RectTransform)cardContent.transform);

            GameObject detail = CreateRectObject("Detail", panel.transform);
            SetRect((RectTransform)detail.transform, new Vector2(-62f, -174f), new Vector2(915f, 267f));
            Image portrait = CreateImageObject("Portrait", detail.transform, Color.white).GetComponent<Image>();
            SetRect(portrait.rectTransform, new Vector2(-335f, 0f), new Vector2(150f, 150f));
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            TextMeshProUGUI nameText = CreateText("Name", detail.transform, string.Empty, koreanFont, 34f,
                new Vector2(-155f, 55f), new Vector2(250f, 46f), TextAlignmentOptions.Left);
            TextMeshProUGUI descriptionText = CreateText("Description", detail.transform, string.Empty, koreanFont, 23f,
                new Vector2(145f, 30f), new Vector2(500f, 105f), TextAlignmentOptions.TopLeft);
            TextMeshProUGUI unlockText = CreateText("Unlock", detail.transform, string.Empty, koreanFont, 20f,
                new Vector2(-155f, 4f), new Vector2(250f, 32f), TextAlignmentOptions.Left);
            unlockText.color = new Color(0.5f, 0.85f, 1f, 1f);
            TextMeshProUGUI statusText = CreateText("Status", detail.transform, string.Empty, koreanFont, 18f,
                new Vector2(-155f, -38f), new Vector2(250f, 40f), TextAlignmentOptions.Left);
            statusText.color = new Color(0.75f, 0.8f, 0.88f, 1f);

            Slider slider = CreateSlider("DownloadProgress", detail.transform);
            SetRect((RectTransform)slider.transform, new Vector2(145f, -78f), new Vector2(500f, 14f));

            GameObject rosterContent = CreateRectObject("RosterContent", panel.transform);
            SetRect((RectTransform)rosterContent.transform, new Vector2(675f, -174f), new Vector2(390f, 178f));
            VerticalLayoutGroup rosterLayout = rosterContent.AddComponent<VerticalLayoutGroup>();
            rosterLayout.spacing = 8f;
            rosterLayout.childAlignment = TextAnchor.UpperCenter;
            rosterLayout.childControlWidth = true;
            rosterLayout.childControlHeight = false;
            rosterLayout.childForceExpandWidth = true;
            rosterLayout.childForceExpandHeight = false;

            Button previous = CreateInvisibleButton("PreviousButton", panel.transform,
                new Vector2(-424f, -406f), new Vector2(242f, 92f));
            Button next = CreateInvisibleButton("NextButton", panel.transform,
                new Vector2(-163f, -406f), new Vector2(242f, 92f));
            Button confirm = CreateInvisibleButton("ConfirmButton", panel.transform,
                new Vector2(116f, -406f), new Vector2(252f, 96f));
            Button back = CreateInvisibleButton("BackButton", panel.transform,
                new Vector2(393f, -406f), new Vector2(242f, 92f));

            CanvasGroup mainMenuGroup = GetOrAddCanvasGroup(canvas.transform.Find("MainMenuBackground")?.gameObject);

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.FindProperty("mainMenuGroup").objectReferenceValue = mainMenuGroup;
            serialized.FindProperty("cardContent").objectReferenceValue = cardContent.transform;
            serialized.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            serialized.FindProperty("rosterContent").objectReferenceValue = rosterContent.transform;
            serialized.FindProperty("rosterItemPrefab").objectReferenceValue = rosterItemPrefab;
            serialized.FindProperty("portraitImage").objectReferenceValue = portrait;
            serialized.FindProperty("nameText").objectReferenceValue = nameText;
            serialized.FindProperty("descriptionText").objectReferenceValue = descriptionText;
            serialized.FindProperty("unlockText").objectReferenceValue = unlockText;
            serialized.FindProperty("statusText").objectReferenceValue = statusText;
            serialized.FindProperty("downloadProgress").objectReferenceValue = slider;
            serialized.FindProperty("previousButton").objectReferenceValue = previous;
            serialized.FindProperty("nextButton").objectReferenceValue = next;
            serialized.FindProperty("confirmButton").objectReferenceValue = confirm;
            serialized.FindProperty("backButton").objectReferenceValue = back;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            previous.onClick.RemoveAllListeners();
            next.onClick.RemoveAllListeners();
            confirm.onClick.RemoveAllListeners();
            back.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(previous.onClick, controller.Previous);
            UnityEventTools.AddPersistentListener(next.onClick, controller.Next);
            UnityEventTools.AddPersistentListener(confirm.onClick, controller.Confirm);
            UnityEventTools.AddPersistentListener(back.onClick, controller.Close);

            TitleMenuTextButton newGameButton = FindNewGameButton();
            if (newGameButton == null)
            {
                throw new InvalidOperationException("TitleScene의 New Game 버튼을 찾지 못했습니다.");
            }

            var newGameSerialized = new SerializedObject(newGameButton);
            newGameSerialized.FindProperty("operatorSelectionUI").objectReferenceValue = controller;
            newGameSerialized.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(newGameButton);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[OperatorSelectionSetup] TitleScene 선택 UI 생성 및 New Game 배선 완료");
        }

        [MenuItem("RCCom/Operators/Validate Title Selection UI")]
        public static void ValidateTitleSelectionUI()
        {
            Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            OperatorSelectionUI controller = UnityEngine.Object.FindFirstObjectByType<OperatorSelectionUI>(
                FindObjectsInactive.Include);
            OperatorSelectionCard card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath)
                ?.GetComponent<OperatorSelectionCard>();
            OperatorRosterPreviewItem rosterItem = AssetDatabase.LoadAssetAtPath<GameObject>(RosterItemPrefabPath)
                ?.GetComponent<OperatorRosterPreviewItem>();
            TitleMenuTextButton newGameButton = FindNewGameButton();

            if (controller == null || card == null || rosterItem == null || newGameButton == null)
            {
                throw new InvalidOperationException("TitleScene의 오퍼레이터 선택 UI 구성 요소를 찾지 못했습니다.");
            }

            var controllerSerialized = new SerializedObject(controller);
            if (controllerSerialized.FindProperty("catalog").objectReferenceValue == null ||
                controllerSerialized.FindProperty("panel").objectReferenceValue == null ||
                controllerSerialized.FindProperty("mainMenuGroup").objectReferenceValue == null ||
                controllerSerialized.FindProperty("cardContent").objectReferenceValue == null ||
                controllerSerialized.FindProperty("cardPrefab").objectReferenceValue != card ||
                controllerSerialized.FindProperty("rosterContent").objectReferenceValue == null ||
                controllerSerialized.FindProperty("rosterItemPrefab").objectReferenceValue != rosterItem ||
                controllerSerialized.FindProperty("portraitImage").objectReferenceValue == null ||
                controllerSerialized.FindProperty("confirmButton").objectReferenceValue == null)
            {
                throw new InvalidOperationException("TitleScene 오퍼레이터 선택 UI 참조가 올바르지 않습니다.");
            }

            Transform panel = controllerSerialized.FindProperty("panel").objectReferenceValue is GameObject panelObject
                ? panelObject.transform
                : null;
            if (panel == null || panel.Find("CenterDimmer") == null || panel.Find("OverlayArtwork") == null ||
                AssetDatabase.LoadAssetAtPath<Sprite>(OverlaySpritePath) == null ||
                AssetDatabase.LoadAssetAtPath<Sprite>(DimmerSpritePath) == null)
            {
                throw new InvalidOperationException("선택 UI 오버레이 또는 중앙 딤 효과가 올바르지 않습니다.");
            }

            var newGameSerialized = new SerializedObject(newGameButton);
            if (newGameSerialized.FindProperty("operatorSelectionUI").objectReferenceValue != controller)
            {
                throw new InvalidOperationException("New Game 버튼의 오퍼레이터 선택 UI 연결이 올바르지 않습니다.");
            }

            if (scene.path != TitleScenePath)
            {
                throw new InvalidOperationException("검증 대상 TitleScene을 열지 못했습니다.");
            }

            Debug.Log("[OperatorSelectionSetup] 카드형 TitleScene 선택 UI 검증 통과");
        }

        private static OperatorSelectionCard BuildCardPrefab(TMP_FontAsset font)
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Prefabs");
            EnsureCanOverwriteGenerated(CardPrefabPath);

            GameObject root = new GameObject(
                "OperatorSelectionCard",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement),
                typeof(OperatorSelectionCard));

            try
            {
                SetRect(root.GetComponent<RectTransform>(), Vector2.zero, new Vector2(880f, 280f));
                Image background = root.GetComponent<Image>();
                background.color = Color.clear;
                Button button = root.GetComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.None;
                LayoutElement layout = root.GetComponent<LayoutElement>();
                layout.preferredWidth = 880f;
                layout.preferredHeight = 280f;

                Image portrait = CreateImageObject("Portrait", root.transform, Color.white).GetComponent<Image>();
                SetRect(portrait.rectTransform, new Vector2(-300f, 0f), new Vector2(210f, 210f));
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;

                TextMeshProUGUI type = CreateText("Type", root.transform, string.Empty, font, 16f,
                    new Vector2(75f, 78f), new Vector2(420f, 28f), TextAlignmentOptions.Left);
                type.color = new Color(0.6f, 0.77f, 0.92f, 1f);
                TextMeshProUGUI name = CreateText("Name", root.transform, string.Empty, font, 28f,
                    new Vector2(75f, 18f), new Vector2(420f, 64f), TextAlignmentOptions.Left);
                TextMeshProUGUI state = CreateText("State", root.transform, string.Empty, font, 16f,
                    new Vector2(75f, -55f), new Vector2(420f, 32f), TextAlignmentOptions.Left);

                Image selection = CreateImageObject("SelectionFrame", root.transform, Color.clear).GetComponent<Image>();
                Stretch(selection.rectTransform);
                selection.raycastTarget = false;
                selection.enabled = false;

                OperatorSelectionCard card = root.GetComponent<OperatorSelectionCard>();
                var serialized = new SerializedObject(card);
                serialized.FindProperty("button").objectReferenceValue = button;
                serialized.FindProperty("portraitImage").objectReferenceValue = portrait;
                serialized.FindProperty("selectionFrame").objectReferenceValue = selection;
                serialized.FindProperty("nameText").objectReferenceValue = name;
                serialized.FindProperty("typeText").objectReferenceValue = type;
                serialized.FindProperty("stateText").objectReferenceValue = state;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath, out bool succeeded);
                if (!succeeded || prefab == null)
                {
                    throw new InvalidOperationException("오퍼레이터 선택 카드 프리팹을 저장하지 못했습니다.");
                }

                AssetDatabase.SetLabels(prefab, new[] { GeneratedLabel });
                EditorUtility.SetDirty(prefab);
                return prefab.GetComponent<OperatorSelectionCard>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static OperatorRosterPreviewItem BuildRosterItemPrefab(TMP_FontAsset font)
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Prefabs");
            EnsureCanOverwriteGenerated(RosterItemPrefabPath);

            GameObject root = new GameObject(
                "OperatorRosterPreviewItem",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement),
                typeof(OperatorRosterPreviewItem));

            try
            {
                SetRect(root.GetComponent<RectTransform>(), Vector2.zero, new Vector2(390f, 82f));
                Image background = root.GetComponent<Image>();
                background.color = Color.clear;
                background.raycastTarget = false;

                LayoutElement layout = root.GetComponent<LayoutElement>();
                layout.preferredWidth = 390f;
                layout.preferredHeight = 82f;

                Image icon = CreateImageObject("Icon", root.transform, Color.white).GetComponent<Image>();
                SetRect(icon.rectTransform, new Vector2(-145f, 0f), new Vector2(68f, 68f));
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                TextMeshProUGUI name = CreateText("Name", root.transform, string.Empty, font, 20f,
                    new Vector2(18f, 14f), new Vector2(235f, 34f), TextAlignmentOptions.Left);
                TextMeshProUGUI cost = CreateText("Cost", root.transform, string.Empty, font, 19f,
                    new Vector2(75f, -23f), new Vector2(120f, 28f), TextAlignmentOptions.Right);
                cost.color = new Color(0.45f, 0.88f, 1f, 1f);

                OperatorRosterPreviewItem item = root.GetComponent<OperatorRosterPreviewItem>();
                var serialized = new SerializedObject(item);
                serialized.FindProperty("icon").objectReferenceValue = icon;
                serialized.FindProperty("nameText").objectReferenceValue = name;
                serialized.FindProperty("costText").objectReferenceValue = cost;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RosterItemPrefabPath, out bool succeeded);
                if (!succeeded || prefab == null)
                {
                    throw new InvalidOperationException("오퍼레이터 로스터 미리보기 프리팹을 저장하지 못했습니다.");
                }

                AssetDatabase.SetLabels(prefab, new[] { GeneratedLabel });
                EditorUtility.SetDirty(prefab);
                return prefab.GetComponent<OperatorRosterPreviewItem>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Sprite BuildCenterDimmerSprite()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Art/UI");
            EnsureFolder("Assets/Art/UI/Sprites");
            EnsureFolder("Assets/Art/UI/Sprites/OperatorSelection");
            EnsureCanOverwriteGenerated(DimmerSpritePath);

            const int width = 512;
            const int height = 288;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float normalizedY = ((float)y / (height - 1) - 0.5f) * 2f;
                for (int x = 0; x < width; x++)
                {
                    float normalizedX = ((float)x / (width - 1) - 0.5f) * 2f;
                    float distance = Mathf.Sqrt(
                        normalizedX * normalizedX / (1.1f * 1.1f) +
                        normalizedY * normalizedY / (0.85f * 0.85f));
                    float centerWeight = Mathf.Pow(1f - Mathf.Clamp01(distance), 1.25f);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(20f, 184f, centerWeight));
                    pixels[y * width + x] = new Color32(0, 4, 12, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            string absolutePath = System.IO.Path.GetFullPath(DimmerSpritePath);
            System.IO.File.WriteAllBytes(absolutePath, png);
            AssetDatabase.ImportAsset(DimmerSpritePath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(DimmerSpritePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("중앙 딤 스프라이트 임포터를 만들지 못했습니다.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(DimmerSpritePath);
            AssetDatabase.SetLabels(asset, new[] { GeneratedLabel });
            EditorUtility.SetDirty(asset);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DimmerSpritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException("중앙 딤 스프라이트를 불러오지 못했습니다.");
            }

            return sprite;
        }

        private static TitleMenuTextButton FindNewGameButton()
        {
            foreach (TitleMenuTextButton button in UnityEngine.Object.FindObjectsByType<TitleMenuTextButton>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(button);
                if (serialized.FindProperty("action").enumValueIndex == 0)
                {
                    return button;
                }
            }

            return null;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }

        private static GameObject CreateImageObject(string name, Transform parent, Color color)
        {
            var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            created.transform.SetParent(parent, false);
            Image image = created.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return created;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float size,
            Vector2 position,
            Vector2 dimensions,
            TextAlignmentOptions alignment)
        {
            var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            created.transform.SetParent(parent, false);
            SetRect((RectTransform)created.transform, position, dimensions);
            TextMeshProUGUI text = created.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            TMP_FontAsset font,
            Vector2 position,
            Vector2 dimensions)
        {
            GameObject created = CreateImageObject(name, parent, new Color(0.12f, 0.2f, 0.3f, 1f));
            SetRect((RectTransform)created.transform, position, dimensions);
            Button button = created.AddComponent<Button>();
            button.targetGraphic = created.GetComponent<Image>();
            TextMeshProUGUI text = CreateText(
                "Label", created.transform, label, font, 30f, Vector2.zero, dimensions, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return button;
        }

        private static Button CreateInvisibleButton(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 dimensions)
        {
            GameObject created = CreateImageObject(name, parent, Color.clear);
            SetRect((RectTransform)created.transform, position, dimensions);
            Image image = created.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = created.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static Slider CreateSlider(string name, Transform parent)
        {
            GameObject root = CreateImageObject(name, parent, new Color(0.1f, 0.13f, 0.18f, 1f));
            Slider slider = root.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.interactable = false;

            GameObject fillArea = CreateRectObject("FillArea", root.transform);
            Stretch((RectTransform)fillArea.transform, 3f);
            GameObject fill = CreateImageObject("Fill", fillArea.transform, new Color(0.25f, 0.75f, 1f, 1f));
            Stretch((RectTransform)fill.transform);
            slider.fillRect = (RectTransform)fill.transform;
            slider.targetGraphic = fill.GetComponent<Image>();
            slider.value = 0f;
            return slider;
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            if (target == null)
            {
                throw new InvalidOperationException("MainMenuBackground를 찾지 못했습니다.");
            }

            if (!target.TryGetComponent(out CanvasGroup group))
            {
                group = target.AddComponent<CanvasGroup>();
            }

            return group;
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
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void EnsureCanOverwriteGenerated(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                return;
            }

            foreach (string label in AssetDatabase.GetLabels(asset))
            {
                if (label == GeneratedLabel)
                {
                    return;
                }
            }

            throw new InvalidOperationException($"자동 생성물이 아닌 기존 에셋은 덮어쓸 수 없습니다: {path}");
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 dimensions)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
        }
    }
}
