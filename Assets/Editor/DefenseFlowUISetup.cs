using RCCom.Definitions.Stage;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 기존 DefenseScene 패널을 재생성하지 않고, 전투 흐름에 필요한 소형 조작 버튼만
    /// 반복 가능하게 추가한다. 사람이 조정한 결과·튜토리얼 레이아웃을 보존하려고 자식
    /// 오브젝트와 참조 필드만 다루며 기존 오브젝트의 위치나 스프라이트는 변경하지 않는다.
    /// </summary>
    public static class DefenseFlowUISetup
    {
        private const string ScenePath = "Assets/Scenes/DefenseScene.unity";
        private const string StageCatalogPath = "Assets/Data/Stages/StageCatalog.asset";

        [MenuItem("RCCom/UI/Setup Tutorial Skip And Story Next Stage")]
        public static void Setup()
        {
            Scene scene = GetOrOpenScene(out bool shouldClose);
            try
            {
                TutorialUI tutorial = FindInScene<TutorialUI>(scene);
                GameResultUI result = FindInScene<GameResultUI>(scene);
                if (tutorial == null || result == null)
                {
                    throw new System.InvalidOperationException(
                        "DefenseScene에서 TutorialUI 또는 GameResultUI를 찾지 못했습니다.");
                }

                SetupTutorialSkip(tutorial);
                SetupNextStage(result);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[DefenseFlowUISetup] 튜토리얼 SKIP 및 스토리 NEXT STAGE 배선 완료");
            }
            finally
            {
                if (shouldClose && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [MenuItem("RCCom/UI/Validate Tutorial Skip And Story Next Stage")]
        public static void Validate()
        {
            Scene scene = GetOrOpenScene(out bool shouldClose);
            try
            {
                TutorialUI tutorial = FindInScene<TutorialUI>(scene);
                GameResultUI result = FindInScene<GameResultUI>(scene);
                bool valid = tutorial != null && result != null &&
                             HasObjectReference(tutorial, "skipButton") &&
                             HasObjectReference(result, "nextStageButton") &&
                             HasObjectReference(result, "nextStageButtonText") &&
                             HasObjectReference(result, "stageCatalog");
                if (!valid)
                {
                    throw new System.InvalidOperationException(
                        "튜토리얼 SKIP 또는 스토리 NEXT STAGE의 인스펙터 참조가 비어 있습니다.");
                }

                EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                int sceneEventSystems = 0;
                for (int i = 0; i < eventSystems.Length; i++)
                {
                    if (eventSystems[i] != null && eventSystems[i].gameObject.scene == scene)
                    {
                        sceneEventSystems++;
                    }
                }

                if (sceneEventSystems != 1)
                {
                    throw new System.InvalidOperationException(
                        $"DefenseScene의 EventSystem은 정확히 1개여야 합니다. 현재 {sceneEventSystems}개입니다.");
                }

                Debug.Log("[DefenseFlowUISetup] 참조·EventSystem 검증 통과");
            }
            finally
            {
                if (shouldClose && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void SetupTutorialSkip(TutorialUI tutorial)
        {
            var serialized = new SerializedObject(tutorial);
            CanvasGroup panel = serialized.FindProperty("panelGroup").objectReferenceValue as CanvasGroup;
            TMP_Text template = serialized.FindProperty("descriptionText").objectReferenceValue as TMP_Text;
            if (panel == null)
            {
                throw new System.InvalidOperationException("TutorialUI.panelGroup이 비어 있습니다.");
            }

            Button button = CreateOrUpdateButton(
                panel.transform,
                "TutorialSkipButton",
                "SKIP  >>",
                template,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-28f, -28f),
                new Vector2(170f, 54f),
                new Color(0.01f, 0.04f, 0.07f, 0.88f),
                new Color(0.02f, 0.55f, 1f, 1f));

            serialized.FindProperty("skipButton").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tutorial);
        }

        private static void SetupNextStage(GameResultUI result)
        {
            var serialized = new SerializedObject(result);
            CanvasGroup panel = serialized.FindProperty("panelGroup").objectReferenceValue as CanvasGroup;
            TMP_Text template = serialized.FindProperty("resultTitleText").objectReferenceValue as TMP_Text;
            if (panel == null)
            {
                throw new System.InvalidOperationException("GameResultUI.panelGroup이 비어 있습니다.");
            }

            Button button = CreateOrUpdateButton(
                panel.transform,
                "NextStageButton",
                "NEXT STAGE",
                template,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -100f),
                new Vector2(558f, 64f),
                new Color(0.015f, 0.18f, 0.38f, 0.96f),
                new Color(0.02f, 0.62f, 1f, 1f));
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);

            StageCatalog catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageCatalogPath);
            if (catalog == null)
            {
                throw new System.InvalidOperationException($"StageCatalog을 찾지 못했습니다: {StageCatalogPath}");
            }

            serialized.FindProperty("nextStageButton").objectReferenceValue = button;
            serialized.FindProperty("nextStageButtonText").objectReferenceValue = label;
            serialized.FindProperty("stageCatalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(result);

            // 스토리 승리 여부를 계산하기 전에는 화면에서 보이지 않아야 한다.
            button.gameObject.SetActive(false);
        }

        private static Button CreateOrUpdateButton(
            Transform parent,
            string objectName,
            string labelText,
            TMP_Text fontTemplate,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color normalColor,
            Color highlightedColor)
        {
            Transform existing = parent.Find(objectName);
            GameObject buttonObject = existing != null ? existing.gameObject : new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(buttonObject, $"Create {objectName}");
                buttonObject.transform.SetParent(parent, false);
            }

            buttonObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;

            Image image = buttonObject.GetComponent<Image>();
            image.color = normalColor;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = highlightedColor;
            colors.pressedColor = new Color(0.02f, 0.35f, 0.68f, 1f);
            colors.selectedColor = highlightedColor;
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick = new Button.ButtonClickedEvent();

            Transform labelTransform = buttonObject.transform.Find("Label");
            GameObject labelObject = labelTransform != null ? labelTransform.gameObject : new GameObject(
                "Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            if (labelTransform == null)
            {
                labelObject.transform.SetParent(buttonObject.transform, false);
            }

            labelObject.layer = buttonObject.layer;
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 4f);
            labelRect.offsetMax = new Vector2(-10f, -4f);
            labelRect.localScale = Vector3.one;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = objectName == "TutorialSkipButton" ? 24f : 27f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.enableAutoSizing = false;
            label.raycastTarget = false;
            if (fontTemplate != null && fontTemplate.font != null)
            {
                label.font = fontTemplate.font;
            }

            EditorUtility.SetDirty(buttonObject);
            return button;
        }

        private static bool HasObjectReference(Object target, string propertyName)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);
            return property != null && property.objectReferenceValue != null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static Scene GetOrOpenScene(out bool shouldClose)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (scene.isDirty)
                {
                    throw new System.InvalidOperationException(
                        "열려 있는 DefenseScene에 저장되지 않은 변경이 있습니다. 먼저 저장한 뒤 다시 실행하세요.");
                }

                shouldClose = false;
                return scene;
            }

            shouldClose = true;
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }
    }
}
