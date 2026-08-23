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
    /// 기존 오퍼레이터 관리·스테이지 선택 화면을 재생성하지 않고 해금 전용 UGUI만 추가한다.
    /// 아트 조정 중인 기존 RuntimeContent를 보존하기 위해 이름이 일치하는 전용 자식만 갱신한다.
    /// </summary>
    public static class OperatorUnlockUISetup
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string FontPath = "Assets/Resource/Font/Pretendard-Bold SDF.asset";
        private const string OperatorCatalogPath = "Assets/Data/Operators/OperatorCatalog.asset";

        [MenuItem("RCCom/Operators/Setup Unlock UGUI")]
        public static void Setup()
        {
            Scene scene = OpenTitleSceneSafely();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            OperatorCatalog catalog = AssetDatabase.LoadAssetAtPath<OperatorCatalog>(OperatorCatalogPath);
            if (font == null || catalog == null)
            {
                throw new InvalidOperationException("해금 UI용 글꼴 또는 OperatorCatalog를 찾지 못했습니다.");
            }

            OperatorManagementUI management = UnityEngine.Object.FindFirstObjectByType<OperatorManagementUI>(
                FindObjectsInactive.Include);
            StageSelectionUI stageSelection = UnityEngine.Object.FindFirstObjectByType<StageSelectionUI>(
                FindObjectsInactive.Include);
            OperatorAcquisitionUI acquisition = UnityEngine.Object.FindFirstObjectByType<OperatorAcquisitionUI>(
                FindObjectsInactive.Include);
            if (management == null || stageSelection == null)
            {
                throw new InvalidOperationException("TitleScene의 오퍼레이터 관리 또는 스테이지 선택 UI가 없습니다.");
            }

            var managementSerialized = new SerializedObject(management);
            GameObject managementPanel = managementSerialized.FindProperty("panel").objectReferenceValue as GameObject;
            if (managementPanel == null)
            {
                throw new InvalidOperationException("OperatorManagementUI.panel 참조가 비어 있습니다.");
            }

            Transform managementParent = managementPanel.transform.Find("RuntimeContent") ??
                managementPanel.transform;
            Button purchase = GetOrCreateButton(managementParent, "PurchaseButton", font, "PURCHASE",
                new Vector2(0.72f, 0.055f), new Vector2(0.82f, 0.12f),
                new Color(0.02f, 0.34f, 0.62f, 0.97f));
            managementSerialized.FindProperty("purchaseButton").objectReferenceValue = purchase;
            managementSerialized.FindProperty("acquisitionUI").objectReferenceValue = acquisition;
            managementSerialized.ApplyModifiedPropertiesWithoutUndo();
            purchase.gameObject.SetActive(false);

            var stageSerialized = new SerializedObject(stageSelection);
            GameObject stagePanel = stageSerialized.FindProperty("panel").objectReferenceValue as GameObject;
            if (stagePanel == null)
            {
                throw new InvalidOperationException("StageSelectionUI.panel 참조가 비어 있습니다.");
            }

            GameObject rewardPanel = GetOrCreateImage(stagePanel.transform, "OperatorReward",
                new Vector2(0.66f, 0.75f), new Vector2(0.92f, 0.9f),
                new Color(0.01f, 0.035f, 0.065f, 0.94f));
            TextMeshProUGUI header = GetOrCreateText(rewardPanel.transform, "RewardHeader", font,
                "OPERATOR REWARD", 14f, new Vector2(0.36f, 0.68f), new Vector2(0.96f, 0.94f));
            header.color = new Color(0.18f, 0.72f, 1f, 1f);
            GameObject portraitObject = GetOrCreateImage(rewardPanel.transform, "RewardPortrait",
                new Vector2(0.03f, 0.08f), new Vector2(0.33f, 0.92f), Color.white);
            Image portrait = portraitObject.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            TextMeshProUGUI rewardName = GetOrCreateText(rewardPanel.transform, "RewardName", font,
                "클리어 보상\nOPERATOR", 18f, new Vector2(0.36f, 0.1f), new Vector2(0.96f, 0.65f));
            rewardName.textWrappingMode = TextWrappingModes.Normal;

            stageSerialized.FindProperty("operatorCatalog").objectReferenceValue = catalog;
            stageSerialized.FindProperty("operatorRewardPanel").objectReferenceValue = rewardPanel;
            stageSerialized.FindProperty("operatorRewardPortrait").objectReferenceValue = portrait;
            stageSerialized.FindProperty("operatorRewardNameText").objectReferenceValue = rewardName;
            stageSerialized.ApplyModifiedPropertiesWithoutUndo();
            rewardPanel.SetActive(false);

            EditorUtility.SetDirty(management);
            EditorUtility.SetDirty(stageSelection);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OperatorUnlockUISetup] 구매 버튼과 스테이지 오퍼레이터 보상 UGUI 배선 완료");
        }

        [MenuItem("RCCom/Operators/Validate Unlock UGUI")]
        public static void Validate()
        {
            OperatorManagementUI management = UnityEngine.Object.FindFirstObjectByType<OperatorManagementUI>(
                FindObjectsInactive.Include);
            StageSelectionUI stage = UnityEngine.Object.FindFirstObjectByType<StageSelectionUI>(
                FindObjectsInactive.Include);
            if (management == null || stage == null)
            {
                throw new InvalidOperationException("해금 UI 컨트롤러를 찾지 못했습니다.");
            }

            var managementSerialized = new SerializedObject(management);
            var stageSerialized = new SerializedObject(stage);
            if (managementSerialized.FindProperty("purchaseButton").objectReferenceValue == null ||
                stageSerialized.FindProperty("operatorCatalog").objectReferenceValue == null ||
                stageSerialized.FindProperty("operatorRewardPanel").objectReferenceValue == null ||
                stageSerialized.FindProperty("operatorRewardPortrait").objectReferenceValue == null ||
                stageSerialized.FindProperty("operatorRewardNameText").objectReferenceValue == null)
            {
                throw new InvalidOperationException("해금 UGUI 참조가 누락되었습니다.");
            }

            Debug.Log("[OperatorUnlockUISetup] 해금 UGUI 검증 통과");
        }

        private static Scene OpenTitleSceneSafely()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == TitleScenePath)
            {
                return active;
            }

            if (active.isDirty)
            {
                throw new InvalidOperationException("현재 씬에 저장하지 않은 변경이 있어 TitleScene을 열 수 없습니다.");
            }

            return EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        }

        private static Button GetOrCreateButton(Transform parent, string name, TMP_FontAsset font,
            string label, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject target = GetOrCreateImage(parent, name, anchorMin, anchorMax, color);
            Button button = target.GetComponent<Button>();
            if (button == null) { button = target.AddComponent<Button>(); }
            button.targetGraphic = target.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;
            GetOrCreateText(target.transform, "Label", font, label, 20f, Vector2.zero, Vector2.one)
                .alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static GameObject GetOrCreateImage(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject target = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (existing == null) { target.transform.SetParent(parent, false); }
            Image image = target.GetComponent<Image>();
            if (image == null) { image = target.AddComponent<Image>(); }
            image.color = color;
            SetRect((RectTransform)target.transform, anchorMin, anchorMax);
            return target;
        }

        private static TextMeshProUGUI GetOrCreateText(Transform parent, string name,
            TMP_FontAsset font, string content, float size, Vector2 anchorMin, Vector2 anchorMax)
        {
            Transform existing = parent.Find(name);
            GameObject target = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            if (existing == null) { target.transform.SetParent(parent, false); }
            TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
            if (text == null) { text = target.AddComponent<TextMeshProUGUI>(); }
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            SetRect((RectTransform)target.transform, anchorMin, anchorMax);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
