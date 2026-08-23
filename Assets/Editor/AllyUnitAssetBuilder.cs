using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Unit;
using RCCom.Effects.Unit;
using RCCom.Effects.UnitVisual;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// JSON 레시피에서 아군 유닛 Definition을 생성한다.
    /// 생성물에는 전용 라벨을 붙여 사람이 만든 에셋을 자동화가 덮어쓰지 않게 한다.
    /// </summary>
    public static class AllyUnitAssetBuilder
    {
        private const string RecipeFolder = "Assets/Editor/AllyUnitRecipes";
        private const string OutputRoot = "Assets/Data/AllyUnits";
        private const string GeneratedLabel = "RCCom.GeneratedAllyUnit";

        [MenuItem("RCCom/Ally Units/Build All Ally Unit Assets")]
        public static void BuildAll()
        {
            List<string> recipePaths = FindRecipePaths();
            if (recipePaths.Count == 0)
            {
                Debug.Log($"[AllyUnitAssetBuilder] 아군 유닛 레시피가 없어 빌드를 건너뜁니다: {RecipeFolder}");
                return;
            }

            EnsureFolder(OutputRoot);
            var changedAssets = new List<string>();
            foreach (string recipePath in recipePaths)
            {
                TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(recipePath);
                AllyUnitAssetRecipe recipe = JsonUtility.FromJson<AllyUnitAssetRecipe>(recipeAsset.text);
                ValidateRecipe(recipe, recipePath);
                BuildUnit(recipe, changedAssets);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AllyUnitCatalogBuilder.BuildAll();

            if (!AllyUnitAssetValidator.ValidateAll(false))
            {
                throw new InvalidOperationException(
                    "아군 유닛 에셋 생성 후 검증에 실패했습니다. 콘솔 오류를 확인하세요.");
            }

            Debug.Log(
                $"[AllyUnitAssetBuilder] 아군 유닛 {recipePaths.Count}종 생성/갱신 및 검증 완료 " +
                $"(내용이 바뀐 에셋 {changedAssets.Count}개)");
        }

        /// <summary>
        /// 선택한 레시피만 빌드하되, 공유 카탈로그 검증은 전체 대상으로 실행한다.
        /// </summary>
        public static AllyUnitBuildReport BuildSingle(string recipePath)
        {
            TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(recipePath);
            if (recipeAsset == null)
            {
                throw new InvalidOperationException($"아군 유닛 레시피를 찾지 못했습니다: {recipePath}");
            }

            AllyUnitAssetRecipe recipe = JsonUtility.FromJson<AllyUnitAssetRecipe>(recipeAsset.text);
            ValidateRecipe(recipe, recipePath);
            EnsureFolder(OutputRoot);

            var report = new AllyUnitBuildReport { unitId = recipe.unitId };
            BuildUnit(recipe, report.changedAssets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AllyUnitCatalogBuilder.BuildForUnit(recipe, report.changedAssets);
            report.validationPassed = AllyUnitAssetValidator.ValidateAll(false);
            Debug.Log(
                $"[AllyUnitAssetBuilder] {recipe.unitId} 단일 빌드 완료 " +
                $"(내용이 바뀐 에셋 {report.changedAssets.Count}개, " +
                $"검증 {(report.validationPassed ? "통과" : "실패")})");
            return report;
        }

        private static void BuildUnit(AllyUnitAssetRecipe recipe, List<string> changedAssets)
        {
            Sprite sprite = LoadOptional<Sprite>(recipe.spritePath);
            var effects = new List<AllyUnitEffectBase>();
            if (recipe.effectPaths != null)
            {
                foreach (string effectPath in recipe.effectPaths)
                {
                    AllyUnitEffectBase effect = LoadOptional<AllyUnitEffectBase>(effectPath);
                    if (effect != null)
                    {
                        effects.Add(effect);
                    }
                }
            }

            var visualEffects = new List<AllyUnitVisualEffectBase>();
            if (recipe.visualEffectPaths != null)
            {
                foreach (string visualEffectPath in recipe.visualEffectPaths)
                {
                    AllyUnitVisualEffectBase visualEffect =
                        LoadOptional<AllyUnitVisualEffectBase>(visualEffectPath);
                    if (visualEffect != null)
                    {
                        visualEffects.Add(visualEffect);
                    }
                }
            }

            string unitFolder = $"{OutputRoot}/{recipe.unitId}";
            EnsureFolder(unitFolder);
            AllyUnitDefinition definition = GetOrCreateOwnedAsset<AllyUnitDefinition>(
                $"{unitFolder}/AllyUnitDefinition.asset",
                changedAssets);
            ApplyIfChanged(definition, asset =>
            {
                asset.data = CloneData(recipe);
                asset.effects = effects;
                asset.visualEffects = visualEffects;
                asset.sprite = sprite;
                asset.tint = recipe.tint;
                asset.spriteForwardOffsetDegrees = recipe.spriteForwardOffsetDegrees;
            }, changedAssets);
        }

        private static AllyUnitData CloneData(AllyUnitAssetRecipe recipe)
        {
            AllyUnitData source = recipe.data ?? new AllyUnitData();
            return new AllyUnitData
            {
                unitId = recipe.unitId,
                displayName = recipe.displayName,
                deployCost = source.deployCost,
                maxHealth = source.maxHealth,
                moveSpeed = source.moveSpeed,
                attackDamage = source.attackDamage,
                attackInterval = source.attackInterval,
                attackRange = source.attackRange,
                detectionRange = source.detectionRange,
                projectileSpeed = source.projectileSpeed,
            };
        }

        private static void ApplyIfChanged<T>(
            T asset,
            Action<T> mutate,
            List<string> changedAssets)
            where T : UnityEngine.Object
        {
            string before = EditorJsonUtility.ToJson(asset);
            mutate(asset);
            if (EditorJsonUtility.ToJson(asset) == before)
            {
                return;
            }

            EditorUtility.SetDirty(asset);
            RecordChange(AssetDatabase.GetAssetPath(asset), changedAssets);
        }

        internal static void RecordChange(string path, List<string> changedAssets)
        {
            if (changedAssets == null || string.IsNullOrEmpty(path) || changedAssets.Contains(path))
            {
                return;
            }

            changedAssets.Add(path);
        }

        private static T LoadOptional<T>(string path) where T : UnityEngine.Object
        {
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static T GetOrCreateOwnedAsset<T>(
            string path,
            List<string> changedAssets)
            where T : ScriptableObject
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null)
            {
                if (existing is not T typedAsset)
                {
                    throw new InvalidOperationException($"생성 대상 경로에 다른 타입의 에셋이 있습니다: {path}");
                }

                string[] labels = AssetDatabase.GetLabels(typedAsset);
                if (Array.IndexOf(labels, GeneratedLabel) < 0)
                {
                    throw new InvalidOperationException(
                        $"자동 생성 라벨이 없는 기존 에셋은 덮어쓸 수 없습니다: {path}");
                }

                return typedAsset;
            }

            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SetLabels(created, new[] { GeneratedLabel });
            RecordChange(path, changedAssets);
            return created;
        }

        private static void ValidateRecipe(AllyUnitAssetRecipe recipe, string recipePath)
        {
            if (recipe == null)
            {
                throw new InvalidOperationException($"JSON을 읽지 못했습니다: {recipePath}");
            }

            if (!IsValidId(recipe.unitId))
            {
                throw new InvalidOperationException(
                    $"unitId는 영문 소문자, 숫자, -, _만 사용할 수 있습니다: {recipe.unitId}");
            }

            if (string.IsNullOrWhiteSpace(recipe.displayName) || recipe.data == null)
            {
                throw new InvalidOperationException($"표시 이름 또는 AllyUnitData가 비어 있습니다: {recipePath}");
            }

            AllyUnitData data = recipe.data;
            if (data.deployCost < 0 || data.maxHealth <= 0f || data.moveSpeed < 0f ||
                data.attackInterval <= 0f || data.attackRange < 0f ||
                data.detectionRange < data.attackRange || data.projectileSpeed < 0f)
            {
                throw new InvalidOperationException(
                    $"AllyUnitData의 필수 수치가 유효하지 않습니다: {recipePath}");
            }
        }

        private static List<string> FindRecipePaths()
        {
            var recipePaths = new List<string>();
            if (!AssetDatabase.IsValidFolder(RecipeFolder))
            {
                return recipePaths;
            }

            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { RecipeFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    recipePaths.Add(path);
                }
            }

            recipePaths.Sort(StringComparer.Ordinal);
            return recipePaths;
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
