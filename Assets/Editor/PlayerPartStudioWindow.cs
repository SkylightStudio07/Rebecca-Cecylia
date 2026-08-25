using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RCCom.Definitions.PlayerPart;
using RCCom.Runtime;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 플레이어 파츠 레시피를 편집하는 단일 저작 창. OperatorStudioWindow/AllyUnitStudioWindow/
    /// EnemyStudioWindow와 같은 결 — Definition·Effect·카탈로그·Addressables는 전부 기존
    /// PlayerPartAssetBuilder가 생성하므로, 이 창은 JSON 레시피만 만지고 실제 반영은 항상
    /// Build를 거친다. 그래야 파츠를 추가할 때 전투 코드를 새로 만들 필요가 없다는 원칙이 유지된다.
    /// </summary>
    public sealed class PlayerPartStudioWindow : EditorWindow
    {
        private static readonly string[] EffectTypes =
        {
            "basic", "multi-barrel", "pierce", "splash", "chain", "overload-attack",
            "inertia", "overboost", "regenerative-armor", "passive-regeneration",
            "rechargeable-shield", "one-time-revival", "core-overload",
        };

        // 주 공격 판정(포탑 슬롯 전용)과 그 외 기믹을 구분해 두면 Effects 탭에서
        // "이 타입은 포탑 전용/이 슬롯엔 못 쓴다" 같은 안내를 보여줄 수 있다.
        private static readonly HashSet<string> PrimaryAttackEffectTypes = new(StringComparer.Ordinal)
        {
            "basic", "multi-barrel", "pierce", "splash", "chain", "overload-attack",
        };

        private readonly List<string> _recipePaths = new();
        private readonly List<string> _recipeLabels = new();

        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;
        private int _selectedRecipeIndex = -1;
        private int _tabIndex;
        private string _selectedRecipePath;
        private PlayerPartAssetRecipe _recipe;

        private enum StudioTab
        {
            Identity,
            Stats,
            Effects,
            Package,
        }

        [MenuItem("RCCom/Player Parts/Open Player Part Studio")]
        public static void Open()
        {
            var size = new Vector2(1000f, 740f);
            PlayerPartStudioWindow[] existingWindows = Resources.FindObjectsOfTypeAll<PlayerPartStudioWindow>();
            for (int i = 0; i < existingWindows.Length; i++)
            {
                existingWindows[i].Close();
            }

            PlayerPartStudioWindow window = GetWindowWithRect<PlayerPartStudioWindow>(
                new Rect(90f, 80f, size.x, size.y), true, "Player Part Studio", true);
            window.minSize = new Vector2(820f, 560f);
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
                    "파츠 레시피가 없습니다. New Part로 제작 원본을 먼저 만드세요.",
                    MessageType.Info);
                if (GUILayout.Button("New Part", GUILayout.Height(30f)))
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
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                RefreshRecipes(_selectedRecipePath);
            }

            GUILayout.Label("RCCom / Player Part Studio", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("New Part", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                CreateNewRecipe();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRecipeSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(230f));
            GUILayout.Label($"Parts  /  {_recipePaths.Count}", EditorStyles.boldLabel);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            PlayerPartSlot? currentGroup = null;
            for (int i = 0; i < _recipePaths.Count; i++)
            {
                PlayerPartSlot? group = LoadRecipeSlot(_recipePaths[i]);
                if (group != currentGroup)
                {
                    currentGroup = group;
                    GUILayout.Space(4f);
                    GUILayout.Label(group?.ToString() ?? "?", EditorStyles.miniBoldLabel);
                }

                GUIStyle style = i == _selectedRecipeIndex
                    ? new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold }
                    : EditorStyles.toolbarButton;
                if (GUILayout.Button(_recipeLabels[i], style, GUILayout.Height(24f)))
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
                new[] { "Identity", "Stats", "Effects", "Package" },
                EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            switch ((StudioTab)_tabIndex)
            {
                case StudioTab.Identity:
                    DrawIdentityTab();
                    break;
                case StudioTab.Stats:
                    DrawStatsTab();
                    break;
                case StudioTab.Effects:
                    DrawEffectsTab();
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
            EnsureData();
            GUILayout.Label("Part Identity", EditorStyles.boldLabel);

            _recipe.partId = NormalizeId(EditorGUILayout.TextField("Part ID", _recipe.partId));

            PlayerPartSlot previousSlot = _recipe.slot;
            _recipe.slot = (PlayerPartSlot)EditorGUILayout.EnumPopup("Slot", _recipe.slot);
            if (_recipe.slot != previousSlot)
            {
                // 슬롯을 바꾸면 이전 슬롯이 쓰던 수치가 "이 슬롯이 소유하지 않는 필드"로 남아
                // 검증기 에러가 되므로, 슬롯 전환 시점에 통째로 비운다.
                _recipe.statOverride = new PlayerPartStatOverride();
            }

            _recipe.grade = (PlayerPartGrade)EditorGUILayout.EnumPopup("Grade", _recipe.grade);
            if (_recipe.grade == PlayerPartGrade.Common)
            {
                _recipe.price = 0;
                EditorGUILayout.HelpBox(
                    "Common은 슬롯당 정확히 1개, 가격 0, 구매/판매 불가인 폴백 파츠입니다.",
                    MessageType.Info);
                if (_recipe.slot == PlayerPartSlot.Special)
                {
                    EditorGUILayout.HelpBox(
                        "특수 소켓은 Common 폴백을 갖지 않습니다(빈 슬롯 = 효과 없음).",
                        MessageType.Warning);
                }
            }

            _recipe.archetypeId = EditorGUILayout.TextField("Archetype ID", _recipe.archetypeId);
            EditorGUILayout.HelpBox(
                "같은 기믹의 Mk1/Mk2(예: 기본형/고급형)를 묶는 그룹 키입니다. 아이콘 기본 경로도 " +
                "이 값으로 찾습니다(Assets/Art/PlayerParts/Icons/{슬롯}/{archetypeId}.png).",
                MessageType.None);

            _recipe.displayName = EditorGUILayout.TextField("Display Name", _recipe.displayName);
            GUILayout.Label("Description");
            _recipe.description = EditorGUILayout.TextArea(_recipe.description, GUILayout.Height(50f));

            using (new EditorGUI.DisabledScope(_recipe.grade == PlayerPartGrade.Common))
            {
                _recipe.price = Mathf.Max(0, EditorGUILayout.IntField("Price", _recipe.price));
            }

            GUILayout.Space(8f);
            GUILayout.Label("Shop Icon", EditorStyles.boldLabel);
            string resolvedIconPath = ResolveIconPath(_recipe);
            Sprite overrideSprite = string.IsNullOrWhiteSpace(_recipe.iconPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>(_recipe.iconPath);
            Sprite selected = (Sprite)EditorGUILayout.ObjectField(
                "Icon Override (optional)", overrideSprite, typeof(Sprite), false);
            _recipe.iconPath = selected == null ? string.Empty : AssetDatabase.GetAssetPath(selected);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Resolved Icon",
                    AssetDatabase.LoadAssetAtPath<Sprite>(resolvedIconPath),
                    typeof(Sprite),
                    false);
            }

            EditorGUILayout.HelpBox(
                "Part ID는 Definition 파일명·카탈로그 키로 쓰이는 영구 식별자입니다. 생성된 뒤에는 " +
                "가급적 바꾸지 마세요(바꾸면 이전 ID의 Definition/Effect 에셋이 고아로 남습니다).",
                MessageType.None);

            GUILayout.Space(10f);
            DrawSaveButton();
        }

        private void DrawStatsTab()
        {
            EnsureData();
            GUILayout.Label($"Stat Override — {_recipe.slot}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "파츠는 델타가 아니라 슬롯이 관장하는 필드의 절대값입니다. 슬롯이 소유하지 않는 " +
                "필드는 여기 보이지 않으며(검증기가 강제), 슬롯을 바꾸면 이 값은 자동으로 초기화됩니다.",
                MessageType.None);
            GUILayout.Space(6f);

            PlayerPartStatOverride value = _recipe.statOverride;
            switch (_recipe.slot)
            {
                case PlayerPartSlot.Thruster:
                    value.moveSpeed = EditorGUILayout.FloatField("Move Speed", value.moveSpeed);
                    GUILayout.Space(6f);
                    GUILayout.Label("Legendary 전용(그 외 등급은 0으로 둘 것)", EditorStyles.miniLabel);
                    value.skillOverdriveMoveSpeedMultiplier = EditorGUILayout.FloatField(
                        "Overdrive Move Speed Multiplier", value.skillOverdriveMoveSpeedMultiplier);
                    break;
                case PlayerPartSlot.Turret:
                    value.attackDamage = EditorGUILayout.FloatField("Attack Damage", value.attackDamage);
                    value.attackRange = EditorGUILayout.FloatField("Attack Range", value.attackRange);
                    value.attackInterval = EditorGUILayout.FloatField("Attack Interval", value.attackInterval);
                    value.projectileSpeed = EditorGUILayout.FloatField("Projectile Speed", value.projectileSpeed);
                    break;
                case PlayerPartSlot.Body:
                    value.maxHealth = EditorGUILayout.FloatField("Max Health", value.maxHealth);
                    value.hitInvulnerabilityDuration = EditorGUILayout.FloatField(
                        "Hit Invulnerability Duration", value.hitInvulnerabilityDuration);
                    break;
                case PlayerPartSlot.Driver:
                    value.skillCooldown = EditorGUILayout.FloatField("Skill Cooldown", value.skillCooldown);
                    value.skillRange = EditorGUILayout.FloatField("Skill Range", value.skillRange);
                    value.skillDamage = EditorGUILayout.FloatField("Skill Damage", value.skillDamage);
                    value.skillBurstCount = Mathf.Max(
                        1, EditorGUILayout.IntField("Skill Burst Count", value.skillBurstCount));
                    value.skillBurstInterval = EditorGUILayout.FloatField(
                        "Skill Burst Interval", value.skillBurstInterval);
                    value.skillChargeCapacity = Mathf.Max(
                        1, EditorGUILayout.IntField("Skill Charge Capacity", value.skillChargeCapacity));
                    break;
                case PlayerPartSlot.Special:
                    EditorGUILayout.HelpBox(
                        "특수 소켓은 수치 오버라이드를 갖지 않습니다 — Effects 탭의 훅만으로 동작합니다.",
                        MessageType.Info);
                    break;
            }

            GUILayout.Space(10f);
            DrawSaveButton();
        }

        private void DrawEffectsTab()
        {
            EnsureData();
            GUILayout.Label($"Effects  /  {_recipe.effects.Count}", EditorStyles.boldLabel);
            if (_recipe.slot == PlayerPartSlot.Turret)
            {
                int primaryCount = 0;
                foreach (PlayerPartEffectRecipe effect in _recipe.effects)
                {
                    if (effect != null && PrimaryAttackEffectTypes.Contains(effect.type ?? string.Empty))
                    {
                        primaryCount++;
                    }
                }

                if (primaryCount != 1)
                {
                    EditorGUILayout.HelpBox(
                        $"포탑 파츠는 주 공격 Effect(basic/multi-barrel/pierce/splash/chain/" +
                        $"overload-attack)가 정확히 1개여야 합니다. 현재 {primaryCount}개 — " +
                        "Validate로 최종 확인하세요.",
                        MessageType.Warning);
                }
            }

            for (int i = 0; i < _recipe.effects.Count; i++)
            {
                DrawEffectEntry(i);
            }

            if (GUILayout.Button("+ Add Effect", GUILayout.Height(26f)))
            {
                _recipe.effects.Add(new PlayerPartEffectRecipe { type = EffectTypes[0] });
            }

            GUILayout.Space(10f);
            DrawSaveButton();
        }

        private void DrawEffectEntry(int index)
        {
            PlayerPartEffectRecipe effect = _recipe.effects[index];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Effect {index + 1}", EditorStyles.boldLabel, GUILayout.Width(70f));

            int currentTypeIndex = Mathf.Max(0, Array.IndexOf(EffectTypes, effect.type));
            int newTypeIndex = EditorGUILayout.Popup(currentTypeIndex, EffectTypes);
            effect.type = EffectTypes[newTypeIndex];

            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("▲", GUILayout.Width(26f)))
                {
                    (_recipe.effects[index], _recipe.effects[index - 1]) =
                        (_recipe.effects[index - 1], _recipe.effects[index]);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            using (new EditorGUI.DisabledScope(index >= _recipe.effects.Count - 1))
            {
                if (GUILayout.Button("▼", GUILayout.Width(26f)))
                {
                    (_recipe.effects[index], _recipe.effects[index + 1]) =
                        (_recipe.effects[index + 1], _recipe.effects[index]);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            if (GUILayout.Button("Remove", GUILayout.Width(62f)))
            {
                _recipe.effects.RemoveAt(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            if (_recipe.slot != PlayerPartSlot.Turret && PrimaryAttackEffectTypes.Contains(effect.type))
            {
                EditorGUILayout.HelpBox("이 타입은 포탑 슬롯 전용 주 공격 판정입니다.", MessageType.Warning);
            }

            DrawEffectFields(effect);
            EditorGUILayout.EndVertical();
        }

        private static void DrawEffectFields(PlayerPartEffectRecipe effect)
        {
            switch (effect.type)
            {
                case "multi-barrel":
                    effect.shotCount = Mathf.Max(1, EditorGUILayout.IntField("Shot Count", effect.shotCount));
                    effect.shotInterval = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Shot Interval", effect.shotInterval));
                    effect.damageMultiplier = EditorGUILayout.FloatField(
                        "Damage Multiplier / Shot", effect.damageMultiplier);
                    break;
                case "pierce":
                    effect.maxTargets = Mathf.Max(1, EditorGUILayout.IntField("Max Targets", effect.maxTargets));
                    effect.damageFalloff = EditorGUILayout.Slider(
                        "Damage Falloff / Target", effect.damageFalloff, 0f, 1f);
                    effect.beamHalfAngleDegrees = EditorGUILayout.Slider(
                        "Beam Half Angle", effect.beamHalfAngleDegrees, 0.1f, 20f);
                    break;
                case "splash":
                    effect.radius = Mathf.Max(0f, EditorGUILayout.FloatField("Radius", effect.radius));
                    effect.splashDamageMultiplier = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Splash Damage Multiplier", effect.splashDamageMultiplier));
                    break;
                case "chain":
                    effect.jumpCount = Mathf.Max(0, EditorGUILayout.IntField("Jump Count", effect.jumpCount));
                    effect.damageFalloff = EditorGUILayout.Slider(
                        "Damage Falloff / Jump", effect.damageFalloff, 0f, 1f);
                    break;
                case "overload-attack":
                    effect.maxTargets = Mathf.Max(
                        1, EditorGUILayout.IntField("Skill Pierce Targets", effect.maxTargets));
                    effect.beamHalfAngleDegrees = EditorGUILayout.Slider(
                        "Beam Half Angle", effect.beamHalfAngleDegrees, 0.1f, 20f);
                    break;
                case "inertia":
                    effect.accelerationDelay = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Acceleration Delay", effect.accelerationDelay));
                    effect.rampDuration = Mathf.Max(
                        0.01f, EditorGUILayout.FloatField("Ramp Duration", effect.rampDuration));
                    effect.maximumSpeedMultiplier = Mathf.Max(
                        1f, EditorGUILayout.FloatField("Maximum Speed Multiplier", effect.maximumSpeedMultiplier));
                    break;
                case "overboost":
                    effect.dashDuration = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Dash Duration", effect.dashDuration));
                    break;
                case "regenerative-armor":
                    effect.delayAfterDamage = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Delay After Damage", effect.delayAfterDamage));
                    effect.maxHealthPerSecond = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Max Health % / Second", effect.maxHealthPerSecond));
                    break;
                case "passive-regeneration":
                    effect.maxHealthPerSecond = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Max Health % / Second", effect.maxHealthPerSecond));
                    effect.healOnSkillRatio = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Heal On Skill Ratio", effect.healOnSkillRatio));
                    break;
                case "rechargeable-shield":
                    effect.barrierAmount = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Barrier Amount", effect.barrierAmount));
                    effect.rechargeInterval = Mathf.Max(
                        0.01f, EditorGUILayout.FloatField("Recharge Interval", effect.rechargeInterval));
                    break;
                case "one-time-revival":
                    effect.restoredHealthRatio = EditorGUILayout.Slider(
                        "Restored Health Ratio", effect.restoredHealthRatio, 0f, 1f);
                    effect.invulnerabilityDuration = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Invulnerability Duration", effect.invulnerabilityDuration));
                    break;
                case "core-overload":
                    effect.maxHealthPerSecond = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Max Health % / Second", effect.maxHealthPerSecond));
                    effect.triggerHealthRatio = EditorGUILayout.Slider(
                        "Trigger Health Ratio", effect.triggerHealthRatio, 0f, 1f);
                    effect.instantHealRatio = EditorGUILayout.Slider(
                        "Instant Heal Ratio", effect.instantHealRatio, 0f, 1f);
                    effect.invulnerabilityDuration = Mathf.Max(
                        0f, EditorGUILayout.FloatField("Invulnerability Duration", effect.invulnerabilityDuration));
                    effect.cooldown = Mathf.Max(0.01f, EditorGUILayout.FloatField("Cooldown", effect.cooldown));
                    break;
                default:
                    EditorGUILayout.HelpBox("basic은 추가 파라미터가 없습니다.", MessageType.None);
                    break;
            }
        }

        private void DrawPackageTab()
        {
            EnsureData();
            GUILayout.Label("Save, Validate & Package", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Save Recipe는 제작 원본만 저장합니다. 게임에 반영하려면 Save + Build로 Definition, " +
                "Effect, 카탈로그, 어드레서블 항목을 함께 갱신하세요.",
                MessageType.Info);
            EditorGUILayout.LabelField("Recipe", _selectedRecipePath ?? string.Empty);
            EditorGUILayout.LabelField(
                "Definition",
                string.IsNullOrWhiteSpace(_recipe.partId)
                    ? string.Empty
                    : PlayerPartAssetBuilder.GetDefinitionPath(_recipe.partId));
            EditorGUILayout.LabelField(
                "Effect Assets", string.IsNullOrWhiteSpace(_recipe.partId)
                    ? string.Empty
                    : $"{PlayerPartAssetBuilder.EffectFolder}/{_recipe.partId}-effect-*.asset ({_recipe.effects.Count}개)");
            EditorGUILayout.LabelField("Catalog Address", PlayerPartContentLoader.CatalogAddress);
            EditorGUILayout.LabelField("Addressables Group", PlayerPartAssetBuilder.AddressableGroupName);

            GUILayout.Space(12f);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_recipe.partId)))
            {
                if (GUILayout.Button($"Save + Build ▶ {_recipe.partId}", GUILayout.Height(34f)))
                {
                    BuildSelectedPart();
                }
            }

            EditorGUILayout.HelpBox(
                "단일 빌드는 이 파츠의 Definition/Effect만 다시 쓰고 카탈로그를 재조립합니다. " +
                "다른 파츠는 디스크의 기존 생성물을 그대로 읽으므로 먼저 한 번은 Build All이 필요합니다.",
                MessageType.None);

            GUILayout.Space(10f);
            if (GUILayout.Button("Save Recipe", GUILayout.Height(26f)))
            {
                SaveCurrentRecipe();
            }

            if (GUILayout.Button("Validate All Player Parts", GUILayout.Height(26f)))
            {
                ValidateAll();
            }

            if (GUILayout.Button("Build All Player Part Assets", GUILayout.Height(30f)))
            {
                SaveCurrentRecipe();
                BuildAll();
            }

            GUILayout.Space(18f);
            EditorGUILayout.HelpBox(
                "삭제는 레시피·Definition·Effect·카탈로그 항목을 함께 정리합니다. Common 등급(슬롯 " +
                "폴백)은 삭제할 수 없습니다. 상점 아이콘 원본은 보존됩니다.",
                MessageType.Warning);
            using (new EditorGUI.DisabledScope(_recipe.grade == PlayerPartGrade.Common))
            {
                if (GUILayout.Button("Delete Selected Part...", GUILayout.Height(28f)))
                {
                    DeleteSelectedPart();
                }
            }
        }

        private void DrawSaveButton()
        {
            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Recipe", GUILayout.Height(28f)))
            {
                SaveCurrentRecipe();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_recipe.partId)))
            {
                if (GUILayout.Button("Save + Build This Part", GUILayout.Height(28f), GUILayout.Width(190f)))
                {
                    BuildSelectedPart();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void RefreshRecipes(string preferredPath)
        {
            _recipePaths.Clear();
            _recipeLabels.Clear();

            if (AssetDatabase.IsValidFolder(PlayerPartAssetBuilder.RecipeFolder))
            {
                string[] guids = AssetDatabase.FindAssets(
                    "t:TextAsset", new[] { PlayerPartAssetBuilder.RecipeFolder });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        _recipePaths.Add(path);
                    }
                }
            }

            // 슬롯 → 등급 → ID 순으로 정렬해 사이드바가 카탈로그 빌드 순서(BuildCatalog 정렬)와
            // 같은 그룹핑으로 보이게 한다.
            _recipePaths.Sort((left, right) =>
            {
                PlayerPartAssetRecipe leftRecipe = LoadRecipeQuiet(left);
                PlayerPartAssetRecipe rightRecipe = LoadRecipeQuiet(right);
                if (leftRecipe == null || rightRecipe == null)
                {
                    return string.CompareOrdinal(left, right);
                }

                int slot = leftRecipe.slot.CompareTo(rightRecipe.slot);
                if (slot != 0) { return slot; }
                int grade = leftRecipe.grade.CompareTo(rightRecipe.grade);
                return grade != 0 ? grade : string.CompareOrdinal(leftRecipe.partId, rightRecipe.partId);
            });

            for (int i = 0; i < _recipePaths.Count; i++)
            {
                PlayerPartAssetRecipe recipe = LoadRecipeQuiet(_recipePaths[i]);
                _recipeLabels.Add(recipe == null || string.IsNullOrWhiteSpace(recipe.displayName)
                    ? Path.GetFileNameWithoutExtension(_recipePaths[i])
                    : $"[{recipe.grade}] {recipe.displayName}");
            }

            if (_recipePaths.Count == 0)
            {
                _selectedRecipeIndex = -1;
                _selectedRecipePath = null;
                _recipe = null;
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
            _recipe = LoadRecipeQuiet(_selectedRecipePath);
            EnsureData();
            _contentScroll = Vector2.zero;
            Repaint();
        }

        private static PlayerPartAssetRecipe LoadRecipeQuiet(string path)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            return asset == null ? null : JsonUtility.FromJson<PlayerPartAssetRecipe>(asset.text);
        }

        private static PlayerPartSlot? LoadRecipeSlot(string path)
        {
            PlayerPartAssetRecipe recipe = LoadRecipeQuiet(path);
            return recipe?.slot;
        }

        private void CreateNewRecipe()
        {
            EnsureFolder(PlayerPartAssetBuilder.RecipeFolder);
            string path = EditorUtility.SaveFilePanelInProject(
                "새 플레이어 파츠 레시피",
                "new-part.json",
                "json",
                "Assets/Editor/PlayerPartRecipes 아래에 영문 소문자 ID로 저장하세요.",
                PlayerPartAssetBuilder.RecipeFolder);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string partId = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!IsValidId(partId))
            {
                EditorUtility.DisplayDialog(
                    "잘못된 Part ID",
                    "파일명은 영문 소문자, 숫자, -, _만 사용할 수 있습니다.",
                    "확인");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null)
            {
                EditorUtility.DisplayDialog("이미 존재하는 레시피", path, "확인");
                return;
            }

            var recipe = new PlayerPartAssetRecipe
            {
                partId = partId,
                slot = PlayerPartSlot.Thruster,
                grade = PlayerPartGrade.Advanced,
                archetypeId = partId,
                displayName = partId,
                description = string.Empty,
                price = 100,
                statOverride = new PlayerPartStatOverride(),
                effects = new List<PlayerPartEffectRecipe>(),
                iconPath = string.Empty,
            };
            File.WriteAllText(Path.GetFullPath(path), JsonUtility.ToJson(recipe, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshRecipes(path);
        }

        private void SaveCurrentRecipe()
        {
            if (_recipe == null || string.IsNullOrWhiteSpace(_selectedRecipePath))
            {
                return;
            }

            EnsureData();
            File.WriteAllText(
                Path.GetFullPath(_selectedRecipePath),
                JsonUtility.ToJson(_recipe, true),
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(_selectedRecipePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string path = _selectedRecipePath;
            RefreshRecipes(path);
            Debug.Log($"[PlayerPartStudio] 저장 완료: {_recipe.partId}");
        }

        private void BuildSelectedPart()
        {
            if (_recipe == null || string.IsNullOrWhiteSpace(_selectedRecipePath))
            {
                return;
            }

            try
            {
                SaveCurrentRecipe();
                PlayerPartBuildReport report = PlayerPartAssetBuilder.BuildSingle(_selectedRecipePath);
                string validation = report.validationPassed
                    ? "검증 통과"
                    : "검증 실패 — Console을 확인하세요.";
                EditorUtility.DisplayDialog($"Build {report.partId}", validation, "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Player Part Build", exception.Message, "확인");
            }
        }

        private void ValidateAll()
        {
            try
            {
                SaveCurrentRecipe();
                bool valid = PlayerPartAssetValidator.ValidateAll(false);
                EditorUtility.DisplayDialog(
                    "Player Part Validation",
                    valid ? "검증 통과" : "검증 실패 — Console을 확인하세요.",
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Player Part Validation", exception.Message, "확인");
            }
        }

        private void BuildAll()
        {
            try
            {
                PlayerPartAssetBuilder.BuildAll();
                EditorUtility.DisplayDialog(
                    "Player Part Build",
                    "Definition, Effect, Catalog, Addressables 갱신 완료",
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Player Part Build", exception.Message, "확인");
            }
        }

        private void DeleteSelectedPart()
        {
            if (_recipe == null || string.IsNullOrWhiteSpace(_recipe.partId))
            {
                return;
            }

            string partId = _recipe.partId;
            if (!EditorUtility.DisplayDialog(
                    "파츠 삭제",
                    $"{partId}의 레시피와 모든 자동 생성물을 삭제합니다. 상점 아이콘 원본은 보존됩니다.",
                    "삭제",
                    "취소"))
            {
                return;
            }

            try
            {
                PlayerPartAssetDeletionService.Delete(partId);
                RefreshRecipes(null);
                EditorUtility.DisplayDialog("파츠 삭제", $"삭제 완료: {partId}", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("파츠 삭제 실패", exception.Message, "확인");
            }
        }

        private void EnsureData()
        {
            if (_recipe == null)
            {
                return;
            }

            _recipe.statOverride ??= new PlayerPartStatOverride();
            _recipe.effects ??= new List<PlayerPartEffectRecipe>();
        }

        private static string ResolveIconPath(PlayerPartAssetRecipe recipe)
        {
            if (!string.IsNullOrWhiteSpace(recipe.iconPath))
            {
                return recipe.iconPath;
            }

            string slotFolder = recipe.slot.ToString().ToLowerInvariant();
            return $"Assets/Art/PlayerParts/Icons/{slotFolder}/{recipe.archetypeId}.png";
        }

        private static string NormalizeId(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool IsValidId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (char character in value)
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
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new ArgumentException("에셋 폴더는 Assets 아래여야 합니다.", nameof(folderPath));
            }

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
