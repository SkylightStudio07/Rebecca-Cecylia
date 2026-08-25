using System;
using System.Collections.Generic;
using RCCom.Definitions.Unit;
using RCCom.Effects.Unit;
using RCCom.Effects.UnitVisual;
using RCCom.Effects.UnitVisual.Concrete;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 아군 유닛 레시피·Definition·Roster·카탈로그·Addressables 구성이 일치하는지 검사한다.
    /// 레시피의 경로 문자열은 에셋 이동 시 끊길 수 있으므로 GUID 참조만 믿지 않고 경로도
    /// 여기서 함께 확인한다.
    /// </summary>
    public static class AllyUnitAssetValidator
    {
        private const string RecipeFolder = "Assets/Editor/AllyUnitRecipes";

        [MenuItem("RCCom/Ally Units/Validate Ally Unit Assets")]
        public static void ValidateMenu()
        {
            if (!ValidateAll())
            {
                throw new InvalidOperationException(
                    "아군 유닛 에셋 검증에 실패했습니다. 콘솔 오류를 확인하세요.");
            }
        }

        public static bool ValidateAll(bool logSuccess = true)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            List<AllyUnitAssetRecipe> recipes = LoadRecipes(errors);
            ValidateRecipeIdsAndDefinitions(recipes, errors);
            ValidateRecipeAssetPaths(recipes, errors);
            ValidateCatalogAndAddressables(recipes, errors);
            ValidateRosters(errors);

            foreach (string error in errors)
            {
                Debug.LogError($"[AllyUnitAssetValidator] {error}");
            }

            foreach (string warning in warnings)
            {
                Debug.LogWarning($"[AllyUnitAssetValidator] {warning}");
            }

            if (errors.Count == 0 && logSuccess)
            {
                Debug.Log(
                    $"[AllyUnitAssetValidator] 아군 유닛 에셋 검증 통과 " +
                    $"(레시피 {recipes.Count}개, 경고 {warnings.Count}건)");
            }

            return errors.Count == 0;
        }

        private static List<AllyUnitAssetRecipe> LoadRecipes(List<string> errors)
        {
            var recipes = new List<AllyUnitAssetRecipe>();
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
                AllyUnitAssetRecipe recipe = JsonUtility.FromJson<AllyUnitAssetRecipe>(text.text);
                if (recipe == null || string.IsNullOrWhiteSpace(recipe.unitId))
                {
                    errors.Add($"유효하지 않은 아군 유닛 레시피입니다: {path}");
                    continue;
                }

                if (!seenIds.Add(recipe.unitId))
                {
                    errors.Add($"아군 유닛 레시피 ID가 중복됩니다: {recipe.unitId} ({path})");
                    continue;
                }

                recipes.Add(recipe);
            }

            return recipes;
        }

        private static void ValidateRecipeIdsAndDefinitions(
            List<AllyUnitAssetRecipe> recipes,
            List<string> errors)
        {
            foreach (AllyUnitAssetRecipe recipe in recipes)
            {
                if (!IsValidId(recipe.unitId))
                {
                    errors.Add(
                        $"unitId는 영문 소문자, 숫자, -, _만 사용할 수 있습니다: {recipe.unitId}");
                }

                string definitionPath = AllyUnitCatalogBuilder.GetDefinitionPath(recipe.unitId);
                AllyUnitDefinition definition =
                    AssetDatabase.LoadAssetAtPath<AllyUnitDefinition>(definitionPath);
                if (definition == null)
                {
                    errors.Add($"레시피에 대응하는 AllyUnitDefinition이 없습니다: {definitionPath}");
                    continue;
                }

                if (definition.data == null || definition.data.unitId != recipe.unitId)
                {
                    errors.Add($"Definition과 레시피의 unitId가 일치하지 않습니다: {definitionPath}");
                }

                if (definition.data == null || string.IsNullOrWhiteSpace(definition.data.displayName) ||
                    definition.data.maxHealth <= 0f || definition.data.moveSpeed < 0f ||
                    definition.data.attackInterval <= 0f || definition.data.attackRange < 0f ||
                    definition.data.detectionRange < definition.data.attackRange ||
                    definition.data.deployCost < 0 || definition.data.projectileSpeed < 0f)
                {
                    errors.Add($"아군 유닛 필수 수치가 유효하지 않습니다: {definitionPath}");
                }

                if (definition.effects == null)
                {
                    errors.Add($"아군 유닛 효과 목록이 null입니다: {definitionPath}");
                }
                else
                {
                    foreach (AllyUnitEffectBase effect in definition.effects)
                    {
                        if (effect == null)
                        {
                            errors.Add($"아군 유닛 효과 목록에 null 항목이 있습니다: {definitionPath}");
                            break;
                        }
                    }

                    // 서포트 전용 유닛은 주 공격 0개가 정상이다. 둘 이상만 막아야 오라 조립만으로
                    // 만든 드론을 강제로 공격형으로 바꾸지 않으면서 중복 피해는 차단할 수 있다.
                    int primaryAttackCount = CountPrimaryAttackEffects(definition.effects);
                    if (primaryAttackCount > 1)
                    {
                        errors.Add(
                            $"아군 유닛 주 공격 Effect는 최대 1개만 조립할 수 있습니다 " +
                            $"(현재 {primaryAttackCount}개): {definitionPath}");
                    }
                }

                if (definition.visualEffects == null)
                {
                    errors.Add($"아군 유닛 비주얼 효과 목록이 null입니다: {definitionPath}");
                }
                else
                {
                    foreach (AllyUnitVisualEffectBase visualEffect in definition.visualEffects)
                    {
                        if (visualEffect == null)
                        {
                            errors.Add($"아군 유닛 비주얼 효과 목록에 null 항목이 있습니다: {definitionPath}");
                            break;
                        }

                        if (visualEffect is RangePulseVisualEffect rangePulse &&
                            rangePulse.Material == null)
                        {
                            errors.Add($"범위 파동 비주얼 효과에 Material이 없습니다: {definitionPath}");
                        }
                    }
                }
            }
        }

        private static void ValidateRecipeAssetPaths(
            List<AllyUnitAssetRecipe> recipes,
            List<string> errors)
        {
            foreach (AllyUnitAssetRecipe recipe in recipes)
            {
                if (!string.IsNullOrWhiteSpace(recipe.spritePath) &&
                    AssetDatabase.LoadAssetAtPath<Sprite>(recipe.spritePath) == null)
                {
                    errors.Add(
                        $"스프라이트 경로가 실제 에셋을 가리키지 않습니다: " +
                        $"{recipe.spritePath} ({recipe.unitId})");
                }

                if (recipe.effectPaths != null)
                {
                    foreach (string effectPath in recipe.effectPaths)
                    {
                        if (string.IsNullOrWhiteSpace(effectPath) ||
                            AssetDatabase.LoadAssetAtPath<AllyUnitEffectBase>(effectPath) == null)
                        {
                            errors.Add(
                                $"효과 경로가 실제 에셋을 가리키지 않습니다: " +
                                $"{effectPath} ({recipe.unitId})");
                        }
                    }
                }

                if (recipe.visualEffectPaths == null)
                {
                    continue;
                }

                foreach (string visualEffectPath in recipe.visualEffectPaths)
                {
                    if (string.IsNullOrWhiteSpace(visualEffectPath) ||
                        AssetDatabase.LoadAssetAtPath<AllyUnitVisualEffectBase>(visualEffectPath) == null)
                    {
                        errors.Add(
                            $"비주얼 효과 경로가 실제 에셋을 가리키지 않습니다: " +
                            $"{visualEffectPath} ({recipe.unitId})");
                    }
                }
            }
        }

        private static void ValidateCatalogAndAddressables(
            List<AllyUnitAssetRecipe> recipes,
            List<string> errors)
        {
            if (recipes.Count == 0)
            {
                return;
            }

            AllyUnitCatalog catalog =
                AssetDatabase.LoadAssetAtPath<AllyUnitCatalog>(AllyUnitCatalogBuilder.CatalogPath);
            if (catalog == null || catalog.entries == null || catalog.entries.Count == 0)
            {
                errors.Add("AllyUnitCatalog가 없거나 비어 있습니다.");
                return;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                errors.Add("Addressables Settings가 없습니다.");
                return;
            }

            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AllyUnitAssetRecipe recipe in recipes)
            {
                recipeIds.Add(recipe.unitId);
                AllyUnitCatalogEntry entry = catalog.FindById(recipe.unitId);
                if (entry == null)
                {
                    errors.Add($"AllyUnitCatalog에 레시피 항목이 없습니다: {recipe.unitId}");
                    continue;
                }

                if (entry.address != AllyUnitCatalogBuilder.GetAddress(recipe.unitId))
                {
                    errors.Add($"AllyUnitCatalog 주소 규칙이 잘못되었습니다: {entry.address}");
                }

                if (entry.remoteContent && entry.previewIcon != null)
                {
                    errors.Add($"원격 아군 유닛의 미리보기 스프라이트가 참조됩니다: {recipe.unitId}");
                }

                string definitionPath = AllyUnitCatalogBuilder.GetDefinitionPath(recipe.unitId);
                AddressableAssetEntry addressableEntry =
                    settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(definitionPath));
                if (addressableEntry == null || addressableEntry.address != entry.address)
                {
                    errors.Add($"Definition의 Addressables 주소가 카탈로그와 일치하지 않습니다: {definitionPath}");
                    continue;
                }

                if (!addressableEntry.labels.Contains(AllyUnitCatalogBuilder.AddressablesLabel))
                {
                    errors.Add($"Definition에 필수 Addressables 라벨이 없습니다: {definitionPath}");
                }

                string expectedGroup =
                    AllyUnitCatalogBuilder.GetGroupName(recipe.unitId, recipe.remoteContent);
                if (addressableEntry.parentGroup == null ||
                    addressableEntry.parentGroup.Name != expectedGroup)
                {
                    errors.Add($"Definition의 Addressables 그룹이 잘못되었습니다: {definitionPath}");
                }

                AddressableAssetGroup group = settings.FindGroup(expectedGroup);
                if (group == null)
                {
                    errors.Add($"아군 유닛 Addressables 그룹이 없습니다: {expectedGroup}");
                    continue;
                }

                // Definition 1개는 필수고, 원격 유닛은 미리보기 아이콘 항목을 같은 그룹에
                // PackSeparately로 함께 둘 수 있다(RemotePreviewSpriteLoader가 Definition
                // 전체를 당기지 않고 아이콘만 받게 하기 위함). 두 라벨 중 어디에도 안 걸린
                // 항목이 섞여 있으면 수작업 오염으로 본다.
                int definitionEntryCount = 0;
                int strayEntryCount = 0;
                foreach (AddressableAssetEntry groupEntry in group.entries)
                {
                    if (groupEntry.labels.Contains(AllyUnitCatalogBuilder.AddressablesLabel))
                    {
                        definitionEntryCount++;
                    }
                    else if (!groupEntry.labels.Contains(AllyUnitCatalogBuilder.PreviewAddressablesLabel))
                    {
                        strayEntryCount++;
                    }
                }

                if (definitionEntryCount != 1 || strayEntryCount != 0)
                {
                    errors.Add($"아군 유닛 그룹은 Definition 1개와 미리보기 아이콘 항목만 가져야 합니다: {expectedGroup}");
                }

                BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
                if (bundled == null)
                {
                    errors.Add($"아군 유닛 그룹에 BundledAssetGroupSchema가 없습니다: {expectedGroup}");
                    continue;
                }

                string expectedBuildPath = recipe.remoteContent
                    ? AddressableAssetSettings.kRemoteBuildPath
                    : AddressableAssetSettings.kLocalBuildPath;
                string expectedLoadPath = recipe.remoteContent
                    ? AddressableAssetSettings.kRemoteLoadPath
                    : AddressableAssetSettings.kLocalLoadPath;
                if (bundled.BuildPath.GetName(settings) != expectedBuildPath ||
                    bundled.LoadPath.GetName(settings) != expectedLoadPath)
                {
                    errors.Add($"아군 유닛 그룹의 Build/Load 경로 유형이 잘못되었습니다: {expectedGroup}");
                }
            }

            foreach (AllyUnitCatalogEntry entry in catalog.entries)
            {
                if (entry == null || !recipeIds.Contains(entry.unitId))
                {
                    errors.Add($"카탈로그에 대응하지 않는 아군 유닛 항목이 남아 있습니다: {entry?.unitId}");
                }
            }
        }

        private static void ValidateRosters(List<string> errors)
        {
            string[] guids = AssetDatabase.FindAssets("t:AllyUnitRoster");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AllyUnitRoster roster = AssetDatabase.LoadAssetAtPath<AllyUnitRoster>(path);
                if (roster == null || roster.unitIds == null || roster.unitIds.Count == 0)
                {
                    errors.Add($"AllyUnitRoster의 unitIds가 비어 있습니다: {path}");
                    continue;
                }

                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (string unitId in roster.unitIds)
                {
                    if (string.IsNullOrWhiteSpace(unitId) || !ids.Add(unitId))
                    {
                        errors.Add($"AllyUnitRoster의 unitId가 비어 있거나 중복됩니다: {path}");
                        continue;
                    }

                    if (AssetDatabase.LoadAssetAtPath<AllyUnitDefinition>(
                            AllyUnitCatalogBuilder.GetDefinitionPath(unitId)) == null)
                    {
                        errors.Add($"AllyUnitRoster가 존재하지 않는 유닛을 가리킵니다: {unitId} ({path})");
                    }
                }
            }
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

        /// <summary>
        /// 메모리 임시 SO로 실제 Validator의 역할 판정을 회귀 검증하기 위한 진입점.
        /// 정책은 ValidateRecipeIdsAndDefinitions에 남기고, 타입 판정만 공유해 검증 규칙이
        /// 구체 Effect 이름 목록과 따로 놀지 않게 한다.
        /// </summary>
        internal static int CountPrimaryAttackEffects(IReadOnlyList<AllyUnitEffectBase> effects)
        {
            if (effects == null)
            {
                return 0;
            }

            int count = 0;
            foreach (AllyUnitEffectBase effect in effects)
            {
                if (effect is IAllyUnitPrimaryAttackEffect)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
