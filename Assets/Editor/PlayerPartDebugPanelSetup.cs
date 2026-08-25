using RCCom.Definitions.PlayerPart;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RCCom.EditorTools
{
    public static class PlayerPartDebugPanelSetup
    {
        private const string ScenePath = "Assets/Scenes/TitleScene.unity";
        private const string RootPath = "Canvas/OperatorUpgradeDebugOverlay";

        [MenuItem("RCCom/Debug/Extend Upgrade Overlay With Player Parts")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject root = GameObject.Find(RootPath);
            if (root == null)
            {
                throw new System.InvalidOperationException($"기존 디버그 오버레이를 찾지 못했습니다: {RootPath}");
            }

            PlayerPartCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayerPartCatalog>(
                PlayerPartAssetBuilder.CatalogPath);
            if (catalog == null)
            {
                throw new System.InvalidOperationException("PlayerPartCatalog를 먼저 빌드하세요.");
            }

            Transform existing = root.transform.Find("PlayerPartLoadoutSection");
            GameObject section = existing != null ? existing.gameObject : CreateUiObject("PlayerPartLoadoutSection", root.transform);
            RectTransform sectionRect = section.GetComponent<RectTransform>();
            sectionRect.anchorMin = new Vector2(0f, 1f);
            sectionRect.anchorMax = new Vector2(0f, 1f);
            sectionRect.pivot = new Vector2(0f, 1f);
            sectionRect.anchoredPosition = new Vector2(0f, -570f);
            sectionRect.sizeDelta = new Vector2(720f, 430f);

            UnityEngine.UI.Image background = GetOrAdd<UnityEngine.UI.Image>(section);
            background.color = new Color(0.025f, 0.055f, 0.09f, 0.96f);

            foreach (Transform child in section.transform)
            {
                Object.DestroyImmediate(child.gameObject);
            }

            TextMeshProUGUI sourceText = root.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = sourceText != null ? sourceText.font : null;
            CreateText(section.transform, "PartTitle", "PLAYER PART LOADOUT  —  전투 테스트 드라이버", font,
                new Vector2(18f, -16f), new Vector2(684f, 36f), 22f, TextAlignmentOptions.Left);

            var previousButtons = new UnityEngine.UI.Button[5];
            var nextButtons = new UnityEngine.UI.Button[5];
            var valueTexts = new TextMeshProUGUI[5];
            string[] labels = { "추진기", "포탑", "바디", "드라이버", "특수 소켓" };
            for (int index = 0; index < 5; index++)
            {
                float y = -64f - index * 54f;
                CreateText(section.transform, $"SlotLabel{index}", labels[index], font,
                    new Vector2(18f, y), new Vector2(112f, 42f), 18f, TextAlignmentOptions.MidlineLeft);
                previousButtons[index] = CreateButton(section.transform, $"Previous{index}", "◀", font,
                    new Vector2(132f, y), new Vector2(48f, 42f));
                valueTexts[index] = CreateText(section.transform, $"Value{index}", string.Empty, font,
                    new Vector2(188f, y), new Vector2(452f, 42f), 16f, TextAlignmentOptions.MidlineLeft);
                nextButtons[index] = CreateButton(section.transform, $"Next{index}", "▶", font,
                    new Vector2(648f, y), new Vector2(48f, 42f));
            }

            UnityEngine.UI.Button resetButton = CreateButton(section.transform, "Reset", "COMMON 폴백으로 초기화", font,
                new Vector2(18f, -346f), new Vector2(250f, 44f));
            TextMeshProUGUI status = CreateText(section.transform, "Status", string.Empty, font,
                new Vector2(282f, -338f), new Vector2(414f, 72f), 13f, TextAlignmentOptions.TopLeft);

            PlayerPartDebugPanel panel = GetOrAdd<PlayerPartDebugPanel>(section);
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            AssignArray(serialized.FindProperty("previousButtons"), previousButtons);
            AssignArray(serialized.FindProperty("nextButtons"), nextButtons);
            AssignArray(serialized.FindProperty("valueTexts"), valueTexts);
            serialized.FindProperty("resetButton").objectReferenceValue = resetButton;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(Mathf.Max(720f, rootRect.sizeDelta.x), Mathf.Max(1000f, rootRect.sizeDelta.y));
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerPartDebugPanelSetup] 기존 Home 디버그 오버레이 아래에 파츠 테스트 드라이버를 배선했습니다.");
        }

        private static void AssignArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string value,
            TMP_FontAsset font,
            Vector2 position,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject gameObject = CreateUiObject(name, parent);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            SetTopLeft(rect, position, size);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.color = new Color(0.82f, 0.95f, 1f, 1f);
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static UnityEngine.UI.Button CreateButton(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset font,
            Vector2 position,
            Vector2 size)
        {
            GameObject gameObject = CreateUiObject(name, parent);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            SetTopLeft(rect, position, size);
            UnityEngine.UI.Image image = gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.08f, 0.23f, 0.34f, 1f);
            UnityEngine.UI.Button button = gameObject.AddComponent<UnityEngine.UI.Button>();
            CreateText(gameObject.transform, "Label", label, font, Vector2.zero, size, 15f, TextAlignmentOptions.Center);
            RectTransform labelRect = gameObject.transform.Find("Label").GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
