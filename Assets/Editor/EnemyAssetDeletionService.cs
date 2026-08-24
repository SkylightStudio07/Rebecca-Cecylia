using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Stage;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 적 레시피와 자동 생성물을 한 트랜잭션처럼 정리한다. 레시피만 지우면 Roster와
    /// Addressables 그룹이 남아 다음 전체 빌드를 막으므로 모든 소유 지점을 한곳에서 처리한다.
    /// </summary>
    public static class EnemyAssetDeletionService
    {
        private const string RecipeFolder = "Assets/Editor/EnemyRecipes";
        private const string OutputRoot = "Assets/Data/Enemies";

        public static void Delete(string enemyId)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                throw new ArgumentException("삭제할 enemyId가 비어 있습니다.", nameof(enemyId));
            }

            List<string> stageReferences = FindStageReferences(enemyId);
            if (stageReferences.Count > 0)
            {
                throw new InvalidOperationException(
                    $"스테이지 편성에서 먼저 제거해야 합니다: {enemyId}\n- " +
                    string.Join("\n- ", stageReferences));
            }

            RemoveAddressableGroups(enemyId);
            DeleteAssetIfExists($"{OutputRoot}/{enemyId}");
            DeleteRecipeIfExists(enemyId);
            RemoveFromRoster(enemyId);
            RemoveFromCatalog(enemyId);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 남은 레시피를 정본으로 다시 빌드해 Catalog·Roster·Addressables가 모두 같은
            // 집합을 가리키는지 기존 검증기까지 통과시킨다.
            EnemyAssetBuilder.BuildAll();
            Debug.Log($"[EnemyAssetDeletionService] 적 삭제 완료: {enemyId} (원본 아트는 보존)");
        }

        /// <summary>헤드리스 자동화에서는 -enemyId enemy-normal 형태로 대상을 전달한다.</summary>
        public static void DeleteFromCommandLine()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-enemyId");
            if (index < 0 || index + 1 >= arguments.Length)
            {
                throw new InvalidOperationException("-enemyId 인자가 필요합니다.");
            }

            Delete(arguments[index + 1]);
        }

        private static List<string> FindStageReferences(string enemyId)
        {
            var references = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:StageDefinition");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StageDefinition stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
                if (stage == null || stage.waves == null)
                {
                    continue;
                }

                for (int waveIndex = 0; waveIndex < stage.waves.Count; waveIndex++)
                {
                    StageWaveDefinition wave = stage.waves[waveIndex];
                    if (wave == null || wave.spawns == null)
                    {
                        continue;
                    }

                    foreach (StageEnemySpawn spawn in wave.spawns)
                    {
                        if (spawn != null && string.Equals(spawn.enemyId, enemyId, StringComparison.Ordinal))
                        {
                            references.Add($"{path} / Wave {waveIndex + 1}");
                            break;
                        }
                    }
                }
            }

            return references;
        }

        private static void RemoveAddressableGroups(string enemyId)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings가 없습니다.");
            }

            string[] groupNames =
            {
                EnemyCatalogBuilder.GetGroupName(enemyId, false),
                EnemyCatalogBuilder.GetGroupName(enemyId, true),
            };

            foreach (string groupName in groupNames)
            {
                AddressableAssetGroup group = settings.FindGroup(groupName);
                if (group != null)
                {
                    settings.RemoveGroup(group);
                }
            }

            EditorUtility.SetDirty(settings);
        }

        private static void DeleteRecipeIfExists(string enemyId)
        {
            if (!AssetDatabase.IsValidFolder(RecipeFolder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { RecipeFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                EnemyAssetRecipe recipe = text == null ? null : JsonUtility.FromJson<EnemyAssetRecipe>(text.text);
                if (recipe != null && string.Equals(recipe.enemyId, enemyId, StringComparison.Ordinal))
                {
                    AssetDatabase.DeleteAsset(path);
                    return;
                }
            }
        }

        private static void RemoveFromRoster(string enemyId)
        {
            EnemyRoster roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>(EnemyCatalogBuilder.RosterPath);
            if (roster == null || roster.enemyIds == null)
            {
                return;
            }

            if (roster.enemyIds.RemoveAll(id => string.Equals(id, enemyId, StringComparison.Ordinal)) > 0)
            {
                EditorUtility.SetDirty(roster);
            }
        }

        private static void RemoveFromCatalog(string enemyId)
        {
            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(EnemyCatalogBuilder.CatalogPath);
            if (catalog == null || catalog.entries == null)
            {
                return;
            }

            if (catalog.entries.RemoveAll(
                    entry => entry != null && string.Equals(entry.enemyId, enemyId, StringComparison.Ordinal)) > 0)
            {
                EditorUtility.SetDirty(catalog);
            }
        }

        private static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
