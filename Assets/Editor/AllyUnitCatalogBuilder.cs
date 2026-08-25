using System;
using System.Collections.Generic;
using RCCom.Definitions.Unit;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 아군 유닛 레시피에서 로컬 카탈로그와 "유닛 1종 = 그룹 1개" Addressables
    /// 구성을 함께 만든다. 유닛을 오퍼레이터 Definition의 암묵적 의존성으로 남기지
    /// 않기 위한 패키징 경계다.
    /// </summary>
    public static class AllyUnitCatalogBuilder
    {
        public const string CatalogPath = "Assets/Data/AllyUnits/AllyUnitCatalog.asset";
        public const string AllyUnitGroupPrefix = "AllyUnit-";
        public const string AddressablesLabel = "ally-unit-definition";
        public const string PreviewAddressablesLabel = "ally-unit-preview";
        private const string RecipeFolder = "Assets/Editor/AllyUnitRecipes";
        private const string GeneratedLabel = "RCCom.GeneratedAllyUnit";
        private const string PreviewSlot = "preview";

        [MenuItem("RCCom/Ally Units/Build Ally Unit Catalog And Addressables")]
        public static void BuildAll()
        {
            List<AllyUnitAssetRecipe> recipes = LoadRecipes();
            if (recipes.Count == 0)
            {
                Debug.Log($"[AllyUnitCatalogBuilder] 아군 유닛 레시피가 없어 카탈로그 갱신을 건너뜁니다: {RecipeFolder}");
                return;
            }

            AllyUnitCatalog catalog = GetOrCreateCatalog();
            var entries = new List<AllyUnitCatalogEntry>();
            var expectedGroupNames = new HashSet<string>(StringComparer.Ordinal);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings를 만들지 못했습니다.");
            }

            settings.AddLabel(AddressablesLabel, false);
            settings.AddLabel(PreviewAddressablesLabel, false);
            foreach (AllyUnitAssetRecipe recipe in recipes)
            {
                string definitionPath = GetDefinitionPath(recipe.unitId);
                AllyUnitDefinition definition = AssetDatabase.LoadAssetAtPath<AllyUnitDefinition>(definitionPath);
                if (definition == null)
                {
                    throw new InvalidOperationException($"먼저 아군 유닛 에셋을 생성해야 합니다: {definitionPath}");
                }

                ConfigureAddressable(settings, definitionPath, recipe, GetAddress(recipe.unitId));
                expectedGroupNames.Add(GetGroupName(recipe.unitId, recipe.remoteContent));
                entries.Add(CreateEntry(recipe, definition));
            }

            RemoveStaleGeneratedGroups(settings, expectedGroupNames);
            ApplyEntriesIfChanged(catalog, entries, null);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AllyUnitCatalogBuilder] 카탈로그 {entries.Count}종 및 Addressables 그룹 갱신 완료");
        }

        /// <summary>
        /// 단일 유닛을 갱신한다. 카탈로그의 나머지 항목은 보존하고, 전체 동기화가 필요한
        /// 고아 그룹 정리는 BuildAll에 맡긴다.
        /// </summary>
        public static void BuildForUnit(AllyUnitAssetRecipe recipe, List<string> changedAssets)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.unitId))
            {
                throw new InvalidOperationException("유효하지 않은 아군 유닛 레시피입니다.");
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings를 만들지 못했습니다.");
            }

            settings.AddLabel(AddressablesLabel, false);
            settings.AddLabel(PreviewAddressablesLabel, false);
            string definitionPath = GetDefinitionPath(recipe.unitId);
            AllyUnitDefinition definition = AssetDatabase.LoadAssetAtPath<AllyUnitDefinition>(definitionPath);
            if (definition == null)
            {
                throw new InvalidOperationException($"먼저 아군 유닛 에셋을 생성해야 합니다: {definitionPath}");
            }

            if (ConfigureAddressable(settings, definitionPath, recipe, GetAddress(recipe.unitId)))
            {
                EditorUtility.SetDirty(settings);
                AllyUnitAssetBuilder.RecordChange(AssetDatabase.GetAssetPath(settings), changedAssets);
            }

            AllyUnitCatalog catalog = GetOrCreateCatalog();
            var entries = catalog.entries == null
                ? new List<AllyUnitCatalogEntry>()
                : new List<AllyUnitCatalogEntry>(catalog.entries);
            AllyUnitCatalogEntry updated = CreateEntry(recipe, definition);
            int index = entries.FindIndex(entry =>
                entry != null && string.Equals(entry.unitId, recipe.unitId, StringComparison.Ordinal));
            if (index >= 0)
            {
                entries[index] = updated;
            }
            else
            {
                entries.Add(updated);
            }

            SortEntriesByRecipeOrder(entries);
            ApplyEntriesIfChanged(catalog, entries, changedAssets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AllyUnitCatalogBuilder] {recipe.unitId} 카탈로그 항목과 Addressables 그룹 갱신 완료");
        }

        public static string GetAddress(string unitId)
        {
            return $"ally-unit/{unitId}";
        }

        public static string GetGroupName(string unitId, bool remoteContent)
        {
            return $"{AllyUnitGroupPrefix}{unitId}-{(remoteContent ? "Remote" : "Local")}";
        }

        public static string GetPreviewAddress(string unitId)
        {
            return $"ally-unit/{unitId}/{PreviewSlot}";
        }

        public static string GetDefinitionPath(string unitId)
        {
            return $"Assets/Data/AllyUnits/{unitId}/AllyUnitDefinition.asset";
        }

        private static AllyUnitCatalogEntry CreateEntry(
            AllyUnitAssetRecipe recipe,
            AllyUnitDefinition definition)
        {
            return new AllyUnitCatalogEntry
            {
                unitId = recipe.unitId,
                displayName = recipe.displayName,
                deployCost = recipe.data.deployCost,
                address = GetAddress(recipe.unitId),
                remoteContent = recipe.remoteContent,
                // 원격 Definition의 Sprite가 카탈로그로 새어 들어가면 본체 빌드에 의존성이
                // 생긴다. tint는 값 타입이므로 다운로드 전 스와치로 항상 복사한다.
                previewIcon = recipe.remoteContent ? null : definition.sprite,
                // 위 Sprite가 원격이라 비어 있는 동안 상점/로스터 화면이 아이콘 한 장만 담긴
                // 독립 번들을 내려받을 수 있도록 주소를 남긴다 — ConfigureAddressable이 같은
                // 이름 규칙으로 그 번들을 등록해 둔다.
                previewIconAddress = recipe.remoteContent && !string.IsNullOrWhiteSpace(recipe.spritePath)
                    ? GetPreviewAddress(recipe.unitId)
                    : null,
                fallbackColor = definition.tint,
            };
        }

        private static void ApplyEntriesIfChanged(
            AllyUnitCatalog catalog,
            List<AllyUnitCatalogEntry> entries,
            List<string> changedAssets)
        {
            string before = EditorJsonUtility.ToJson(catalog);
            catalog.entries = entries;
            if (EditorJsonUtility.ToJson(catalog) == before)
            {
                return;
            }

            EditorUtility.SetDirty(catalog);
            AllyUnitAssetBuilder.RecordChange(CatalogPath, changedAssets);
        }

        private static void SortEntriesByRecipeOrder(List<AllyUnitCatalogEntry> entries)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (AllyUnitAssetRecipe recipe in LoadRecipes())
            {
                order[recipe.unitId] = recipe.catalogOrder;
            }

            entries.Sort((left, right) =>
            {
                int leftOrder = order.TryGetValue(left.unitId, out int leftValue) ? leftValue : int.MaxValue;
                int rightOrder = order.TryGetValue(right.unitId, out int rightValue) ? rightValue : int.MaxValue;
                int compared = leftOrder.CompareTo(rightOrder);
                return compared != 0 ? compared : string.CompareOrdinal(left.unitId, right.unitId);
            });
        }

        private static bool ConfigureAddressable(
            AddressableAssetSettings settings,
            string definitionPath,
            AllyUnitAssetRecipe recipe,
            string address)
        {
            bool changed = false;
            string groupName = GetGroupName(recipe.unitId, recipe.remoteContent);
            // 원격 그룹은 PackSeparately로 묶는다: 미리보기 아이콘을 Definition과 별개
            // 항목으로 등록해도 PackTogether면 결국 한 번들로 합쳐져, 상점/로스터가 아이콘
            // 하나만 보려 해도 프리팹·이펙트가 딸린 Definition 전체를 받게 된다. 로컬
            // 그룹은 어차피 본체 빌드에 통째로 들어가므로 번들 수를 굳이 늘리지 않는다.
            AddressableAssetGroup group = AddressableGroupPolicy.EnsureGroup(
                settings,
                groupName,
                recipe.remoteContent,
                recipe.remoteContent
                    ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately
                    : BundledAssetGroupSchema.BundlePackingMode.PackTogether,
                ref changed);

            string guid = AssetDatabase.AssetPathToGUID(definitionPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, true);
            if (entry.address != address)
            {
                entry.address = address;
                changed = true;
            }

            if (!entry.labels.Contains(AddressablesLabel))
            {
                entry.SetLabel(AddressablesLabel, true, true, false);
                changed = true;
            }

            if (recipe.remoteContent && !string.IsNullOrWhiteSpace(recipe.spritePath))
            {
                string spriteGuid = AssetDatabase.AssetPathToGUID(recipe.spritePath);
                if (string.IsNullOrEmpty(spriteGuid))
                {
                    throw new InvalidOperationException($"스프라이트 경로가 실제 에셋을 가리키지 않습니다: {recipe.spritePath}");
                }

                string previewAddress = GetPreviewAddress(recipe.unitId);
                AddressableAssetEntry previewEntry = settings.CreateOrMoveEntry(spriteGuid, group, false, true);
                if (previewEntry.address != previewAddress)
                {
                    previewEntry.address = previewAddress;
                    changed = true;
                }

                if (!previewEntry.labels.Contains(PreviewAddressablesLabel))
                {
                    previewEntry.SetLabel(PreviewAddressablesLabel, true, true, false);
                    changed = true;
                }
            }

            return changed;
        }

        private static void RemoveStaleGeneratedGroups(
            AddressableAssetSettings settings,
            HashSet<string> expectedGroupNames)
        {
            var groups = new List<AddressableAssetGroup>(settings.groups);
            foreach (AddressableAssetGroup group in groups)
            {
                if (group == null || !group.Name.StartsWith(AllyUnitGroupPrefix, StringComparison.Ordinal) ||
                    expectedGroupNames.Contains(group.Name))
                {
                    continue;
                }

                if (group.entries.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"이전 아군 유닛 그룹에 항목이 남아 자동 삭제할 수 없습니다: {group.Name}");
                }

                settings.RemoveGroup(group);
                Debug.Log($"[AllyUnitCatalogBuilder] 사용하지 않는 빈 그룹 정리: {group.Name}");
            }
        }

        private static AllyUnitCatalog GetOrCreateCatalog()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(CatalogPath);
            if (existing != null)
            {
                if (existing is not AllyUnitCatalog catalog)
                {
                    throw new InvalidOperationException($"카탈로그 경로에 다른 타입의 에셋이 있습니다: {CatalogPath}");
                }

                if (Array.IndexOf(AssetDatabase.GetLabels(catalog), GeneratedLabel) < 0)
                {
                    throw new InvalidOperationException(
                        $"자동 생성 라벨 없는 카탈로그는 덮어쓸 수 없습니다: {CatalogPath}");
                }

                return catalog;
            }

            var created = ScriptableObject.CreateInstance<AllyUnitCatalog>();
            AssetDatabase.CreateAsset(created, CatalogPath);
            AssetDatabase.SetLabels(created, new[] { GeneratedLabel });
            return created;
        }

        private static List<AllyUnitAssetRecipe> LoadRecipes()
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
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                AllyUnitAssetRecipe recipe = JsonUtility.FromJson<AllyUnitAssetRecipe>(text.text);
                if (recipe == null || string.IsNullOrWhiteSpace(recipe.unitId))
                {
                    throw new InvalidOperationException($"유효하지 않은 아군 유닛 레시피입니다: {path}");
                }

                if (!ids.Add(recipe.unitId))
                {
                    throw new InvalidOperationException($"아군 유닛 레시피 ID가 중복됩니다: {recipe.unitId}");
                }

                recipes.Add(recipe);
            }

            recipes.Sort((left, right) =>
            {
                int order = left.catalogOrder.CompareTo(right.catalogOrder);
                return order != 0 ? order : string.CompareOrdinal(left.unitId, right.unitId);
            });
            return recipes;
        }
    }
}
