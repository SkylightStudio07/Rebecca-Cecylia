using System;
using TMPro;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Runtime;
using RCCom.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 아티스트가 TitleScene에 잡아둔 GachaGainBackground 초안을 보존하면서
    /// 반복 가능한 방식으로 런타임 컴포넌트와 대사 패널만 배선한다.
    /// </summary>
    public static class OperatorAcquisitionSetup
    {
        private const string CatalogPath = "Assets/Data/Operators/OperatorCatalog.asset";
        private const string KoreanFontPath = "Assets/Resource/Font/Pretendard-Bold SDF.asset";

        [MenuItem("RCCom/UI/Setup Operator Acquisition UI")]
        public static void Setup()
        {
            GameObject root = FindSceneObject("GachaGainBackground");
            if (root == null)
            {
                throw new InvalidOperationException(
                    "현재 열린 씬에서 GachaGainBackground를 찾지 못했습니다. TitleScene을 연 뒤 다시 실행하세요.");
            }

            RequireRect(root);
            root.SetActive(true);
            root.transform.SetAsLastSibling();

            CanvasGroup rootGroup = GetOrAdd<CanvasGroup>(root);
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;

            Graphic rootGraphic = root.GetComponent<Graphic>();
            if (rootGraphic == null)
            {
                Image image = Undo.AddComponent<Image>(root);
                image.color = new Color(0f, 0f, 0f, 0.001f);
                rootGraphic = image;
            }

            Button advanceButton = GetOrAdd<Button>(root);
            advanceButton.transition = Selectable.Transition.None;
            advanceButton.targetGraphic = rootGraphic;
            rootGraphic.raycastTarget = true;

            RectTransform standing = RequireChildRect(root.transform, "CharacterStanding");
            Image standingImage = standing.GetComponent<Image>();
            if (standingImage == null)
            {
                standingImage = Undo.AddComponent<Image>(standing.gameObject);
            }
            standingImage.preserveAspect = true;
            GetOrAdd<CanvasGroup>(standing.gameObject);

            RectTransform newLabel = RequireChildRect(root.transform, "NEW");
            RectTransform operatorLabel = RequireChildRect(root.transform, "OPERATOR");
            RectTransform nameLabel = RequireChildRect(root.transform, "OperatorNameText");
            TMP_Text operatorNameText = nameLabel.GetComponent<TMP_Text>();
            if (operatorNameText == null)
            {
                throw new InvalidOperationException("OperatorNameText에 TMP_Text가 없습니다.");
            }
            TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (koreanFont != null)
            {
                operatorNameText.font = koreanFont;
            }

            RectTransform dialoguePanel = FindOrCreateRect(root.transform, "AcquisitionDialogue");
            dialoguePanel.anchorMin = new Vector2(0.12f, 0f);
            dialoguePanel.anchorMax = new Vector2(0.88f, 0f);
            dialoguePanel.pivot = new Vector2(0.5f, 0f);
            dialoguePanel.anchoredPosition = new Vector2(0f, 42f);
            dialoguePanel.sizeDelta = new Vector2(0f, 166f);
            Image panelImage = GetOrAdd<Image>(dialoguePanel.gameObject);
            panelImage.color = new Color(0.015f, 0.025f, 0.045f, 0.88f);
            panelImage.raycastTarget = false;
            GetOrAdd<CanvasGroup>(dialoguePanel.gameObject);

            RectTransform accent = FindOrCreateRect(dialoguePanel, "Accent");
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.anchoredPosition = Vector2.zero;
            accent.sizeDelta = new Vector2(7f, 0f);
            Image accentImage = GetOrAdd<Image>(accent.gameObject);
            accentImage.color = new Color(0f, 0.65f, 1f, 1f);
            accentImage.raycastTarget = false;

            TextMeshProUGUI speaker = CreateOrGetText(dialoguePanel, "SpeakerText");
            ConfigureTextRect(speaker.rectTransform, new Vector2(26f, -18f),
                new Vector2(-52f, 40f), new Vector2(0f, 1f), new Vector2(1f, 1f));
            speaker.fontSize = 28f;
            speaker.fontStyle = FontStyles.Bold;
            speaker.font = koreanFont != null ? koreanFont : operatorNameText.font;
            speaker.color = new Color(0.25f, 0.78f, 1f, 1f);
            speaker.alignment = TextAlignmentOptions.Left;
            speaker.text = "OPERATOR";

            TextMeshProUGUI body = CreateOrGetText(dialoguePanel, "DialogueText");
            ConfigureTextRect(body.rectTransform, new Vector2(26f, 18f),
                new Vector2(-52f, -60f), Vector2.zero, Vector2.one);
            body.fontSize = 25f;
            body.font = koreanFont != null ? koreanFont : operatorNameText.font;
            body.color = Color.white;
            body.alignment = TextAlignmentOptions.MidlineLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.text = "새로운 오퍼레이터가 합류했습니다.";

            TextMeshProUGUI hint = CreateOrGetText(dialoguePanel, "ContinueHint");
            ConfigureTextRect(hint.rectTransform, new Vector2(-18f, 10f),
                new Vector2(180f, 28f), Vector2.one, Vector2.one);
            hint.fontSize = 17f;
            hint.font = koreanFont != null ? koreanFont : operatorNameText.font;
            hint.color = new Color(0.75f, 0.82f, 0.9f, 0.9f);
            hint.alignment = TextAlignmentOptions.Right;
            hint.text = "CLICK TO CONTINUE  >";

            // 전체 화면 버튼이 입력을 받도록 장식 Graphic은 광선 검사를 끈다.
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = graphics[i] == rootGraphic;
            }

            OperatorAcquisitionUI controller = GetOrAdd<OperatorAcquisitionUI>(root);
            SerializedObject serialized = new SerializedObject(controller);
            Assign(serialized, "catalog", AssetDatabase.LoadAssetAtPath<OperatorCatalog>(CatalogPath));
            Assign(serialized, "mainMenuBackground", FindSceneObject("MainMenuBackground"));
            Assign(serialized, "rootGroup", rootGroup);
            Assign(serialized, "advanceButton", advanceButton);
            Assign(serialized, "newLabel", newLabel);
            Assign(serialized, "operatorLabel", operatorLabel);
            Assign(serialized, "operatorNameLabel", nameLabel);
            Assign(serialized, "operatorNameText", operatorNameText);
            Assign(serialized, "characterStanding", standingImage);
            Assign(serialized, "characterGroup", standing.GetComponent<CanvasGroup>());
            Assign(serialized, "dialoguePanel", dialoguePanel);
            Assign(serialized, "dialogueGroup", dialoguePanel.GetComponent<CanvasGroup>());
            Assign(serialized, "dialogueSpeakerText", speaker);
            Assign(serialized, "dialogueBodyText", body);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OperatorAcquisitionSetup] 기존 획득 화면 초안에 연출·대사 패널 배선을 완료했습니다.");
        }

        [MenuItem("RCCom/디버그/획득 연출 기록 초기화")]
        public static void ResetPresentationHistory()
        {
            var storage = new PlayerPrefsProfileStorage();
            PlayerProfile profile = storage.Load();
            profile.presentedOperatorAcquisitionIds.Clear();
            storage.Save(profile);
            Debug.Log("[OperatorAcquisitionSetup] 획득 연출 표시 이력을 초기화했습니다. 기본 오퍼레이터를 제외한 해금 캐릭터가 다음 로비 진입 때 다시 등장합니다.");
        }

        private static void ConfigureTextRect(RectTransform rect, Vector2 anchoredPosition,
            Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x < 0.5f ? 0f : 1f, anchorMin.y < 0.5f ? 0f : 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static TextMeshProUGUI CreateOrGetText(Transform parent, string name)
        {
            RectTransform rect = FindOrCreateRect(parent, name);
            TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
            }

            text.raycastTarget = false;
            return text;
        }

        private static RectTransform RequireChildRect(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null || child is not RectTransform rect)
            {
                throw new InvalidOperationException($"{parent.name}/{name} RectTransform을 찾지 못했습니다.");
            }

            return rect;
        }

        private static RectTransform FindOrCreateRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing is RectTransform existingRect)
            {
                return existingRect;
            }

            GameObject created = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            RectTransform rect = created.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform RequireRect(GameObject target)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            if (rect == null)
            {
                throw new InvalidOperationException($"{target.name}에 RectTransform이 없습니다.");
            }

            return rect;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private static void Assign(SerializedObject serialized, string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"OperatorAcquisitionUI.{propertyName} 필드를 찾지 못했습니다.");
            }

            property.objectReferenceValue = value;
        }

        private static GameObject FindSceneObject(string name)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate.scene.IsValid() && candidate.scene.isLoaded && candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
