using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RCCom.Data;
using RCCom.Definitions.Card;
using RCCom.Definitions.Operator;
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
            "operatorAcquired", "lobbyInteraction", "lobbyReturnTogether", "lobbyReturn",
            "lobbyTouchUnfamiliar", "lobbyTouchFavorable", "lobbyTouchJoy",
            "lobbyTouchLove", "lobbyTouchEx", "gameStart", "skillUsed",
            "baseAttacked", "playerHit", "playerHitCritical", "insufficientGold",
            "slotUnavailable", "playerDied", "baseDestroyed",
        };

        private static readonly string[] DialogueLabels =
        {
            "오퍼레이터 획득", "로비 클릭", "귀환·참전", "귀환·비참전",
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
            Dossier,
            Loadout,
            Dialogue,
            Upgrades,
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
                new[] { "Identity", "Dossier", "Loadout", "Dialogue", "Upgrades", "Package" },
                EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            switch ((StudioTab)_tabIndex)
            {
                case StudioTab.Identity:
                    DrawIdentityTab();
                    break;
                case StudioTab.Dossier:
                    DrawDossierTab();
                    break;
                case StudioTab.Loadout:
                    DrawLoadoutTab();
                    break;
                case StudioTab.Dialogue:
                    DrawDialogueTab();
                    break;
                case StudioTab.Upgrades:
                    DrawUpgradesTab();
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
            _recipe.catalogOrder = Mathf.Max(0, EditorGUILayout.IntField("Catalog Slot", _recipe.catalogOrder));
            _recipe.displayName = EditorGUILayout.TextField("Display Name", _recipe.displayName);
            _recipe.playStyleDescription = EditorGUILayout.TextArea(
                _recipe.playStyleDescription ?? string.Empty,
                GUILayout.MinHeight(48));
            _recipe.selectionPortraitPath = DrawAssetPathField<Sprite>(
                "Selection Portrait", _recipe.selectionPortraitPath);
            _recipe.managementPortraitPath = DrawAssetPathField<Sprite>(
                "Management Card Portrait", _recipe.managementPortraitPath);
            GUILayout.Space(6);
            GUILayout.Label("Recruit Shop", EditorStyles.boldLabel);
            _recipe.shopPortraitPath = DrawAssetPathField<Sprite>(
                "Shop Portrait", _recipe.shopPortraitPath);
            _recipe.shopUpperBodyPortraitPath = DrawAssetPathField<Sprite>(
                "Shop Upper-body Portrait", _recipe.shopUpperBodyPortraitPath);
            _recipe.shopUpperBodyPortraitDimmedPath = DrawAssetPathField<Sprite>(
                "Shop Upper-body Portrait (Dimmed)", _recipe.shopUpperBodyPortraitDimmedPath);
            _recipe.alternateName = EditorGUILayout.TextField(
                "Another Name", _recipe.alternateName ?? string.Empty);
            EditorGUILayout.LabelField("Shop Dialogue");
            _recipe.shopDialogue = EditorGUILayout.TextArea(
                _recipe.shopDialogue ?? string.Empty,
                GUILayout.MinHeight(44));
            _recipe.unlockRewardPortraitPath = DrawAssetPathField<Sprite>(
                "Stage Reward Portrait", _recipe.unlockRewardPortraitPath);
            EditorGUILayout.HelpBox(
                "Selection Portrait는 선택 화면용 머리 크롭, Management Card Portrait는 Operators 관리 카드용 전신·반신 이미지입니다. Shop 항목은 리크루트 화면 전용입니다.",
                MessageType.None);
            _recipe.remoteContent = EditorGUILayout.ToggleLeft("Remote Content", _recipe.remoteContent);
            GUILayout.Space(8);
            GUILayout.Label("Unlock Conditions (OR)", EditorStyles.boldLabel);
            _recipe.unlockConditions ??= new List<OperatorUnlockCondition>();
            int removeCondition = -1;
            for (int i = 0; i < _recipe.unlockConditions.Count; i++)
            {
                OperatorUnlockCondition condition = _recipe.unlockConditions[i];
                if (condition == null)
                {
                    _recipe.unlockConditions[i] = condition = new OperatorUnlockCondition();
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Condition {i + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(62))) { removeCondition = i; }
                EditorGUILayout.EndHorizontal();
                condition.type = (OperatorUnlockType)EditorGUILayout.EnumPopup("Type", condition.type);
                switch (condition.type)
                {
                    case OperatorUnlockType.BestWave:
                        condition.requiredBestWave = Mathf.Max(0,
                            EditorGUILayout.IntField("Required Best Wave", condition.requiredBestWave));
                        break;
                    case OperatorUnlockType.CommodityPurchase:
                        condition.purchasePrice = Mathf.Max(1,
                            EditorGUILayout.IntField("Purchase Price", condition.purchasePrice));
                        break;
                    case OperatorUnlockType.StageClearReward:
                        condition.requiredStageId = EditorGUILayout.TextField(
                            "Required Stage ID", condition.requiredStageId ?? string.Empty).Trim();
                        break;
                }
                EditorGUILayout.EndVertical();
            }

            if (removeCondition >= 0) { _recipe.unlockConditions.RemoveAt(removeCondition); }
            if (GUILayout.Button("+ Add Unlock Condition", GUILayout.Height(24)))
            {
                _recipe.unlockConditions.Add(new OperatorUnlockCondition());
            }
            EditorGUILayout.HelpBox("조건 중 하나만 충족해도 해금됩니다. 구매 조건이 있으면 Recruit 상점 목록에 표시됩니다.",
                MessageType.None);

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

        private void DrawUpgradesTab()
        {
            GUILayout.Label("Upgrade Tracks", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "오퍼레이터당 강화 트랙은 5개입니다. 각 트랙은 한 번의 구매로 여러 modifier를 " +
                "적용할 수 있고, 비용·호감도 조건·수치는 Lv1부터 순서대로 정확한 표로 저장됩니다.",
                MessageType.Info);

            _recipe.upgradeTracks ??= new List<OperatorUpgradeTrack>();
            int removeIndex = -1;
            for (int i = 0; i < _recipe.upgradeTracks.Count; i++)
            {
                OperatorUpgradeTrack track = _recipe.upgradeTracks[i];
                if (track == null)
                {
                    _recipe.upgradeTracks[i] = track = new OperatorUpgradeTrack();
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Track {i + 1}", EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(62)))
                {
                    removeIndex = i;
                }

                EditorGUILayout.EndHorizontal();

                track.trackId = EditorGUILayout.TextField("Track ID", track.trackId ?? string.Empty);
                track.displayName = EditorGUILayout.TextField("Display Name", track.displayName ?? string.Empty);
                track.description = EditorGUILayout.TextArea(track.description ?? string.Empty,
                    GUILayout.MinHeight(38));
                track.category = (OperatorUpgradeCategory)EditorGUILayout.EnumPopup("Category", track.category);
                track.maxLevel = Mathf.Max(1, EditorGUILayout.IntField("Max Level", track.maxLevel));
                track.levelCosts ??= new List<int>();
                track.requiredAffinityByLevel ??= new List<int>();
                EnsureIntListSize(track.levelCosts, track.maxLevel);
                EnsureIntListSize(track.requiredAffinityByLevel, track.maxLevel);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Level", EditorStyles.miniBoldLabel, GUILayout.Width(45));
                GUILayout.Label("Cost", EditorStyles.miniBoldLabel, GUILayout.Width(70));
                GUILayout.Label("Affinity", EditorStyles.miniBoldLabel, GUILayout.Width(70));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Default Costs", GUILayout.Width(95)))
                {
                    ApplyDefaultCosts(track);
                }
                EditorGUILayout.EndHorizontal();

                for (int levelIndex = 0; levelIndex < track.maxLevel; levelIndex++)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"Lv{levelIndex + 1}", GUILayout.Width(45));
                    track.levelCosts[levelIndex] = Mathf.Max(0,
                        EditorGUILayout.IntField(track.levelCosts[levelIndex], GUILayout.Width(70)));
                    track.requiredAffinityByLevel[levelIndex] = Mathf.Clamp(
                        EditorGUILayout.IntField(track.requiredAffinityByLevel[levelIndex], GUILayout.Width(70)),
                        0, PlayerProfile.MaxOperatorAffinity);
                    EditorGUILayout.EndHorizontal();
                }

                track.modifiers ??= new List<OperatorUpgradeModifier>();
                int removeModifier = -1;
                for (int modifierIndex = 0; modifierIndex < track.modifiers.Count; modifierIndex++)
                {
                    OperatorUpgradeModifier modifier = track.modifiers[modifierIndex];
                    if (modifier == null)
                    {
                        track.modifiers[modifierIndex] = modifier = new OperatorUpgradeModifier();
                    }

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"Modifier {modifierIndex + 1}", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("-", GUILayout.Width(24)))
                    {
                        removeModifier = modifierIndex;
                    }
                    EditorGUILayout.EndHorizontal();

                    modifier.targetKind = (OperatorUpgradeTargetKind)EditorGUILayout.EnumPopup(
                        "Target Kind", modifier.targetKind);
                    modifier.targetUnitId = EditorGUILayout.TextField(
                        "Target Unit ID", modifier.targetUnitId ?? string.Empty);
                    modifier.isInteger = EditorGUILayout.ToggleLeft("Integer Result", modifier.isInteger);
                    EnsureFloatListSize(modifier.levelDeltas, track.maxLevel);
                    for (int levelIndex = 0; levelIndex < track.maxLevel; levelIndex++)
                    {
                        modifier.levelDeltas[levelIndex] = EditorGUILayout.FloatField(
                            $"Lv{levelIndex + 1} Delta", modifier.levelDeltas[levelIndex]);
                    }

                    EditorGUILayout.BeginHorizontal();
                    modifier.hasMinValue = EditorGUILayout.ToggleLeft("Min", modifier.hasMinValue,
                        GUILayout.Width(50));
                    using (new EditorGUI.DisabledGroupScope(!modifier.hasMinValue))
                    {
                        modifier.minValue = EditorGUILayout.FloatField(modifier.minValue);
                    }
                    modifier.hasMaxValue = EditorGUILayout.ToggleLeft("Max", modifier.hasMaxValue,
                        GUILayout.Width(50));
                    using (new EditorGUI.DisabledGroupScope(!modifier.hasMaxValue))
                    {
                        modifier.maxValue = EditorGUILayout.FloatField(modifier.maxValue);
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }

                if (removeModifier >= 0)
                {
                    track.modifiers.RemoveAt(removeModifier);
                }
                if (GUILayout.Button("+ Add Modifier"))
                {
                    var modifier = new OperatorUpgradeModifier();
                    EnsureFloatListSize(modifier.levelDeltas, track.maxLevel);
                    track.modifiers.Add(modifier);
                }
                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                _recipe.upgradeTracks.RemoveAt(removeIndex);
            }

            GUILayout.Space(6);
            if (GUILayout.Button("+ Add Track", GUILayout.Height(26)))
            {
                _recipe.upgradeTracks.Add(new OperatorUpgradeTrack
                {
                    trackId = $"{_recipe.operatorId}.new-track-{_recipe.upgradeTracks.Count + 1}",
                });
                OperatorUpgradeTrack added = _recipe.upgradeTracks[^1];
                ApplyDefaultCosts(added);
                EnsureIntListSize(added.requiredAffinityByLevel, added.maxLevel);
                var modifier = new OperatorUpgradeModifier();
                EnsureFloatListSize(modifier.levelDeltas, added.maxLevel);
                added.modifiers.Add(modifier);
            }

            GUILayout.Space(12);
            DrawSaveButton();
        }

        private void DrawDossierTab()
        {
            GUILayout.Label("Operator Dossier", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "프로필 항목과 인연 기록은 OperatorDefinition에 포함되어 Addressables 콘텐츠 갱신만으로 교체됩니다.",
                MessageType.Info);

            _recipe.codename = EditorGUILayout.TextField("Codename", _recipe.codename ?? string.Empty);
            _recipe.role = EditorGUILayout.TextField("Role", _recipe.role ?? string.Empty);
            _recipe.faction = EditorGUILayout.TextField("Faction", _recipe.faction ?? string.Empty);
            _recipe.height = EditorGUILayout.TextField("Height", _recipe.height ?? string.Empty);
            _recipe.birthday = EditorGUILayout.TextField("Birthday", _recipe.birthday ?? string.Empty);
            _recipe.speciality = EditorGUILayout.TextField("Speciality", _recipe.speciality ?? string.Empty);
            _recipe.weapon = EditorGUILayout.TextField("Weapon", _recipe.weapon ?? string.Empty);
            _recipe.origin = EditorGUILayout.TextField("Origin", _recipe.origin ?? string.Empty);

            GUILayout.Space(12);
            GUILayout.Label("Bond Archive", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "낯섦(0) · 호감(25) · 기쁨(50) · 사랑(75) · EX(100)에서 순서대로 공개됩니다. 1단계는 처음부터 열립니다.",
                MessageType.None);

            EnsureBondRecords(_recipe);
            string[] labels = { "1 · 낯섦", "2 · 호감", "3 · 기쁨", "4 · 사랑", "5 · EX" };
            for (int i = 0; i < _recipe.bondRecords.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(labels[i], EditorStyles.miniBoldLabel);
                _recipe.bondRecords[i].description = EditorGUILayout.TextArea(
                    _recipe.bondRecords[i].description ?? string.Empty,
                    GUILayout.MinHeight(62));
                EditorGUILayout.EndVertical();
            }

            DrawSaveButton();
        }

        private static void EnsureBondRecords(OperatorAssetRecipe recipe)
        {
            recipe.bondRecords ??= new List<OperatorBondRecord>();
            while (recipe.bondRecords.Count < 5)
            {
                recipe.bondRecords.Add(new OperatorBondRecord());
            }

            if (recipe.bondRecords.Count > 5)
            {
                recipe.bondRecords.RemoveRange(5, recipe.bondRecords.Count - 5);
            }

            for (int i = 0; i < recipe.bondRecords.Count; i++)
            {
                recipe.bondRecords[i] ??= new OperatorBondRecord();
            }
        }

        private static void EnsureIntListSize(List<int> values, int size)
        {
            while (values.Count < size) values.Add(0);
            if (values.Count > size) values.RemoveRange(size, values.Count - size);
        }

        private static void EnsureFloatListSize(List<float> values, int size)
        {
            while (values.Count < size) values.Add(0f);
            if (values.Count > size) values.RemoveRange(size, values.Count - size);
        }

        private static void ApplyDefaultCosts(OperatorUpgradeTrack track)
        {
            int[] costs = track.category == OperatorUpgradeCategory.Core
                ? new[] { 20, 35, 55, 80, 120, 170, 230, 290 }
                : new[] { 10, 15, 25, 40, 60, 90, 130, 180 };
            EnsureIntListSize(track.levelCosts, track.maxLevel);
            for (int i = 0; i < track.maxLevel; i++)
            {
                track.levelCosts[i] = costs[Mathf.Min(i, costs.Length - 1)];
            }
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
            bool isLobbySlot = slotIndex <= 8;
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
                "Save Recipe는 제작 원본만 저장합니다. 저장한 레시피를 게임에 반영하려면 아래 단일 빌드 버튼으로 " +
                "저장·빌드·검증을 한 번에 실행하세요.",
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
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_recipe.operatorId)))
            {
                if (GUILayout.Button(
                        $"Save + Validate + Build \u25B6 {_recipe.operatorId}",
                        GUILayout.Height(34)))
                {
                    BuildSelectedOperator();
                }
            }

            EditorGUILayout.HelpBox(
                "이 오퍼레이터의 Definition·Roster 복제본·카탈로그 항목·전용 Addressables 그룹만 갱신합니다. " +
                "다른 오퍼레이터의 에셋은 건드리지 않으므로 형상관리에 불필요한 변경이 생기지 않습니다.",
                MessageType.None);

            GUILayout.Space(12);
            if (GUILayout.Button("Save Recipe & Dialogue", GUILayout.Height(26)))
            {
                SaveCurrentAssets();
            }

            if (GUILayout.Button("Validate All Operators", GUILayout.Height(26)))
            {
                ValidateAll();
            }

            if (GUILayout.Button("Build All Operator Assets + Addressables", GUILayout.Height(26)))
            {
                SaveCurrentAssets();
                BuildAll();
            }

            EditorGUILayout.HelpBox(
                "Build All은 레시피 전체를 다시 만들고, 카탈로그에서 사라진 오퍼레이터 그룹까지 정리합니다. " +
                "오퍼레이터를 추가·삭제했거나 단일 빌드 검증이 다른 오퍼레이터 문제를 지적할 때 사용하세요.",
                MessageType.None);
        }

        private void DrawSaveButton()
        {
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Recipe & Dialogue", GUILayout.Height(28)))
            {
                SaveCurrentAssets();
            }

            // 편집 중인 탭에서 바로 게임 반영까지 끝낼 수 있게 단일 빌드를 함께 둔다.
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_recipe.operatorId)))
            {
                if (GUILayout.Button("Save + Build This Operator", GUILayout.Height(28), GUILayout.Width(190)))
                {
                    BuildSelectedOperator();
                }
            }

            EditorGUILayout.EndHorizontal();
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
            EnsureBondRecords(_recipe);
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

            EnsureBondRecords(_recipe);

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
                catalogOrder = _recipePaths.Count,
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
                "operatorAcquired" => _dialogueSet.operatorAcquired,
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

        private void BuildSelectedOperator()
        {
            if (_recipe == null || string.IsNullOrWhiteSpace(_selectedRecipePath))
            {
                return;
            }

            string recipePath = _selectedRecipePath;
            try
            {
                SaveCurrentAssets();
                OperatorBuildReport report = OperatorAssetBuilder.BuildSingle(recipePath);
                string changes = report.changedAssets.Count == 0
                    ? "이미 최신 상태입니다. 다시 쓴 에셋 없음."
                    : $"갱신된 에셋 {report.changedAssets.Count}개:\n· " +
                      string.Join("\n· ", report.changedAssets);
                string validation = report.validationPassed
                    ? "검증 통과"
                    : "검증 실패 — Console을 확인하세요.";
                EditorUtility.DisplayDialog(
                    $"Build {report.operatorId}", $"{changes}\n\n{validation}", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Operator Build", exception.Message, "확인");
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
