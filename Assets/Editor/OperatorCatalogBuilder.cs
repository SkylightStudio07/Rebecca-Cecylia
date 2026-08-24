using System;
using System.Collections.Generic;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Unit;
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
            // 오퍼레이터 카탈로그의 유닛 미리보기는 유닛 전용 카탈로그가 정본이다.
            // 이 호출을 여기에도 두어 Operator 메뉴만 실행해도 두 카탈로그가 어긋나지 않게 한다.
            AllyUnitCatalogBuilder.BuildAll();
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

                ConfigureAddressable(settings, definitionPath, recipe, GetAddress(recipe.operatorId));
                expectedGroupNames.Add(GetGroupName(recipe.operatorId, recipe.remoteContent));
                entries.Add(CreateEntry(recipe, definition));
            }

            RemoveStaleGeneratedGroups(settings, expectedGroupNames);

            ApplyEntriesIfChanged(catalog, entries, null);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[OperatorCatalogBuilder] 카탈로그 {entries.Count}명 및 Addressables 그룹 갱신 완료");
        }

        /// <summary>
        /// 오퍼레이터 한 명만 카탈로그와 Addressables에 반영한다. 다른 오퍼레이터의 카탈로그
        /// 항목은 기존 값을 그대로 두어, 손대지 않은 캐릭터의 에셋이 다시 쓰이지 않게 한다.
        /// 사라진 그룹 정리처럼 전체를 봐야 하는 동기화는 BuildAll이 계속 담당한다.
        /// </summary>
        public static void BuildForOperator(OperatorAssetRecipe recipe, List<string> changedAssets)
        {
            AllyUnitCatalogBuilder.BuildAll();
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.operatorId))
            {
                throw new InvalidOperationException("유효하지 않은 오퍼레이터 레시피입니다.");
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings를 만들지 못했습니다.");
            }

            settings.AddLabel(AddressablesLabel, false);

            string definitionPath = $"Assets/Data/Operators/{recipe.operatorId}/OperatorDefinition.asset";
            OperatorDefinition definition = AssetDatabase.LoadAssetAtPath<OperatorDefinition>(definitionPath);
            if (definition == null)
            {
                throw new InvalidOperationException($"먼저 오퍼레이터 에셋을 생성해야 합니다: {definitionPath}");
            }

            if (ConfigureAddressable(settings, definitionPath, recipe, GetAddress(recipe.operatorId)))
            {
                EditorUtility.SetDirty(settings);
                OperatorAssetBuilder.RecordChange(
                    AssetDatabase.GetAssetPath(settings), changedAssets);
            }

            OperatorCatalog catalog = GetOrCreateCatalog();
            var entries = catalog.entries == null
                ? new List<OperatorCatalogEntry>()
                : new List<OperatorCatalogEntry>(catalog.entries);
            OperatorCatalogEntry updated = CreateEntry(recipe, definition);
            int index = entries.FindIndex(
                entry => entry != null &&
                         string.Equals(entry.operatorId, recipe.operatorId, StringComparison.Ordinal));
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

            Debug.Log($"[OperatorCatalogBuilder] {recipe.operatorId} 카탈로그 항목과 Addressables 그룹 갱신 완료");
        }

        private static OperatorCatalogEntry CreateEntry(OperatorAssetRecipe recipe, OperatorDefinition definition)
        {
            return new OperatorCatalogEntry
            {
                operatorId = recipe.operatorId,
                displayName = recipe.displayName,
                playStyleDescription = recipe.playStyleDescription,
                // 원격 Definition의 선택 초상화를 로컬 카탈로그가 직접 참조하면
                // CDN 콘텐츠가 본체 빌드로 새어 나온다. 원격은 Definition을 받은 뒤
                // 실제 초상화를 사용하고, 카탈로그에는 ID/설명만 남긴다.
                previewPortrait = recipe.remoteContent ? null : definition.selectionPortrait,
                managementPortrait = recipe.remoteContent ? null : definition.managementPortrait,
                shopPortrait = recipe.remoteContent ? null : definition.shopPortrait,
                shopUpperBodyPortrait = recipe.remoteContent ? null : definition.shopUpperBodyPortrait,
                alternateName = definition.alternateName,
                shopDialogue = definition.shopDialogue,
                unlockRewardPortrait = recipe.remoteContent ? null :
                    (definition.unlockRewardPortrait != null
                        ? definition.unlockRewardPortrait
                        : definition.selectionPortrait),
                address = GetAddress(recipe.operatorId),
                remoteContent = recipe.remoteContent,
                unlockType = recipe.unlockType,
                requiredBestWave = recipe.requiredBestWave,
                purchasePrice = recipe.purchasePrice,
                requiredStageId = recipe.requiredStageId ?? string.Empty,
                unitPreviews = BuildUnitPreviews(definition, recipe.remoteContent),
            };
        }

        /// <summary>
        /// 내용이 같은 카탈로그를 다시 저장하면 형상관리에 의미 없는 변경으로 잡히므로,
        /// 직렬화 결과가 실제로 달라진 경우에만 더티 플래그를 세운다.
        /// </summary>
        private static void ApplyEntriesIfChanged(
            OperatorCatalog catalog,
            List<OperatorCatalogEntry> entries,
            List<string> changedAssets)
        {
            string before = EditorJsonUtility.ToJson(catalog);
            catalog.entries = entries;
            if (EditorJsonUtility.ToJson(catalog) == before)
            {
                return;
            }

            EditorUtility.SetDirty(catalog);
            OperatorAssetBuilder.RecordChange(CatalogPath, changedAssets);
        }

        private static void SortEntriesByRecipeOrder(List<OperatorCatalogEntry> entries)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (OperatorAssetRecipe recipe in LoadRecipes())
            {
                order[recipe.operatorId] = recipe.catalogOrder;
            }

            entries.Sort((left, right) =>
            {
                // 레시피가 사라진 항목은 뒤로 밀어 두고 검증이 잡게 한다.
                int leftOrder = order.TryGetValue(left.operatorId, out int leftValue) ? leftValue : int.MaxValue;
                int rightOrder = order.TryGetValue(right.operatorId, out int rightValue) ? rightValue : int.MaxValue;
                int compared = leftOrder.CompareTo(rightOrder);
                return compared != 0 ? compared : string.CompareOrdinal(left.operatorId, right.operatorId);
            });
        }

        private static string GetAddress(string operatorId)
        {
            return $"operator/{operatorId}";
        }

        private static List<AllyUnitCatalogEntry> BuildUnitPreviews(
            OperatorDefinition definition,
            bool remoteContent)
        {
            var previews = new List<AllyUnitCatalogEntry>();
            if (definition.allyUnitRoster == null)
            {
                return previews;
            }

            AllyUnitCatalog catalog =
                AssetDatabase.LoadAssetAtPath<AllyUnitCatalog>(AllyUnitCatalogBuilder.CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning(
                    $"[OperatorCatalogBuilder] AllyUnitCatalog가 없어 유닛 미리보기를 비웁니다: " +
                    $"{definition.operatorId}");
                return previews;
            }

            List<string> unitIds = definition.allyUnitRoster.unitIds;
            if ((unitIds == null || unitIds.Count == 0) &&
                definition.allyUnitRoster.units != null)
            {
                // 스키마 전환 중인 에셋을 한 번 읽을 수 있게 하는 대체 경로다. 최종
                // 생성물은 unitIds만 보유하며 이 경로는 다음 빌드부터 사용되지 않는다.
                unitIds = new List<string>();
                foreach (AllyUnitDefinition unit in definition.allyUnitRoster.units)
                {
                    if (unit != null && unit.data != null)
                    {
                        unitIds.Add(unit.data.unitId);
                    }
                }
            }

            if (unitIds == null)
            {
                return previews;
            }

            foreach (string unitId in unitIds)
            {
                AllyUnitCatalogEntry source = catalog.FindById(unitId);
                if (source == null)
                {
                    Debug.LogWarning(
                        $"[OperatorCatalogBuilder] 유닛 카탈로그 항목을 찾지 못했습니다: " +
                        $"{unitId} ({definition.operatorId})");
                    continue;
                }

                previews.Add(new AllyUnitCatalogEntry
                {
                    unitId = source.unitId,
                    displayName = source.displayName,
                    deployCost = source.deployCost,
                    address = source.address,
                    remoteContent = source.remoteContent,
                    // 원격 오퍼레이터 또는 원격 유닛이면 실제 Sprite를 카탈로그에
                    // 남기지 않는다. fallbackColor는 값 타입이라 항상 복사한다.
                    previewIcon = remoteContent || source.remoteContent ? null : source.previewIcon,
                    fallbackColor = source.fallbackColor,
                });
            }

            return previews;
        }

        /// <summary>
        /// 그룹과 엔트리를 기대 상태로 맞추고, 실제로 바꾼 것이 있는지 돌려준다.
        /// 이미 같은 값이면 다시 쓰지 않아 Addressables 에셋이 불필요하게 갱신되지 않는다.
        /// </summary>
        private static bool ConfigureAddressable(
            AddressableAssetSettings settings,
            string definitionPath,
            OperatorAssetRecipe recipe,
            string address)
        {
            bool changed = false;
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
                changed = true;
            }

            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null)
            {
                bundled = group.AddSchema<BundledAssetGroupSchema>();
                changed = true;
            }

            if (group.GetSchema<ContentUpdateGroupSchema>() == null)
            {
                group.AddSchema<ContentUpdateGroupSchema>();
                changed = true;
            }

            string buildPath = recipe.remoteContent
                ? AddressableAssetSettings.kRemoteBuildPath
                : AddressableAssetSettings.kLocalBuildPath;
            string loadPath = recipe.remoteContent
                ? AddressableAssetSettings.kRemoteLoadPath
                : AddressableAssetSettings.kLocalLoadPath;
            bool schemaChanged = false;
            if (bundled.BuildPath.GetName(settings) != buildPath)
            {
                bundled.BuildPath.SetVariableByName(settings, buildPath);
                schemaChanged = true;
            }

            if (bundled.LoadPath.GetName(settings) != loadPath)
            {
                bundled.LoadPath.SetVariableByName(settings, loadPath);
                schemaChanged = true;
            }

            if (bundled.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackTogether)
            {
                bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                schemaChanged = true;
            }

            if (!bundled.IncludeInBuild)
            {
                bundled.IncludeInBuild = true;
                schemaChanged = true;
            }

            if (schemaChanged)
            {
                EditorUtility.SetDirty(bundled);
                EditorUtility.SetDirty(group);
                changed = true;
            }

            string guid = AssetDatabase.AssetPathToGUID(definitionPath);
            AddressableAssetEntry existing = settings.FindAssetEntry(guid);
            if (existing == null || existing.parentGroup != group)
            {
                changed = true;
            }

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

            return changed;
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
