using System;
using System.Collections.Generic;
using TMPro;
using RCCom.Definitions.UI;
using RCCom.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 사용자가 배치한 두 이미지의 RectTransform과 Sprite는 보존하고,
    /// 씬 간 유지 캔버스·TMP·런타임 참조·Addressable 팁 에셋을 반복 가능하게 배선한다.
    /// </summary>
    public static class UILoadingTransitionSetup
    {
        private const string CanvasName = "UILoadingTransitionCanvas";
        private const string BackgroundName = "UILoadingImageBackground";
        private const string GeometryName = "UILoadingImageGeometry";
        private const string PrefabPath = "Assets/Data/Prefabs/UI/UILoadingTransitionCanvas.prefab";
        private const string TipSetPath = "Assets/Data/UI/LoadingTipSet.asset";
        private const string FontPath = "Assets/Resource/Font/Pretendard-Bold SDF.asset";
        private const string RemoteGroupName = "UI-Live-Remote";
        private const string AddressablesLabel = "ui-loading";

        [MenuItem("RCCom/UI/Setup Loading Transition")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("로딩 UI Setup은 씬과 프리팹을 저장하므로 Edit Mode에서 실행해야 합니다.");
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            bool createPrefab = prefabAsset == null;

            GameObject canvasObject = FindSceneObject(CanvasName);
            if (canvasObject == null && prefabAsset != null)
            {
                canvasObject = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                if (canvasObject == null)
                {
                    throw new InvalidOperationException($"{PrefabPath} 인스턴스를 만들지 못했습니다.");
                }
                Undo.RegisterCreatedObjectUndo(canvasObject, $"Instantiate {CanvasName}");
            }

            GameObject backgroundObject = canvasObject != null
                ? FindDirectChild(canvasObject.transform, BackgroundName)
                : FindSceneObject(BackgroundName);
            if (backgroundObject == null)
            {
                throw new InvalidOperationException(
                    $"현재 열린 씬에서 {BackgroundName}을 찾지 못했습니다. 초안이 있는 TitleScene을 연 뒤 다시 실행하세요.");
            }

            RectTransform background = RequireRect(backgroundObject);
            RectTransform geometry = RequireChildRect(background, GeometryName);

            if (canvasObject == null)
            {
                canvasObject = new GameObject(CanvasName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(canvasObject, $"Create {CanvasName}");
            }

            RectTransform canvasRect = RequireRect(canvasObject);
            Canvas canvas = GetOrAdd<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;

            CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAdd<GraphicRaycaster>(canvasObject);

            CanvasGroup canvasGroup = GetOrAdd<CanvasGroup>(canvasObject);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (background.parent != canvasRect)
            {
                Undo.SetTransformParent(background, canvasRect, "Move Loading Background To Persistent Canvas");
            }

            if (createPrefab)
            {
                background.anchorMin = Vector2.zero;
                background.anchorMax = Vector2.one;
                background.pivot = new Vector2(0.5f, 0.5f);
                background.anchoredPosition = Vector2.zero;
                background.sizeDelta = Vector2.zero;
                background.localScale = Vector3.one;
            }
            background.SetAsFirstSibling();
            backgroundObject.SetActive(true);
            RemoveChildIfExists(background, "LoadingSerialText");

            Image backgroundImage = background.GetComponent<Image>();
            if (backgroundImage == null)
            {
                throw new InvalidOperationException($"{BackgroundName}에 Image가 없습니다.");
            }
            backgroundImage.raycastTarget = true;

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            TextMeshProUGUI tipLabel = CreateOrGetText(background, "LoadingTipLabel", font);
            if (createPrefab)
            {
                SetRect(tipLabel.rectTransform, new Vector2(0.24f, 0.31f), new Vector2(0.24f, 0.31f),
                    new Vector2(0f, 0.5f), Vector2.zero, new Vector2(160f, 42f));
                tipLabel.fontSize = 24f;
                tipLabel.fontStyle = FontStyles.Bold;
                tipLabel.color = new Color(0f, 0.75f, 1f, 1f);
                tipLabel.alignment = TextAlignmentOptions.Left;
                tipLabel.text = "TIPS";
            }

            TextMeshProUGUI tip = CreateOrGetText(background, "LoadingTipText", font);
            if (createPrefab)
            {
                SetRect(tip.rectTransform, new Vector2(0.24f, 0.26f), new Vector2(0.24f, 0.26f),
                    new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1050f, 100f));
                tip.fontSize = 31f;
                tip.color = new Color(0.88f, 0.9f, 0.94f, 1f);
                tip.alignment = TextAlignmentOptions.Left;
                tip.textWrappingMode = TextWrappingModes.Normal;
                tip.text = "당신은 최고의 지휘관입니다!";
            }

            TextMeshProUGUI loadingState = CreateOrGetText(background, "LoadingStateText", font);
            if (createPrefab)
            {
                SetRect(loadingState.rectTransform, new Vector2(0.82f, 0.08f), new Vector2(0.82f, 0.08f),
                    new Vector2(1f, 0.5f), Vector2.zero, new Vector2(440f, 55f));
                loadingState.fontSize = 25f;
                loadingState.fontStyle = FontStyles.Bold;
                loadingState.color = new Color(0f, 0.72f, 1f, 0.95f);
                loadingState.alignment = TextAlignmentOptions.Right;
                loadingState.text = "LOADING...";
            }

            LoadingTipSet tipSet = GetOrCreateTipSet();
            ConfigureAddressable(tipSet);

            UILoadingTransition transition = GetOrAdd<UILoadingTransition>(canvasObject);
            SerializedObject serialized = new SerializedObject(transition);
            Assign(serialized, "canvasGroup", canvasGroup);
            Assign(serialized, "loadingBackground", background);
            Assign(serialized, "loadingGeometry", geometry);
            Assign(serialized, "tipText", tip);
            Assign(serialized, "loadingText", loadingState);
            serialized.FindProperty("loadingTipsAddress").stringValue = UILoadingTransition.LoadingTipsAddress;
            serialized.FindProperty("coverDuration").floatValue = 0.6f;
            serialized.FindProperty("revealDuration").floatValue = 0.6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (createPrefab)
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    canvasObject, PrefabPath, InteractionMode.UserAction);
                if (saved == null)
                {
                    throw new InvalidOperationException($"로딩 UI 프리팹 저장에 실패했습니다: {PrefabPath}");
                }
            }

            // 프리팹 모드에서는 곧바로 레이아웃을 볼 수 있어야 하지만, TitleScene 편집 화면은
            // 로딩 패널에 가려지면 안 된다. 원본은 표시하고 씬 인스턴스에만 숨김 오버라이드를 둔다.
            EnsureEditablePrefabPreview();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(canvasGroup);

            EditorUtility.SetDirty(canvasObject);
            EditorUtility.SetDirty(backgroundObject);
            EditorSceneManager.MarkSceneDirty(canvasObject.scene);
            EditorSceneManager.SaveScene(canvasObject.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UILoadingTransitionSetup] 로딩 UI 프리팹과 씬 인스턴스 배선을 완료했습니다: {PrefabPath}");
        }

        private static void EnsureEditablePrefabPreview()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                CanvasGroup group = contents.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    throw new InvalidOperationException($"{PrefabPath} 루트에 CanvasGroup이 없습니다.");
                }

                group.alpha = 1f;
                group.interactable = false;
                group.blocksRaycasts = false;
                Transform background = contents.transform.Find(BackgroundName);
                if (background != null)
                {
                    RemoveChildIfExists(background, "LoadingSerialText");
                }
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static LoadingTipSet GetOrCreateTipSet()
        {
            EnsureFolder("Assets/Data/UI");
            LoadingTipSet existing = AssetDatabase.LoadAssetAtPath<LoadingTipSet>(TipSetPath);
            if (existing != null)
            {
                return existing;
            }

            LoadingTipSet created = ScriptableObject.CreateInstance<LoadingTipSet>();
            created.tips = new List<string>
            {
                "당신은 최고의 지휘관입니다!",
                "오퍼레이터마다 사용할 수 있는 전술 로스터가 다릅니다.",
                "아군 유닛은 최종 랠리 포인트에서 출격합니다.",
                "타워와 아군 유닛의 역할을 조합해 방어선을 유지하십시오.",
                "새 오퍼레이터 콘텐츠는 전투 코드 변경 없이 배포할 수 있습니다.",
            };
            AssetDatabase.CreateAsset(created, TipSetPath);
            return created;
        }

        private static void ConfigureAddressable(LoadingTipSet tipSet)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings를 만들지 못했습니다.");
            }

            settings.AddLabel(AddressablesLabel, false);
            AddressableAssetGroup group = settings.FindGroup(RemoteGroupName);
            if (group == null)
            {
                group = settings.CreateGroup(RemoteGroupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            }

            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null)
            {
                bundled = group.AddSchema<BundledAssetGroupSchema>();
            }
            if (group.GetSchema<ContentUpdateGroupSchema>() == null)
            {
                group.AddSchema<ContentUpdateGroupSchema>();
            }

            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.IncludeInBuild = true;

            string path = AssetDatabase.GetAssetPath(tipSet);
            string guid = AssetDatabase.AssetPathToGUID(path);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, true);
            entry.address = UILoadingTransition.LoadingTipsAddress;
            entry.SetLabel(AddressablesLabel, true, true, false);
            EditorUtility.SetDirty(settings);
        }

        private static TextMeshProUGUI CreateOrGetText(Transform parent, string name, TMP_FontAsset font)
        {
            RectTransform rect = FindOrCreateRect(parent, name);
            TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
            }

            if (font != null)
            {
                text.font = font;
            }
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
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

        private static RectTransform RequireChildRect(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child is not RectTransform rect)
            {
                throw new InvalidOperationException($"{parent.name}/{name} RectTransform을 찾지 못했습니다.");
            }

            return rect;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(target);
        }

        private static void Assign(SerializedObject serialized, string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"UILoadingTransition.{propertyName} 필드를 찾지 못했습니다.");
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

        private static GameObject FindDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child != null ? child.gameObject : null;
        }

        private static void RemoveChildIfExists(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
