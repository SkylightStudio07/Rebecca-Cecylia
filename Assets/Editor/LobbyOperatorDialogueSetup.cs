using System;
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
    /// TitleScene의 메인 로비에 오퍼레이터 클릭 영역과 대사창을 반복 생성·검증한다.
    /// 캐릭터 아트와 실제 버튼은 분리해 후속 아트 교체가 대사 배선을 건드리지 않게 한다.
    /// </summary>
    public static class LobbyOperatorDialogueSetup
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string MainMenuName = "MainMenuBackground";
        private const string SystemName = "LobbyOperatorDialogueSystem";
        private const string DialogueSetPath = "Assets/Data/Prefabs/UI/New Operator Dialogue Set.asset";
        private const string KoreanFontPath = "Assets/Resource/Font/Pretendard-Bold SDF.asset";

        [MenuItem("RCCom/UI/Build Lobby Operator Dialogue")]
        public static void Build()
        {
            Scene scene = OpenTitleSceneSafely();
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                throw new InvalidOperationException("TitleScene에 Canvas가 없습니다.");
            }

            Transform mainMenu = canvas.transform.Find(MainMenuName);
            if (mainMenu == null)
            {
                throw new InvalidOperationException("TitleScene에서 MainMenuBackground를 찾지 못했습니다.");
            }

            OperatorDialogueSet dialogueSet = AssetDatabase.LoadAssetAtPath<OperatorDialogueSet>(DialogueSetPath);
            TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (dialogueSet == null || koreanFont == null)
            {
                throw new InvalidOperationException("로비 대사 데이터 또는 TMP 글꼴을 찾지 못했습니다.");
            }

            Transform existing = mainMenu.Find(SystemName);
            GameObject systemObject;
            if (existing != null)
            {
                systemObject = existing.gameObject;
                if (systemObject.GetComponent<LobbyOperatorDialogueUI>() == null)
                {
                    throw new InvalidOperationException("같은 이름의 기존 오브젝트가 있어 자동 갱신할 수 없습니다.");
                }

                ClearChildren(systemObject.transform);
            }
            else
            {
                systemObject = CreateRectObject(SystemName, mainMenu);
            }

            RectTransform systemRect = (RectTransform)systemObject.transform;
            Stretch(systemRect);
            LobbyOperatorDialogueUI controller = systemObject.GetComponent<LobbyOperatorDialogueUI>();
            if (controller == null)
            {
                controller = systemObject.AddComponent<LobbyOperatorDialogueUI>();
            }

            Button operatorButton = CreateTransparentButton("OperatorClickTarget", systemObject.transform);
            RectTransform operatorRect = (RectTransform)operatorButton.transform;
            operatorRect.anchorMin = new Vector2(0f, 0.06f);
            operatorRect.anchorMax = new Vector2(0.47f, 0.94f);
            operatorRect.offsetMin = Vector2.zero;
            operatorRect.offsetMax = Vector2.zero;

            Button dialogueButton = CreateDialogueBubble(systemObject.transform, koreanFont, out TextMeshProUGUI dialogueText,
                out CanvasGroup dialogueGroup);

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("dialogueSet").objectReferenceValue = dialogueSet;
            serialized.FindProperty("operatorButton").objectReferenceValue = operatorButton;
            serialized.FindProperty("dialogueButton").objectReferenceValue = dialogueButton;
            serialized.FindProperty("dialogueText").objectReferenceValue = dialogueText;
            serialized.FindProperty("dialogueGroup").objectReferenceValue = dialogueGroup;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LobbyOperatorDialogueSetup] TitleScene 오퍼레이터 클릭 대사창 배선 완료");
        }

        [MenuItem("RCCom/UI/Validate Lobby Operator Dialogue")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            LobbyOperatorDialogueUI controller = UnityEngine.Object.FindFirstObjectByType<LobbyOperatorDialogueUI>(
                FindObjectsInactive.Include);
            if (controller == null)
            {
                throw new InvalidOperationException("TitleScene에 로비 오퍼레이터 대사 UI가 없습니다.");
            }

            var serialized = new SerializedObject(controller);
            if (serialized.FindProperty("dialogueSet").objectReferenceValue == null ||
                serialized.FindProperty("operatorButton").objectReferenceValue == null ||
                serialized.FindProperty("dialogueButton").objectReferenceValue == null ||
                serialized.FindProperty("dialogueText").objectReferenceValue == null ||
                serialized.FindProperty("dialogueGroup").objectReferenceValue == null)
            {
                throw new InvalidOperationException("로비 오퍼레이터 대사 UI 필드가 올바르게 연결되지 않았습니다.");
            }

            if (scene.path != TitleScenePath)
            {
                throw new InvalidOperationException("검증 대상 TitleScene을 열지 못했습니다.");
            }

            Debug.Log("[LobbyOperatorDialogueSetup] TitleScene 로비 대사 UI 검증 통과");
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
                throw new InvalidOperationException("저장되지 않은 다른 씬이 열려 있어 TitleScene을 열 수 없습니다.");
            }

            return EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        }

        private static Button CreateTransparentButton(string name, Transform parent)
        {
            GameObject target = CreateImageObject(name, parent, new Color(0f, 0f, 0f, 0f));
            Image image = target.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = target.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static Button CreateDialogueBubble(Transform parent, TMP_FontAsset font,
            out TextMeshProUGUI dialogueText, out CanvasGroup dialogueGroup)
        {
            GameObject bubble = CreateImageObject("DialogueBubble", parent, new Color(0.015f, 0.02f, 0.035f, 0.94f));
            RectTransform bubbleRect = (RectTransform)bubble.transform;
            bubbleRect.anchorMin = new Vector2(0f, 0f);
            bubbleRect.anchorMax = new Vector2(0f, 0f);
            bubbleRect.pivot = new Vector2(0f, 0f);
            bubbleRect.anchoredPosition = new Vector2(72f, 92f);
            bubbleRect.sizeDelta = new Vector2(760f, 142f);

            dialogueGroup = bubble.AddComponent<CanvasGroup>();
            Button button = bubble.AddComponent<Button>();
            button.targetGraphic = bubble.GetComponent<Image>();
            button.transition = Selectable.Transition.None;

            GameObject accent = CreateImageObject("Accent", bubble.transform, new Color(0f, 0.68f, 1f, 1f));
            RectTransform accentRect = (RectTransform)accent.transform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(7f, 0f);
            accent.GetComponent<Image>().raycastTarget = false;

            GameObject textObject = CreateRectObject("DialogueText", bubble.transform);
            RectTransform textRect = (RectTransform)textObject.transform;
            Stretch(textRect);
            textRect.offsetMin = new Vector2(34f, 20f);
            textRect.offsetMax = new Vector2(-28f, -20f);
            dialogueText = textObject.AddComponent<TextMeshProUGUI>();
            dialogueText.font = font;
            dialogueText.fontSize = 27f;
            dialogueText.color = Color.white;
            dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.raycastTarget = false;

            return button;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static GameObject CreateImageObject(string name, Transform parent, Color color)
        {
            GameObject gameObject = CreateRectObject(name, parent);
            Image image = gameObject.AddComponent<Image>();
            image.color = color;
            return gameObject;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
