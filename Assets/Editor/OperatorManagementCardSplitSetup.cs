using System;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    public static class OperatorManagementCardSplitSetup
    {
        private const string CardPrefabPath = "Assets/Data/Prefabs/OperatorManagementCard.prefab";
        private const string NormalPrefabPath = "Assets/Data/Prefabs/OperatorManagementCard_Normal.prefab";
        private const string HoverPrefabPath = "Assets/Data/Prefabs/OperatorManagementCard_Hover.prefab";
        private const string LockedPrefabPath = "Assets/Data/Prefabs/OperatorManagementCard_Locked.prefab";
        private const string CardSheetPath = "Assets/Art/UI/OperatorManaging/OperatorPanels_Managing.png";

        public static OperatorManagementCardView LoadOrCreateCardPrefab(TMP_FontAsset font)
        {
            OperatorManagementCardVisual normal = LoadOrCreateVisual(NormalPrefabPath,
                LoadSprite("OperatorPanels_Managing_0"), font, "AVAILABLE");
            OperatorManagementCardVisual hover = LoadOrCreateVisual(HoverPrefabPath,
                LoadSprite("OperatorPanels_Managing_1"), font, "AVAILABLE");
            OperatorManagementCardVisual locked = LoadOrCreateVisual(LockedPrefabPath,
                LoadSprite("OperatorPanels_Managing_2"), font, "LOCKED");

            OperatorManagementCardView existing = AssetDatabase.LoadAssetAtPath<OperatorManagementCardView>(CardPrefabPath);
            if (existing != null && IsSplit(existing))
            {
                return existing;
            }

            GameObject root = new("OperatorManagementCard", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(LayoutElement), typeof(OperatorManagementCardView));
            ((RectTransform)root.transform).sizeDelta = new Vector2(210f, 560f);

            Image hitArea = root.GetComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;

            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 210f;
            layout.preferredHeight = 560f;

            OperatorManagementCardVisual normalInstance = InstantiateVisual(normal, root.transform, "NormalVisual");
            OperatorManagementCardVisual hoverInstance = InstantiateVisual(hover, root.transform, "HoverVisual");
            OperatorManagementCardVisual lockedInstance = InstantiateVisual(locked, root.transform, "LockedVisual");

            OperatorManagementCardView view = root.GetComponent<OperatorManagementCardView>();
            SerializedObject serialized = new(view);
            SetReference(serialized, "button", button);
            SetReference(serialized, "normalVisual", normalInstance);
            SetReference(serialized, "hoverVisual", hoverInstance);
            SetReference(serialized, "lockedVisual", lockedInstance);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (saved == null) { throw new InvalidOperationException("분리형 오퍼레이터 카드 프리팹 저장에 실패했습니다."); }
            return saved.GetComponent<OperatorManagementCardView>();
        }

        private static OperatorManagementCardVisual LoadOrCreateVisual(
            string path, Sprite frameSprite, TMP_FontAsset font, string defaultState)
        {
            OperatorManagementCardVisual existing = AssetDatabase.LoadAssetAtPath<OperatorManagementCardVisual>(path);
            if (existing != null)
            {
                ValidateVisual(existing, path);
                return existing;
            }

            GameObject root = new(System.IO.Path.GetFileNameWithoutExtension(path), typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(OperatorManagementCardVisual));
            ((RectTransform)root.transform).sizeDelta = new Vector2(210f, 560f);

            Image frame = root.GetComponent<Image>();
            frame.sprite = frameSprite;
            frame.preserveAspect = false;
            frame.raycastTarget = false;

            GameObject viewport = new("PortraitViewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(root.transform, false);
            SetRect((RectTransform)viewport.transform, new Vector2(0.09f, 0.24f), new Vector2(0.91f, 0.91f));

            Image portrait = CreateImage("Portrait", viewport.transform, null, Color.white,
                Vector2.zero, Vector2.one);
            portrait.preserveAspect = true;

            TextMeshProUGUI index = CreateText("Index", root.transform, font, "01", 24f,
                new Vector2(0.09f, 0.815f), new Vector2(0.34f, 0.875f));
            TextMeshProUGUI state = CreateText("State", root.transform, font, defaultState, 11f,
                new Vector2(0.09f, 0.755f), new Vector2(0.58f, 0.795f));
            TextMeshProUGUI name = CreateText("Name", root.transform, font, "OPERATOR", 22f,
                new Vector2(0.18f, 0.19f), new Vector2(0.90f, 0.255f));
            TextMeshProUGUI affinity = CreateText("Affinity", root.transform, font, "AFFINITY 000", 11f,
                new Vector2(0.18f, 0.145f), new Vector2(0.90f, 0.19f));

            name.enableAutoSizing = true;
            name.fontSizeMin = 12f;
            name.fontSizeMax = 22f;

            Image badge = CreateImage("ActiveBadge", root.transform, null, new Color(0.02f, 0.42f, 0.85f, 0.94f),
                new Vector2(0.6f, 0.88f), new Vector2(0.91f, 0.945f));
            TextMeshProUGUI badgeText = CreateText("Text", badge.transform, font, "ACTIVE", 13f,
                Vector2.zero, Vector2.one);
            badgeText.alignment = TextAlignmentOptions.Center;

            OperatorManagementCardVisual visual = root.GetComponent<OperatorManagementCardVisual>();
            SerializedObject serialized = new(visual);
            SetReference(serialized, "portraitImage", portrait);
            SetReference(serialized, "indexText", index);
            SetReference(serialized, "nameText", name);
            SetReference(serialized, "stateText", state);
            SetReference(serialized, "affinityText", affinity);
            SetReference(serialized, "activeBadge", badge.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            if (saved == null) { throw new InvalidOperationException($"Visual 프리팹 저장 실패: {path}"); }
            return saved.GetComponent<OperatorManagementCardVisual>();
        }

        private static OperatorManagementCardVisual InstantiateVisual(
            OperatorManagementCardVisual prefab, Transform parent, string instanceName)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            instance.name = instanceName;
            RectTransform rect = (RectTransform)instance.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            return instance.GetComponent<OperatorManagementCardVisual>();
        }

        private static bool IsSplit(OperatorManagementCardView view)
        {
            SerializedObject serialized = new(view);
            SerializedProperty normal = serialized.FindProperty("normalVisual");
            SerializedProperty hover = serialized.FindProperty("hoverVisual");
            SerializedProperty locked = serialized.FindProperty("lockedVisual");
            return normal != null && normal.objectReferenceValue != null &&
                hover != null && hover.objectReferenceValue != null &&
                locked != null && locked.objectReferenceValue != null;
        }

        private static void ValidateVisual(OperatorManagementCardVisual visual, string path)
        {
            SerializedObject serialized = new(visual);
            string[] names = { "portraitImage", "indexText", "nameText", "stateText", "affinityText", "activeBadge" };
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty property = serialized.FindProperty(names[i]);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException($"Visual 필수 참조 누락: {path} / {names[i]}");
                }
            }
        }

        private static Sprite LoadSprite(string name)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(CardSheetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && sprite.name == name) { return sprite; }
            }
            throw new InvalidOperationException($"카드 스프라이트를 찾지 못했습니다: {name}");
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, anchorMin, anchorMax);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string value,
            float size, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, anchorMin, anchorMax);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
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

        private static void SetReference(SerializedObject serialized, string field, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) { throw new InvalidOperationException($"직렬화 필드 누락: {field}"); }
            property.objectReferenceValue = value;
        }
    }
}
