using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RCCom.Data;
using RCCom.Definitions.Card;
using RCCom.Definitions.Tower;
using RCCom.Definitions.Unit;
using RCCom.UI;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 오퍼레이터 제작의 단일 진입점.
    /// JSON 레시피의 구조 데이터와 DialogueSet SO의 스프라이트 참조를 한 화면에서 편집하지만,
    /// 런타임에서 읽는 OperatorDefinition과 카탈로그는 기존 빌더가 계속 생성하도록 경계를 유지한다.
    /// </summary>
    public sealed class OperatorStudioWindow : EditorWindow
    {
        private const string RecipeFolder = "Assets/Editor/OperatorRecipes";
        private const string DialogueRoot = "Assets/Data/Operators";

        private static readonly string[] DialogueFields =
        {
            "lobbyInteraction", "lobbyReturnTogether", "lobbyReturn",
            "lobbyTouchUnfamiliar", "lobbyTouchFavorable", "lobbyTouchJoy",
            "lobbyTouchLove", "lobbyTouchEx", "gameStart", "skillUsed",
            "baseAttacked", "playerHit", "playerHitCritical", "insufficientGold",
            "slotUnavailable", "playerDied", "baseDestroyed",
        };

        private static readonly string[] DialogueLabels =
        {
            "로비 클릭", "귀환·참전", "귀환·비참전",
            "터치·낯섦", "터치·호감", "터치·기쁨", "터치·사랑", "터치·EX",
            "게임 개시", "스킬 사용", "거점 피격", "플레이어 피격",
            "플레이어 피격·위험", "골드 부족", "슬롯 부족", "플레이어 사망", "거점 파괴",
        };

        private readonly List<string> _recipePaths = new();
        private readonly List<string> _recipeLabels = new();
        private readonly bool[] _dialogueFoldouts = new bool[DialogueFields.Length];

        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;
        private int _selectedRecipeIndex = -1;
        private int _tabIndex;
        private string _selectedRecipePath;
        private OperatorAssetRecipe _recipe;
        private OperatorDialogueSet _dialogueSet;
        private SerializedObject _dialogueSerializedObject;

        private enum StudioTab
        {
            Identity,
            Loadout,
            Dialogue,
            Package,
        }

        [MenuItem("RCCom/Operators/Open Operator Studio")]
        public static void Open()
        {
            // 기존 레이아웃에 저장된 도킹 위치가 화면 밖으로 남아 있으면 메뉴가
            // 실행되어도 창이 보이지 않는다. 메인 에디터 중앙에 보조 창으로 열어
            // 첫 실행과 레이아웃 복구 상황 모두에서 접근 가능하게 한다.
            Vector2 size = new Vector2(1000f, 760f);
            // Unity 6의 일부 레이아웃에서는 GetMainWindowPosition이 현재 도킹 창의
            // 위치를 반환해 음수·화면 밖 좌표를 재사용한다. 기본 모니터의 안전 여백에서
            // 시작하면 첫 실행 후 사용자가 원하는 모니터로 자유롭게 옮길 수 있다.
            float x = 80f;
            float y = 80f;
            OperatorStudioWindow[] existingWindows =
                Resources.FindObjectsOfTypeAll<OperatorStudioWindow>();
            for (int i = 0; i < existingWindows.Length; i++)
            {
                // 이미 화면 밖 도킹 상태로 살아 있는 인스턴스는 GetWindowWithRect가
                // 기존 위치를 우선하므로 닫고 새 보조 창으로 재생성한다.
                existingWindows[i].Close();
            }

            OperatorStudioWindow window = GetWindowWithRect<OperatorStudioWindow>(
                new Rect(x, y, size.x, size.y), true, "Operator Studio", true);
            window.position = new Rect(x, y, size.x, size.y);
            window.minSize = new Vector2(760f, 540f);
            window.ShowUtility();
            window.Focus();
        }

        private void OnEnable()
        {
            RefreshRecipes(null);
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_recipe == null)
            {
                EditorGUILayout.HelpBox(
                    "오퍼레이터 레시피가 없습니다. New Operator로 제작 원본을 먼저 만드세요.",
                    MessageType.Info);
                if (GUILayout.Button("New Operator", GUILayout.Height(28)))
                {
                    CreateNewRecipe();
                }

                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawRecipeSidebar();
            DrawEditorContent();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshRecipes(_selectedRecipePath);
            }

            GUILayout.Label("RCCom / Operator Studio", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("New Operator", EditorStyles.toolbarButton, GUILayout.Width(92)))
            {
                CreateNewRecipe();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRecipeSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(215));
            GUILayout.Label("Operators", EditorStyles.boldLabel);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            for (int i = 0; i < _recipePaths.Count; i++)
            {
                GUIStyle style = i == _selectedRecipeIndex
                    ? new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold }
                    : EditorStyles.toolbarButton;
                if (GUILayout.Button(_recipeLabels[i], style, GUILayout.Height(25)))
                {
                    LoadRecipe(i);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEditorContent()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _tabIndex = GUILayout.Toolbar(
                _tabIndex,
                new[] { "Identity", "Loadout", "Dialogue", "Package" },
                EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            switch ((StudioTab)_tabIndex)
            {
                case StudioTab.Identity:
                    DrawIdentityTab();
                    break;
                case StudioTab.Loadout:
                    DrawLoadoutTab();
                    break;
                case StudioTab.Dialogue:
                    DrawDialogueTab();
                    break;
                case StudioTab.Package:
                    DrawPackageTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawIdentityTab()
        {
            GUILayout.Label("Operator Identity", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string operatorId = EditorGUILayout.TextField("Operator ID", _recipe.operatorId);
            if (EditorGUI.EndChangeCheck())
            {
                _recipe.operatorId = operatorId.Trim().ToLowerInvariant();
            }

            EditorGUILayout.HelpBox(
                "ID는 세이브와 Addressables 주소에 사용되는 영구 식별자입니다. 생성된 Definition이 있는 뒤에는 변경하지 않는 것을 권장합니다.",
                MessageType.None);
            _recipe.displayName = EditorGUILayout.TextField("Display Name", _recipe.displayName);
            _recipe.playStyleDescription = EditorGUILayout.TextArea(
                _recipe.playStyleDescription ?? string.Empty,
                GUILayout.MinHeight(48));
            _recipe.selectionPortraitPath = DrawAssetPathField<Sprite>(
                "Selection Portrait", _recipe.selectionPortraitPath);
            _recipe.managementPortraitPath = DrawAssetPathField<Sprite>(
                "Management Card Portrait", _recipe.managementPortraitPath);
            EditorGUILayout.HelpBox(
                "Selection Portrait는 선택 화면용 머리 크롭, Management Card Portrait는 Operators 관리 카드용 전신·반신 이미지입니다.",
                MessageType.None);
            _recipe.remoteContent = EditorGUILayout.ToggleLeft("Remote Content", _recipe.remoteContent);
            _recipe.requiredBestWave = Mathf.Max(
                0,
                EditorGUILayout.IntField("Required Best Wave", _recipe.requiredBestWave));

            GUILayout.Space(12);
            DrawSaveButton();
        }

        private void DrawLoadoutTab()
        {
            GUILayout.Label("Player Data", EditorStyles.boldLabel);
            if (_recipe.playerData == null)
            {
                _recipe.playerData = new PlayerData();
            }

            PlayerData data = _recipe.playerData;
            data.maxHealth = EditorGUILayout.FloatField("Max Health", data.maxHealth);
            data.moveSpeed = EditorGUILayout.FloatField("Move Speed", data.moveSpeed);
            data.hitInvulnerabilityDuration = EditorGUILayout.FloatField(
                "Hit Invulnerability", data.hitInvulnerabilityDuration);
            data.attackDamage = EditorGUILayout.FloatField("Attack Damage", data.attackDamage);
            data.attackRange = EditorGUILayout.FloatField("Attack Range", data.attackRange);
            data.attackInterval = EditorGUILayout.FloatField("Attack Interval", data.attackInterval);
            data.projectileSpeed = EditorGUILayout.FloatField("Projectile Speed", data.projectileSpeed);
            data.skillCooldown = EditorGUILayout.FloatField("Skill Cooldown", data.skillCooldown);
            data.skillRange = EditorGUILayout.FloatField("Skill Range", data.skillRange);
            data.skillDamage = EditorGUILayout.FloatField("Skill Damage", data.skillDamage);

            GUILayout.Space(12);
            GUILayout.Label("Content Pools", EditorStyles.boldLabel);
            _recipe.sourceTowerRosterPath = DrawAssetPathField<TowerRoster>(
                "Source Tower Roster", _recipe.sourceTowerRosterPath);
            _recipe.sourceCardRosterPath = DrawAssetPathField<CardRoster>(
                "Source Card Roster", _recipe.sourceCardRosterPath);
            _recipe.sourceAllyUnitRosterPath = DrawAssetPathField<AllyUnitRoster>(
                "Source Ally Unit Roster", _recipe.sourceAllyUnitRosterPath);

            EditorGUILayout.HelpBox(
                "이 탭에서 지정한 Roster는 원본 풀입니다. 오퍼레이터별 복제본은 Build 시 자동 생성되며, 생성물은 직접 편집하지 않습니다.",
                MessageType.None);
            GUILayout.Space(8);
            DrawSaveButton();
        }

        private void DrawDialogueTab()
        {
            GUILayout.Label("Dialogue & Portraits", EditorStyles.boldLabel);
            if (_dialogueSet == null || _dialogueSerializedObject == null)
            {
                EditorGUILayout.HelpBox(
                    "dialogueSetPath가 비어 있거나 OperatorDialogueSet을 찾지 못했습니다. Identity 탭에서 새 대화 에셋을 연결하세요.",
                    MessageType.Error);
                return;
            }

            _dialogueSerializedObject.Update();
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(AssetDatabase.GetAssetPath(_dialogueSet));
            if (GUILayout.Button("Migrate All Legacy Lines", GUILayout.Width(155)))
            {
                MigrateAllLegacyLines();
                _dialogueSerializedObject.Update();
            }

            EditorGUILayout.EndHorizontal();
            SerializedProperty lobbyIdleSprite = _dialogueSerializedObject.FindProperty("lobbyIdleSprite");
            SerializedProperty idleSprite = _dialogueSerializedObject.FindProperty("idleSprite");
            EditorGUILayout.PropertyField(lobbyIdleSprite, new GUIContent("Lobby Idle Sprite"));
            EditorGUILayout.PropertyField(idleSprite, new GUIContent("Combat Idle Portrait"));

            for (int i = 0; i < DialogueFields.Length; i++)
            {
                SerializedProperty lineSet = _dialogueSerializedObject.FindProperty(DialogueFields[i]);
                if (lineSet == null)
                {
                    continue;
                }

                SerializedProperty entries = lineSet.FindPropertyRelative("entries");
                int legacyCount = lineSet.FindPropertyRelative("lines")?.arraySize ?? 0;
                string title = $"{DialogueLabels[i]}  ({(entries == null ? 0 : entries.arraySize)} lines)";
                if (legacyCount > 0 && (entries == null || entries.arraySize == 0))
                {
                    title += $"  [legacy {legacyCount}]";
                }

                _dialogueFoldouts[i] = EditorGUILayout.Foldout(_dialogueFoldouts[i], title, true);
                if (_dialogueFoldouts[i])
                {
                    DrawLineSet(lineSet, i);
                }
            }

            if (_dialogueSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_dialogueSet);
            }

            GUILayout.Space(8);
            DrawSaveButton();
        }

        private void DrawLineSet(SerializedProperty lineSet, int slotIndex)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool isLobbySlot = slotIndex <= 7;
            SerializedProperty slotSprite = lineSet.FindPropertyRelative(
                isLobbySlot ? "defaultLobbySprite" : "portraitSprite");
            EditorGUILayout.PropertyField(slotSprite, new GUIContent(
                isLobbySlot ? "Default Lobby Sprite" : "Situation Portrait"));

            SerializedProperty entries = lineSet.FindPropertyRelative("entries");
            if (entries == null)
            {
                EditorGUILayout.HelpBox("entries 필드를 찾지 못했습니다. 스크립트 리컴파일 후 다시 여세요.", MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            SerializedProperty legacyLines = lineSet.FindPropertyRelative("lines");
            if (entries.arraySize == 0 && legacyLines != null && legacyLines.arraySize > 0 &&
                GUILayout.Button("Migrate This Slot"))
            {
                MigrateLegacySlot(slotIndex);
                entries = lineSet.FindPropertyRelative("entries");
            }

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.textArea);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Line {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("Duplicate", GUILayout.Width(68)))
                {
                    entries.InsertArrayElementAtIndex(i);
                    break;
                }

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    entries.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
                if (isLobbySlot)
                {
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("lobbySprite"),
                        new GUIContent("Lobby Sprite"));
                }
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("text"), new GUIContent("Text"));
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Dialogue Line"))
            {
                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty newEntry = entries.GetArrayElementAtIndex(index);
                newEntry.FindPropertyRelative("text").stringValue = string.Empty;
                if (isLobbySlot)
                {
                    newEntry.FindPropertyRelative("lobbySprite").objectReferenceValue =
                        slotSprite.objectReferenceValue;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPackageTab()
        {
            GUILayout.Label("Save, Validate & Package", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Save Recipe는 제작 원본만 저장합니다. Build Operator Assets는 Definition·Roster 복제본·카탈로그·오퍼레이터별 Addressables 그룹까지 갱신합니다.",
                MessageType.Info);
            EditorGUILayout.LabelField("Recipe", _selectedRecipePath ?? string.Empty);
            EditorGUILayout.LabelField("Dialogue Set", _recipe.dialogueSetPath ?? string.Empty);
            EditorGUILayout.LabelField("Address", string.IsNullOrWhiteSpace(_recipe.operatorId)
                ? string.Empty
                : $"operator/{_recipe.operatorId}");
            EditorGUILayout.LabelField("Group", string.IsNullOrWhiteSpace(_recipe.operatorId)
                ? string.Empty
                : OperatorCatalogBuilder.GetGroupName(_recipe.operatorId, _recipe.remoteContent));

            GUILayout.Space(12);
            if (GUILayout.Button("Save Recipe & Dialogue", GUILayout.Height(30)))
            {
                SaveCurrentAssets();
            }

            if (GUILayout.Button("Validate All Operators", GUILayout.Height(26)))
            {
                ValidateAll();
            }

            if (GUILayout.Button("Build All Operator Assets + Addressables", GUILayout.Height(30)))
            {
                SaveCurrentAssets();
                BuildAll();
            }
        }

        private void DrawSaveButton()
        {
            GUILayout.Space(8);
            if (GUILayout.Button("Save Recipe & Dialogue", GUILayout.Height(28)))
            {
                SaveCurrentAssets();
            }
        }

        private void RefreshRecipes(string preferredPath)
        {
            _recipePaths.Clear();
            _recipeLabels.Clear();
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { RecipeFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _recipePaths.Add(path);
            }

            _recipePaths.Sort(StringComparer.Ordinal);
            for (int i = 0; i < _recipePaths.Count; i++)
            {
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(_recipePaths[i]);
                OperatorAssetRecipe recipe = asset == null ? null : JsonUtility.FromJson<OperatorAssetRecipe>(asset.text);
                _recipeLabels.Add(recipe == null || string.IsNullOrWhiteSpace(recipe.displayName)
                    ? Path.GetFileNameWithoutExtension(_recipePaths[i])
                    : recipe.displayName);
            }

            if (_recipePaths.Count == 0)
            {
                _selectedRecipeIndex = -1;
                _selectedRecipePath = null;
                _recipe = null;
                _dialogueSet = null;
                _dialogueSerializedObject = null;
                return;
            }

            int preferredIndex = string.IsNullOrWhiteSpace(preferredPath)
                ? Mathf.Clamp(_selectedRecipeIndex, 0, _recipePaths.Count - 1)
                : _recipePaths.IndexOf(preferredPath);
            LoadRecipe(Mathf.Max(0, preferredIndex));
        }

        private void LoadRecipe(int index)
        {
            if (index < 0 || index >= _recipePaths.Count)
            {
                return;
            }

            _selectedRecipeIndex = index;
            _selectedRecipePath = _recipePaths[index];
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(_selectedRecipePath);
            _recipe = asset == null ? null : JsonUtility.FromJson<OperatorAssetRecipe>(asset.text);
            if (_recipe == null)
            {
                _dialogueSet = null;
                _dialogueSerializedObject = null;
                return;
            }

            _dialogueSet = AssetDatabase.LoadAssetAtPath<OperatorDialogueSet>(_recipe.dialogueSetPath);
            _dialogueSerializedObject = _dialogueSet == null ? null : new SerializedObject(_dialogueSet);
            if (_dialogueSet != null)
            {
                _dialogueSet.EnsureLineSets();
                EditorUtility.SetDirty(_dialogueSet);
            }

            Repaint();
        }

        private void SaveCurrentAssets()
        {
            if (_recipe == null || string.IsNullOrWhiteSpace(_selectedRecipePath))
            {
                return;
            }

            if (_dialogueSet != null)
            {
                _recipe.dialogueSetPath = AssetDatabase.GetAssetPath(_dialogueSet);
                EditorUtility.SetDirty(_dialogueSet);
            }

            string absolutePath = Path.GetFullPath(_selectedRecipePath);
            File.WriteAllText(absolutePath, JsonUtility.ToJson(_recipe, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(_selectedRecipePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshRecipes(_selectedRecipePath);
            Debug.Log($"[OperatorStudio] 저장 완료: {_recipe.operatorId}");
        }

        private void CreateNewRecipe()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "새 오퍼레이터 레시피",
                "new-operator.json",
                "json",
                "Assets/Editor/OperatorRecipes 아래에 영문 소문자 ID로 저장하세요.",
                RecipeFolder);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string operatorId = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!IsValidOperatorId(operatorId))
            {
                EditorUtility.DisplayDialog("잘못된 Operator ID", "파일명은 영문 소문자, 숫자, -, _만 사용할 수 있습니다.", "확인");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null)
            {
                EditorUtility.DisplayDialog("이미 존재하는 레시피", path, "확인");
                return;
            }

            EnsureFolder($"{DialogueRoot}/{operatorId}");
            string dialoguePath = $"{DialogueRoot}/{operatorId}/OperatorDialogueSet.asset";
            OperatorDialogueSet dialogueSet = AssetDatabase.LoadAssetAtPath<OperatorDialogueSet>(dialoguePath);
            if (dialogueSet == null)
            {
                dialogueSet = CreateInstance<OperatorDialogueSet>();
                dialogueSet.EnsureLineSets();
                AssetDatabase.CreateAsset(dialogueSet, dialoguePath);
            }

            var recipe = new OperatorAssetRecipe
            {
                operatorId = operatorId,
                displayName = operatorId,
                playStyleDescription = string.Empty,
                dialogueSetPath = dialoguePath,
                playerData = new PlayerData
                {
                    maxHealth = 30f,
                    moveSpeed = 5f,
                    attackDamage = 5f,
                    attackRange = 4f,
                    attackInterval = 1f,
                    projectileSpeed = 1f,
                    skillCooldown = 12f,
                    skillRange = 5f,
                    skillDamage = 5f,
                },
            };
            File.WriteAllText(Path.GetFullPath(path), JsonUtility.ToJson(recipe, true), new UTF8Encoding(false));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshRecipes(path);
        }

        private void MigrateAllLegacyLines()
        {
            if (_dialogueSet == null)
            {
                return;
            }

            Undo.RecordObject(_dialogueSet, "Migrate Operator Dialogue Lines");
            int migrated = 0;
            for (int i = 0; i < DialogueFields.Length; i++)
            {
                OperatorLineSet lineSet = GetLineSet(i);
                if (lineSet != null)
                {
                    migrated += lineSet.MigrateLegacyEntries();
                }
            }

            EditorUtility.SetDirty(_dialogueSet);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OperatorStudio] 레거시 대사 {migrated}개를 문장 엔트리로 변환했습니다.");
        }

        private void MigrateLegacySlot(int index)
        {
            OperatorLineSet lineSet = GetLineSet(index);
            if (lineSet == null)
            {
                return;
            }

            Undo.RecordObject(_dialogueSet, "Migrate Operator Dialogue Slot");
            lineSet.MigrateLegacyEntries();
            EditorUtility.SetDirty(_dialogueSet);
            AssetDatabase.SaveAssets();
        }

        private OperatorLineSet GetLineSet(int index)
        {
            if (_dialogueSet == null || index < 0 || index >= DialogueFields.Length)
            {
                return null;
            }

            return DialogueFields[index] switch
            {
                "lobbyInteraction" => _dialogueSet.lobbyInteraction,
                "lobbyReturnTogether" => _dialogueSet.lobbyReturnTogether,
                "lobbyReturn" => _dialogueSet.lobbyReturn,
                "lobbyTouchUnfamiliar" => _dialogueSet.lobbyTouchUnfamiliar,
                "lobbyTouchFavorable" => _dialogueSet.lobbyTouchFavorable,
                "lobbyTouchJoy" => _dialogueSet.lobbyTouchJoy,
                "lobbyTouchLove" => _dialogueSet.lobbyTouchLove,
                "lobbyTouchEx" => _dialogueSet.lobbyTouchEx,
                "gameStart" => _dialogueSet.gameStart,
                "skillUsed" => _dialogueSet.skillUsed,
                "baseAttacked" => _dialogueSet.baseAttacked,
                "playerHit" => _dialogueSet.playerHit,
                "playerHitCritical" => _dialogueSet.playerHitCritical,
                "insufficientGold" => _dialogueSet.insufficientGold,
                "slotUnavailable" => _dialogueSet.slotUnavailable,
                "playerDied" => _dialogueSet.playerDied,
                "baseDestroyed" => _dialogueSet.baseDestroyed,
                _ => null,
            };
        }

        private void ValidateAll()
        {
            try
            {
                SaveCurrentAssets();
                bool valid = OperatorAssetValidator.ValidateAll();
                EditorUtility.DisplayDialog("Operator Validation", valid ? "검증 통과" : "검증 실패 — Console을 확인하세요.", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Operator Validation", exception.Message, "확인");
            }
        }

        private void BuildAll()
        {
            try
            {
                OperatorAssetBuilder.BuildAll();
                EditorUtility.DisplayDialog("Operator Build", "Definition, Catalog, Addressables 갱신 완료", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Operator Build", exception.Message, "확인");
            }
        }

        private static string DrawAssetPathField<T>(string label, string path) where T : UnityEngine.Object
        {
            T current = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
            T selected = (T)EditorGUILayout.ObjectField(label, current, typeof(T), false);
            return selected == current ? path : selected == null ? string.Empty : AssetDatabase.GetAssetPath(selected);
        }

        private static bool IsValidOperatorId(string operatorId)
        {
            if (string.IsNullOrWhiteSpace(operatorId))
            {
                return false;
            }

            foreach (char character in operatorId)
            {
                bool allowed = character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
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
