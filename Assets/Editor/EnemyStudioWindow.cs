using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RCCom.Data;
using RCCom.Effects.Enemy;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 적 레시피를 편집하는 단일 저작 창.
    /// Definition과 Addressables 그룹은 기존 빌더가 생성하므로 이 창은 JSON 원본만
    /// 수정한다. 이렇게 경계를 나누면 적을 추가할 때 전투 코드를 새로 만들 필요가 없다.
    /// </summary>
    public sealed class EnemyStudioWindow : EditorWindow
    {
        private const string RecipeFolder = "Assets/Editor/EnemyRecipes";
        private const string OutputRoot = "Assets/Data/Enemies";

        private readonly List<string> _recipePaths = new();
        private readonly List<string> _recipeLabels = new();

        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;
        private int _selectedRecipeIndex = -1;
        private int _tabIndex;
        private string _selectedRecipePath;
        private EnemyAssetRecipe _recipe;

        private enum StudioTab
        {
            Identity,
            Combat,
            Presentation,
            Package,
        }

        [MenuItem("RCCom/Enemies/Open Enemy Studio")]
        public static void Open()
        {
            Vector2 size = new Vector2(980f, 720f);
            EnemyStudioWindow[] existingWindows = Resources.FindObjectsOfTypeAll<EnemyStudioWindow>();
            for (int i = 0; i < existingWindows.Length; i++)
            {
                existingWindows[i].Close();
            }

            EnemyStudioWindow window = GetWindowWithRect<EnemyStudioWindow>(
                new Rect(90f, 80f, size.x, size.y), true, "Enemy Studio", true);
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
                    "적 레시피가 없습니다. New Enemy로 제작 원본을 먼저 만드세요.",
                    MessageType.Info);
                if (GUILayout.Button("New Enemy", GUILayout.Height(30f)))
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

            GUILayout.Label("RCCom / Enemy Studio", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("New Enemy", EditorStyles.toolbarButton, GUILayout.Width(86f)))
            {
                CreateNewRecipe();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRecipeSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(215f));
            GUILayout.Label("Enemies", EditorStyles.boldLabel);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            for (int i = 0; i < _recipePaths.Count; i++)
            {
                GUIStyle style = i == _selectedRecipeIndex
                    ? new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold }
                    : EditorStyles.toolbarButton;
                if (GUILayout.Button(_recipeLabels[i], style, GUILayout.Height(25f)))
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
                new[] { "Identity", "Combat", "Presentation", "Package" },
                EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            switch ((StudioTab)_tabIndex)
            {
                case StudioTab.Identity:
                    DrawIdentityTab();
                    break;
                case StudioTab.Combat:
                    DrawCombatTab();
                    break;
                case StudioTab.Presentation:
                    DrawPresentationTab();
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
            GUILayout.Label("Enemy Identity", EditorStyles.boldLabel);

            string enemyId = EditorGUILayout.TextField("Enemy ID", _recipe.enemyId);
            _recipe.enemyId = NormalizeId(enemyId);
            _recipe.catalogOrder = Mathf.Max(0, EditorGUILayout.IntField("Catalog Order", _recipe.catalogOrder));
            _recipe.displayName = EditorGUILayout.TextField("Display Name", _recipe.displayName);
            _recipe.data.kind = (EnemyKind)EditorGUILayout.EnumPopup("Kind", _recipe.data.kind);
            _recipe.remoteContent = EditorGUILayout.ToggleLeft("Remote Content", _recipe.remoteContent);

            EditorGUILayout.HelpBox(
                "ID는 Addressables 주소와 웨이브 편성에서 쓰이는 영구 식별자입니다. 생성된 Definition이 " +
                "있는 뒤에는 파일명과 함께 변경하지 않는 것을 권장합니다.",
                MessageType.None);

            GUILayout.Space(10f);
            DrawSaveButton();
        }

        private void DrawCombatTab()
        {
            EnsureData();
            GUILayout.Label("Combat Data", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "근접 적은 attackRange가 0이어도 정상입니다. ContactDamageEffect가 접촉 시 피해를 " +
                "주는 구조이므로 값을 임의로 보정하지 않습니다.",
                MessageType.Info);

            EnemyData data = _recipe.data;
            data.maxHealth = Mathf.Max(0f, EditorGUILayout.FloatField("Max Health", data.maxHealth));
            data.moveSpeed = Mathf.Max(0f, EditorGUILayout.FloatField("Move Speed", data.moveSpeed));
            data.contactDamage = Mathf.Max(0f, EditorGUILayout.FloatField("Contact Damage", data.contactDamage));
            data.attackRange = Mathf.Max(0f, EditorGUILayout.FloatField("Attack Range", data.attackRange));
            data.attackInterval = Mathf.Max(0.01f, EditorGUILayout.FloatField("Attack Interval", data.attackInterval));

            GUILayout.Space(8f);
            GUILayout.Label("Wave Budget", EditorStyles.boldLabel);
            data.waveCost = Mathf.Max(0f, EditorGUILayout.FloatField("Wave Cost", data.waveCost));
            data.minWave = Mathf.Max(0, EditorGUILayout.IntField("Minimum Wave", data.minWave));

            GUILayout.Space(8f);
            GUILayout.Label("Defeat Reward", EditorStyles.boldLabel);
            data.goldReward = Mathf.Max(0, EditorGUILayout.IntField("Gold Reward", data.goldReward));
            data.expReward = Mathf.Max(0, EditorGUILayout.IntField("EXP Reward", data.expReward));

            GUILayout.Space(10f);
            DrawSaveButton();
        }

        private void DrawPresentationTab()
        {
            EnsureData();
            GUILayout.Label("Presentation & Effects", EditorStyles.boldLabel);
            _recipe.spritePath = DrawAssetPathField<Sprite>("Sprite", _recipe.spritePath);
            _recipe.spriteForwardOffsetDegrees = EditorGUILayout.FloatField(
                "Sprite Forward Offset", _recipe.spriteForwardOffsetDegrees);

            EditorGUILayout.HelpBox(
                "경로 문자열은 레시피에서 에셋을 다시 찾는 키입니다. 아트나 효과 에셋을 이동한 뒤에는 " +
                "이 창에서 다시 연결하고 Validate로 끊어진 경로를 확인하세요.",
                MessageType.None);

            GUILayout.Space(8f);
            GUILayout.Label($"Effects  /  {_recipe.effectPaths.Count}", EditorStyles.boldLabel);
            for (int i = 0; i < _recipe.effectPaths.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                string path = DrawAssetPathField<EnemyEffectBase>(
                    $"Effect {i + 1}", _recipe.effectPaths[i]);
                _recipe.effectPaths[i] = path;

                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(28f)))
                    {
                        SwapEffects(i, i - 1);
                        break;
                    }
                }

                using (new EditorGUI.DisabledScope(i >= _recipe.effectPaths.Count - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(28f)))
                    {
                        SwapEffects(i, i + 1);
                        break;
                    }
                }

                if (GUILayout.Button("Remove", GUILayout.Width(62f)))
                {
                    _recipe.effectPaths.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Effect", GUILayout.Height(26f)))
            {
                _recipe.effectPaths.Add(string.Empty);
            }

            GUILayout.Space(10f);
            DrawSaveButton();
        }

        private void DrawPackageTab()
        {
            EnsureData();
            GUILayout.Label("Save, Validate & Package", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Save Recipe는 제작 원본만 저장합니다. 게임에 반영하려면 Save + Validate + Build로 " +
                "Definition, 카탈로그, 적 전용 Addressables 그룹을 함께 갱신하세요.",
                MessageType.Info);
            EditorGUILayout.LabelField("Recipe", _selectedRecipePath ?? string.Empty);
            EditorGUILayout.LabelField("Definition", GetDefinitionPath(_recipe.enemyId));
            EditorGUILayout.LabelField("Address", string.IsNullOrWhiteSpace(_recipe.enemyId)
                ? string.Empty
                : $"enemy/{_recipe.enemyId}");
            EditorGUILayout.LabelField("Group", string.IsNullOrWhiteSpace(_recipe.enemyId)
                ? string.Empty
                : EnemyCatalogBuilder.GetGroupName(_recipe.enemyId, _recipe.remoteContent));

            GUILayout.Space(12f);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_recipe.enemyId)))
            {
                if (GUILayout.Button(
                        $"Save + Validate + Build ▶ {_recipe.enemyId}",
                        GUILayout.Height(34f)))
                {
                    BuildSelectedEnemy();
                }
            }

            EditorGUILayout.HelpBox(
                "단일 빌드는 선택한 레시피와 해당 카탈로그 항목만 갱신합니다. 적을 추가·삭제했거나 " +
                "고아 그룹을 정리할 때는 Build All을 사용하세요.",
                MessageType.None);

            GUILayout.Space(10f);
            if (GUILayout.Button("Save Recipe", GUILayout.Height(26f)))
            {
                SaveCurrentRecipe();
            }

            if (GUILayout.Button("Validate All Enemies", GUILayout.Height(26f)))
            {
                ValidateAll();
            }

            if (GUILayout.Button("Build All Enemy Assets + Addressables", GUILayout.Height(30f)))
            {
                SaveCurrentRecipe();
                BuildAll();
            }

            GUILayout.Space(18f);
            EditorGUILayout.HelpBox(
                "삭제는 레시피·Definition·Roster·Catalog·Addressables 그룹을 함께 정리합니다. " +
                "원본 스프라이트는 재사용할 수 있도록 보존합니다.",
                MessageType.Warning);
            if (GUILayout.Button("Delete Selected Enemy...", GUILayout.Height(28f)))
            {
                DeleteSelectedEnemy();
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

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_recipe.enemyId)))
            {
                if (GUILayout.Button("Save + Build This Enemy", GUILayout.Height(28f), GUILayout.Width(190f)))
                {
                    BuildSelectedEnemy();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void RefreshRecipes(string preferredPath)
        {
            _recipePaths.Clear();
            _recipeLabels.Clear();

            if (AssetDatabase.IsValidFolder(RecipeFolder))
            {
                string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { RecipeFolder });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        _recipePaths.Add(path);
                    }
                }
            }

            _recipePaths.Sort(StringComparer.Ordinal);
            for (int i = 0; i < _recipePaths.Count; i++)
            {
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(_recipePaths[i]);
                EnemyAssetRecipe recipe = asset == null
                    ? null
                    : JsonUtility.FromJson<EnemyAssetRecipe>(asset.text);
                _recipeLabels.Add(recipe == null || string.IsNullOrWhiteSpace(recipe.displayName)
                    ? Path.GetFileNameWithoutExtension(_recipePaths[i])
                    : $"{recipe.displayName}  /  {recipe.enemyId}");
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
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(_selectedRecipePath);
            _recipe = asset == null ? null : JsonUtility.FromJson<EnemyAssetRecipe>(asset.text);
            EnsureData();
            _contentScroll = Vector2.zero;
            Repaint();
        }

        private void CreateNewRecipe()
        {
            EnsureFolder(RecipeFolder);
            string path = EditorUtility.SaveFilePanelInProject(
                "새 적 레시피",
                "new-enemy.json",
                "json",
                "Assets/Editor/EnemyRecipes 아래에 영문 소문자 ID로 저장하세요.",
                RecipeFolder);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string enemyId = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!IsValidId(enemyId))
            {
                EditorUtility.DisplayDialog(
                    "잘못된 Enemy ID",
                    "파일명은 영문 소문자, 숫자, -, _만 사용할 수 있습니다.",
                    "확인");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null)
            {
                EditorUtility.DisplayDialog("이미 존재하는 레시피", path, "확인");
                return;
            }

            var recipe = new EnemyAssetRecipe
            {
                enemyId = enemyId,
                catalogOrder = _recipePaths.Count,
                displayName = enemyId,
                spriteForwardOffsetDegrees = 90f,
                effectPaths = new List<string>(),
                data = new EnemyData
                {
                    enemyId = enemyId,
                    displayName = enemyId,
                    kind = EnemyKind.Normal,
                    maxHealth = 30f,
                    moveSpeed = 2f,
                    contactDamage = 5f,
                    attackRange = 0f,
                    attackInterval = 1f,
                    waveCost = 1f,
                    minWave = 1,
                    goldReward = 10,
                    expReward = 5,
                },
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
            SyncIdentityToData();
            File.WriteAllText(
                Path.GetFullPath(_selectedRecipePath),
                JsonUtility.ToJson(_recipe, true),
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(_selectedRecipePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string path = _selectedRecipePath;
            RefreshRecipes(path);
            Debug.Log($"[EnemyStudio] 저장 완료: {_recipe.enemyId}");
        }

        private void BuildSelectedEnemy()
        {
            if (_recipe == null || string.IsNullOrWhiteSpace(_selectedRecipePath))
            {
                return;
            }

            try
            {
                SaveCurrentRecipe();
                EnemyBuildReport report = EnemyAssetBuilder.BuildSingle(_selectedRecipePath);
                string changes = report.changedAssets.Count == 0
                    ? "이미 최신 상태입니다. 다시 쓴 에셋 없음."
                    : $"갱신된 에셋 {report.changedAssets.Count}개:\n· " +
                      string.Join("\n· ", report.changedAssets);
                string validation = report.validationPassed
                    ? "검증 통과"
                    : "검증 실패 — Console을 확인하세요.";
                EditorUtility.DisplayDialog(
                    $"Build {report.enemyId}",
                    $"{changes}\n\n{validation}",
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Enemy Build", exception.Message, "확인");
            }
        }

        private void ValidateAll()
        {
            try
            {
                SaveCurrentRecipe();
                bool valid = EnemyAssetValidator.ValidateAll();
                EditorUtility.DisplayDialog(
                    "Enemy Validation",
                    valid ? "검증 통과" : "검증 실패 — Console을 확인하세요.",
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Enemy Validation", exception.Message, "확인");
            }
        }

        private void BuildAll()
        {
            try
            {
                EnemyAssetBuilder.BuildAll();
                EditorUtility.DisplayDialog(
                    "Enemy Build",
                    "Definition, Catalog, Addressables 갱신 완료",
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Enemy Build", exception.Message, "확인");
            }
        }

        private void DeleteSelectedEnemy()
        {
            if (_recipe == null || string.IsNullOrWhiteSpace(_recipe.enemyId))
            {
                return;
            }

            string enemyId = _recipe.enemyId;
            if (!EditorUtility.DisplayDialog(
                    "적 삭제",
                    $"{enemyId}의 레시피와 모든 자동 생성물을 삭제합니다.\n" +
                    "스테이지에서 사용 중이면 삭제가 중단되며 원본 스프라이트는 보존됩니다.",
                    "삭제",
                    "취소"))
            {
                return;
            }

            try
            {
                EnemyAssetDeletionService.Delete(enemyId);
                RefreshRecipes(null);
                EditorUtility.DisplayDialog("적 삭제", $"삭제 완료: {enemyId}", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("적 삭제 실패", exception.Message, "확인");
            }
        }

        private void EnsureData()
        {
            if (_recipe == null)
            {
                return;
            }

            if (_recipe.data == null)
            {
                _recipe.data = new EnemyData();
            }

            if (_recipe.effectPaths == null)
            {
                _recipe.effectPaths = new List<string>();
            }
        }

        private void SyncIdentityToData()
        {
            _recipe.data.enemyId = _recipe.enemyId;
            _recipe.data.displayName = _recipe.displayName;
        }

        private static string DrawAssetPathField<T>(string label, string path) where T : UnityEngine.Object
        {
            T current = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
            T selected = (T)EditorGUILayout.ObjectField(label, current, typeof(T), false);
            return selected == current
                ? path
                : selected == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(selected);
        }

        private void SwapEffects(int left, int right)
        {
            // 리스트 순서는 효과 호출 순서이므로 UI에서 재배열한 결과를 그대로 레시피에 저장한다.
            if (_recipe == null || _recipe.effectPaths == null)
            {
                return;
            }

            List<string> paths = _recipe.effectPaths;
            if (left < 0 || right < 0 || left >= paths.Count || right >= paths.Count)
            {
                return;
            }

            string temporary = paths[left];
            paths[left] = paths[right];
            paths[right] = temporary;
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

        private static string GetDefinitionPath(string enemyId)
        {
            return string.IsNullOrWhiteSpace(enemyId)
                ? string.Empty
                : $"{OutputRoot}/{enemyId}/EnemyDefinition.asset";
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
