using RCCom.Data;
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
    /// TitleScene 우측 상단의 에디터 전용 호감도 테스트 패널을 생성하고 배선한다.
    /// 씬 YAML을 직접 고치지 않고 같은 결과를 반복 생성할 수 있게 에디터 메뉴로 제공한다.
    /// </summary>
    public static class OperatorAffinityDebugPanelSetup
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string OverlayName = "OperatorAffinityDebugOverlay";
        private const string KoreanFontPath = "Assets/Resource/Font/Pretendard-Bold SDF.asset";

        [MenuItem("RCCom/Debug/Build Affinity Overlay")]
        public static void Build()
        {
            Scene scene = OpenTitleSceneSafely();
            Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                throw new System.InvalidOperationException("TitleScene에 Canvas가 없습니다.");
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null)
            {
                throw new System.InvalidOperationException("호감도 디버그 패널용 TMP 글꼴을 찾지 못했습니다.");
            }

            LobbyOperatorDialogueUI lobbyDialogueUi = Object.FindFirstObjectByType<LobbyOperatorDialogueUI>(
                FindObjectsInactive.Include);
            if (lobbyDialogueUi == null)
            {
                throw new System.InvalidOperationException("TitleScene에 LobbyOperatorDialogueUI가 없습니다.");
            }

            Transform existing = canvas.transform.Find(OverlayName);
            GameObject rootObject;
            if (existing != null)
            {
                rootObject = existing.gameObject;
                if (rootObject.GetComponent<OperatorAffinityDebugPanel>() == null)
                {
                    throw new System.InvalidOperationException("같은 이름의 오브젝트가 있어 자동 갱신할 수 없습니다.");
                }

                ClearChildren(rootObject.transform);
            }
            else
            {
                rootObject = CreateRectObject(OverlayName, canvas.transform);
            }

            RectTransform rootRect = (RectTransform)rootObject.transform;
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-24f, -24f);
            rootRect.sizeDelta = new Vector2(360f, 390f);

            Image background = rootObject.GetComponent<Image>();
            if (background == null)
            {
                background = rootObject.AddComponent<Image>();
            }

            background.color = new Color(0.01f, 0.02f, 0.035f, 0.88f);
            background.raycastTarget = true;

            CanvasGroup panelGroup = rootObject.GetComponent<CanvasGroup>();
            if (panelGroup == null)
            {
                panelGroup = rootObject.AddComponent<CanvasGroup>();
            }

            TextMeshProUGUI title = CreateLabel("Title", rootObject.transform, font,
                "AFFINITY DEBUG", 18f, Color.cyan, TextAlignmentOptions.Left,
                16f, 12f, 328f, 26f);
            TMP_InputField operatorIdInput = CreateInputField("OperatorId", rootObject.transform, font,
                "operator id", 16f, 46f, 224f, 30f);
            Button refreshButton = CreateButton("Refresh", rootObject.transform, font,
                "새로고침", 248f, 46f, 96f, 30f, new Color(0.08f, 0.15f, 0.2f, 1f));
            TextMeshProUGUI status = CreateLabel("Status", rootObject.transform, font,
                string.Empty, 13f, Color.white, TextAlignmentOptions.Left,
                16f, 84f, 328f, 70f);
            Slider slider = CreateSlider("AffinitySlider", rootObject.transform,
                16f, 162f, 328f, 22f);
            Button apply = CreateButton("Apply", rootObject.transform, font,
                "적용", 16f, 194f, 108f, 28f, new Color(0.05f, 0.22f, 0.32f, 1f));
            Button decrease = CreateButton("Decrease", rootObject.transform, font,
                "-1", 132f, 194f, 90f, 28f, new Color(0.08f, 0.08f, 0.11f, 1f));
            Button increase = CreateButton("Increase", rootObject.transform, font,
                "+1", 230f, 194f, 114f, 28f, new Color(0.08f, 0.08f, 0.11f, 1f));

            Button unfamiliar = CreateButton("SetUnfamiliar", rootObject.transform, font,
                "0 낯섦", 16f, 232f, 60f, 28f, new Color(0.08f, 0.08f, 0.11f, 1f));
            Button favorable = CreateButton("SetFavorable", rootObject.transform, font,
                "25 호감", 82f, 232f, 60f, 28f, new Color(0.08f, 0.08f, 0.11f, 1f));
            Button joy = CreateButton("SetJoy", rootObject.transform, font,
                "50 기쁨", 148f, 232f, 60f, 28f, new Color(0.08f, 0.08f, 0.11f, 1f));
            Button love = CreateButton("SetLove", rootObject.transform, font,
                "75 사랑", 214f, 232f, 60f, 28f, new Color(0.08f, 0.08f, 0.11f, 1f));
            Button ex = CreateButton("SetEx", rootObject.transform, font,
                "100 EX", 280f, 232f, 64f, 28f, new Color(0.24f, 0.13f, 0.06f, 1f));

            Button participatedReturn = CreateButton("QueueParticipatedReturn", rootObject.transform, font,
                "귀환 +5", 16f, 270f, 156f, 28f, new Color(0.05f, 0.22f, 0.32f, 1f));
            Button otherReturn = CreateButton("QueueOtherReturn", rootObject.transform, font,
                "비참전 +2", 188f, 270f, 156f, 28f, new Color(0.12f, 0.12f, 0.16f, 1f));
            Button clearReturn = CreateButton("ClearReturn", rootObject.transform, font,
                "예약 초기화", 16f, 306f, 156f, 28f, new Color(0.12f, 0.06f, 0.08f, 1f));
            Button showDialogue = CreateButton("ShowDialogue", rootObject.transform, font,
                "대사 출력", 188f, 306f, 156f, 28f, new Color(0.05f, 0.22f, 0.32f, 1f));
            TextMeshProUGUI footer = CreateLabel("Footer", rootObject.transform, font,
                "에디터 전용 · 실제 로비 클릭 경로 사용", 11f, new Color(0.55f, 0.65f, 0.7f),
                TextAlignmentOptions.Left, 16f, 350f, 328f, 22f);

            OperatorAffinityDebugPanel panel = rootObject.GetComponent<OperatorAffinityDebugPanel>();
            if (panel == null)
            {
                panel = rootObject.AddComponent<OperatorAffinityDebugPanel>();
            }

            SerializedObject serialized = new SerializedObject(panel);
            serialized.FindProperty("panelGroup").objectReferenceValue = panelGroup;
            serialized.FindProperty("operatorIdInput").objectReferenceValue = operatorIdInput;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.FindProperty("affinitySlider").objectReferenceValue = slider;
            serialized.FindProperty("applyAffinityButton").objectReferenceValue = apply;
            serialized.FindProperty("decreaseAffinityButton").objectReferenceValue = decrease;
            serialized.FindProperty("increaseAffinityButton").objectReferenceValue = increase;
            serialized.FindProperty("setUnfamiliarButton").objectReferenceValue = unfamiliar;
            serialized.FindProperty("setFavorableButton").objectReferenceValue = favorable;
            serialized.FindProperty("setJoyButton").objectReferenceValue = joy;
            serialized.FindProperty("setLoveButton").objectReferenceValue = love;
            serialized.FindProperty("setExButton").objectReferenceValue = ex;
            serialized.FindProperty("queueParticipatedReturnButton").objectReferenceValue = participatedReturn;
            serialized.FindProperty("queueOtherReturnButton").objectReferenceValue = otherReturn;
            serialized.FindProperty("clearReturnButton").objectReferenceValue = clearReturn;
            serialized.FindProperty("showDialogueButton").objectReferenceValue = showDialogue;
            serialized.FindProperty("refreshButton").objectReferenceValue = refreshButton;
            serialized.FindProperty("lobbyDialogueUi").objectReferenceValue = lobbyDialogueUi;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            rootObject.SetActive(true);
            rootObject.transform.SetAsLastSibling();
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[OperatorAffinityDebugPanelSetup] 호감도 디버그 오버레이 배선 완료");
        }

        [MenuItem("RCCom/Debug/Validate Affinity Overlay")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            OperatorAffinityDebugPanel panel = Object.FindFirstObjectByType<OperatorAffinityDebugPanel>(
                FindObjectsInactive.Include);
            if (panel == null)
            {
                throw new System.InvalidOperationException("호감도 디버그 오버레이가 없습니다.");
            }

            SerializedObject serialized = new SerializedObject(panel);
            string[] requiredProperties =
            {
                "operatorIdInput", "statusText", "affinitySlider", "applyAffinityButton",
                "queueParticipatedReturnButton", "queueOtherReturnButton", "clearReturnButton",
                "showDialogueButton", "lobbyDialogueUi",
            };
            for (int i = 0; i < requiredProperties.Length; i++)
            {
                if (serialized.FindProperty(requiredProperties[i]).objectReferenceValue == null)
                {
                    throw new System.InvalidOperationException(
                        $"호감도 디버그 오버레이 참조가 비어 있습니다: {requiredProperties[i]}");
                }
            }

            if (!panel.gameObject.activeSelf || scene.path != TitleScenePath)
            {
                throw new System.InvalidOperationException("호감도 디버그 오버레이 씬 상태가 올바르지 않습니다.");
            }

            Debug.Log("[OperatorAffinityDebugPanelSetup] 호감도 디버그 오버레이 검증 통과");
        }

        private static Scene OpenTitleSceneSafely()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == TitleScenePath)
            {
                return activeScene;
            }

            if (activeScene.isDirty)
            {
                throw new System.InvalidOperationException(
                    "저장되지 않은 다른 씬이 열려 있어 TitleScene을 열 수 없습니다.");
            }

            return EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static TextMeshProUGUI CreateLabel(string name, Transform parent, TMP_FontAsset font,
            string text, float fontSize, Color color, TextAlignmentOptions alignment,
            float x, float y, float width, float height)
        {
            GameObject created = CreateRectObject(name, parent);
            RectTransform rect = (RectTransform)created.transform;
            SetTopLeftRect(rect, x, y, width, height);
            TextMeshProUGUI label = created.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static TMP_InputField CreateInputField(string name, Transform parent, TMP_FontAsset font,
            string placeholderText, float x, float y, float width, float height)
        {
            GameObject created = CreateImageObject(name, parent, new Color(0f, 0f, 0f, 0.55f));
            SetTopLeftRect((RectTransform)created.transform, x, y, width, height);
            TMP_InputField input = created.AddComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;

            TextMeshProUGUI text = CreateLabel("Text", created.transform, font, string.Empty,
                13f, Color.white, TextAlignmentOptions.Left, 8f, 4f, width - 16f, height - 8f);
            TextMeshProUGUI placeholder = CreateLabel("Placeholder", created.transform, font,
                placeholderText, 13f, new Color(0.5f, 0.55f, 0.6f), TextAlignmentOptions.Left,
                8f, 4f, width - 16f, height - 8f);
            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static Slider CreateSlider(string name, Transform parent,
            float x, float y, float width, float height)
        {
            GameObject created = CreateImageObject(name, parent, new Color(0.05f, 0.08f, 0.1f, 1f));
            SetTopLeftRect((RectTransform)created.transform, x, y, width, height);
            Slider slider = created.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = PlayerProfile.MaxOperatorAffinity;
            slider.wholeNumbers = true;
            slider.direction = Slider.Direction.LeftToRight;

            GameObject fill = CreateImageObject("Fill", created.transform, new Color(0.05f, 0.62f, 0.95f, 1f));
            RectTransform fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            slider.fillRect = fillRect;
            slider.targetGraphic = fill.GetComponent<Image>();
            return slider;
        }

        private static Button CreateButton(string name, Transform parent, TMP_FontAsset font,
            string text, float x, float y, float width, float height, Color backgroundColor)
        {
            GameObject created = CreateImageObject(name, parent, backgroundColor);
            SetTopLeftRect((RectTransform)created.transform, x, y, width, height);
            Button button = created.AddComponent<Button>();
            Image image = created.GetComponent<Image>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.1f, 0.55f, 0.8f, 1f);
            colors.pressedColor = new Color(0.05f, 0.3f, 0.5f, 1f);
            button.colors = colors;
            CreateLabel("Text", created.transform, font, text, 11f, Color.white,
                TextAlignmentOptions.Center, 4f, 3f, width - 8f, height - 6f);
            return button;
        }

        private static GameObject CreateImageObject(string name, Transform parent, Color color)
        {
            GameObject created = CreateRectObject(name, parent);
            Image image = created.AddComponent<Image>();
            image.color = color;
            return created;
        }

        private static void SetTopLeftRect(RectTransform rect, float x, float y,
            float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
