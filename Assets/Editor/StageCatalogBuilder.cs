using System;
using System.Collections.Generic;
using RCCom.Definitions.Stage;
using RCCom.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
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
        public const string StageGroupPrefix = "Stage-";
        public const string AddressablesLabel = "stage-definition";
        public const string BackgroundAddressablesLabel = "stage-background";
        private const string StageRoot = "Assets/Data/Stages";

        public static string GetAddress(string stageId)
        {
            return $"stage/{stageId}";
        }

        public static string GetBackgroundAddress(string stageId)
        {
            return $"stage/{stageId}/description-background";
        }

        public static string GetGroupName(string stageId, bool remoteContent)
        {
            return $"{StageGroupPrefix}{stageId}-{(remoteContent ? "Remote" : "Local")}";
        }

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
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings를 만들지 못했습니다.");
            }

            settings.AddLabel(AddressablesLabel, false);
            settings.AddLabel(BackgroundAddressablesLabel, false);

            catalog.entries = new List<StageCatalogEntry>(definitions.Count);
            var liveEntries = new List<StageCatalogEntry>();
            var expectedGroupNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (StageDefinition definition in definitions)
            {
                ConfigureAddressable(settings, definition);
                expectedGroupNames.Add(GetGroupName(definition.stageId, definition.remoteContent));
                if (definition.remoteContent)
                {
                    // 정본 카탈로그와 라이브 카탈로그가 같은 객체를 공유하면 한쪽 편집이 다른
                    // 쪽에 새어 들어가므로 별도 인스턴스를 만든다.
                    liveEntries.Add(CreateEntry(definition));
                }
                else
                {
                    // 플레이어 내장 카탈로그에는 로컬 스테이지만 둔다. 원격 메타데이터까지
                    // 넣으면 서버 조회 전에도 후반 스테이지가 노출되어 버튼 경계가 무너진다.
                    catalog.entries.Add(CreateEntry(definition));
                }
            }

            RemoveStaleGeneratedGroups(settings, expectedGroupNames);
            StageLiveCatalogBuilder.Build(liveEntries);

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
        }

        private static StageCatalogEntry CreateEntry(StageDefinition definition)
        {
            bool remote = definition.remoteContent;
            bool hasBackground = definition.descriptionBackground != null;
            return new StageCatalogEntry
            {
                stageId = definition.stageId,
                chapterId = definition.chapterId,
                displayName = definition.displayName,
                subtitle = definition.subtitle,
                description = definition.description,
                order = definition.order,
                requiredBestWave = definition.requiredBestWave,
                recommendedLevel = definition.recommendedLevel,
                // 원격 스테이지의 배경을 카탈로그가 직접 참조하면 CDN 콘텐츠가 본체 빌드로
                // 새어 나간다(오퍼레이터 초상화와 같은 규칙). 원격은 주소만 남긴다.
                descriptionBackground = remote ? null : definition.descriptionBackground,
                descriptionBackgroundAddress = remote && hasBackground
                    ? GetBackgroundAddress(definition.stageId)
                    : string.Empty,
                hasWaves = definition.waves != null && definition.waves.Count > 0,
                address = GetAddress(definition.stageId),
                remoteContent = remote,
                enemyPreviews = BuildEnemyPreviews(definition),
                rewards = BuildRewardPreviews(definition, remote),
                // 원격 스테이지는 직접 참조를 비워야 한다. 남겨두면 Definition이 본체 빌드에
                // 딸려 들어가 원격으로 뺀 의미가 사라진다.
                stageDefinition = remote ? null : definition,
            };
        }

        private static List<StageEnemyPreview> BuildEnemyPreviews(StageDefinition definition)
        {
            var result = new List<StageEnemyPreview>();
            var byId = new Dictionary<string, StageEnemyPreview>(StringComparer.Ordinal);
            if (definition.waves == null) { return result; }

            foreach (StageWaveDefinition wave in definition.waves)
            {
                if (wave == null || wave.spawns == null) { continue; }
                foreach (StageEnemySpawn spawn in wave.spawns)
                {
                    if (spawn == null || string.IsNullOrWhiteSpace(spawn.enemyId)) { continue; }
                    if (!byId.TryGetValue(spawn.enemyId, out StageEnemyPreview preview))
                    {
                        preview = new StageEnemyPreview { enemyId = spawn.enemyId };
                        byId.Add(spawn.enemyId, preview);
                        result.Add(preview);
                    }

                    preview.totalCount += Mathf.Max(0, spawn.count);
                }
            }

            return result;
        }

        private static List<StageReward> BuildRewardPreviews(StageDefinition definition, bool remote)
        {
            var result = new List<StageReward>();
            if (definition.rewards == null) { return result; }
            foreach (StageReward reward in definition.rewards)
            {
                if (reward == null) { continue; }
                result.Add(new StageReward
                {
                    rewardId = reward.rewardId,
                    displayName = reward.displayName,
                    // 원격 스테이지의 전용 아이콘을 직접 물리면 본체 번들로 참조가 샌다.
                    // 공용 골드 아이콘은 UI 컴포넌트가 별도로 공급한다.
                    icon = remote ? null : reward.icon,
                    amount = reward.amount,
                });
            }

            return result;
        }

        /// <summary>
        /// 스테이지 하나를 "스테이지 1개 = 그룹 1개"로 배선한다. 오퍼레이터·유닛과 같은 규칙이라
        /// 원격으로 돌릴 스테이지만 remoteContent를 켜면 배포 단위가 그대로 분리된다.
        /// </summary>
        private static void ConfigureAddressable(AddressableAssetSettings settings, StageDefinition definition)
        {
            string definitionPath = AssetDatabase.GetAssetPath(definition);
            if (string.IsNullOrEmpty(definitionPath))
            {
                throw new InvalidOperationException($"StageDefinition의 에셋 경로를 찾을 수 없습니다: {definition.stageId}");
            }

            bool changed = false;
            AddressableAssetGroup group = AddressableGroupPolicy.EnsureGroup(
                settings,
                GetGroupName(definition.stageId, definition.remoteContent),
                definition.remoteContent,
                // 원격 스테이지는 배경 한 장만 먼저 받아 브리핑을 그릴 수 있어야 하므로
                // Definition과 배경을 다른 번들로 쪼갠다(오퍼레이터 초상화와 같은 이유).
                definition.remoteContent
                    ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately
                    : BundledAssetGroupSchema.BundlePackingMode.PackTogether,
                ref changed);

            AddressableGroupPolicy.AssignEntry(
                settings, group, definitionPath, GetAddress(definition.stageId), AddressablesLabel);

            if (!definition.remoteContent || definition.descriptionBackground == null)
            {
                return;
            }

            string backgroundPath = AssetDatabase.GetAssetPath(definition.descriptionBackground);
            if (string.IsNullOrEmpty(backgroundPath))
            {
                return;
            }

            AddressableGroupPolicy.AssignEntry(
                settings, group, backgroundPath, GetBackgroundAddress(definition.stageId),
                BackgroundAddressablesLabel);
        }

        /// <summary>
        /// 더 이상 StageDefinition이 없는 자동 생성 그룹을 지운다. 남겨두면 빈 그룹이 계속
        /// 빌드되고, 이름만 보고 존재하지 않는 스테이지가 있다고 착각하게 된다.
        /// </summary>
        private static void RemoveStaleGeneratedGroups(
            AddressableAssetSettings settings, HashSet<string> expectedGroupNames)
        {
            var stale = new List<AddressableAssetGroup>();
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null || group.ReadOnly || !group.Name.StartsWith(StageGroupPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!expectedGroupNames.Contains(group.Name))
                {
                    stale.Add(group);
                }
            }

            foreach (AddressableAssetGroup group in stale)
            {
                Debug.Log($"[StageCatalogBuilder] 사라진 스테이지의 그룹 제거: {group.Name}");
                settings.RemoveGroup(group);
            }
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
