using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Unit;
using RCCom.Effects.Unit;
using RCCom.Effects.UnitVisual;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// AllyUnitDefinition과 AllyUnitRoster 제작의 단일 진입점. 유닛 종류 차이를 새 런타임
    /// 클래스가 아니라 데이터·효과 SO 조립으로 유지하고, 오퍼레이터 빌더의 생성물은 원본과
    /// 혼동해 편집하지 않도록 읽기 전용으로 구분한다.
    /// </summary>
    public sealed class AllyUnitStudioWindow : EditorWindow
    {
        private const string DefaultAssetRoot = "Assets/Data/Operators";
        private const string RecipeFolder = "Assets/Editor/AllyUnitRecipes";
        private const string OutputRoot = "Assets/Data/AllyUnits";
        private const string EffectsPropertyPath = "effects";
        private const string UnitsPropertyPath = "unitIds";
        private const string GeneratedOperatorLabel = "RCCom.GeneratedOperator";
        private const string VerticalSliceLabel = "RCCom.GeneratedAllyUnitVerticalSlice";

        private readonly List<AllyUnitDefinition> _units = new();
        private readonly List<AllyUnitRoster> _rosters = new();
        private readonly List<OperatorDefinition> _operators = new();

        // IMGUI 키보드 포커스는 그리기 순서로 매겨진 컨트롤 ID를 붙잡고 있으므로, 편집 중인 값에
        // 따라 컨트롤 개수가 달라지면 포커스가 옆 필드로 튀고 편집 중이던 문자열이 그쪽에 써진다.
        // 목록·이슈·검색 필터는 Layout 이벤트에서 한 번만 확정해 한 프레임 안에서 개수를 고정한다.
        private readonly List<string> _unitSearchKeys = new();
        private readonly List<string> _rosterSearchKeys = new();
        private readonly List<AllyUnitDefinition> _filteredUnits = new();
        private readonly List<AllyUnitRoster> _filteredRosters = new();
        private readonly List<string> _unitIssues = new();
        private readonly List<string> _rosterIssues = new();
        private PendingArrayEdit _pendingArrayEdit;

        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;
        private int _tabIndex;
        private string _searchText = string.Empty;
        private AllyUnitDefinition _selectedUnit;
        private AllyUnitRoster _selectedRoster;
        private AllyUnitAssetRecipe _recipe;
        private string _selectedRecipePath;
        private SerializedObject _serializedUnit;
        private SerializedObject _serializedRoster;
        private GUIStyle _selectedSidebarButtonStyle;
        private GUIStyle _pathStyle;

        private enum StudioTab
        {
            Units,
            Rosters,
            Audit,
            Package,
        }

        private enum ArrayEditKind
        {
            Move,
            Remove,
            Insert,
        }

        /// <summary>
        /// 배열 편집은 클릭 이벤트 도중이 아니라 다음 Layout 이벤트에서 적용한다. 같은 프레임 안에서
        /// arraySize를 바꾸면 Layout 패스와 Repaint 패스의 컨트롤 수가 어긋난다.
        /// </summary>
        private struct PendingArrayEdit
        {
            public bool active;
            public bool onRoster;
            public string propertyPath;
            public ArrayEditKind kind;
            public int index;
            public int targetIndex;
            public UnityEngine.Object value;
        }

        private GUIStyle SelectedSidebarButtonStyle =>
            _selectedSidebarButtonStyle ??= new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };

        private GUIStyle PathStyle => _pathStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
        };

        [MenuItem("RCCom/Ally Units/Open Ally Unit Studio")]
        public static void Open()
        {
            Vector2 size = new Vector2(1120f, 800f);
            AllyUnitStudioWindow[] existingWindows =
                Resources.FindObjectsOfTypeAll<AllyUnitStudioWindow>();
            for (int i = 0; i < existingWindows.Length; i++)
            {
                // 화면 밖에 저장된 도킹 위치보다 매번 접근 가능한 보조 창을 우선한다.
                existingWindows[i].Close();
            }

            AllyUnitStudioWindow window = GetWindowWithRect<AllyUnitStudioWindow>(
                new Rect(100f, 80f, size.x, size.y), true, "Ally Unit Studio", true);
            window.minSize = new Vector2(860f, 600f);
            window.ShowUtility();
            window.Focus();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += HandleUndoRedo;
            RefreshAssets(null, null);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout)
            {
                ApplyPendingArrayEdit();
                RebuildFrameCaches();
            }

            DrawToolbar();

            if ((StudioTab)_tabIndex == StudioTab.Audit ||
                (StudioTab)_tabIndex == StudioTab.Package)
            {
                _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
                if ((StudioTab)_tabIndex == StudioTab.Audit)
                {
                    DrawAuditTab();
                }
                else
                {
                    DrawPackageTab();
                }

                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawSidebar();
            DrawEditorContent();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                RefreshAssets(GetPath(_selectedUnit), GetPath(_selectedRoster));
            }

            GUILayout.Label("RCCom / Ally Unit Studio", EditorStyles.boldLabel);
            GUILayout.Space(12f);
            int previousTab = _tabIndex;
            _tabIndex = GUILayout.Toolbar(
                _tabIndex,
                new[] { $"Units ({_units.Count})", $"Rosters ({_rosters.Count})", "Audit", "Package" },
                EditorStyles.toolbarButton,
                GUILayout.Width(390f));
            if (previousTab != _tabIndex)
            {
                _contentScroll = Vector2.zero;
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save All", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                SaveAll();
            }

            if (GUILayout.Button("New Unit", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                CreateNewUnit();
            }

            if (GUILayout.Button("New Roster", EditorStyles.toolbarButton, GUILayout.Width(82f)))
            {
                CreateNewRoster();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(255f));
            string title = (StudioTab)_tabIndex == StudioTab.Units ? "Unit Definitions" : "Rosters";
            GUILayout.Label(title, EditorStyles.boldLabel);
            _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            // 버튼 개수는 Layout에서 확정한 _filtered* 목록이 정하고, 글자만 실시간 라벨을 쓴다.
            // 라벨 문자열이 바뀌는 것은 컨트롤 ID에 영향이 없지만 개수가 바뀌면 포커스가 튄다.
            if ((StudioTab)_tabIndex == StudioTab.Units)
            {
                for (int i = 0; i < _filteredUnits.Count; i++)
                {
                    AllyUnitDefinition definition = _filteredUnits[i];
                    GUIStyle style = definition == _selectedUnit
                        ? SelectedSidebarButtonStyle
                        : EditorStyles.toolbarButton;
                    if (GUILayout.Button(GetUnitLabel(definition), style, GUILayout.Height(27f)))
                    {
                        SelectUnit(definition);
                    }
                }
            }
            else
            {
                for (int i = 0; i < _filteredRosters.Count; i++)
                {
                    AllyUnitRoster roster = _filteredRosters[i];
                    GUIStyle style = roster == _selectedRoster
                        ? SelectedSidebarButtonStyle
                        : EditorStyles.toolbarButton;
                    if (GUILayout.Button(GetRosterLabel(roster), style, GUILayout.Height(27f)))
                    {
                        SelectRoster(roster);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEditorContent()
        {
            EditorGUILayout.BeginVertical();
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            if ((StudioTab)_tabIndex == StudioTab.Units)
            {
                DrawUnitRecipeEditor();
            }
            else
            {
                DrawRosterEditor();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawUnitRecipeEditor()
        {
            if (_recipe == null)
            {
                EditorGUILayout.HelpBox(
                    "AllyUnit 레시피가 없습니다. New Unit으로 제작 원본을 먼저 만드세요.",
                    MessageType.Info);
                return;
            }

            EnsureRecipeData();
            GUILayout.Label("Ally Unit Recipe", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Recipe", _selectedRecipePath ?? string.Empty);
            EditorGUILayout.HelpBox(
                "이 탭은 Definition 결과물이 아니라 JSON 레시피를 편집합니다. Save + Build를 실행하면 " +
                "유닛 Definition·카탈로그·Addressables 그룹이 같은 원본에서 갱신됩니다.",
                MessageType.Info);

            GUILayout.Label("Identity & Deployment", EditorStyles.boldLabel);
            _recipe.unitId = NormalizeId(EditorGUILayout.TextField("Unit ID", _recipe.unitId));
            _recipe.catalogOrder = Mathf.Max(
                0,
                EditorGUILayout.IntField("Catalog Order", _recipe.catalogOrder));
            _recipe.displayName = EditorGUILayout.TextField("Display Name", _recipe.displayName);
            _recipe.data.deployCost = Mathf.Max(
                0,
                EditorGUILayout.IntField("Deploy Cost", _recipe.data.deployCost));
            _recipe.remoteContent = EditorGUILayout.ToggleLeft("Remote Content", _recipe.remoteContent);
            EditorGUILayout.HelpBox(
                "Unit ID는 Roster와 Addressables 주소에 쓰이는 영구 식별자입니다. 배포 후에는 변경하지 마세요.",
                MessageType.None);

            GUILayout.Space(10f);
            GUILayout.Label("Combat Data", EditorStyles.boldLabel);
            _recipe.data.maxHealth = Mathf.Max(
                0f,
                EditorGUILayout.FloatField("Max Health", _recipe.data.maxHealth));
            _recipe.data.moveSpeed = Mathf.Max(
                0f,
                EditorGUILayout.FloatField("Move Speed", _recipe.data.moveSpeed));
            _recipe.data.attackDamage = Mathf.Max(
                0f,
                EditorGUILayout.FloatField("Attack Damage", _recipe.data.attackDamage));
            _recipe.data.attackInterval = Mathf.Max(
                0.01f,
                EditorGUILayout.FloatField("Attack Interval", _recipe.data.attackInterval));
            _recipe.data.attackRange = Mathf.Max(
                0f,
                EditorGUILayout.FloatField("Attack Range", _recipe.data.attackRange));
            _recipe.data.detectionRange = Mathf.Max(
                _recipe.data.attackRange,
                EditorGUILayout.FloatField("Detection Range", _recipe.data.detectionRange));
            _recipe.data.projectileSpeed = Mathf.Max(
                0f,
                EditorGUILayout.FloatField("Projectile Speed", _recipe.data.projectileSpeed));

            GUILayout.Space(10f);
            GUILayout.Label("Presentation", EditorStyles.boldLabel);
            _recipe.spritePath = DrawRecipeAssetPathField<Sprite>("Sprite", _recipe.spritePath);
            _recipe.tint = EditorGUILayout.ColorField("Fallback Tint", _recipe.tint);
            _recipe.spriteForwardOffsetDegrees = EditorGUILayout.FloatField(
                "Forward Offset Degrees",
                _recipe.spriteForwardOffsetDegrees);
            DrawSpritePreview(
                string.IsNullOrWhiteSpace(_recipe.spritePath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<Sprite>(_recipe.spritePath));

            GUILayout.Space(10f);
            DrawRecipeEffects();
            GUILayout.Space(10f);
            DrawRecipeVisualEffects();
            DrawUnitUsage(_selectedUnit);

            GUILayout.Space(12f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Recipe", GUILayout.Height(30f)))
            {
                SaveCurrentRecipe();
            }

            if (GUILayout.Button("Duplicate Unit", GUILayout.Height(30f)))
            {
                DuplicateSelectedUnit();
            }

            if (GUILayout.Button("Ping Definition", GUILayout.Height(30f)))
            {
                if (_selectedUnit != null)
                {
                    Selection.activeObject = _selectedUnit;
                    EditorGUIUtility.PingObject(_selectedUnit);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRecipeEffects()
        {
            EnsureRecipeData();
            GUILayout.Label($"Effect Composition  /  {_recipe.effectPaths.Count}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "효과 SO는 공유되는 무상태 훅입니다. 효과의 경로 순서는 실행 순서가 되며, " +
                "개체별 상태는 AllyUnitInstance가 소유합니다.",
                MessageType.Info);

            for (int i = 0; i < _recipe.effectPaths.Count; i++)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                _recipe.effectPaths[i] = DrawRecipeAssetPathField<AllyUnitEffectBase>(
                    $"Effect {i + 1}",
                    _recipe.effectPaths[i]);
                using (new EditorGUI.DisabledGroupScope(i <= 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(28f)))
                    {
                        SwapRecipeEffects(i, i - 1);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                }

                using (new EditorGUI.DisabledGroupScope(i >= _recipe.effectPaths.Count - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(28f)))
                    {
                        SwapRecipeEffects(i, i + 1);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                }

                if (GUILayout.Button("Remove", GUILayout.Width(62f)))
                {
                    _recipe.effectPaths.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Effect"))
            {
                _recipe.effectPaths.Add(string.Empty);
            }
        }

        private void DrawRecipeVisualEffects()
        {
            EnsureRecipeData();
            GUILayout.Label(
                $"Visual Effect Composition  /  {_recipe.visualEffectPaths.Count}",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "비주얼 SO에는 색상·주기·선 두께 같은 표현 데이터만 둡니다. " +
                "애니메이션 진행도와 Renderer는 AllyUnitView의 런타임 객체가 소유합니다.",
                MessageType.Info);

            for (int i = 0; i < _recipe.visualEffectPaths.Count; i++)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                _recipe.visualEffectPaths[i] = DrawRecipeAssetPathField<AllyUnitVisualEffectBase>(
                    $"Visual Effect {i + 1}",
                    _recipe.visualEffectPaths[i]);
                if (GUILayout.Button("Remove", GUILayout.Width(62f)))
                {
                    _recipe.visualEffectPaths.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Visual Effect"))
            {
                _recipe.visualEffectPaths.Add(string.Empty);
            }
        }

        private void DrawRosterEditor()
        {
            if (_selectedRoster == null || _serializedRoster == null)
            {
                EditorGUILayout.HelpBox(
                    "AllyUnitRoster가 없습니다. New Roster로 출격 목록을 만드세요.",
                    MessageType.Info);
                return;
            }

            _serializedRoster.Update();
            bool readOnly = HasLabel(_selectedRoster, GeneratedOperatorLabel);
            DrawAssetHeader(_selectedRoster, "Ally Unit Roster");
            DrawGeneratedAssetNotice(_selectedRoster, readOnly);
            DrawIssueSummary(_rosterIssues);

            SerializedProperty units = _serializedRoster.FindProperty(UnitsPropertyPath);
            GUILayout.Label($"Roster Entries  /  {units.arraySize}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Roster는 Definition 참조가 아니라 Unit ID 순서를 저장합니다. 위에서 아래 순서가 " +
                "선택 화면과 전투 배치 메뉴의 버튼 순서입니다.",
                MessageType.Info);

            DrawRosterEntries(units, readOnly);

            using (new EditorGUI.DisabledGroupScope(readOnly))
            {
                if (GUILayout.Button("+ Add Empty Slot", GUILayout.Height(28f)))
                {
                    QueueInsert(true, UnitsPropertyPath, null);
                }

                if (_selectedUnit != null && !RosterContains(_selectedRoster, _selectedUnit) &&
                    GUILayout.Button($"+ Add Selected Unit: {GetUnitLabel(_selectedUnit)}", GUILayout.Height(28f)))
                {
                    QueueInsert(true, UnitsPropertyPath, _selectedUnit);
                }

                DrawAvailableUnits(units);
            }

            DrawRosterUsage(_selectedRoster);
            DrawIssueDetails(_rosterIssues);

            GUILayout.Space(12f);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledGroupScope(readOnly))
            {
                if (GUILayout.Button("Save Roster", GUILayout.Height(30f)))
                {
                    SaveAll();
                }
            }

            if (GUILayout.Button("Ping Asset", GUILayout.Height(30f)))
            {
                Selection.activeObject = _selectedRoster;
                EditorGUIUtility.PingObject(_selectedRoster);
            }

            EditorGUILayout.EndHorizontal();

            if (_serializedRoster.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selectedRoster);
            }
        }

        private void DrawEffects(SerializedProperty effects)
        {
            GUILayout.Label($"Effect Composition  /  {effects.arraySize}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "효과 SO는 공유되는 무상태 훅입니다. 개체별 쿨다운이나 버프 잔여 시간은 Definition이 아니라 AllyUnitInstance가 소유해야 합니다.",
                MessageType.Info);

            for (int i = 0; i < effects.arraySize; i++)
            {
                SerializedProperty effect = effects.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(effect, new GUIContent($"Effect {i + 1}"));
                using (new EditorGUI.DisabledGroupScope(i <= 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(28f)))
                    {
                        QueueMove(false, EffectsPropertyPath, i, i - 1);
                    }
                }

                using (new EditorGUI.DisabledGroupScope(i >= effects.arraySize - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(28f)))
                    {
                        QueueMove(false, EffectsPropertyPath, i, i + 1);
                    }
                }

                if (GUILayout.Button("Remove", GUILayout.Width(62f)))
                {
                    QueueRemove(false, EffectsPropertyPath, i);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Effect Slot"))
            {
                QueueInsert(false, EffectsPropertyPath, null);
            }
        }

        private void DrawRosterEntries(SerializedProperty units, bool readOnly)
        {
            for (int i = 0; i < units.arraySize; i++)
            {
                SerializedProperty element = units.GetArrayElementAtIndex(i);
                string unitId = element.stringValue;
                AllyUnitDefinition definition = FindUnitById(unitId);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledGroupScope(readOnly))
                {
                    EditorGUILayout.PropertyField(element, new GUIContent($"Slot {i + 1}"));
                }

                using (new EditorGUI.DisabledGroupScope(readOnly || i <= 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(28f)))
                    {
                        QueueMove(true, UnitsPropertyPath, i, i - 1);
                    }
                }

                using (new EditorGUI.DisabledGroupScope(readOnly || i >= units.arraySize - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(28f)))
                    {
                        QueueMove(true, UnitsPropertyPath, i, i + 1);
                    }
                }

                using (new EditorGUI.DisabledGroupScope(readOnly))
                {
                    if (GUILayout.Button("Remove", GUILayout.Width(62f)))
                    {
                        QueueRemove(true, UnitsPropertyPath, i);
                    }
                }

                EditorGUILayout.EndHorizontal();
                if (definition != null && definition.data != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(
                        $"{definition.data.unitId}  /  CP {definition.data.deployCost}  /  " +
                        $"HP {definition.data.maxHealth:0.##}  /  Range {definition.data.attackRange:0.##}",
                        EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Edit", GUILayout.Width(48f)))
                    {
                        if (definition != null)
                        {
                            SelectUnit(definition);
                            _tabIndex = (int)StudioTab.Units;
                        }

                        _contentScroll = Vector2.zero;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawAvailableUnits(SerializedProperty rosterUnits)
        {
            var available = new List<AllyUnitDefinition>();
            for (int i = 0; i < _units.Count; i++)
            {
                if (!SerializedArrayContainsString(rosterUnits, _units[i].data?.unitId))
                {
                    available.Add(_units[i]);
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label($"Available Definitions  /  {available.Count}", EditorStyles.boldLabel);
            if (available.Count == 0)
            {
                GUILayout.Label("모든 Definition이 이 Roster에 등록되어 있습니다.", EditorStyles.miniLabel);
                return;
            }

            for (int i = 0; i < available.Count; i++)
            {
                AllyUnitDefinition definition = available[i];
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(GetUnitLabel(definition));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add", GUILayout.Width(50f)))
                {
                    QueueInsert(true, UnitsPropertyPath, definition);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawAuditTab()
        {
            GUILayout.Label("Ally Unit Dataset Audit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "제작 중인 모든 Definition·Roster와 오퍼레이터 연결 상태를 읽기 전용으로 요약합니다. " +
                "전체 프로젝트 규칙 판정은 아래 OperatorAssetValidator를 사용합니다.",
                MessageType.Info);

            var registeredUnits = new HashSet<AllyUnitDefinition>();
            for (int i = 0; i < _rosters.Count; i++)
            {
                AllyUnitRoster roster = _rosters[i];
                if (roster == null || roster.unitIds == null)
                {
                    continue;
                }

                for (int unitIndex = 0; unitIndex < roster.unitIds.Count; unitIndex++)
                {
                    AllyUnitDefinition definition = FindUnitById(roster.unitIds[unitIndex]);
                    if (definition != null)
                    {
                        registeredUnits.Add(definition);
                    }
                }
            }

            var connectedRosters = new HashSet<AllyUnitRoster>();
            for (int i = 0; i < _operators.Count; i++)
            {
                if (_operators[i] != null && _operators[i].allyUnitRoster != null)
                {
                    connectedRosters.Add(_operators[i].allyUnitRoster);
                }
            }

            EditorGUILayout.LabelField("Definitions", _units.Count.ToString());
            EditorGUILayout.LabelField("Rosters", _rosters.Count.ToString());
            EditorGUILayout.LabelField("Operator-linked Rosters", connectedRosters.Count.ToString());

            GUILayout.Space(12f);
            GUILayout.Label("Definitions outside every Roster", EditorStyles.boldLabel);
            int unregisteredCount = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                if (registeredUnits.Contains(_units[i]))
                {
                    continue;
                }

                unregisteredCount++;
                DrawAuditObjectRow(_units[i], GetUnitLabel(_units[i]));
            }

            if (unregisteredCount == 0)
            {
                GUILayout.Label("없음", EditorStyles.miniLabel);
            }

            GUILayout.Space(12f);
            GUILayout.Label("Rosters not linked by an Operator", EditorStyles.boldLabel);
            int orphanRosterCount = 0;
            for (int i = 0; i < _rosters.Count; i++)
            {
                if (connectedRosters.Contains(_rosters[i]))
                {
                    continue;
                }

                orphanRosterCount++;
                DrawAuditObjectRow(_rosters[i], GetRosterLabel(_rosters[i]));
            }

            if (orphanRosterCount == 0)
            {
                GUILayout.Label("없음", EditorStyles.miniLabel);
            }

            GUILayout.Space(12f);
            GUILayout.Label("Inline Data Issues", EditorStyles.boldLabel);
            int issueCount = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                List<string> issues = CollectUnitIssues(_units[i]);
                for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                {
                    issueCount++;
                    EditorGUILayout.HelpBox($"{GetUnitLabel(_units[i])}: {issues[issueIndex]}", MessageType.Error);
                }
            }

            for (int i = 0; i < _rosters.Count; i++)
            {
                List<string> issues = CollectRosterIssues(_rosters[i]);
                for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                {
                    issueCount++;
                    EditorGUILayout.HelpBox($"{GetRosterLabel(_rosters[i])}: {issues[issueIndex]}", MessageType.Error);
                }
            }

            if (issueCount == 0)
            {
                EditorGUILayout.HelpBox("인라인 데이터 오류가 없습니다.", MessageType.Info);
            }

            GUILayout.Space(14f);
            if (GUILayout.Button("Save & Run Full Operator Asset Validator", GUILayout.Height(34f)))
            {
                ValidateAllAssets();
            }

            if (GUILayout.Button("Open Operator Studio", GUILayout.Height(28f)))
            {
                OperatorStudioWindow.Open();
            }
        }

        private static void DrawAuditObjectRow(UnityEngine.Object asset, string label)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Ping", GUILayout.Width(50f)))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPackageTab()
        {
            GUILayout.Label("Save, Validate & Package", EditorStyles.boldLabel);
            if (_recipe == null)
            {
                EditorGUILayout.HelpBox(
                    "선택한 유닛의 레시피를 찾지 못했습니다. Refresh 후 마이그레이션/빌드 상태를 확인하세요.",
                    MessageType.Error);
                return;
            }

            EnsureRecipeData();
            EditorGUILayout.HelpBox(
                "레시피를 저장하면 제작 원본만 바뀝니다. 단일 빌드는 Definition·AllyUnitCatalog·" +
                "전용 Addressables 그룹을 함께 갱신합니다.",
                MessageType.Info);
            EditorGUILayout.LabelField("Recipe", _selectedRecipePath ?? string.Empty);
            EditorGUILayout.LabelField(
                "Definition",
                AllyUnitCatalogBuilder.GetDefinitionPath(_recipe.unitId));
            EditorGUILayout.LabelField("Address", AllyUnitCatalogBuilder.GetAddress(_recipe.unitId));
            EditorGUILayout.LabelField(
                "Group",
                AllyUnitCatalogBuilder.GetGroupName(_recipe.unitId, _recipe.remoteContent));

            GUILayout.Space(12f);
            if (GUILayout.Button(
                    $"Save + Validate + Build ▶ {_recipe.unitId}",
                    GUILayout.Height(34f)))
            {
                BuildSelectedUnit();
            }

            if (GUILayout.Button("Save Recipe", GUILayout.Height(28f)))
            {
                SaveCurrentRecipe();
            }

            if (GUILayout.Button("Validate Ally Unit Assets", GUILayout.Height(28f)))
            {
                ValidateAllyUnitAssets();
            }

            if (GUILayout.Button("Build All Ally Unit Assets + Addressables", GUILayout.Height(32f)))
            {
                SaveAll();
                BuildAllUnits();
            }

            if (GUILayout.Button("Save & Run Full Operator Asset Validator", GUILayout.Height(32f)))
            {
                ValidateAllAssets();
            }
        }

        private void DrawAssetHeader(UnityEngine.Object asset, string typeLabel)
        {
            GUILayout.Label(typeLabel, EditorStyles.largeLabel);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            using (new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUILayout.ObjectField("Asset", asset, asset.GetType(), false);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Label(GetPath(asset), PathStyle);
            GUILayout.Space(6f);
        }

        private static void DrawGeneratedAssetNotice(UnityEngine.Object asset, bool readOnly)
        {
            if (HasLabel(asset, GeneratedOperatorLabel))
            {
                EditorGUILayout.HelpBox(
                    "OperatorAssetBuilder가 만드는 오퍼레이터 전용 복제본입니다. 다음 Build에서 원본 Roster로 다시 생성되므로 이 창에서는 읽기 전용입니다.",
                    MessageType.Warning);
            }
            else if (HasLabel(asset, VerticalSliceLabel))
            {
                EditorGUILayout.HelpBox(
                    "테스트 수직 슬라이스 빌더가 관리하는 회색상자 에셋입니다. 편집할 수 있지만 빌더를 다시 실행하면 값이 초기화될 수 있습니다.",
                    MessageType.Warning);
            }
            else if (readOnly)
            {
                EditorGUILayout.HelpBox("자동 생성 에셋이라 읽기 전용입니다.", MessageType.Warning);
            }
        }

        private static void DrawSpritePreview(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            Texture2D preview = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
            if (preview == null)
            {
                return;
            }

            Rect rect = GUILayoutUtility.GetAspectRect(2.4f, GUILayout.MaxHeight(180f));
            EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.ScaleToFit);
        }

        private void DrawUnitUsage(AllyUnitDefinition definition)
        {
            GUILayout.Space(10f);
            GUILayout.Label("Roster Usage", EditorStyles.boldLabel);
            int count = 0;
            for (int i = 0; i < _rosters.Count; i++)
            {
                if (!RosterContains(_rosters[i], definition))
                {
                    continue;
                }

                count++;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(GetRosterLabel(_rosters[i]));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open", GUILayout.Width(52f)))
                {
                    SelectRoster(_rosters[i]);
                    _tabIndex = (int)StudioTab.Rosters;
                    _contentScroll = Vector2.zero;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (count == 0)
            {
                EditorGUILayout.HelpBox(
                    "어느 AllyUnitRoster에도 등록되지 않았습니다. 제작 중일 수 있지만 출격 목록에는 나타나지 않습니다.",
                    MessageType.Warning);
            }
        }

        private void DrawRosterUsage(AllyUnitRoster roster)
        {
            GUILayout.Space(10f);
            GUILayout.Label("Operator Usage", EditorStyles.boldLabel);
            int count = 0;
            for (int i = 0; i < _operators.Count; i++)
            {
                OperatorDefinition definition = _operators[i];
                if (definition == null || definition.allyUnitRoster != roster)
                {
                    continue;
                }

                count++;
                EditorGUILayout.ObjectField(definition.displayName, definition, typeof(OperatorDefinition), false);
            }

            if (count == 0)
            {
                EditorGUILayout.HelpBox(
                    "어느 OperatorDefinition에도 직접 연결되지 않았습니다. 원본 풀이라면 Operator Studio의 Source Ally Unit Roster에서 연결하세요.",
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// 입력 필드보다 위에 그려지므로 개수가 항상 하나로 고정되어야 한다. 내용과 아이콘만 바뀐다.
        /// </summary>
        private static void DrawIssueSummary(List<string> issues)
        {
            EditorGUILayout.HelpBox(
                issues.Count == 0
                    ? "인라인 데이터 오류가 없습니다."
                    : $"인라인 데이터 오류 {issues.Count}건 — 아래 Validation 섹션에서 확인하세요.",
                issues.Count == 0 ? MessageType.Info : MessageType.Error);
        }

        private static void DrawIssueDetails(List<string> issues)
        {
            if (issues.Count == 0)
            {
                return;
            }

            GUILayout.Space(10f);
            GUILayout.Label("Validation", EditorStyles.boldLabel);
            for (int i = 0; i < issues.Count; i++)
            {
                EditorGUILayout.HelpBox(issues[i], MessageType.Error);
            }
        }

        private static List<string> CollectUnitIssues(AllyUnitDefinition definition)
        {
            var issues = new List<string>();
            if (definition == null)
            {
                issues.Add("Definition이 null입니다.");
                return issues;
            }

            AllyUnitData data = definition.data;
            if (data == null)
            {
                issues.Add("AllyUnitData가 null입니다.");
                return issues;
            }

            if (string.IsNullOrWhiteSpace(data.unitId)) { issues.Add("Unit ID가 비어 있습니다."); }
            if (string.IsNullOrWhiteSpace(data.displayName)) { issues.Add("Display Name이 비어 있습니다."); }
            if (data.deployCost < 0) { issues.Add("Deploy Cost는 음수일 수 없습니다."); }
            if (data.maxHealth <= 0f) { issues.Add("Max Health는 0보다 커야 합니다."); }
            if (data.moveSpeed < 0f) { issues.Add("Move Speed는 음수일 수 없습니다."); }
            if (data.attackInterval <= 0f) { issues.Add("Attack Interval은 0보다 커야 합니다."); }
            if (data.attackRange < 0f) { issues.Add("Attack Range는 음수일 수 없습니다."); }
            if (data.detectionRange < data.attackRange)
            {
                issues.Add("Detection Range는 Attack Range 이상이어야 합니다.");
            }

            if (definition.effects == null)
            {
                issues.Add("Effect 목록이 null입니다.");
            }
            else
            {
                for (int i = 0; i < definition.effects.Count; i++)
                {
                    if (definition.effects[i] == null)
                    {
                        issues.Add($"Effect {i + 1}이 null입니다.");
                    }
                }
            }

            if (definition.visualEffects == null)
            {
                issues.Add("Visual Effect 목록이 null입니다.");
            }
            else
            {
                for (int i = 0; i < definition.visualEffects.Count; i++)
                {
                    if (definition.visualEffects[i] == null)
                    {
                        issues.Add($"Visual Effect {i + 1}이 null입니다.");
                    }
                }
            }

            return issues;
        }

        private List<string> CollectRosterIssues(AllyUnitRoster roster)
        {
            var issues = new List<string>();
            if (roster == null || roster.unitIds == null)
            {
                issues.Add("Roster 목록이 null입니다.");
                return issues;
            }

            if (roster.unitIds.Count == 0)
            {
                issues.Add("Roster가 비어 있습니다.");
                return issues;
            }

            var unitIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < roster.unitIds.Count; i++)
            {
                string unitId = roster.unitIds[i];
                if (string.IsNullOrWhiteSpace(unitId))
                {
                    issues.Add($"Slot {i + 1}의 Unit ID가 비어 있습니다.");
                    continue;
                }

                if (!unitIds.Add(unitId))
                {
                    issues.Add($"Unit ID가 중복됩니다: {unitId}");
                }

                if (FindUnitById(unitId) == null)
                {
                    issues.Add($"존재하지 않는 AllyUnitDefinition을 가리킵니다: {unitId}");
                }
            }

            return issues;
        }

        /// <summary>
        /// 한 프레임 안에서 Layout·Repaint 패스가 같은 컨트롤 수를 그리도록, 값에 따라 개수가 변하는
        /// 목록을 Layout 이벤트에서만 확정한다.
        /// </summary>
        private void RebuildFrameCaches()
        {
            _filteredUnits.Clear();
            for (int i = 0; i < _units.Count; i++)
            {
                // 검색 판정은 편집 중에 실시간으로 흔들리지 않도록 Refresh 시점 스냅샷 라벨로 한다.
                string key = i < _unitSearchKeys.Count ? _unitSearchKeys[i] : GetUnitLabel(_units[i]);
                if (MatchesSearch(key, _searchText))
                {
                    _filteredUnits.Add(_units[i]);
                }
            }

            _filteredRosters.Clear();
            for (int i = 0; i < _rosters.Count; i++)
            {
                string key = i < _rosterSearchKeys.Count ? _rosterSearchKeys[i] : GetRosterLabel(_rosters[i]);
                if (MatchesSearch(key, _searchText))
                {
                    _filteredRosters.Add(_rosters[i]);
                }
            }

            _unitIssues.Clear();
            if (_selectedUnit != null)
            {
                _unitIssues.AddRange(CollectUnitIssues(_selectedUnit));
            }

            _rosterIssues.Clear();
            if (_selectedRoster != null)
            {
                _rosterIssues.AddRange(CollectRosterIssues(_selectedRoster));
            }
        }

        private void QueueMove(bool onRoster, string propertyPath, int index, int targetIndex)
        {
            QueueArrayEdit(onRoster, propertyPath, ArrayEditKind.Move, index, targetIndex, null);
        }

        private void QueueRemove(bool onRoster, string propertyPath, int index)
        {
            QueueArrayEdit(onRoster, propertyPath, ArrayEditKind.Remove, index, -1, null);
        }

        private void QueueInsert(bool onRoster, string propertyPath, UnityEngine.Object value)
        {
            QueueArrayEdit(onRoster, propertyPath, ArrayEditKind.Insert, -1, -1, value);
        }

        private void QueueArrayEdit(
            bool onRoster,
            string propertyPath,
            ArrayEditKind kind,
            int index,
            int targetIndex,
            UnityEngine.Object value)
        {
            _pendingArrayEdit = new PendingArrayEdit
            {
                active = true,
                onRoster = onRoster,
                propertyPath = propertyPath,
                kind = kind,
                index = index,
                targetIndex = targetIndex,
                value = value,
            };
            Repaint();
        }

        private void ApplyPendingArrayEdit()
        {
            if (!_pendingArrayEdit.active)
            {
                return;
            }

            PendingArrayEdit edit = _pendingArrayEdit;
            _pendingArrayEdit = default;

            SerializedObject owner = edit.onRoster ? _serializedRoster : _serializedUnit;
            UnityEngine.Object target = edit.onRoster ? _selectedRoster : (UnityEngine.Object)_selectedUnit;
            if (owner == null || target == null)
            {
                return;
            }

            owner.Update();
            SerializedProperty array = owner.FindProperty(edit.propertyPath);
            if (array == null || !array.isArray)
            {
                return;
            }

            switch (edit.kind)
            {
                case ArrayEditKind.Move:
                    if (edit.index < 0 || edit.index >= array.arraySize ||
                        edit.targetIndex < 0 || edit.targetIndex >= array.arraySize)
                    {
                        return;
                    }

                    array.MoveArrayElement(edit.index, edit.targetIndex);
                    break;
                case ArrayEditKind.Remove:
                    if (edit.index < 0 || edit.index >= array.arraySize)
                    {
                        return;
                    }

                    RemoveArrayElement(array, edit.index);
                    break;
                case ArrayEditKind.Insert:
                    if (edit.onRoster)
                    {
                        AllyUnitDefinition definition = edit.value as AllyUnitDefinition;
                        InsertStringValue(
                            array,
                            definition != null && definition.data != null
                                ? definition.data.unitId
                                : string.Empty);
                    }
                    else
                    {
                        InsertObjectReference(array, edit.value);
                    }

                    break;
            }

            if (owner.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
            }
        }

        private void RefreshAssets(string preferredUnitPath, string preferredRosterPath)
        {
            _pendingArrayEdit = default;
            _units.Clear();
            _rosters.Clear();
            _operators.Clear();
            LoadAssets(_units);
            LoadAssets(_rosters);
            LoadAssets(_operators);

            _units.Sort((left, right) => string.CompareOrdinal(GetPath(left), GetPath(right)));
            _rosters.Sort((left, right) => string.CompareOrdinal(GetPath(left), GetPath(right)));
            _operators.Sort((left, right) => string.CompareOrdinal(GetPath(left), GetPath(right)));

            _unitSearchKeys.Clear();
            for (int i = 0; i < _units.Count; i++)
            {
                _unitSearchKeys.Add(GetUnitLabel(_units[i]));
            }

            _rosterSearchKeys.Clear();
            for (int i = 0; i < _rosters.Count; i++)
            {
                _rosterSearchKeys.Add(GetRosterLabel(_rosters[i]));
            }

            AllyUnitDefinition preferredUnit = AssetDatabase.LoadAssetAtPath<AllyUnitDefinition>(preferredUnitPath);
            AllyUnitRoster preferredRoster = AssetDatabase.LoadAssetAtPath<AllyUnitRoster>(preferredRosterPath);
            SelectUnit(preferredUnit != null ? preferredUnit : _units.Count > 0 ? _units[0] : null);
            SelectRoster(preferredRoster != null ? preferredRoster : _rosters.Count > 0 ? _rosters[0] : null);
            Repaint();
        }

        private static void LoadAssets<T>(List<T> results) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    results.Add(asset);
                }
            }
        }

        private void SelectUnit(AllyUnitDefinition definition)
        {
            _selectedUnit = definition;
            _serializedUnit = definition != null ? new SerializedObject(definition) : null;
            _recipe = null;
            _selectedRecipePath = null;
            if (definition == null || definition.data == null ||
                string.IsNullOrWhiteSpace(definition.data.unitId))
            {
                return;
            }

            _selectedRecipePath = $"{RecipeFolder}/{definition.data.unitId}.json";
            TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(_selectedRecipePath);
            _recipe = recipeAsset == null
                ? null
                : JsonUtility.FromJson<AllyUnitAssetRecipe>(recipeAsset.text);
            EnsureRecipeData();
        }

        private void SelectRoster(AllyUnitRoster roster)
        {
            _selectedRoster = roster;
            _serializedRoster = roster != null ? new SerializedObject(roster) : null;
        }

        private void CreateNewUnit()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "새 아군 유닛 레시피",
                "new-ally-unit.json",
                "json",
                "Assets/Editor/AllyUnitRecipes 아래에 영문 소문자 ID로 저장하세요.",
                RecipeFolder);
            if (string.IsNullOrWhiteSpace(path) || !EnsureNewAssetPath(path, "AllyUnitRecipe"))
            {
                return;
            }

            string unitId = NormalizeId(Path.GetFileNameWithoutExtension(path));
            var recipe = new AllyUnitAssetRecipe
            {
                unitId = unitId,
                catalogOrder = _units.Count,
                displayName = unitId,
                spriteForwardOffsetDegrees = 0f,
                effectPaths = new List<string> { "Assets/Data/Effects/Unit/BasicAttackEffect.asset" },
                visualEffectPaths = new List<string>(),
                data = new AllyUnitData
                {
                    unitId = unitId,
                    displayName = unitId,
                    deployCost = 10,
                    maxHealth = 10f,
                    moveSpeed = 1f,
                    attackDamage = 1f,
                    attackInterval = 1f,
                    attackRange = 1f,
                    detectionRange = 2f,
                    projectileSpeed = 8f,
                },
            };
            WriteRecipeFile(recipe, path);
            BuildRecipe(path);
            RefreshAssets(GetPath(_selectedUnit), GetPath(_selectedRoster));
            _tabIndex = (int)StudioTab.Units;
        }

        private void DuplicateSelectedUnit()
        {
            if (_recipe == null)
            {
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "아군 유닛 레시피 복제",
                $"{_recipe.unitId}-copy.json",
                "json",
                "효과와 Sprite 경로를 재사용하고 AllyUnitData를 복제합니다.",
                RecipeFolder);
            if (string.IsNullOrWhiteSpace(path) || !EnsureNewAssetPath(path, "AllyUnitRecipe"))
            {
                return;
            }

            string unitId = NormalizeId(Path.GetFileNameWithoutExtension(path));
            AllyUnitAssetRecipe duplicate =
                JsonUtility.FromJson<AllyUnitAssetRecipe>(JsonUtility.ToJson(_recipe));
            duplicate.unitId = unitId;
            duplicate.displayName = unitId;
            duplicate.data ??= new AllyUnitData();
            duplicate.data.unitId = unitId;
            duplicate.data.displayName = unitId;
            WriteRecipeFile(duplicate, path);
            BuildRecipe(path);
            RefreshAssets(GetPath(_selectedUnit), GetPath(_selectedRoster));
            _tabIndex = (int)StudioTab.Units;
        }

        private void CreateNewRoster()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "새 아군 유닛 Roster",
                "AllyUnitRoster.asset",
                "asset",
                "오퍼레이터 레시피에서 원본 풀로 사용할 Roster를 저장하세요.",
                GetDefaultFolder());
            if (string.IsNullOrWhiteSpace(path) || !EnsureNewAssetPath(path, "AllyUnitRoster"))
            {
                return;
            }

            AllyUnitRoster roster = CreateInstance<AllyUnitRoster>();
            roster.name = Path.GetFileNameWithoutExtension(path);
            roster.unitIds = new List<string>();
            if (_selectedUnit != null && _selectedUnit.data != null)
            {
                // 빈 Roster는 전체 검증을 막으므로 현재 선택 유닛이 있으면 첫 항목으로 사용한다.
                roster.unitIds.Add(_selectedUnit.data.unitId);
            }

            AssetDatabase.CreateAsset(roster, path);
            Undo.RegisterCreatedObjectUndo(roster, "Create Ally Unit Roster");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshAssets(GetPath(_selectedUnit), path);
            _tabIndex = (int)StudioTab.Rosters;
        }

        private void SaveAll()
        {
            _serializedRoster?.ApplyModifiedProperties();
            if (_selectedRoster != null && !HasLabel(_selectedRoster, GeneratedOperatorLabel))
            {
                EditorUtility.SetDirty(_selectedRoster);
            }

            string unitPath = GetPath(_selectedUnit);
            string rosterPath = GetPath(_selectedRoster);
            SaveRecipeToDisk();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshAssets(unitPath, rosterPath);
            Debug.Log("[AllyUnitStudio] 아군 유닛 데이터셋 저장 완료");
        }

        private void SaveCurrentRecipe()
        {
            if (_recipe == null || string.IsNullOrWhiteSpace(_selectedRecipePath))
            {
                return;
            }

            SaveRecipeToDisk();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshAssets(GetPath(_selectedUnit), GetPath(_selectedRoster));
            Debug.Log($"[AllyUnitStudio] 레시피 저장 완료: {_recipe.unitId}");
        }

        private void SaveRecipeToDisk()
        {
            if (_recipe == null || string.IsNullOrWhiteSpace(_selectedRecipePath))
            {
                return;
            }

            EnsureRecipeData();
            _recipe.data.unitId = _recipe.unitId;
            _recipe.data.displayName = _recipe.displayName;
            File.WriteAllText(
                Path.GetFullPath(_selectedRecipePath),
                JsonUtility.ToJson(_recipe, true),
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(_selectedRecipePath, ImportAssetOptions.ForceUpdate);
        }

        private static void WriteRecipeFile(AllyUnitAssetRecipe recipe, string path)
        {
            File.WriteAllText(
                Path.GetFullPath(path),
                JsonUtility.ToJson(recipe, true),
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void BuildRecipe(string recipePath)
        {
            try
            {
                AllyUnitBuildReport report = AllyUnitAssetBuilder.BuildSingle(recipePath);
                string changes = report.changedAssets.Count == 0
                    ? "이미 최신 상태입니다. 다시 쓴 에셋 없음."
                    : $"갱신된 에셋 {report.changedAssets.Count}개:\n· " +
                      string.Join("\n· ", report.changedAssets);
                EditorUtility.DisplayDialog(
                    $"Build {report.unitId}",
                    $"{changes}\n\n{(report.validationPassed ? "검증 통과" : "검증 실패 — Console을 확인하세요.")}",
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Ally Unit Build", exception.Message, "확인");
            }
        }

        private void BuildSelectedUnit()
        {
            if (_recipe == null || string.IsNullOrWhiteSpace(_selectedRecipePath))
            {
                return;
            }

            SaveCurrentRecipe();
            BuildRecipe(_selectedRecipePath);
            RefreshAssets(GetPath(_selectedUnit), GetPath(_selectedRoster));
        }

        private void BuildAllUnits()
        {
            try
            {
                AllyUnitAssetBuilder.BuildAll();
                EditorUtility.DisplayDialog(
                    "Ally Unit Build",
                    "Definition, Catalog, Addressables 갱신 완료",
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Ally Unit Build", exception.Message, "확인");
            }
        }

        private void ValidateAllyUnitAssets()
        {
            try
            {
                SaveCurrentRecipe();
                bool valid = AllyUnitAssetValidator.ValidateAll();
                EditorUtility.DisplayDialog(
                    "Ally Unit Validation",
                    valid ? "검증 통과" : "검증 실패 — Console을 확인하세요.",
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Ally Unit Validation", exception.Message, "확인");
            }
        }

        private void ValidateAllAssets()
        {
            try
            {
                SaveAll();
                bool valid = AllyUnitAssetValidator.ValidateAll(false) &&
                             OperatorAssetValidator.ValidateAll();
                EditorUtility.DisplayDialog(
                    valid ? "Ally Unit Validation" : "Ally Unit Validation Failed",
                    valid ? "전체 오퍼레이터·유닛 에셋 검증을 통과했습니다." : "검증 실패 — Console을 확인하세요.",
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Ally Unit Validation Failed", exception.Message, "확인");
            }
        }

        private void HandleUndoRedo()
        {
            RefreshAssets(GetPath(_selectedUnit), GetPath(_selectedRoster));
        }

        private string GetDefaultFolder()
        {
            string selectedPath = GetPath(_selectedUnit);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                selectedPath = GetPath(_selectedRoster);
            }

            string folder = Path.GetDirectoryName(selectedPath)?.Replace('\\', '/');
            return string.IsNullOrWhiteSpace(folder) ? DefaultAssetRoot : folder;
        }

        private static bool EnsureNewAssetPath(string path, string typeName)
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null)
            {
                return true;
            }

            EditorUtility.DisplayDialog($"이미 존재하는 {typeName}", path, "확인");
            return false;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "new-ally-unit"
                : value.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        private void EnsureRecipeData()
        {
            if (_recipe == null)
            {
                return;
            }

            _recipe.data ??= new AllyUnitData();
            _recipe.effectPaths ??= new List<string>();
            _recipe.visualEffectPaths ??= new List<string>();
        }

        private void SwapRecipeEffects(int left, int right)
        {
            EnsureRecipeData();
            if (left < 0 || right < 0 ||
                left >= _recipe.effectPaths.Count || right >= _recipe.effectPaths.Count)
            {
                return;
            }

            string temporary = _recipe.effectPaths[left];
            _recipe.effectPaths[left] = _recipe.effectPaths[right];
            _recipe.effectPaths[right] = temporary;
        }

        private static string DrawRecipeAssetPathField<T>(string label, string path)
            where T : UnityEngine.Object
        {
            T current = string.IsNullOrWhiteSpace(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<T>(path);
            T selected = (T)EditorGUILayout.ObjectField(label, current, typeof(T), false);
            return selected == current
                ? path
                : selected == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(selected);
        }

        private static string GetUnitLabel(AllyUnitDefinition definition)
        {
            if (definition == null)
            {
                return "<Missing Unit>";
            }

            string displayName = definition.data?.displayName;
            string unitId = definition.data?.unitId;
            if (string.IsNullOrWhiteSpace(displayName)) { displayName = definition.name; }
            return string.IsNullOrWhiteSpace(unitId) ? displayName : $"{displayName}  /  {unitId}";
        }

        private AllyUnitDefinition FindUnitById(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            for (int i = 0; i < _units.Count; i++)
            {
                AllyUnitDefinition definition = _units[i];
                if (definition != null && definition.data != null &&
                    string.Equals(definition.data.unitId, unitId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private static string GetRosterLabel(AllyUnitRoster roster)
        {
            if (roster == null)
            {
                return "<Missing Roster>";
            }

            int count = roster.unitIds?.Count ?? 0;
            string suffix = HasLabel(roster, GeneratedOperatorLabel) ? "  [generated]" : string.Empty;
            return $"{roster.name}  ({count}){suffix}";
        }

        private static bool MatchesSearch(string value, string search)
        {
            return string.IsNullOrWhiteSpace(search) ||
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasLabel(UnityEngine.Object asset, string label)
        {
            return asset != null && Array.IndexOf(AssetDatabase.GetLabels(asset), label) >= 0;
        }

        private static string GetPath(UnityEngine.Object asset)
        {
            return asset != null ? AssetDatabase.GetAssetPath(asset) : null;
        }

        private static bool RosterContains(AllyUnitRoster roster, AllyUnitDefinition definition)
        {
            return roster != null && roster.unitIds != null && definition != null &&
                   definition.data != null && roster.unitIds.Contains(definition.data.unitId);
        }

        private static bool SerializedArrayContains(SerializedProperty array, UnityEngine.Object value)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SerializedArrayContainsString(SerializedProperty array, string value)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).stringValue == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static void InsertObjectReference(SerializedProperty array, UnityEngine.Object value)
        {
            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            array.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }

        private static void InsertStringValue(SerializedProperty array, string value)
        {
            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            array.GetArrayElementAtIndex(index).stringValue = value ?? string.Empty;
        }

        private static void RemoveArrayElement(SerializedProperty array, int index)
        {
            int previousSize = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            // Unity 버전에 따라 ObjectReference 배열은 첫 호출에서 참조만 null로 만들 수 있다.
            if (array.arraySize == previousSize)
            {
                array.DeleteArrayElementAtIndex(index);
            }
        }
    }
}
