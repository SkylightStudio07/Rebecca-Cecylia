using System;
using System.Collections.Generic;
using RCCom.Definitions.Stage;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// StageDefinition을 제작 원본으로 삼아 선택 화면용 경량 카탈로그를 다시 만든다.
    /// 표시 데이터의 수동 이중 입력을 없애 Studio와 런타임 사이의 불일치를 막는다.
    /// </summary>
    public static class StageCatalogBuilder
    {
        public const string CatalogPath = "Assets/Data/Stages/StageCatalog.asset";
        private const string StageRoot = "Assets/Data/Stages";

        [MenuItem("RCCom/Stages/Rebuild Stage Catalog")]
        public static void Build()
        {
            BuildCatalog();
            Debug.Log("[StageCatalogBuilder] StageDefinition에서 카탈로그 갱신 완료");
        }

        public static StageCatalog BuildCatalog()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder(StageRoot);

            StageCatalog catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StageCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            Dictionary<string, StageCatalogEntry> previousEntries = BuildPreviousEntryMap(catalog);
            List<StageDefinition> definitions = LoadDefinitions();
            foreach (StageDefinition definition in definitions)
            {
                MigrateLegacyMetadata(definition, previousEntries);
            }

            definitions.Sort(CompareDefinitions);
            catalog.entries = new List<StageCatalogEntry>(definitions.Count);
            foreach (StageDefinition definition in definitions)
            {
                catalog.entries.Add(new StageCatalogEntry
                {
                    stageId = definition.stageId,
                    chapterId = definition.chapterId,
                    displayName = definition.displayName,
                    subtitle = definition.subtitle,
                    description = definition.description,
                    order = definition.order,
                    requiredBestWave = definition.requiredBestWave,
                    stageDefinition = definition
                });
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
        }

        public static List<StageDefinition> LoadDefinitions()
        {
            var definitions = new List<StageDefinition>();
            string[] guids = AssetDatabase.FindAssets("t:StageDefinition", new[] { StageRoot });
            foreach (string guid in guids)
            {
                StageDefinition definition = AssetDatabase.LoadAssetAtPath<StageDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition != null) { definitions.Add(definition); }
            }

            return definitions;
        }

        private static Dictionary<string, StageCatalogEntry> BuildPreviousEntryMap(StageCatalog catalog)
        {
            var result = new Dictionary<string, StageCatalogEntry>(StringComparer.Ordinal);
            if (catalog.entries == null) { return result; }
            foreach (StageCatalogEntry entry in catalog.entries)
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.stageId))
                {
                    result[entry.stageId] = entry;
                }
            }

            return result;
        }

        private static void MigrateLegacyMetadata(StageDefinition definition,
            IReadOnlyDictionary<string, StageCatalogEntry> previousEntries)
        {
            if (definition.schemaVersion >= StageDefinition.CurrentSchemaVersion)
            {
                return;
            }

            if (previousEntries.TryGetValue(definition.stageId, out StageCatalogEntry previous))
            {
                definition.chapterId = string.IsNullOrWhiteSpace(previous.chapterId) ? "ch1" : previous.chapterId;
                definition.displayName = string.IsNullOrWhiteSpace(definition.displayName)
                    ? previous.displayName
                    : definition.displayName;
                definition.subtitle = previous.subtitle;
                definition.description = string.IsNullOrWhiteSpace(definition.description)
                    ? previous.description
                    : definition.description;
                definition.order = previous.order;
                definition.requiredBestWave = previous.requiredBestWave;
            }

            definition.schemaVersion = StageDefinition.CurrentSchemaVersion;
            EditorUtility.SetDirty(definition);
        }

        private static int CompareDefinitions(StageDefinition left, StageDefinition right)
        {
            int chapter = string.Compare(left.chapterId, right.chapterId, StringComparison.Ordinal);
            if (chapter != 0) { return chapter; }
            int order = left.order.CompareTo(right.order);
            return order != 0 ? order : string.Compare(left.stageId, right.stageId, StringComparison.Ordinal);
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) { AssetDatabase.CreateFolder(current, parts[i]); }
                current = next;
            }
        }
    }
}
