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
                : CreateImageObject(PanelName, canvas.transform, new Color(0.025f, 0.035f, 0.06f, 0.97f));
            RectTransform panelRect = (RectTransform)panel.transform;
            Stretch(panelRect);
            panel.transform.SetAsLastSibling();

            ClearChildren(panel.transform);

            CreateText("Header", panel.transform, "SELECT OPERATOR", titleFont, 64f,
                new Vector2(0f, 430f), new Vector2(900f, 90f), TextAlignmentOptions.Center);

            GameObject portraitFrame = CreateImageObject(
                "PortraitFrame", panel.transform, new Color(0.08f, 0.11f, 0.17f, 1f));
            SetRect((RectTransform)portraitFrame.transform, new Vector2(-430f, 20f), new Vector2(540f, 620f));
            Image portrait = CreateImageObject("Portrait", portraitFrame.transform, Color.white).GetComponent<Image>();
            Stretch((RectTransform)portrait.transform, 24f);
            portrait.preserveAspect = true;

            GameObject detail = CreateImageObject("Detail", panel.transform, new Color(0.045f, 0.065f, 0.1f, 0.96f));
            SetRect((RectTransform)detail.transform, new Vector2(300f, 45f), new Vector2(780f, 560f));

            TextMeshProUGUI nameText = CreateText("Name", detail.transform, string.Empty, koreanFont, 54f,
                new Vector2(0f, 185f), new Vector2(680f, 90f), TextAlignmentOptions.Left);
            TextMeshProUGUI descriptionText = CreateText("Description", detail.transform, string.Empty, koreanFont, 29f,
                new Vector2(0f, 45f), new Vector2(680f, 180f), TextAlignmentOptions.TopLeft);
            TextMeshProUGUI unlockText = CreateText("Unlock", detail.transform, string.Empty, koreanFont, 28f,
                new Vector2(0f, -90f), new Vector2(680f, 70f), TextAlignmentOptions.Left);
            unlockText.color = new Color(0.5f, 0.85f, 1f, 1f);
            TextMeshProUGUI statusText = CreateText("Status", detail.transform, string.Empty, koreanFont, 23f,
                new Vector2(0f, -165f), new Vector2(680f, 70f), TextAlignmentOptions.Left);
            statusText.color = new Color(0.75f, 0.8f, 0.88f, 1f);

            Slider slider = CreateSlider("DownloadProgress", detail.transform);
            SetRect((RectTransform)slider.transform, new Vector2(0f, -225f), new Vector2(680f, 24f));

            Button previous = CreateButton("PreviousButton", panel.transform, "<", koreanFont,
                new Vector2(-250f, -380f), new Vector2(150f, 72f));
            Button next = CreateButton("NextButton", panel.transform, ">", koreanFont,
                new Vector2(-70f, -380f), new Vector2(150f, 72f));
            Button confirm = CreateButton("ConfirmButton", panel.transform, "선택", koreanFont,
                new Vector2(360f, -380f), new Vector2(260f, 72f));
            Button back = CreateButton("BackButton", panel.transform, "뒤로", koreanFont,
                new Vector2(660f, -380f), new Vector2(220f, 72f));

            CanvasGroup mainMenuGroup = GetOrAddCanvasGroup(canvas.transform.Find("MainMenuBackground")?.gameObject);

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.FindProperty("mainMenuGroup").objectReferenceValue = mainMenuGroup;
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
            text.enableWordWrapping = true;
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
