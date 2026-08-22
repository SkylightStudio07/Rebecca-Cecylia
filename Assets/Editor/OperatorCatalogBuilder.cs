using System;
using System.Collections.Generic;
using RCCom.Definitions.Operator;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 오퍼레이터 레시피에서 로컬 카탈로그와 "오퍼레이터 1명 = 그룹 1개" Addressables
    /// 구성을 함께 만든다. 콘텐츠 추가 시 수작업 그룹 배선이 누락되지 않게 한 경로로 묶는다.
    /// </summary>
    public static class OperatorCatalogBuilder
    {
        public const string CatalogPath = "Assets/Data/Operators/OperatorCatalog.asset";
        public const string OperatorGroupPrefix = "Operator-";
        public const string AddressablesLabel = "operator-definition";
        private const string RecipeFolder = "Assets/Editor/OperatorRecipes";
        private const string GeneratedLabel = "RCCom.GeneratedOperator";

        [MenuItem("RCCom/Operators/Build Operator Catalog And Addressables")]
        public static void BuildAll()
        {
            List<OperatorAssetRecipe> recipes = LoadRecipes();
            OperatorCatalog catalog = GetOrCreateCatalog();
            var entries = new List<OperatorCatalogEntry>();
            var expectedGroupNames = new HashSet<string>(StringComparer.Ordinal);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings를 만들지 못했습니다.");
            }

            settings.AddLabel(AddressablesLabel, false);

            foreach (OperatorAssetRecipe recipe in recipes)
            {
                string definitionPath = $"Assets/Data/Operators/{recipe.operatorId}/OperatorDefinition.asset";
                OperatorDefinition definition = AssetDatabase.LoadAssetAtPath<OperatorDefinition>(definitionPath);
                if (definition == null)
                {
                    throw new InvalidOperationException($"먼저 오퍼레이터 에셋을 생성해야 합니다: {definitionPath}");
                }

                string address = $"operator/{recipe.operatorId}";
                ConfigureAddressable(settings, definitionPath, recipe, address);
                expectedGroupNames.Add(GetGroupName(recipe.operatorId, recipe.remoteContent));
                entries.Add(new OperatorCatalogEntry
                {
                    operatorId = recipe.operatorId,
                    displayName = recipe.displayName,
                    playStyleDescription = recipe.playStyleDescription,
                    // 원격 Definition의 선택 초상화를 로컬 카탈로그가 직접 참조하면
                    // CDN 콘텐츠가 본체 빌드로 새어 나온다. 원격은 Definition을 받은 뒤
                    // 실제 초상화를 사용하고, 카탈로그에는 ID/설명만 남긴다.
                    previewPortrait = recipe.remoteContent ? null : definition.selectionPortrait,
                    managementPortrait = recipe.remoteContent ? null : definition.managementPortrait,
                    address = address,
                    remoteContent = recipe.remoteContent,
                    requiredBestWave = recipe.requiredBestWave,
                    unitPreviews = BuildUnitPreviews(definition, recipe.remoteContent),
                });
            }

            RemoveStaleGeneratedGroups(settings, expectedGroupNames);

            catalog.entries = entries;
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[OperatorCatalogBuilder] 카탈로그 {entries.Count}명 및 Addressables 그룹 갱신 완료");
        }

        private static List<OperatorUnitPreview> BuildUnitPreviews(
            OperatorDefinition definition,
            bool remoteContent)
        {
            var previews = new List<OperatorUnitPreview>();
            if (definition.allyUnitRoster == null || definition.allyUnitRoster.units == null)
            {
                return previews;
            }

            foreach (var unit in definition.allyUnitRoster.units)
            {
                if (unit == null || unit.data == null)
                {
                    continue;
                }

                previews.Add(new OperatorUnitPreview
                {
                    displayName = unit.data.displayName,
                    deployCost = unit.data.deployCost,
                    // 원격 전용 Definition의 실제 스프라이트를 카탈로그가 참조하면 메인 빌드에
                    // 의존성이 새어 들어간다. 원격 유닛은 색상 미리보기만 로컬에 남긴다.
                    previewIcon = remoteContent ? null : unit.sprite,
                    fallbackColor = unit.tint,
                });
            }

            return previews;
        }

        private static void ConfigureAddressable(
            AddressableAssetSettings settings,
            string definitionPath,
            OperatorAssetRecipe recipe,
            string address)
        {
            string groupName = GetGroupName(recipe.operatorId, recipe.remoteContent);
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(
                    groupName,
                    false,
                    false,
                    true,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null)
            {
                bundled = group.AddSchema<BundledAssetGroupSchema>();
            }

            if (group.GetSchema<ContentUpdateGroupSchema>() == null)
            {
                group.AddSchema<ContentUpdateGroupSchema>();
            }

            bundled.BuildPath.SetVariableByName(
                settings,
                recipe.remoteContent ? AddressableAssetSettings.kRemoteBuildPath : AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(
                settings,
                recipe.remoteContent ? AddressableAssetSettings.kRemoteLoadPath : AddressableAssetSettings.kLocalLoadPath);
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.IncludeInBuild = true;

            string guid = AssetDatabase.AssetPathToGUID(definitionPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, true);
            entry.address = address;
            entry.SetLabel(AddressablesLabel, true, true, false);
        }

        public static string GetGroupName(string operatorId, bool remoteContent)
        {
            return $"{OperatorGroupPrefix}{operatorId}-{(remoteContent ? "Remote" : "Local")}";
        }

        private static void RemoveStaleGeneratedGroups(
            AddressableAssetSettings settings,
            HashSet<string> expectedGroupNames)
        {
            var groups = new List<AddressableAssetGroup>(settings.groups);
            foreach (AddressableAssetGroup group in groups)
            {
                if (group == null || !group.Name.StartsWith(OperatorGroupPrefix, StringComparison.Ordinal) ||
                    expectedGroupNames.Contains(group.Name))
                {
                    continue;
                }

                // 자동화 이름을 쓰더라도 항목이 남은 그룹은 사람이 추가한 콘텐츠일 수 있다.
                // 데이터 유실을 피하기 위해 빈 그룹만 자동 정리하고, 나머지는 명시적으로 중단한다.
                if (group.entries.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"이전 오퍼레이터 그룹에 항목이 남아 자동 삭제할 수 없습니다: {group.Name}");
                }

                settings.RemoveGroup(group);
                Debug.Log($"[OperatorCatalogBuilder] 사용하지 않는 빈 그룹 정리: {group.Name}");
            }
        }

        private static OperatorCatalog GetOrCreateCatalog()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(CatalogPath);
            if (existing != null)
            {
                if (existing is not OperatorCatalog catalog)
                {
                    throw new InvalidOperationException($"카탈로그 경로에 다른 타입의 에셋이 있습니다: {CatalogPath}");
                }

                if (Array.IndexOf(AssetDatabase.GetLabels(catalog), GeneratedLabel) < 0)
                {
                    throw new InvalidOperationException($"자동 생성 라벨 없는 카탈로그는 덮어쓸 수 없습니다: {CatalogPath}");
                }

                return catalog;
            }

            var created = ScriptableObject.CreateInstance<OperatorCatalog>();
            AssetDatabase.CreateAsset(created, CatalogPath);
            AssetDatabase.SetLabels(created, new[] { GeneratedLabel });
            return created;
        }

        private static List<OperatorAssetRecipe> LoadRecipes()
        {
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
            var recipes = new List<OperatorAssetRecipe>();
            var operatorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                OperatorAssetRecipe recipe = JsonUtility.FromJson<OperatorAssetRecipe>(text.text);
                if (recipe == null || string.IsNullOrWhiteSpace(recipe.operatorId))
                {
                    throw new InvalidOperationException($"유효하지 않은 오퍼레이터 레시피입니다: {path}");
                }

                if (!operatorIds.Add(recipe.operatorId))
                {
                    throw new InvalidOperationException($"오퍼레이터 레시피 ID가 중복됩니다: {recipe.operatorId}");
                }

                recipes.Add(recipe);
            }

            if (recipes.Count == 0)
            {
                throw new InvalidOperationException($"오퍼레이터 레시피가 없습니다: {RecipeFolder}");
            }

            recipes.Sort((left, right) =>
            {
                int order = left.catalogOrder.CompareTo(right.catalogOrder);
                return order != 0 ? order : string.CompareOrdinal(left.operatorId, right.operatorId);
            });

            return recipes;
        }
    }
}
