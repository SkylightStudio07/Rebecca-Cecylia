using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RCCom.Data;
using RCCom.Definitions.Unit;
using RCCom.Effects.Unit;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 기존 AllyUnitDefinition 4종과 Roster 5개를 레시피/ID 목록 구조로 옮긴다.
    /// Definition은 MoveAsset으로 이동해 GUID를 보존하고, Roster는 먼저 unitIds를
    /// 저장한 뒤 다음 스키마 변경에서 units를 런타임 목록으로 바꿀 수 있게 한다.
    /// </summary>
    public static class AllyUnitRecipeMigrator
    {
        private const string RecipeFolder = "Assets/Editor/AllyUnitRecipes";
        private const string OutputRoot = "Assets/Data/AllyUnits";
        private const string LegacyRoot = "Assets/Data/Operators/cassia/AllyUnits";

        private static readonly string[] LegacyDefinitionNames =
        {
            "cassia-guard.asset",
            "cassia-vanguard.asset",
            "TestRifleman.asset",
            "TestGuard.asset",
        };

        [MenuItem("RCCom/Ally Units/Dry Run Recipe Migration")]
        public static void DryRun()
        {
            MigrationPlan plan = BuildPlan();
            LogPlan(plan, true);
        }

        [MenuItem("RCCom/Ally Units/Migrate Definitions To Recipes")]
        public static void MigrateAll()
        {
            MigrationPlan plan = BuildPlan();
            LogPlan(plan, false);

            foreach (MigrationItem item in plan.items)
            {
                EnsureFolder(Path.GetDirectoryName(item.destinationPath)?.Replace('\\', '/'));
                if (item.sourcePath != item.destinationPath)
                {
                    string moveError = AssetDatabase.MoveAsset(item.sourcePath, item.destinationPath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        throw new InvalidOperationException(
                            $"아군 유닛 Definition 이동에 실패했습니다: {item.sourcePath} → " +
                            $"{item.destinationPath} ({moveError})");
                    }
                }

                AllyUnitDefinition movedDefinition =
                    AssetDatabase.LoadAssetAtPath<AllyUnitDefinition>(item.destinationPath);
                if (movedDefinition == null)
                {
                    throw new InvalidOperationException(
                        $"이동한 Definition을 다시 로드하지 못했습니다: {item.destinationPath}");
                }

                // 기존 수동/수직 슬라이스 에셋도 이제 레시피가 정본이므로, 빌더가
                // 이후 수정할 수 있도록 생성물 라벨을 명시한다.
                AssetDatabase.SetLabels(movedDefinition, new[] { "RCCom.GeneratedAllyUnit" });
                WriteRecipe(item.recipe, item.recipePath);
            }

            foreach (RosterMigration rosterMigration in plan.rosters)
            {
                AllyUnitRoster roster = AssetDatabase.LoadAssetAtPath<AllyUnitRoster>(rosterMigration.path);
                if (roster == null)
                {
                    throw new InvalidOperationException($"AllyUnitRoster를 다시 로드하지 못했습니다: {rosterMigration.path}");
                }

                roster.unitIds = new List<string>(rosterMigration.unitIds);
                EditorUtility.SetDirty(roster);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[AllyUnitRecipeMigrator] 아군 유닛 {plan.items.Count}종과 Roster " +
                $"{plan.rosters.Count}개를 레시피/ID 목록으로 마이그레이션했습니다.");
        }

        [MenuItem("RCCom/Ally Units/Normalize Roster Serialization")]
        public static void NormalizeRosterSerialization()
        {
            var paths = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:AllyUnitRoster");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(path);
                }
            }

            if (paths.Count == 0)
            {
                Debug.Log("[AllyUnitRecipeMigrator] 직렬화할 AllyUnitRoster가 없습니다.");
                return;
            }

            // units 필드를 [NonSerialized]로 바꾼 뒤에도 이전 YAML의 참조가 남을 수 있다.
            // Unity가 현재 스키마로 다시 쓰게 해 Definition이 Roster 의존성으로 끌려오지 않게 한다.
            AssetDatabase.ForceReserializeAssets(paths);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AllyUnitRecipeMigrator] AllyUnitRoster {paths.Count}개 직렬화 정규화 완료");
        }

        private static MigrationPlan BuildPlan()
        {
            EnsureFolder(RecipeFolder);
            EnsureFolder(OutputRoot);

            var plan = new MigrationPlan();
            var definitionsById = new Dictionary<string, AllyUnitDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < LegacyDefinitionNames.Length; i++)
            {
                string legacyPath = $"{LegacyRoot}/{LegacyDefinitionNames[i]}";
                string unitId = string.Empty;
                AllyUnitDefinition definition = AssetDatabase.LoadAssetAtPath<AllyUnitDefinition>(legacyPath);
                string destinationPath = string.Empty;

                if (definition == null)
                {
                    // 재실행 시에는 이미 이동된 경로를 읽어 계획을 다시 만들 수 있어야 한다.
                    unitId = LegacyDefinitionNames[i]
                        .Replace(".asset", string.Empty, StringComparison.OrdinalIgnoreCase)
                        .ToLowerInvariant();
                    if (string.Equals(unitId, "testrifleman", StringComparison.Ordinal))
                    {
                        unitId = "test-rifleman";
                    }
                    else if (string.Equals(unitId, "testguard", StringComparison.Ordinal))
                    {
                        unitId = "test-guard";
                    }

                    destinationPath = $"{OutputRoot}/{unitId}/AllyUnitDefinition.asset";
                    definition = AssetDatabase.LoadAssetAtPath<AllyUnitDefinition>(destinationPath);
                    if (definition == null)
                    {
                        throw new InvalidOperationException(
                            $"마이그레이션 대상 Definition을 찾지 못했습니다: {legacyPath} 또는 {destinationPath}");
                    }
                }
                else
                {
                    if (definition.data == null || string.IsNullOrWhiteSpace(definition.data.unitId))
                    {
                        throw new InvalidOperationException($"unitId가 비어 있는 Definition입니다: {legacyPath}");
                    }

                    unitId = definition.data.unitId;
                    destinationPath = $"{OutputRoot}/{unitId}/AllyUnitDefinition.asset";
                }

                if (!definitionsById.TryAdd(unitId, definition))
                {
                    throw new InvalidOperationException($"마이그레이션 대상 unitId가 중복됩니다: {unitId}");
                }

                plan.items.Add(new MigrationItem
                {
                    sourcePath = AssetDatabase.GetAssetPath(definition),
                    destinationPath = destinationPath,
                    recipePath = $"{RecipeFolder}/{unitId}.json",
                    recipe = CreateRecipe(definition, i),
                });
            }

            string[] rosterGuids = AssetDatabase.FindAssets("t:AllyUnitRoster");
            Array.Sort(rosterGuids, StringComparer.Ordinal);
            foreach (string guid in rosterGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/Data/Operators/", StringComparison.Ordinal))
                {
                    continue;
                }

                AllyUnitRoster roster = AssetDatabase.LoadAssetAtPath<AllyUnitRoster>(path);
                if (roster == null || roster.units == null || roster.units.Count == 0)
                {
                    throw new InvalidOperationException($"비어 있는 기존 AllyUnitRoster입니다: {path}");
                }

                var unitIds = new List<string>();
                var seenIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (AllyUnitDefinition definition in roster.units)
                {
                    if (definition == null || definition.data == null ||
                        string.IsNullOrWhiteSpace(definition.data.unitId))
                    {
                        throw new InvalidOperationException($"Roster에 유효하지 않은 유닛 참조가 있습니다: {path}");
                    }

                    if (!seenIds.Add(definition.data.unitId))
                    {
                        throw new InvalidOperationException(
                            $"Roster 안에서 unitId가 중복됩니다: {definition.data.unitId} ({path})");
                    }

                    unitIds.Add(definition.data.unitId);
                }

                plan.rosters.Add(new RosterMigration
                {
                    path = path,
                    unitIds = unitIds,
                });
            }

            if (plan.rosters.Count != 5)
            {
                throw new InvalidOperationException(
                    $"기대했던 AllyUnitRoster 5개와 현재 수가 다릅니다: {plan.rosters.Count}개");
            }

            return plan;
        }

        private static AllyUnitAssetRecipe CreateRecipe(AllyUnitDefinition definition, int catalogOrder)
        {
            AllyUnitData data = definition.data;
            var effectPaths = new List<string>();
            if (definition.effects != null)
            {
                foreach (AllyUnitEffectBase effect in definition.effects)
                {
                    if (effect == null)
                    {
                        throw new InvalidOperationException(
                            $"효과 목록에 null이 있어 레시피로 옮길 수 없습니다: {AssetDatabase.GetAssetPath(definition)}");
                    }

                    effectPaths.Add(AssetDatabase.GetAssetPath(effect));
                }
            }

            return new AllyUnitAssetRecipe
            {
                unitId = data.unitId,
                catalogOrder = catalogOrder,
                displayName = data.displayName,
                spritePath = definition.sprite == null ? string.Empty : AssetDatabase.GetAssetPath(definition.sprite),
                tint = definition.tint,
                spriteForwardOffsetDegrees = definition.spriteForwardOffsetDegrees,
                effectPaths = effectPaths,
                remoteContent = false,
                data = CloneData(data),
            };
        }

        private static AllyUnitData CloneData(AllyUnitData source)
        {
            return new AllyUnitData
            {
                unitId = source.unitId,
                displayName = source.displayName,
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

        private static void WriteRecipe(AllyUnitAssetRecipe recipe, string path)
        {
            File.WriteAllText(
                Path.GetFullPath(path),
                JsonUtility.ToJson(recipe, true),
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void LogPlan(MigrationPlan plan, bool dryRun)
        {
            string mode = dryRun ? "Dry Run" : "실행 계획";
            Debug.Log($"[AllyUnitRecipeMigrator] {mode}: Definition {plan.items.Count}종, Roster {plan.rosters.Count}개");
            foreach (MigrationItem item in plan.items)
            {
                Debug.Log($"  Definition: {item.sourcePath} → {item.destinationPath}; Recipe: {item.recipePath}");
            }

            foreach (RosterMigration roster in plan.rosters)
            {
                Debug.Log($"  Roster: {roster.path} → [{string.Join(", ", roster.unitIds)}]");
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

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

        private sealed class MigrationPlan
        {
            public readonly List<MigrationItem> items = new();
            public readonly List<RosterMigration> rosters = new();
        }

        private sealed class MigrationItem
        {
            public string sourcePath;
            public string destinationPath;
            public string recipePath;
            public AllyUnitAssetRecipe recipe;
        }

        private sealed class RosterMigration
        {
            public string path;
            public List<string> unitIds;
        }
    }
}
