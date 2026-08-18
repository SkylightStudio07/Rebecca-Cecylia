using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Unit;
using RCCom.Effects.Unit;
using RCCom.Effects.Unit.Concrete;
using RCCom.Managers;
using RCCom.Runtime;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 아트 없이도 아군 유닛 수직 슬라이스를 플레이할 수 있도록 임시 Definition·Roster·UI와
    /// DefenseScene 연결을 같은 경로로 만든다. 신규 오퍼레이터 데이터가 준비되면 이 생성물을
    /// 교체하는 대신, 공용 프리팹과 Controller 계약은 그대로 재사용한다.
    /// </summary>
    public static class AllyUnitVerticalSliceBuilder
    {
        private const string GeneratedLabel = "RCCom.GeneratedAllyUnitVerticalSlice";
        private const string DataFolder = "Assets/Data/Operators/cassia/AllyUnits";
        private const string EffectPath = "Assets/Data/Effects/Unit/BasicAttackEffect.asset";
        private const string RiflemanPath = DataFolder + "/TestRifleman.asset";
        private const string GuardPath = DataFolder + "/TestGuard.asset";
        private const string RosterPath = DataFolder + "/CassiaTestAllyUnitRoster.asset";
        private const string CombatSettingsPath = DataFolder + "/TestUnitCombatSettings.asset";
        private const string ButtonPrefabPath = "Assets/Data/Prefabs/UnitDeployButton.prefab";
        private const string CassiaOperatorPath = "Assets/Data/Operators/cassia/OperatorDefinition.asset";
        private const string DefenseScenePath = "Assets/Scenes/DefenseScene.unity";
        private const string ControllerName = "UnitDeployController";
        private const string MenuName = "UnitDeployMenu";

        [MenuItem("RCCom/Ally Units/Build Test Vertical Slice")]
        public static void BuildAndWire()
        {
            EnsureFolder(DataFolder);
            EnsureFolder("Assets/Data/Effects");
            EnsureFolder("Assets/Data/Effects/Unit");
            EnsureFolder("Assets/Data/Prefabs");

            BasicAttackEffect basicAttack = LoadOrCreateGenerated<BasicAttackEffect>(EffectPath);
            AllyUnitDefinition rifleman = LoadOrCreateGenerated<AllyUnitDefinition>(RiflemanPath);
            AllyUnitDefinition guard = LoadOrCreateGenerated<AllyUnitDefinition>(GuardPath);
            AllyUnitRoster roster = LoadOrCreateGenerated<AllyUnitRoster>(RosterPath);
            UnitCombatSettings combatSettings = LoadOrCreateGenerated<UnitCombatSettings>(CombatSettingsPath);

            ConfigureDefinition(
                rifleman,
                "test-rifleman",
                "전진 사수",
                25,
                40f,
                2.7f,
                6f,
                0.55f,
                3.8f,
                5f,
                12f,
                new Color(0.25f, 0.75f, 1f, 1f),
                basicAttack);
            ConfigureDefinition(
                guard,
                "test-guard",
                "방호 요원",
                55,
                110f,
                1.6f,
                9f,
                0.9f,
                1.1f,
                3f,
                8f,
                new Color(1f, 0.58f, 0.18f, 1f),
                basicAttack);

            roster.units = new List<AllyUnitDefinition> { rifleman, guard };
            ConfigureCombatSettings(combatSettings, 0.75f, 0.05f);
            ConnectCassiaRoster(roster);
            EditorUtility.SetDirty(roster);

            EditorSceneManager.OpenScene(DefenseScenePath, OpenSceneMode.Single);
            TMP_FontAsset font = FindSceneFont();
            UnitDeployButton buttonPrefab = BuildButtonPrefab(font);
            WireDefenseScene(roster, combatSettings, buttonPrefab, font);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[AllyUnitVerticalSliceBuilder] 임시 유닛 2종·지휘 포인트·DefenseScene 배선 완료");
        }

        [MenuItem("RCCom/Ally Units/Validate Test Vertical Slice")]
        public static void Validate()
        {
            AllyUnitDefinition rifleman = RequireAsset<AllyUnitDefinition>(RiflemanPath);
            AllyUnitDefinition guard = RequireAsset<AllyUnitDefinition>(GuardPath);
            AllyUnitRoster roster = RequireAsset<AllyUnitRoster>(RosterPath);
            UnitCombatSettings combatSettings = RequireAsset<UnitCombatSettings>(CombatSettingsPath);
            UnitDeployButton buttonPrefab = RequirePrefabComponent<UnitDeployButton>(ButtonPrefabPath);
            OperatorDefinition cassia = RequireAsset<OperatorDefinition>(CassiaOperatorPath);

            if (roster.units.Count != 2 || roster.units[0] != rifleman || roster.units[1] != guard ||
                cassia.allyUnitRoster != roster)
            {
                throw new InvalidOperationException("Cassia 임시 아군 Roster 연결이 올바르지 않습니다.");
            }

            ValidateDefinition(rifleman, "test-rifleman", 25, 40f, 2.7f, 6f, 0.55f, 3.8f);
            ValidateDefinition(guard, "test-guard", 55, 110f, 1.6f, 9f, 0.9f, 1.1f);
            if (!Mathf.Approximately(combatSettings.ContactRange, 0.75f) ||
                !Mathf.Approximately(combatSettings.SeparationMargin, 0.05f))
            {
                throw new InvalidOperationException("임시 유닛 교전 거리 설정이 올바르지 않습니다.");
            }

            Scene scene = EditorSceneManager.OpenScene(DefenseScenePath, OpenSceneMode.Single);
            UnitDeployController[] controllers = FindAllInScene<UnitDeployController>();
            UnitDeployMenuUI[] menus = FindAllInScene<UnitDeployMenuUI>();
            if (controllers.Length != 1 || menus.Length != 1)
            {
                throw new InvalidOperationException(
                    $"DefenseScene의 유닛 배치 Controller와 메뉴는 각각 하나여야 합니다. " +
                    $"Controller={controllers.Length}, Menu={menus.Length}");
            }

            UnitDeployController controller = controllers[0];
            UnitDeployMenuUI menu = menus[0];

            var controllerSerialized = new SerializedObject(controller);
            if (controllerSerialized.FindProperty("mapManager").objectReferenceValue == null ||
                controllerSerialized.FindProperty("waveManager").objectReferenceValue == null ||
                controllerSerialized.FindProperty("allyUnitRoster").objectReferenceValue != roster ||
                controllerSerialized.FindProperty("viewPrefab").objectReferenceValue == null ||
                controllerSerialized.FindProperty("combatSettings").objectReferenceValue != combatSettings ||
                controllerSerialized.FindProperty("startingCommandPoints").intValue != 40 ||
                controllerSerialized.FindProperty("maxCommandPoints").intValue != 100 ||
                !Mathf.Approximately(controllerSerialized.FindProperty("commandPointRecoveryPerSecond").floatValue, 4f))
            {
                throw new InvalidOperationException("DefenseScene UnitDeployController 참조 또는 지휘 포인트 초깃값이 올바르지 않습니다.");
            }

            var menuSerialized = new SerializedObject(menu);
            UnityEngine.Object serializedButtonPrefab =
                menuSerialized.FindProperty("buttonPrefab").objectReferenceValue;
            if (menuSerialized.FindProperty("deployController").objectReferenceValue != controller ||
                menuSerialized.FindProperty("panelGroup").objectReferenceValue == null ||
                menuSerialized.FindProperty("contentParent").objectReferenceValue == null ||
                serializedButtonPrefab == null || serializedButtonPrefab != buttonPrefab ||
                menuSerialized.FindProperty("deployButton").objectReferenceValue == null ||
                menuSerialized.FindProperty("commandPointsText").objectReferenceValue == null)
            {
                throw new InvalidOperationException("DefenseScene UnitDeployMenuUI 참조가 올바르지 않습니다.");
            }

            if (scene.path != DefenseScenePath)
            {
                throw new InvalidOperationException("검증 대상 DefenseScene을 열지 못했습니다.");
            }

            Debug.Log("[AllyUnitVerticalSliceBuilder] 임시 아군 수직 슬라이스 검증 통과");
        }

        private static void ConfigureDefinition(
            AllyUnitDefinition definition,
            string unitId,
            string displayName,
            int deployCost,
            float maxHealth,
            float moveSpeed,
            float attackDamage,
            float attackInterval,
            float attackRange,
            float detectionRange,
            float projectileSpeed,
            Color tint,
            AllyUnitEffectBase basicAttack)
        {
            definition.data = new AllyUnitData
            {
                unitId = unitId,
                displayName = displayName,
                deployCost = deployCost,
                maxHealth = maxHealth,
                moveSpeed = moveSpeed,
                attackDamage = attackDamage,
                attackInterval = attackInterval,
                attackRange = attackRange,
                detectionRange = detectionRange,
                projectileSpeed = projectileSpeed,
            };
            definition.effects = new List<AllyUnitEffectBase> { basicAttack };
            definition.sprite = null;
            definition.tint = tint;
            definition.spriteForwardOffsetDegrees = 0f;
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigureCombatSettings(
            UnitCombatSettings settings,
            float contactRange,
            float separationMargin)
        {
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("contactRange").floatValue = contactRange;
            serialized.FindProperty("separationMargin").floatValue = separationMargin;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void ConnectCassiaRoster(AllyUnitRoster roster)
        {
            OperatorDefinition cassia = RequireAsset<OperatorDefinition>(CassiaOperatorPath);
            cassia.allyUnitRoster = roster;
            EditorUtility.SetDirty(cassia);
        }

        private static UnitDeployButton BuildButtonPrefab(TMP_FontAsset font)
        {
            GameObject root = new GameObject(
                "UnitDeployButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement),
                typeof(UnitDeployButton));

            try
            {
                Image background = root.GetComponent<Image>();
                background.color = new Color(0.08f, 0.16f, 0.24f, 0.96f);
                Button button = root.GetComponent<Button>();
                button.targetGraphic = background;
                LayoutElement layout = root.GetComponent<LayoutElement>();
                layout.preferredHeight = 56f;

                Image icon = CreateImage("Icon", root.transform, new Color(0.3f, 0.75f, 1f, 0.85f));
                SetRect((RectTransform)icon.transform, new Vector2(10f, 6f), new Vector2(54f, 44f),
                    Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));

                TextMeshProUGUI nameText = CreateText("Name", root.transform, font, 20f, TextAlignmentOptions.Left);
                SetRect(nameText.rectTransform, new Vector2(74f, 5f), new Vector2(-76f, -2f),
                    new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));

                TextMeshProUGUI costText = CreateText("Cost", root.transform, font, 18f, TextAlignmentOptions.Right);
                SetRect(costText.rectTransform, new Vector2(-70f, 5f), new Vector2(-12f, -2f),
                    new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));

                Image selection = CreateImage("SelectionIndicator", root.transform, new Color(0.95f, 0.8f, 0.18f, 0.22f));
                Stretch((RectTransform)selection.transform, 2f);
                selection.raycastTarget = false;
                selection.gameObject.SetActive(false);

                UnitDeployButton deployButton = root.GetComponent<UnitDeployButton>();
                var serialized = new SerializedObject(deployButton);
                serialized.FindProperty("icon").objectReferenceValue = icon;
                serialized.FindProperty("nameText").objectReferenceValue = nameText;
                serialized.FindProperty("costText").objectReferenceValue = costText;
                serialized.FindProperty("button").objectReferenceValue = button;
                serialized.FindProperty("selectionIndicator").objectReferenceValue = selection.gameObject;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EnsureCanOverwriteGenerated(ButtonPrefabPath);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ButtonPrefabPath, out bool succeeded);
                if (!succeeded || prefab == null)
                {
                    throw new InvalidOperationException($"유닛 배치 버튼 프리팹 저장에 실패했습니다: {ButtonPrefabPath}");
                }

                AssetDatabase.SetLabels(prefab, new[] { GeneratedLabel });
                EditorUtility.SetDirty(prefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            // SaveAsPrefabAsset 직후 읽은 컴포넌트는 임시 루트를 파괴하는 과정에서 무효화될
            // 수 있다. 임시 객체를 먼저 정리한 다음 영속 프리팹을 새로 로드해야 씬 직렬화가
            // 안정적인 GUID/local file ID를 기록한다.
            GameObject persistedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            UnitDeployButton persistedButton =
                persistedPrefab != null ? persistedPrefab.GetComponent<UnitDeployButton>() : null;
            if (persistedButton == null || !EditorUtility.IsPersistent(persistedButton))
            {
                throw new InvalidOperationException("저장된 유닛 배치 버튼 프리팹을 다시 로드하지 못했습니다.");
            }

            return persistedButton;
        }

        private static void WireDefenseScene(
            AllyUnitRoster roster,
            UnitCombatSettings combatSettings,
            UnitDeployButton buttonPrefab,
            TMP_FontAsset font)
        {
            Scene scene = EditorSceneManager.OpenScene(DefenseScenePath, OpenSceneMode.Single);
            MapManager mapManager = FindFirstInScene<MapManager>();
            WaveManager waveManager = FindFirstInScene<WaveManager>();
            Canvas canvas = FindFirstInScene<Canvas>();
            AllyUnitView viewPrefab = RequirePrefabComponent<AllyUnitView>(AllyUnitViewPrefabBuilder.PrefabPath);
            if (mapManager == null || waveManager == null || canvas == null)
            {
                throw new InvalidOperationException("DefenseScene에서 MapManager, WaveManager 또는 Canvas를 찾지 못했습니다.");
            }

            GameObject controllerObject = FindOrCreateRootObject(ControllerName);
            UnitDeployController controller = controllerObject.GetComponent<UnitDeployController>();
            if (controller == null)
            {
                controller = controllerObject.AddComponent<UnitDeployController>();
            }

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("mapManager").objectReferenceValue = mapManager;
            controllerSerialized.FindProperty("waveManager").objectReferenceValue = waveManager;
            controllerSerialized.FindProperty("allyUnitRoster").objectReferenceValue = roster;
            controllerSerialized.FindProperty("viewPrefab").objectReferenceValue = viewPrefab;
            controllerSerialized.FindProperty("combatSettings").objectReferenceValue = combatSettings;
            controllerSerialized.FindProperty("startingCommandPoints").intValue = 40;
            controllerSerialized.FindProperty("maxCommandPoints").intValue = 100;
            controllerSerialized.FindProperty("commandPointRecoveryPerSecond").floatValue = 4f;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            UnitDeployMenuUI menu = BuildMenu(canvas.transform, controller, buttonPrefab, font);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static UnitDeployMenuUI BuildMenu(
            Transform canvasTransform,
            UnitDeployController controller,
            UnitDeployButton buttonPrefab,
            TMP_FontAsset font)
        {
            Transform existing = canvasTransform.Find(MenuName);
            GameObject root = existing != null
                ? existing.gameObject
                : new GameObject(MenuName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            if (existing == null)
            {
                root.transform.SetParent(canvasTransform, false);
            }

            Image background = root.GetComponent<Image>();
            if (background == null)
            {
                background = root.AddComponent<Image>();
            }

            background.color = new Color(0.025f, 0.055f, 0.09f, 0.94f);
            RectTransform rootRect = (RectTransform)root.transform;
            // 우측 하단 고정 패널이다. 기존의 양수 offsetMax는 기준점 바깥으로 밀려
            // 해상도에 따라 일부가 잘릴 수 있어, 같은 앵커 기준의 음수 여백으로 통일한다.
            SetRect(rootRect, new Vector2(-404f, 24f), new Vector2(-24f, 342f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            ClearChildren(root.transform);

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = root.AddComponent<CanvasGroup>();
            }

            TextMeshProUGUI title = CreateText("Title", root.transform, font, 24f, TextAlignmentOptions.Left);
            title.text = "UNIT DEPLOY";
            SetRect(title.rectTransform, new Vector2(16f, -10f), new Vector2(-16f, -38f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));

            TextMeshProUGUI commandPoints = CreateText("CommandPoints", root.transform, font, 18f, TextAlignmentOptions.Right);
            SetRect(commandPoints.rectTransform, new Vector2(16f, -10f), new Vector2(-16f, -38f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            commandPoints.color = new Color(0.4f, 0.85f, 1f, 1f);

            GameObject content = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(root.transform, false);
            RectTransform contentRect = (RectTransform)content.transform;
            SetRect(contentRect, new Vector2(14f, 92f), new Vector2(-14f, -56f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            TextMeshProUGUI rallyHint = CreateText("RallyHint", root.transform, font, 14f, TextAlignmentOptions.Center);
            rallyHint.text = "LAST RALLY POINT에서 즉시 출격";
            rallyHint.color = new Color(0.62f, 0.74f, 0.86f, 1f);
            SetRect(rallyHint.rectTransform, new Vector2(14f, 62f), new Vector2(-14f, 86f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f));

            Button deployButton = CreateTextButton("DeployButton", root.transform, "선택 유닛 출격", font);
            SetRect((RectTransform)deployButton.transform, new Vector2(14f, 12f), new Vector2(-14f, 56f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f));

            UnitDeployMenuUI menu = root.GetComponent<UnitDeployMenuUI>();
            if (menu == null)
            {
                menu = root.AddComponent<UnitDeployMenuUI>();
            }

            var serialized = new SerializedObject(menu);
            serialized.FindProperty("deployController").objectReferenceValue = controller;
            serialized.FindProperty("panelGroup").objectReferenceValue = group;
            serialized.FindProperty("contentParent").objectReferenceValue = content.transform;
            serialized.FindProperty("buttonPrefab").objectReferenceValue = buttonPrefab;
            serialized.FindProperty("deployButton").objectReferenceValue = deployButton;
            serialized.FindProperty("commandPointsText").objectReferenceValue = commandPoints;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            deployButton.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(deployButton.onClick, menu.TryDeploySelected);
            return menu;
        }

        private static void ValidateDefinition(
            AllyUnitDefinition definition,
            string id,
            int cost,
            float health,
            float speed,
            float damage,
            float interval,
            float range)
        {
            if (definition.data == null || definition.data.unitId != id || definition.data.deployCost != cost ||
                !Mathf.Approximately(definition.data.maxHealth, health) ||
                !Mathf.Approximately(definition.data.moveSpeed, speed) ||
                !Mathf.Approximately(definition.data.attackDamage, damage) ||
                !Mathf.Approximately(definition.data.attackInterval, interval) ||
                !Mathf.Approximately(definition.data.attackRange, range) ||
                definition.effects == null || definition.effects.Count != 1 || definition.effects[0] == null)
            {
                throw new InvalidOperationException($"임시 유닛 Definition 값이 올바르지 않습니다: {id}");
            }
        }

        private static TMP_FontAsset FindSceneFont()
        {
            TextMeshProUGUI text = FindFirstInScene<TextMeshProUGUI>();
            TMP_FontAsset font = text != null ? text.font : TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                throw new InvalidOperationException("DefenseScene에서 TMP 글꼴을 찾지 못했습니다.");
            }

            return font;
        }

        private static T FindFirstInScene<T>() where T : Component
        {
            return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }

        private static T[] FindAllInScene<T>() where T : Component
        {
            return UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static GameObject FindOrCreateRootObject(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            return existing != null ? existing : new GameObject(objectName);
        }

        private static T LoadOrCreateGenerated<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                EnsureGeneratedAsset(asset, path);
                return asset;
            }

            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                throw new InvalidOperationException($"다른 종류의 기존 에셋을 덮어쓸 수 없습니다: {path}");
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SetLabels(asset, new[] { GeneratedLabel });
            return asset;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"필수 에셋을 찾지 못했습니다: {path}");
            }

            return asset;
        }

        private static T RequirePrefabComponent<T>(string path) where T : Component
        {
            GameObject prefab = RequireAsset<GameObject>(path);
            T component = prefab.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException($"프리팹에 필수 컴포넌트가 없습니다: {path} ({typeof(T).Name})");
            }

            return component;
        }

        private static void EnsureCanOverwriteGenerated(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
            {
                EnsureGeneratedAsset(asset, path);
            }
        }

        private static void EnsureGeneratedAsset(UnityEngine.Object asset, string path)
        {
            foreach (string label in AssetDatabase.GetLabels(asset))
            {
                if (label == GeneratedLabel)
                {
                    return;
                }
            }

            throw new InvalidOperationException($"자동 생성물이 아닌 기존 에셋은 덮어쓸 수 없습니다: {path}");
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject created = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            created.transform.SetParent(parent, false);
            Image image = created.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string objectName,
            Transform parent,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject created = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            created.transform.SetParent(parent, false);
            TextMeshProUGUI text = created.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static Button CreateTextButton(string objectName, Transform parent, string label, TMP_FontAsset font)
        {
            Image image = CreateImage(objectName, parent, new Color(0.1f, 0.34f, 0.5f, 1f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI text = CreateText("Label", image.transform, font, 22f, TextAlignmentOptions.Center);
            text.text = label;
            Stretch(text.rectTransform);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            SetRect(rect, new Vector2(inset, inset), new Vector2(-inset, -inset), Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f));
        }
    }
}
