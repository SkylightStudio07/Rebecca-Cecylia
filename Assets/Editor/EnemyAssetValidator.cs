using System;
using System.Collections.Generic;
using RCCom.Definitions.Enemy;
using RCCom.Effects.Enemy;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 적 레시피·카탈로그·Addressables 구성이 서로 어긋나지 않는지 검사한다.
    /// OperatorAssetValidator와 달리 레시피 JSON을 직접 열어 spritePath/effectPaths 같은
    /// 경로 문자열이 실제 에셋을 가리키는지도 확인한다 — 오퍼레이터와 달리 이 참조들은
    /// GUID가 아니라 경로 문자열이라 에셋을 옮기면 조용히 끊어질 수 있기 때문이다.
    /// </summary>
    public static class EnemyAssetValidator
    {
        private const string RecipeFolder = "Assets/Editor/EnemyRecipes";

        [MenuItem("RCCom/Enemies/Validate Enemy Assets")]
        public static void ValidateMenu()
        {
            if (!ValidateAll())
            {
                throw new InvalidOperationException("적 에셋 검증에 실패했습니다. 콘솔 오류를 확인하세요.");
            }
        }

        public static bool ValidateAll(bool logSuccess = true)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            List<EnemyAssetRecipe> recipes = LoadRecipes(errors);
            ValidateRecipeIdsAndDefinitions(recipes, errors);
            ValidateRecipeAssetPaths(recipes, errors);
            ValidateRoster(recipes, errors);
            ValidateCatalogAndAddressables(recipes, errors);

            foreach (string error in errors)
            {
                Debug.LogError($"[EnemyAssetValidator] {error}");
            }

            foreach (string warning in warnings)
            {
                Debug.LogWarning($"[EnemyAssetValidator] {warning}");
            }

            if (errors.Count == 0 && logSuccess)
            {
                Debug.Log($"[EnemyAssetValidator] 적 에셋 검증 통과 (레시피 {recipes.Count}개, 경고 {warnings.Count}건)");
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// 레시피가 아직 하나도 없는 상태(마이그레이션 전)는 오류가 아니다 — 파이프라인
        /// 골격만 갖춘 정상 상태를 검증 실패로 취급하면 기존 빌드/검증 루프가 막힌다.
        /// </summary>
        private static List<EnemyAssetRecipe> LoadRecipes(List<string> errors)
        {
            var recipes = new List<EnemyAssetRecipe>();
            if (!AssetDatabase.IsValidFolder(RecipeFolder))
            {
                return recipes;
            }

            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { RecipeFolder });
            var paths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                EnemyAssetRecipe recipe = JsonUtility.FromJson<EnemyAssetRecipe>(text.text);
                if (recipe == null || string.IsNullOrWhiteSpace(recipe.enemyId))
                {
                    errors.Add($"유효하지 않은 적 레시피입니다: {path}");
                    continue;
                }

                if (!seenIds.Add(recipe.enemyId))
                {
                    errors.Add($"적 레시피 ID가 중복됩니다: {recipe.enemyId} ({path})");
                    continue;
                }

                recipes.Add(recipe);
            }

            return recipes;
        }

        private static void ValidateRecipeIdsAndDefinitions(List<EnemyAssetRecipe> recipes, List<string> errors)
        {
            foreach (EnemyAssetRecipe recipe in recipes)
            {
                foreach (char character in recipe.enemyId)
                {
                    bool isAllowed = character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
                    if (!isAllowed)
                    {
                        errors.Add($"enemyId는 영문 소문자, 숫자, -, _만 사용할 수 있습니다: {recipe.enemyId}");
                        break;
                    }
                }

                string definitionPath = $"Assets/Data/Enemies/{recipe.enemyId}/EnemyDefinition.asset";
                if (AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitionPath) == null)
                {
                    errors.Add($"레시피에 대응하는 EnemyDefinition이 없습니다: {definitionPath}");
                }
            }
        }

        private static void ValidateRecipeAssetPaths(List<EnemyAssetRecipe> recipes, List<string> errors)
        {
            foreach (EnemyAssetRecipe recipe in recipes)
            {
                if (!string.IsNullOrWhiteSpace(recipe.spritePath) &&
                    AssetDatabase.LoadAssetAtPath<Sprite>(recipe.spritePath) == null)
                {
                    errors.Add($"스프라이트 경로가 실제 에셋을 가리키지 않습니다: {recipe.spritePath} ({recipe.enemyId})");
                }

                if (recipe.effectPaths == null)
                {
                    continue;
                }

                foreach (string effectPath in recipe.effectPaths)
                {
                    if (string.IsNullOrWhiteSpace(effectPath) ||
                        AssetDatabase.LoadAssetAtPath<EnemyEffectBase>(effectPath) == null)
                    {
                        errors.Add($"효과 경로가 실제 에셋을 가리키지 않습니다: {effectPath} ({recipe.enemyId})");
                    }
                }
            }
        }

        private static void ValidateRoster(List<EnemyAssetRecipe> recipes, List<string> errors)
        {
            EnemyRoster roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>(EnemyCatalogBuilder.RosterPath);
            if (roster == null)
            {
                errors.Add($"전투용 EnemyRoster가 없습니다: {EnemyCatalogBuilder.RosterPath}");
                return;
            }

            var registeredIds = new HashSet<string>(StringComparer.Ordinal);
            if (roster.enemyIds != null)
            {
                foreach (string enemyId in roster.enemyIds)
                {
                    if (string.IsNullOrWhiteSpace(enemyId))
                    {
                        errors.Add("EnemyRoster에 비어 있는 enemyId가 있습니다.");
                    }
                    else if (!registeredIds.Add(enemyId))
                    {
                        errors.Add($"EnemyRoster의 enemyId가 중복됩니다: {enemyId}");
                    }
                }
            }

            foreach (EnemyAssetRecipe recipe in recipes)
            {
                if (!registeredIds.Contains(recipe.enemyId))
                {
                    errors.Add($"적 레시피가 EnemyRoster에 등록되지 않았습니다: {recipe.enemyId}");
                }
            }
        }

        private static void ValidateCatalogAndAddressables(List<EnemyAssetRecipe> recipes, List<string> errors)
        {
            if (recipes.Count == 0)
            {
                // 레시피가 없으면 카탈로그도 아직 없는 것이 정상이다 — EnemyCatalogBuilder가
                // 그런 상태를 그대로 두고 넘어가므로 여기서도 동일하게 통과시킨다.
                return;
            }

            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(EnemyCatalogBuilder.CatalogPath);
            if (catalog == null || catalog.entries == null || catalog.entries.Count == 0)
            {
                errors.Add("EnemyCatalog가 없거나 비어 있습니다.");
                return;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                errors.Add("Addressables Settings가 없습니다.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var expectedGroups = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnemyCatalogEntry catalogEntry in catalog.entries)
            {
                if (catalogEntry == null || string.IsNullOrWhiteSpace(catalogEntry.enemyId) ||
                    string.IsNullOrWhiteSpace(catalogEntry.address))
                {
                    errors.Add("EnemyCatalog에 식별자 또는 주소가 비어 있는 항목이 있습니다.");
                    continue;
                }

                if (!ids.Add(catalogEntry.enemyId))
                {
                    errors.Add($"EnemyCatalog의 enemyId가 중복됩니다: {catalogEntry.enemyId}");
                }

                string expectedAddress = $"enemy/{catalogEntry.enemyId}";
                if (catalogEntry.address != expectedAddress)
                {
                    errors.Add($"EnemyCatalog 주소 규칙이 잘못되었습니다: {catalogEntry.address} (기대값 {expectedAddress})");
                }

                if (catalogEntry.remoteContent && catalogEntry.previewSprite != null)
                {
                    errors.Add($"원격 적의 미리보기 스프라이트가 로컬 카탈로그에 참조됩니다: {catalogEntry.enemyId}");
                }

                string definitionPath = $"Assets/Data/Enemies/{catalogEntry.enemyId}/EnemyDefinition.asset";
                AddressableAssetEntry addressableEntry =
                    settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(definitionPath));
                if (addressableEntry == null || addressableEntry.address != catalogEntry.address)
                {
                    errors.Add($"Definition의 Addressables 주소가 카탈로그와 일치하지 않습니다: {definitionPath}");
                }
                else if (!addressableEntry.labels.Contains(EnemyCatalogBuilder.AddressablesLabel))
                {
                    errors.Add($"Definition에 필수 Addressables 라벨이 없습니다: {definitionPath}");
                }

                string expectedGroup = EnemyCatalogBuilder.GetGroupName(catalogEntry.enemyId, catalogEntry.remoteContent);
                expectedGroups.Add(expectedGroup);
                if (addressableEntry != null && addressableEntry.parentGroup.Name != expectedGroup)
                {
                    errors.Add($"Definition의 Addressables 그룹이 잘못되었습니다: {definitionPath}");
                }

                AddressableAssetGroup group = settings.FindGroup(expectedGroup);
                if (group == null)
                {
                    errors.Add($"적 Addressables 그룹이 없습니다: {expectedGroup}");
                    continue;
                }

                if (group.entries.Count != 1)
                {
                    errors.Add($"적 그룹은 명시적 Definition 1개만 가져야 합니다: {expectedGroup}");
                }

                BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
                if (bundled == null)
                {
                    errors.Add($"적 그룹에 BundledAssetGroupSchema가 없습니다: {expectedGroup}");
                    continue;
                }

                string expectedBuildPath = catalogEntry.remoteContent
                    ? AddressableAssetSettings.kRemoteBuildPath
                    : AddressableAssetSettings.kLocalBuildPath;
                string expectedLoadPath = catalogEntry.remoteContent
                    ? AddressableAssetSettings.kRemoteLoadPath
                    : AddressableAssetSettings.kLocalLoadPath;
                if (bundled.BuildPath.GetName(settings) != expectedBuildPath ||
                    bundled.LoadPath.GetName(settings) != expectedLoadPath)
                {
                    errors.Add($"적 그룹의 Build/Load 경로 유형이 잘못되었습니다: {expectedGroup}");
                }
            }

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group != null &&
                    group.Name.StartsWith(EnemyCatalogBuilder.EnemyGroupPrefix, StringComparison.Ordinal) &&
                    !expectedGroups.Contains(group.Name))
                {
                    errors.Add($"카탈로그에 대응하지 않는 이전 적 그룹이 남아 있습니다: {group.Name}");
                }
            }
        }
    }
}
