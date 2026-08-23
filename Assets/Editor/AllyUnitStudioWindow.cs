using System;
using System.Collections.Generic;
using System.IO;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Unit;
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
        private const string EffectsPropertyPath = "effects";
        private const string UnitsPropertyPath = "units";
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
        private SerializedObject _serializedUnit;
        private SerializedObject _serializedRoster;
        private GUIStyle _selectedSidebarButtonStyle;
        private GUIStyle _pathStyle;

        private enum StudioTab
        {
            Units,
            Rosters,
            Audit,
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

            if ((StudioTab)_tabIndex == StudioTab.Audit)
            {
                _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
                DrawAuditTab();
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
                new[] { $"Units ({_units.Count})", $"Rosters ({_rosters.Count})", "Audit" },
                EditorStyles.toolbarButton,
                GUILayout.Width(310f));
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
                DrawUnitEditor();
            }
            else
            {
                DrawRosterEditor();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawUnitEditor()
        {
            if (_selectedUnit == null || _serializedUnit == null)
            {
                EditorGUILayout.HelpBox(
                    "AllyUnitDefinition이 없습니다. New Unit으로 첫 유닛 데이터를 만드세요.",
                    MessageType.Info);
                return;
            }

            _serializedUnit.Update();
            DrawAssetHeader(_selectedUnit, "Unit Definition");
            DrawGeneratedAssetNotice(_selectedUnit, false);
            DrawIssueSummary(_unitIssues);

            SerializedProperty data = _serializedUnit.FindProperty("data");
            GUILayout.Label("Identity & Deployment", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(data.FindPropertyRelative("unitId"), new GUIContent("Unit ID"));
            EditorGUILayout.PropertyField(data.FindPropertyRelative("displayName"), new GUIContent("Display Name"));
            EditorGUILayout.PropertyField(data.FindPropertyRelative("deployCost"), new GUIContent("Deploy Cost"));
            EditorGUILayout.HelpBox(
                "Unit ID는 Roster 내부 조회에 쓰이는 영구 식별자입니다. 같은 Roster 안에서는 중복될 수 없습니다.",
                MessageType.None);

            GUILayout.Space(10f);
            GUILayout.Label("Combat Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(data.FindPropertyRelative("maxHealth"), new GUIContent("Max Health"));
            EditorGUILayout.PropertyField(data.FindPropertyRelative("moveSpeed"), new GUIContent("Move Speed"));
            EditorGUILayout.PropertyField(data.FindPropertyRelative("attackDamage"), new GUIContent("Attack Damage"));
            EditorGUILayout.PropertyField(data.FindPropertyRelative("attackInterval"), new GUIContent("Attack Interval"));
            EditorGUILayout.PropertyField(data.FindPropertyRelative("attackRange"), new GUIContent("Attack Range"));
            EditorGUILayout.PropertyField(data.FindPropertyRelative("detectionRange"), new GUIContent("Detection Range"));
            EditorGUILayout.PropertyField(data.FindPropertyRelative("projectileSpeed"), new GUIContent("Projectile Speed"));

            GUILayout.Space(10f);
            GUILayout.Label("Presentation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serializedUnit.FindProperty("sprite"), new GUIContent("Sprite"));
            EditorGUILayout.PropertyField(_serializedUnit.FindProperty("tint"), new GUIContent("Tint"));
            EditorGUILayout.PropertyField(
                _serializedUnit.FindProperty("spriteForwardOffsetDegrees"),
                new GUIContent("Forward Offset Degrees"));
            DrawSpritePreview(_serializedUnit.FindProperty("sprite").objectReferenceValue as Sprite);

            GUILayout.Space(10f);
            DrawEffects(_serializedUnit.FindProperty("effects"));
            DrawUnitUsage(_selectedUnit);
            DrawIssueDetails(_unitIssues);

            GUILayout.Space(12f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Unit", GUILayout.Height(30f)))
            {
                SaveAll();
            }

            if (GUILayout.Button("Duplicate Unit", GUILayout.Height(30f)))
            {
                DuplicateSelectedUnit();
            }

            if (GUILayout.Button("Ping Asset", GUILayout.Height(30f)))
            {
                Selection.activeObject = _selectedUnit;
                EditorGUIUtility.PingObject(_selectedUnit);
            }

            EditorGUILayout.EndHorizontal();

            if (_serializedUnit.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selectedUnit);
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

            SerializedProperty units = _serializedRoster.FindProperty("units");
            GUILayout.Label($"Roster Entries  /  {units.arraySize}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "위에서 아래 순서가 선택 화면과 전투 배치 메뉴의 버튼 순서입니다.",
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
                AllyUnitDefinition definition = element.objectReferenceValue as AllyUnitDefinition;
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
                        SelectUnit(definition);
                        _tabIndex = (int)StudioTab.Units;
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
                if (!SerializedArrayContains(rosterUnits, _units[i]))
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
                if (roster == null || roster.units == null)
                {
                    continue;
                }

                for (int unitIndex = 0; unitIndex < roster.units.Count; unitIndex++)
                {
                    AllyUnitDefinition definition = roster.units[unitIndex];
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

            return issues;
        }

        private static List<string> CollectRosterIssues(AllyUnitRoster roster)
        {
            var issues = new List<string>();
            if (roster == null || roster.units == null)
            {
                issues.Add("Roster 목록이 null입니다.");
                return issues;
            }

            if (roster.units.Count == 0)
            {
                issues.Add("Roster가 비어 있습니다.");
                return issues;
            }

            var unitIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < roster.units.Count; i++)
            {
                AllyUnitDefinition definition = roster.units[i];
                if (definition == null)
                {
                    issues.Add($"Slot {i + 1}이 null입니다.");
                    continue;
                }

                string unitId = definition.data?.unitId;
                if (string.IsNullOrWhiteSpace(unitId))
                {
                    issues.Add($"Slot {i + 1}의 Unit ID가 비어 있습니다.");
                }
                else if (!unitIds.Add(unitId))
                {
                    issues.Add($"Unit ID가 중복됩니다: {unitId}");
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
                    InsertObjectReference(array, edit.value);
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
        }

        private void SelectRoster(AllyUnitRoster roster)
        {
            _selectedRoster = roster;
            _serializedRoster = roster != null ? new SerializedObject(roster) : null;
        }

        private void CreateNewUnit()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "새 아군 유닛 Definition",
                "new-ally-unit.asset",
                "asset",
                "유닛 데이터 원본을 저장할 위치를 선택하세요.",
                GetDefaultFolder());
            if (string.IsNullOrWhiteSpace(path) || !EnsureNewAssetPath(path, "AllyUnitDefinition"))
            {
                return;
            }

            string assetName = Path.GetFileNameWithoutExtension(path);
            AllyUnitDefinition definition = CreateInstance<AllyUnitDefinition>();
            definition.name = assetName;
            definition.data = new AllyUnitData
            {
                unitId = NormalizeId(assetName),
                displayName = assetName,
                deployCost = 10,
                maxHealth = 10f,
                moveSpeed = 1f,
                attackDamage = 1f,
                attackInterval = 1f,
                attackRange = 1f,
                detectionRange = 2f,
                projectileSpeed = 8f,
            };
            AssetDatabase.CreateAsset(definition, path);
            Undo.RegisterCreatedObjectUndo(definition, "Create Ally Unit Definition");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshAssets(path, GetPath(_selectedRoster));
            _tabIndex = (int)StudioTab.Units;
        }

        private void DuplicateSelectedUnit()
        {
            if (_selectedUnit == null)
            {
                return;
            }

            string sourcePath = GetPath(_selectedUnit);
            string sourceFolder = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string path = EditorUtility.SaveFilePanelInProject(
                "아군 유닛 Definition 복제",
                $"{_selectedUnit.name}-copy.asset",
                "asset",
                "효과와 Sprite 참조는 재사용하고 인라인 AllyUnitData는 복제합니다.",
                string.IsNullOrWhiteSpace(sourceFolder) ? DefaultAssetRoot : sourceFolder);
            if (string.IsNullOrWhiteSpace(path) || !EnsureNewAssetPath(path, "AllyUnitDefinition"))
            {
                return;
            }

            AllyUnitDefinition duplicate = Instantiate(_selectedUnit);
            string assetName = Path.GetFileNameWithoutExtension(path);
            duplicate.name = assetName;
            duplicate.data ??= new AllyUnitData();
            duplicate.data.unitId = NormalizeId(assetName);
            duplicate.data.displayName = assetName;
            AssetDatabase.CreateAsset(duplicate, path);
            Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate Ally Unit Definition");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshAssets(path, GetPath(_selectedRoster));
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
            if (_selectedUnit != null)
            {
                // 빈 Roster는 전체 검증을 막으므로 현재 선택 유닛이 있으면 첫 항목으로 사용한다.
                roster.units.Add(_selectedUnit);
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
            _serializedUnit?.ApplyModifiedProperties();
            _serializedRoster?.ApplyModifiedProperties();
            if (_selectedUnit != null) { EditorUtility.SetDirty(_selectedUnit); }
            if (_selectedRoster != null && !HasLabel(_selectedRoster, GeneratedOperatorLabel))
            {
                EditorUtility.SetDirty(_selectedRoster);
            }

            string unitPath = GetPath(_selectedUnit);
            string rosterPath = GetPath(_selectedRoster);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshAssets(unitPath, rosterPath);
            Debug.Log("[AllyUnitStudio] 아군 유닛 데이터셋 저장 완료");
        }

        private void ValidateAllAssets()
        {
            try
            {
                SaveAll();
                bool valid = OperatorAssetValidator.ValidateAll();
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

        private static string GetRosterLabel(AllyUnitRoster roster)
        {
            if (roster == null)
            {
                return "<Missing Roster>";
            }

            int count = roster.units?.Count ?? 0;
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
            return roster != null && roster.units != null && roster.units.Contains(definition);
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

        private static void InsertObjectReference(SerializedProperty array, UnityEngine.Object value)
        {
            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            array.GetArrayElementAtIndex(index).objectReferenceValue = value;
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
