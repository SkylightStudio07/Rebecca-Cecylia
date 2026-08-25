using System;
using System.Collections.Generic;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Stage;
using RCCom.Data;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 사용자가 배치한 StageSelectionRightPanel 배경은 유지하고, 비어 있는 정보 영역만 만든다.
    /// 재실행 시 기존 RectTransform을 다시 쓰지 않아 수동 미세 조정을 보존한다.
    /// </summary>
    public static class StageSelectionRightPanelSetup
    {
        private const string RootName = "StageSelectionSystem";
        private const string PanelName = "StageSelectionRightPanel";

        [MenuItem("RCCom/UI/Wire Stage Selection Right Panel")]
        public static void Build()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null) { throw new InvalidOperationException($"{RootName}을 찾지 못했습니다."); }
            Transform panelTransform = FindChild(root.transform, PanelName);
            if (panelTransform == null) { throw new InvalidOperationException($"{PanelName}을 찾지 못했습니다."); }

            Transform briefingSourceTransform = FindChild(root.transform, "SelectedDescription");
            TextMeshProUGUI fontSource = briefingSourceTransform != null
                ? briefingSourceTransform.GetComponent<TextMeshProUGUI>()
                : root.GetComponentInChildren<TextMeshProUGUI>(true);
            TMP_FontAsset font = fontSource != null ? fontSource.font : null;
            StageSelectionRightPanelUI rightPanel = GetOrAdd<StageSelectionRightPanelUI>(panelTransform.gameObject);

            TextMeshProUGUI title = CreateText(panelTransform, "StageTitleText",
                new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.90f), Vector2.zero, Vector2.zero,
                48f, FontStyles.Bold, TextAlignmentOptions.Left, font);
            TextMeshProUGUI level = CreateText(panelTransform, "RecommendedLevelText",
                new Vector2(0.06f, 0.79f), new Vector2(0.94f, 0.835f), Vector2.zero, Vector2.zero,
                28f, FontStyles.Normal, TextAlignmentOptions.Left, font);
            TextMeshProUGUI briefing = CreateText(panelTransform, "MissionBriefingText",
                new Vector2(0.06f, 0.53f), new Vector2(0.94f, 0.66f), Vector2.zero, Vector2.zero,
                30f, FontStyles.Normal, TextAlignmentOptions.TopLeft, font);

            StageSelectionPreviewSlot[] enemies = CreateSlots(panelTransform, "EnemyPreviewRow", 4,
                new Vector2(0.06f, 0.27f), new Vector2(0.94f, 0.40f), Vector2.zero, Vector2.zero, font);
            StageSelectionPreviewSlot[] rewards = CreateSlots(panelTransform, "RewardPreviewRow", 5,
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.17f), Vector2.zero, Vector2.zero, font);

            SerializedObject rightSerialized = new SerializedObject(rightPanel);
            rightSerialized.FindProperty("stageTitleText").objectReferenceValue = title;
            rightSerialized.FindProperty("recommendedLevelText").objectReferenceValue = level;
            rightSerialized.FindProperty("missionBriefingText").objectReferenceValue = briefing;
            AssignArray(rightSerialized.FindProperty("enemySlots"), enemies);
            AssignArray(rightSerialized.FindProperty("rewardSlots"), rewards);
            rightSerialized.FindProperty("goldSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Lobby/goldsprite.png");
            rightSerialized.FindProperty("defaultGoldReward").intValue = 100;
            rightSerialized.ApplyModifiedPropertiesWithoutUndo();

            StageSelectionUI selection = root.GetComponent<StageSelectionUI>();
            if (selection == null) { throw new InvalidOperationException("StageSelectionUI 컴포넌트가 없습니다."); }
            SerializedObject selectionSerialized = new SerializedObject(selection);
            selectionSerialized.FindProperty("rightPanel").objectReferenceValue = rightPanel;
            selectionSerialized.FindProperty("enemyCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<EnemyCatalog>("Assets/Data/Enemies/EnemyCatalog.asset");
            selectionSerialized.ApplyModifiedPropertiesWithoutUndo();

            RenderEditorPreview(root, rightPanel);

            EditorUtility.SetDirty(rightPanel);
            EditorUtility.SetDirty(selection);
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StageSelectionRightPanelSetup] 기존 배경을 보존하고 우측 정보 UI 배선을 완료했습니다.");
        }

        private static StageSelectionPreviewSlot[] CreateSlots(Transform parent, string rowName, int count,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, TMP_FontAsset font)
        {
            RectTransform row = FindOrCreateRect(parent, rowName, anchorMin, anchorMax, offsetMin, offsetMax);
            HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(row.gameObject);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var result = new StageSelectionPreviewSlot[count];
            for (int i = 0; i < count; i++)
            {
                Transform existing = row.Find($"Slot{i + 1}");
                GameObject slotObject = existing != null ? existing.gameObject : new GameObject($"Slot{i + 1}", typeof(RectTransform));
                if (existing == null)
                {
                    Undo.RegisterCreatedObjectUndo(slotObject, "Create stage preview slot");
                    slotObject.transform.SetParent(row, false);
                }

                StageSelectionPreviewSlot slot = GetOrAdd<StageSelectionPreviewSlot>(slotObject);
                Image icon = CreateImage(slotObject.transform, "Icon", new Vector2(0.1f, 0.28f), new Vector2(0.9f, 1f));
                TextMeshProUGUI name = CreateText(slotObject.transform, "Name",
                    new Vector2(0f, 0f), new Vector2(1f, 0.28f), Vector2.zero, Vector2.zero,
                    24f, FontStyles.Normal, TextAlignmentOptions.Center, font);
                TextMeshProUGUI amount = CreateText(slotObject.transform, "Amount",
                    new Vector2(0.52f, 0.68f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
                    22f, FontStyles.Bold, TextAlignmentOptions.BottomRight, font);
                SerializedObject serialized = new SerializedObject(slot);
                serialized.FindProperty("iconImage").objectReferenceValue = icon;
                serialized.FindProperty("nameText").objectReferenceValue = name;
                serialized.FindProperty("amountText").objectReferenceValue = amount;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                // 빈 Image가 Scene View에서 빨간 X로 보이지 않게 실제 데이터가 그려질 때만 켠다.
                slotObject.SetActive(false);
                result[i] = slot;
            }
            return result;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            float size, FontStyles style, TextAlignmentOptions alignment, TMP_FontAsset font)
        {
            RectTransform rect = FindOrCreateRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(rect.gameObject);
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = FindOrCreateRect(parent, name, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            Image image = GetOrAdd<Image>(rect.gameObject);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static RectTransform FindOrCreateRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                RectTransform existingRect = existing as RectTransform;
                // 이 메서드가 다루는 이름은 전부 이 도구가 만든 내부 정보 요소다. 배경 패널과
                // 사용자가 만든 형제 UI는 건드리지 않고, 해상도 독립 비율 배치만 갱신한다.
                if (existingRect != null)
                {
                    existingRect.anchorMin = anchorMin;
                    existingRect.anchorMax = anchorMax;
                    existingRect.offsetMin = offsetMin;
                    existingRect.offsetMax = offsetMax;
                }
                return existingRect;
            }
            GameObject created = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, "Create stage selection right panel UI");
            RectTransform rect = created.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(gameObject);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) { return child; }
            }
            return null;
        }

        private static void AssignArray(SerializedProperty property, IReadOnlyList<StageSelectionPreviewSlot> values)
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void RenderEditorPreview(GameObject root, StageSelectionRightPanelUI rightPanel)
        {
            StageCatalog catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageCatalogBuilder.CatalogPath);
            EnemyCatalog enemies = AssetDatabase.LoadAssetAtPath<EnemyCatalog>("Assets/Data/Enemies/EnemyCatalog.asset");
            OperatorCatalog operators = AssetDatabase.LoadAssetAtPath<OperatorCatalog>(
                "Assets/Data/Operators/OperatorCatalog.asset");
            if (catalog == null || catalog.entries == null || catalog.entries.Count == 0) { return; }

            StageCatalogEntry preview = catalog.entries[0];
            Transform selectedTitleTransform = FindChild(root.transform, "SelectedTitle");
            TextMeshProUGUI selectedTitle = selectedTitleTransform != null
                ? selectedTitleTransform.GetComponent<TextMeshProUGUI>()
                : null;
            if (selectedTitle != null && !string.IsNullOrWhiteSpace(selectedTitle.text))
            {
                StageCatalogEntry matched = catalog.entries.Find(entry => entry != null &&
                    string.Equals(entry.displayName, selectedTitle.text, StringComparison.OrdinalIgnoreCase));
                if (matched != null) { preview = matched; }
            }

            rightPanel.Render(preview, new PlayerProfile(), enemies, operators);
        }
    }
}
