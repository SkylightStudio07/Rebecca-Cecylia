using System;
using System.Collections.Generic;
using System.IO;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Stage;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// StageDefinition 제작의 단일 진입점. SO가 웨이브와 Sprite 참조를 자연스럽게 보존하므로
    /// 오퍼레이터의 JSON 레시피를 복제하지 않고 Definition 자체를 원본으로 편집한다.
    /// </summary>
    public sealed class StageStudioWindow : EditorWindow
    {
        private const string StageRoot = "Assets/Data/Stages";

        private readonly List<string> _stagePaths = new();
        private readonly List<string> _stageLabels = new();
        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;
        private int _selectedIndex = -1;
        private int _tabIndex;
        private StageDefinition _stage;
        private SerializedObject _serializedStage;

        private enum StudioTab
        {
            Identity,
            Map,
            Waves,
            Rewards,
            Publish,
        }

        [MenuItem("RCCom/Stages/Open Stage Studio")]
        public static void Open()
        {
            Vector2 size = new Vector2(1080f, 780f);
            StageStudioWindow[] existing = Resources.FindObjectsOfTypeAll<StageStudioWindow>();
            for (int i = 0; i < existing.Length; i++) { existing[i].Close(); }

            StageStudioWindow window = GetWindowWithRect<StageStudioWindow>(
                new Rect(100f, 80f, size.x, size.y), true, "Stage Studio", true);
            window.minSize = new Vector2(820f, 580f);
            window.ShowUtility();
            window.Focus();
        }

        private void OnEnable()
        {
            RefreshStages(null);
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_stage == null || _serializedStage == null)
            {
                EditorGUILayout.HelpBox("StageDefinition이 없습니다. New Stage로 첫 스테이지를 만드세요.", MessageType.Info);
                if (GUILayout.Button("New Stage", GUILayout.Height(30))) { CreateNewStage(); }
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawSidebar();
            DrawContent();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshStages(_stage != null ? AssetDatabase.GetAssetPath(_stage) : null);
            }

            GUILayout.Label("RCCom / Stage Studio", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("New Stage", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                CreateNewStage();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(230f));
            GUILayout.Label("Stages", EditorStyles.boldLabel);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);
            for (int i = 0; i < _stagePaths.Count; i++)
            {
                GUIStyle style = i == _selectedIndex
                    ? new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold }
                    : EditorStyles.toolbarButton;
                if (GUILayout.Button(_stageLabels[i], style, GUILayout.Height(26f))) { LoadStage(i); }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawContent()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _tabIndex = GUILayout.Toolbar(_tabIndex,
                new[] { "Identity", "Map", "Waves", "Rewards", "Publish" }, EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            _serializedStage.Update();
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            switch ((StudioTab)_tabIndex)
            {
                case StudioTab.Identity:
                    DrawIdentityTab();
                    break;
                case StudioTab.Map:
                    DrawMapTab();
                    break;
                case StudioTab.Waves:
                    DrawWavesTab();
                    break;
                case StudioTab.Rewards:
                    DrawRewardsTab();
                    break;
                case StudioTab.Publish:
                    DrawPublishTab();
                    break;
            }
            EditorGUILayout.EndScrollView();

            if (_serializedStage.ApplyModifiedProperties())
            {
                _stage.schemaVersion = StageDefinition.CurrentSchemaVersion;
                EditorUtility.SetDirty(_stage);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawIdentityTab()
        {
            GUILayout.Label("Stage Identity", EditorStyles.boldLabel);
            DrawProperty("stageId", "Stage ID");
            EditorGUILayout.HelpBox(
                "Stage ID는 세이브와 향후 Addressables 주소에 쓰일 영구 식별자입니다. 배포 후에는 변경하지 마세요.",
                MessageType.None);
            DrawProperty("chapterId", "Chapter ID");
            DrawProperty("displayName", "Display Name");
            DrawProperty("subtitle", "Subtitle");
            DrawProperty("recommendedLevel", "Recommended Level");
            DrawProperty("order", "Chapter Order");
            DrawProperty("requiredBestWave", "Required Best Wave");

            GUILayout.Space(12f);
            GUILayout.Label("Mission Briefing", EditorStyles.boldLabel);
            DrawProperty("description", "Description", true);
            DrawProperty("descriptionBackground", "Description Background");
            DrawSpritePreview("descriptionBackground", 3.2f);
            DrawSaveButton();
        }

        private void DrawMapTab()
        {
            GUILayout.Label("Battlefield Authoring", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "첫 Route Point는 적 생성점, 마지막 점은 거점 및 아군 출격점입니다. " +
                "좌표는 Test Scene에서 배치한 뒤 Capture하는 방식을 권장합니다.",
                MessageType.Info);

            DrawProperty("battleBackground", "Battle Background");
            DrawProperty("battleBackgroundPosition", "Background Position");
            DrawProperty("battleBackgroundScale", "Background Scale");
            DrawSpritePreview("battleBackground", 2.2f);

            GUILayout.Space(12f);
            DrawProperty("pathSmoothness", "Path Smoothness");
            DrawProperty("maxPointSpacing", "Max Point Spacing");
            DrawProperty("routePoints", "Route Points", true);

            GUILayout.Space(12f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("DefenseScene Route Workspace", EditorStyles.boldLabel);
            if (GUILayout.Button("Open Test Scene & Load Selected Stage", GUILayout.Height(32f)))
            {
                SaveCurrent(false);
                StageRouteAuthoringTool.OpenAndLoad(_stage);
            }
            if (GUILayout.Button("Reload Selected Stage Into Test Scene", GUILayout.Height(28f)))
            {
                SaveCurrent(false);
                StageRouteAuthoringTool.LoadIntoOpenTestScene(_stage);
            }
            if (GUILayout.Button("Capture Test Scene Into Selected Stage", GUILayout.Height(32f)))
            {
                StageRouteAuthoringTool.CaptureFromOpenTestScene(_stage);
                _serializedStage.Update();
                Repaint();
            }
            EditorGUILayout.EndVertical();
            DrawSaveButton();
        }

        private void DrawWavesTab()
        {
            SerializedProperty waves = _serializedStage.FindProperty("waves");
            GUILayout.Label($"Wave Composition  /  {waves.arraySize} waves", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "한 웨이브 안의 Spawn 항목은 위에서부터 큐에 들어갑니다. Initial Delay는 해당 편성 시작 전 대기 시간입니다.",
                MessageType.Info);

            for (int waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
            {
                SerializedProperty wave = waves.GetArrayElementAtIndex(waveIndex);
                string title = wave.FindPropertyRelative("displayName").stringValue;
                if (string.IsNullOrWhiteSpace(title)) { title = $"WAVE {waveIndex + 1:00}"; }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                wave.isExpanded = EditorGUILayout.Foldout(wave.isExpanded, $"{waveIndex + 1:00}  {title}", true);
                if (wave.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("displayName"), new GUIContent("Display Name"));
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("buildPhaseDuration"), new GUIContent("Build Phase Seconds"));
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("healthMultiplier"), new GUIContent("Enemy Health Multiplier"));
                    DrawSpawns(wave.FindPropertyRelative("spawns"));
                    EditorGUI.indentLevel--;

                    if (GUILayout.Button("Remove This Wave"))
                    {
                        waves.DeleteArrayElementAtIndex(waveIndex);
                        EditorGUILayout.EndVertical();
                        break;
                    }
                }
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
            }

            if (GUILayout.Button("+ Add Wave", GUILayout.Height(30f))) { AddWave(waves); }
            DrawSaveButton();
        }

        private static void DrawSpawns(SerializedProperty spawns)
        {
            GUILayout.Space(6f);
            GUILayout.Label($"Enemy Spawns  /  {spawns.arraySize}", EditorStyles.boldLabel);
            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(EnemyCatalogBuilder.CatalogPath);
            for (int spawnIndex = 0; spawnIndex < spawns.arraySize; spawnIndex++)
            {
                SerializedProperty spawn = spawns.GetArrayElementAtIndex(spawnIndex);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Spawn {spawnIndex + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    spawns.DeleteArrayElementAtIndex(spawnIndex);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                DrawEnemyIdField(spawn.FindPropertyRelative("enemyId"), catalog);
                EditorGUILayout.PropertyField(spawn.FindPropertyRelative("count"), new GUIContent("Count"));
                EditorGUILayout.PropertyField(spawn.FindPropertyRelative("interval"), new GUIContent("Spawn Interval"));
                EditorGUILayout.PropertyField(spawn.FindPropertyRelative("initialDelay"), new GUIContent("Initial Delay"));
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Enemy Spawn")) { AddSpawn(spawns); }
        }

        /// <summary>
        /// EnemyCatalog가 있으면 등록된 enemyId 중에서 고르는 팝업으로 보여준다. 카탈로그
        /// 에셋 자체가 아직 없는 초기 상태(레시피/빌드 전)에는 팝업을 만들 수 없으므로 예외
        /// 대신 일반 텍스트 필드로 저하시켜, 이 창이 그 단계에서도 계속 열리게 한다.
        /// </summary>
        private static void DrawEnemyIdField(SerializedProperty enemyIdProperty, EnemyCatalog catalog)
        {
            if (catalog == null || catalog.entries == null || catalog.entries.Count == 0)
            {
                EditorGUILayout.PropertyField(enemyIdProperty, new GUIContent("Enemy ID"));
                EditorGUILayout.HelpBox("EnemyCatalog를 찾을 수 없어 직접 입력 모드로 표시합니다.", MessageType.None);
                return;
            }

            var ids = new List<string>();
            var labels = new List<string>();
            foreach (EnemyCatalogEntry entry in catalog.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.enemyId))
                {
                    continue;
                }

                ids.Add(entry.enemyId);
                labels.Add(string.IsNullOrWhiteSpace(entry.displayName)
                    ? entry.enemyId
                    : $"{entry.displayName} ({entry.enemyId})");
            }

            int currentIndex = ids.IndexOf(enemyIdProperty.stringValue);
            int selectedIndex = EditorGUILayout.Popup("Enemy ID", currentIndex, labels.ToArray());
            if (selectedIndex >= 0 && selectedIndex < ids.Count)
            {
                enemyIdProperty.stringValue = ids[selectedIndex];
            }
        }

        private void DrawRewardsTab()
        {
            SerializedProperty rewards = _serializedStage.FindProperty("rewards");
            GUILayout.Label($"Clear Rewards  /  {rewards.arraySize}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "현재는 표시용 보상 매니페스트입니다. 실제 지급은 계정 재화·인벤토리 시스템 확정 후 Reward ID로 연결합니다.",
                MessageType.Info);

            for (int rewardIndex = 0; rewardIndex < rewards.arraySize; rewardIndex++)
            {
                SerializedProperty reward = rewards.GetArrayElementAtIndex(rewardIndex);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Reward {rewardIndex + 1}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    rewards.DeleteArrayElementAtIndex(rewardIndex);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(reward.FindPropertyRelative("rewardId"), new GUIContent("Reward ID"));
                EditorGUILayout.PropertyField(reward.FindPropertyRelative("displayName"), new GUIContent("Display Name"));
                EditorGUILayout.PropertyField(reward.FindPropertyRelative("icon"), new GUIContent("Icon"));
                EditorGUILayout.PropertyField(reward.FindPropertyRelative("amount"), new GUIContent("Amount"));
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Reward", GUILayout.Height(28f))) { AddReward(rewards); }
            DrawSaveButton();
        }

        private void DrawPublishTab()
        {
            GUILayout.Label("Save, Validate & Catalog", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "StageDefinition이 제작 원본이며 StageCatalog는 Build 시 자동 갱신되는 결과물입니다.",
                MessageType.Info);
            EditorGUILayout.LabelField("Definition", AssetDatabase.GetAssetPath(_stage));
            EditorGUILayout.LabelField("Catalog", StageCatalogBuilder.CatalogPath);
            EditorGUILayout.LabelField("Waves", _stage.waves != null ? _stage.waves.Count.ToString() : "0");
            EditorGUILayout.LabelField("Rewards", _stage.rewards != null ? _stage.rewards.Count.ToString() : "0");

            GUILayout.Space(12f);
            if (GUILayout.Button("Save Current Stage", GUILayout.Height(30f))) { SaveCurrent(false); }
            if (GUILayout.Button("Validate Current Stage", GUILayout.Height(28f))) { ValidateCurrent(); }
            if (GUILayout.Button("Validate All Stages", GUILayout.Height(28f))) { ValidateAll(); }
            if (GUILayout.Button("Save & Rebuild Stage Catalog", GUILayout.Height(34f))) { SaveCurrent(true); }
        }

        private void DrawSpritePreview(string propertyName, float aspect)
        {
            Sprite sprite = _serializedStage.FindProperty(propertyName).objectReferenceValue as Sprite;
            if (sprite == null) { return; }
            Texture2D preview = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
            if (preview == null) { return; }

            Rect rect = GUILayoutUtility.GetAspectRect(aspect, GUILayout.MaxHeight(220f));
            EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.ScaleToFit);
        }

        private void DrawProperty(string propertyName, string label, bool includeChildren = false)
        {
            EditorGUILayout.PropertyField(_serializedStage.FindProperty(propertyName), new GUIContent(label), includeChildren);
        }

        private void DrawSaveButton()
        {
            GUILayout.Space(10f);
            if (GUILayout.Button("Save Current Stage", GUILayout.Height(28f))) { SaveCurrent(false); }
        }

        private void RefreshStages(string preferredPath)
        {
            _stagePaths.Clear();
            _stageLabels.Clear();
            string[] guids = AssetDatabase.FindAssets("t:StageDefinition", new[] { StageRoot });
            foreach (string guid in guids) { _stagePaths.Add(AssetDatabase.GUIDToAssetPath(guid)); }
            _stagePaths.Sort(StringComparer.Ordinal);

            foreach (string path in _stagePaths)
            {
                StageDefinition definition = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
                string label = definition == null || string.IsNullOrWhiteSpace(definition.displayName)
                    ? Path.GetFileNameWithoutExtension(path)
                    : $"{definition.displayName}  /  {definition.subtitle}";
                _stageLabels.Add(label);
            }

            if (_stagePaths.Count == 0)
            {
                _selectedIndex = -1;
                _stage = null;
                _serializedStage = null;
                return;
            }

            int index = string.IsNullOrWhiteSpace(preferredPath) ? 0 : _stagePaths.IndexOf(preferredPath);
            LoadStage(index >= 0 ? index : 0);
        }

        private void LoadStage(int index)
        {
            if (index < 0 || index >= _stagePaths.Count) { return; }
            _selectedIndex = index;
            _stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(_stagePaths[index]);
            _serializedStage = _stage != null ? new SerializedObject(_stage) : null;
            _contentScroll = Vector2.zero;
            Repaint();
        }

        private void CreateNewStage()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "새 스테이지 Definition", "ch1-06.asset", "asset",
                "Assets/Data/Stages 아래에 영문 소문자 ID로 저장하세요.", StageRoot);
            if (string.IsNullOrWhiteSpace(path)) { return; }
            if (AssetDatabase.LoadAssetAtPath<StageDefinition>(path) != null)
            {
                EditorUtility.DisplayDialog("이미 존재하는 Stage", path, "확인");
                return;
            }

            string stageId = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            StageDefinition definition = CreateInstance<StageDefinition>();
            definition.schemaVersion = StageDefinition.CurrentSchemaVersion;
            definition.stageId = stageId;
            definition.chapterId = stageId.Contains("-") ? stageId.Substring(0, stageId.IndexOf('-')) : "ch1";
            definition.displayName = stageId;
            definition.subtitle = stageId.ToUpperInvariant();
            definition.waves.Add(new RCCom.Data.StageWaveDefinition { displayName = "WAVE 01" });
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshStages(path);
        }

        private void SaveCurrent(bool rebuildCatalog)
        {
            if (_serializedStage == null || _stage == null) { return; }
            _serializedStage.ApplyModifiedProperties();
            _stage.schemaVersion = StageDefinition.CurrentSchemaVersion;
            EditorUtility.SetDirty(_stage);
            AssetDatabase.SaveAssets();
            if (rebuildCatalog) { StageCatalogBuilder.BuildCatalog(); }
            AssetDatabase.Refresh();
            RefreshStages(AssetDatabase.GetAssetPath(_stage));
            Debug.Log($"[StageStudio] 저장 완료: {_stage.stageId}");
        }

        private void ValidateCurrent()
        {
            SaveCurrent(false);
            bool valid = StageAssetValidator.ValidateCurrent(_stage, out string report);
            EditorUtility.DisplayDialog(valid ? "Stage Validation" : "Stage Validation Failed", report, "확인");
        }

        private void ValidateAll()
        {
            SaveCurrent(false);
            bool valid = StageAssetValidator.ValidateAll(out string report);
            EditorUtility.DisplayDialog(valid ? "Stage Validation" : "Stage Validation Failed", report, "확인");
        }

        private static void AddWave(SerializedProperty waves)
        {
            int index = waves.arraySize;
            waves.InsertArrayElementAtIndex(index);
            SerializedProperty wave = waves.GetArrayElementAtIndex(index);
            wave.FindPropertyRelative("displayName").stringValue = $"WAVE {index + 1:00}";
            wave.FindPropertyRelative("buildPhaseDuration").floatValue = 3f;
            wave.FindPropertyRelative("healthMultiplier").floatValue = 1f;
            wave.FindPropertyRelative("spawns").arraySize = 0;
            wave.isExpanded = true;
        }

        private static void AddSpawn(SerializedProperty spawns)
        {
            int index = spawns.arraySize;
            spawns.InsertArrayElementAtIndex(index);
            SerializedProperty spawn = spawns.GetArrayElementAtIndex(index);
            spawn.FindPropertyRelative("enemyId").stringValue = string.Empty;
            spawn.FindPropertyRelative("count").intValue = 1;
            spawn.FindPropertyRelative("interval").floatValue = 1f;
            spawn.FindPropertyRelative("initialDelay").floatValue = 0f;
        }

        private static void AddReward(SerializedProperty rewards)
        {
            int index = rewards.arraySize;
            rewards.InsertArrayElementAtIndex(index);
            SerializedProperty reward = rewards.GetArrayElementAtIndex(index);
            reward.FindPropertyRelative("rewardId").stringValue = string.Empty;
            reward.FindPropertyRelative("displayName").stringValue = string.Empty;
            reward.FindPropertyRelative("icon").objectReferenceValue = null;
            reward.FindPropertyRelative("amount").intValue = 1;
        }
    }
}
