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
    /// 사용자가 배치한 로비 배경·오퍼레이터를 보존하면서 목업 기준의 메뉴 5종을 반복 생성한다.
    /// 씬 YAML을 직접 편집하지 않고 모든 참조와 RectTransform을 Editor API로 저장한다.
    /// </summary>
    public static class CommandLobbyMenuSetup
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string LobbyName = "MainMenuBackground";
        private const string DraftLobbyName = "HomeBackground";
        private const string LegacyLobbyName = "LegacyMainMenuBackground";
        private const string MenuRootName = "CommandMenuPanels";
        private const string SpriteFolder = "Assets/Art/UI/Sprites/CommandLobbyMenu/";
        private const string KoreanFontPath = "Assets/Resource/Font/Pretendard-Bold SDF.asset";

        [MenuItem("RCCom/UI/Build Command Lobby Menu")]
        public static void Build()
        {
            Scene scene = OpenTitleSceneSafely();
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                throw new InvalidOperationException("TitleScene에 Canvas가 없습니다.");
            }

            Transform lobby = ResolveLobbyRoot(canvas.transform, out Transform legacyLobby);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null)
            {
                throw new InvalidOperationException("로비 메뉴용 TMP 글꼴을 찾지 못했습니다.");
            }

            PrepareLobbyRoot(lobby, legacyLobby);
            Transform menuRoot = RebuildMenuRoot(lobby);

            TitleSceneController titleController = UnityEngine.Object.FindFirstObjectByType<TitleSceneController>(
                FindObjectsInactive.Include);
            TitleConfigurationController configurationController =
                UnityEngine.Object.FindFirstObjectByType<TitleConfigurationController>(FindObjectsInactive.Include);
            OperatorSelectionUI selectionUI = UnityEngine.Object.FindFirstObjectByType<OperatorSelectionUI>(
                FindObjectsInactive.Include);

            if (titleController == null || configurationController == null || selectionUI == null)
            {
                throw new InvalidOperationException("기존 타이틀·설정·오퍼레이터 선택 Controller를 찾지 못했습니다.");
            }

            CreatePanel(menuRoot, font, "LiveContent", "LIVE CONTENT", "REMOTE CONTENT ACCESS",
                new Vector2(430f, 300f), new Vector2(430f, 110f), -1.5f, 34f, 13f, 62f,
                null, titleController, configurationController, selectionUI);
            CreatePanel(menuRoot, font, "Operators", "OPERATORS", "MANAGE YOUR TEAM",
                new Vector2(420f, 165f), new Vector2(560f, 140f), -2f, 43f, 15f, 78f,
                null, titleController, configurationController, selectionUI);
            CreatePanel(menuRoot, font, "Operation", "OPERATION", "DEPLOY TO BATTLEFIELD",
                new Vector2(315f, -15f), new Vector2(760f, 220f), -4.5f, 58f, 18f, 135f,
                TitleMenuTextButton.MenuAction.NewGame, titleController, configurationController, selectionUI);
            CreatePanel(menuRoot, font, "Records", "RECORDS", "BATTLE DATA ARCHIVE",
                new Vector2(365f, -220f), new Vector2(500f, 125f), -3f, 38f, 14f, 76f,
                null, titleController, configurationController, selectionUI);
            CreatePanel(menuRoot, font, "Configuration", "CONFIGURATION", "SYSTEM SETTINGS",
                new Vector2(430f, -350f), new Vector2(560f, 110f), -2.5f, 35f, 13f, 84f,
                TitleMenuTextButton.MenuAction.Preference, titleController, configurationController, selectionUI);

            RewireControllers(lobby.gameObject, titleController, configurationController, selectionUI);

            // 편집 중에는 배치를 바로 확인하고, 플레이 시에는 TitleSceneController.Awake가 다시 숨긴다.
            lobby.gameObject.SetActive(true);
            EditorUtility.SetDirty(lobby.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CommandLobbyMenuSetup] TitleScene 로비 메뉴 5종 배치·참조 연결 완료");
        }

        [MenuItem("RCCom/UI/Validate Command Lobby Menu")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            Transform lobby = canvas != null ? canvas.transform.Find(LobbyName) : null;
            Transform menuRoot = lobby != null ? lobby.Find(MenuRootName) : null;

            if (lobby == null || menuRoot == null || menuRoot.childCount != 5)
            {
                throw new InvalidOperationException("TitleScene 로비 메뉴 5종이 올바르게 생성되지 않았습니다.");
            }

            string[] names = { "LiveContent", "Operators", "Operation", "Records", "Configuration" };
            foreach (string name in names)
            {
                Transform panel = menuRoot.Find(name);
                if (panel == null || panel.GetComponent<Button>() == null ||
                    panel.GetComponent<CommandLobbyMenuItem>() == null || panel.Find("Title") == null ||
                    panel.Find("Subtitle") == null)
                {
                    throw new InvalidOperationException($"로비 메뉴 패널 구성이 올바르지 않습니다: {name}");
                }
            }

            TitleSceneController titleController = UnityEngine.Object.FindFirstObjectByType<TitleSceneController>(
                FindObjectsInactive.Include);
            var titleSerialized = new SerializedObject(titleController);
            if (titleSerialized.FindProperty("mainMenuBackground").objectReferenceValue != lobby.gameObject)
            {
                throw new InvalidOperationException("TitleSceneController가 새 로비 루트를 참조하지 않습니다.");
            }

            if (scene.path != TitleScenePath)
            {
                throw new InvalidOperationException("검증 대상 TitleScene을 열지 못했습니다.");
            }

            Debug.Log("[CommandLobbyMenuSetup] TitleScene 로비 메뉴 배치 검증 통과");
        }

        private static Transform ResolveLobbyRoot(Transform canvas, out Transform legacyLobby)
        {
            Transform draft = canvas.Find(DraftLobbyName);
            Transform current = canvas.Find(LobbyName);
            legacyLobby = canvas.Find(LegacyLobbyName);

            if (draft != null)
            {
                if (current != null)
                {
                    if (legacyLobby != null && legacyLobby != current)
                    {
                        throw new InvalidOperationException("보존할 기존 메인 메뉴 루트가 둘 이상이라 자동 변경할 수 없습니다.");
                    }

                    current.name = LegacyLobbyName;
                    legacyLobby = current;
                }

                draft.name = LobbyName;
                draft.SetSiblingIndex(Mathf.Min(2, canvas.childCount - 1));
                return draft;
            }

            if (current == null || current.Find(MenuRootName) == null)
            {
                throw new InvalidOperationException("사용자가 배치한 HomeBackground 또는 기존 CommandMenuPanels를 찾지 못했습니다.");
            }

            return current;
        }

        private static void PrepareLobbyRoot(Transform lobby, Transform legacyLobby)
        {
            Image lobbyImage = lobby.GetComponent<Image>();
            if (lobbyImage != null)
            {
                lobbyImage.raycastTarget = false;
            }

            Transform operatorImage = lobby.Find("OperatorImage");
            if (operatorImage != null && operatorImage.TryGetComponent(out Image operatorGraphic))
            {
                operatorGraphic.raycastTarget = false;
            }

            Transform manualPreview = lobby.Find("Image") ?? lobby.Find("ManualPanelPreview_Disabled");
            if (manualPreview != null && manualPreview.TryGetComponent(out Image previewImage) &&
                AssetDatabase.GetAssetPath(previewImage.sprite) == "Assets/Art/UI/Sprites/CommandLobbyPanel-Normal-v1.png")
            {
                manualPreview.name = "ManualPanelPreview_Disabled";
                manualPreview.gameObject.SetActive(false);
            }

            CanvasGroup lobbyGroup = lobby.GetComponent<CanvasGroup>();
            if (lobbyGroup == null)
            {
                lobbyGroup = lobby.gameObject.AddComponent<CanvasGroup>();
            }
            lobbyGroup.alpha = 1f;
            lobbyGroup.interactable = true;
            lobbyGroup.blocksRaycasts = true;

            if (legacyLobby == null)
            {
                return;
            }

            Transform dialogue = legacyLobby.Find("LobbyOperatorDialogueSystem");
            if (dialogue != null && lobby.Find("LobbyOperatorDialogueSystem") == null)
            {
                dialogue.SetParent(lobby, false);
            }

            legacyLobby.gameObject.SetActive(false);
            CanvasGroup legacyGroup = legacyLobby.GetComponent<CanvasGroup>();
            if (legacyGroup != null)
            {
                legacyGroup.interactable = false;
                legacyGroup.blocksRaycasts = false;
            }
        }

        private static Transform RebuildMenuRoot(Transform lobby)
        {
            Transform existing = lobby.Find(MenuRootName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            GameObject rootObject = new(MenuRootName, typeof(RectTransform));
            rootObject.transform.SetParent(lobby, false);
            RectTransform rect = (RectTransform)rootObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rootObject.transform;
        }

        private static void CreatePanel(Transform parent, TMP_FontAsset font, string name, string title,
            string subtitle, Vector2 position, Vector2 size, float rotation, float titleSize, float subtitleSize,
            float leftPadding, TitleMenuTextButton.MenuAction? action, TitleSceneController titleController,
            TitleConfigurationController configurationController, OperatorSelectionUI selectionUI)
        {
            Sprite normal = LoadSprite(name, "Normal");
            Sprite hover = LoadSprite(name, "Hover");

            GameObject panelObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button), typeof(CommandLobbyMenuItem));
            panelObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)panelObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;

            Image image = panelObject.GetComponent<Image>();
            image.sprite = normal;
            image.preserveAspect = true;
            image.raycastTarget = true;

            Button button = panelObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            TextMeshProUGUI titleText = CreateText("Title", panelObject.transform, font, title, titleSize,
                new Vector2(0f, 0.38f), new Vector2(1f, 0.88f), new Vector2(leftPadding, 0f),
                new Vector2(-112f, 0f));
            TextMeshProUGUI subtitleText = CreateText("Subtitle", panelObject.transform, font, subtitle, subtitleSize,
                new Vector2(0f, 0.13f), new Vector2(1f, 0.44f), new Vector2(leftPadding + 4f, 0f),
                new Vector2(-112f, 0f));
            titleText.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
            subtitleText.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);

            CommandLobbyMenuItem visual = panelObject.GetComponent<CommandLobbyMenuItem>();
            var visualSerialized = new SerializedObject(visual);
            visualSerialized.FindProperty("panelImage").objectReferenceValue = image;
            visualSerialized.FindProperty("normalSprite").objectReferenceValue = normal;
            visualSerialized.FindProperty("hoverSprite").objectReferenceValue = hover;
            visualSerialized.FindProperty("titleText").objectReferenceValue = titleText;
            visualSerialized.FindProperty("subtitleText").objectReferenceValue = subtitleText;
            visualSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (!action.HasValue)
            {
                return;
            }

            TitleMenuTextButton actionButton = panelObject.AddComponent<TitleMenuTextButton>();
            actionButton.Configure(action.Value, titleController);
            var actionSerialized = new SerializedObject(actionButton);
            actionSerialized.FindProperty("configurationController").objectReferenceValue = configurationController;
            actionSerialized.FindProperty("operatorSelectionUI").objectReferenceValue = selectionUI;
            actionSerialized.FindProperty("hoverScale").floatValue = 1.025f;
            actionSerialized.FindProperty("shakeAmount").floatValue = 0f;
            actionSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string content,
            float fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.text = content;
            text.color = new Color(0.035f, 0.045f, 0.06f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite LoadSprite(string name, string state)
        {
            string path = $"{SpriteFolder}{name}-{state}-v1.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException($"로비 메뉴 스프라이트를 찾지 못했습니다: {path}");
            }
            return sprite;
        }

        private static void RewireControllers(GameObject lobby, TitleSceneController titleController,
            TitleConfigurationController configurationController, OperatorSelectionUI selectionUI)
        {
            CanvasGroup lobbyGroup = lobby.GetComponent<CanvasGroup>();

            var titleSerialized = new SerializedObject(titleController);
            titleSerialized.FindProperty("mainMenuBackground").objectReferenceValue = lobby;
            titleSerialized.ApplyModifiedPropertiesWithoutUndo();

            var configurationSerialized = new SerializedObject(configurationController);
            configurationSerialized.FindProperty("mainMenuBackground").objectReferenceValue = lobby;
            configurationSerialized.ApplyModifiedPropertiesWithoutUndo();

            var selectionSerialized = new SerializedObject(selectionUI);
            selectionSerialized.FindProperty("mainMenuGroup").objectReferenceValue = lobbyGroup;
            selectionSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(titleController);
            EditorUtility.SetDirty(configurationController);
            EditorUtility.SetDirty(selectionUI);
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
    }
}
