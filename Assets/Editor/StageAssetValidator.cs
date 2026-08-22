using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Stage;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>스테이지 ID 충돌과 웨이브·적·보상 누락을 제작 단계에서 차단한다.</summary>
    public static class StageAssetValidator
    {
        [MenuItem("RCCom/Stages/Validate All Stage Assets")]
        public static void ValidateAllMenu()
        {
            if (!ValidateAll(out string report))
            {
                throw new InvalidOperationException(report);
            }

            Debug.Log(report);
        }

        public static bool ValidateAll(out string report)
        {
            List<StageDefinition> definitions = StageCatalogBuilder.LoadDefinitions();
            var errors = new List<string>();
            var warnings = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (StageDefinition definition in definitions)
            {
                ValidateDefinition(definition, ids, errors, warnings);
            }

            report = BuildReport(definitions.Count, errors, warnings);
            return errors.Count == 0;
        }

        public static bool ValidateCurrent(StageDefinition definition, out string report)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            ValidateDefinition(definition, new HashSet<string>(StringComparer.Ordinal), errors, warnings);
            report = BuildReport(definition != null ? 1 : 0, errors, warnings);
            return errors.Count == 0;
        }

        private static void ValidateDefinition(StageDefinition definition, ISet<string> ids,
            ICollection<string> errors, ICollection<string> warnings)
        {
            if (definition == null)
            {
                errors.Add("null StageDefinition이 포함되어 있습니다.");
                return;
            }

            string path = AssetDatabase.GetAssetPath(definition);
            if (string.IsNullOrWhiteSpace(definition.stageId))
            {
                errors.Add($"Stage ID가 비어 있습니다: {path}");
            }
            else if (!ids.Add(definition.stageId))
            {
                errors.Add($"Stage ID가 중복됩니다: {definition.stageId}");
            }

            if (string.IsNullOrWhiteSpace(definition.chapterId)) { errors.Add($"Chapter ID 누락: {path}"); }
            if (string.IsNullOrWhiteSpace(definition.displayName)) { errors.Add($"표시 이름 누락: {path}"); }
            if (string.IsNullOrWhiteSpace(definition.subtitle)) { warnings.Add($"부제 누락: {path}"); }
            if (string.IsNullOrWhiteSpace(definition.description)) { warnings.Add($"설명 누락: {path}"); }
            if (definition.descriptionBackground == null) { warnings.Add($"설명 배경 누락: {path}"); }
            if (definition.recommendedLevel < 1) { errors.Add($"추천 레벨은 1 이상이어야 합니다: {path}"); }
            if (definition.requiredBestWave < 0) { errors.Add($"해금 웨이브는 음수일 수 없습니다: {path}"); }

            if (definition.waves == null || definition.waves.Count == 0)
            {
                errors.Add($"웨이브가 없습니다: {path}");
            }
            else
            {
                for (int waveIndex = 0; waveIndex < definition.waves.Count; waveIndex++)
                {
                    ValidateWave(definition.waves[waveIndex], path, waveIndex, errors);
                }
            }

            if (definition.rewards == null || definition.rewards.Count == 0)
            {
                warnings.Add($"클리어 보상이 없습니다: {path}");
            }
            else
            {
                for (int i = 0; i < definition.rewards.Count; i++)
                {
                    StageReward reward = definition.rewards[i];
                    if (reward == null || string.IsNullOrWhiteSpace(reward.rewardId))
                    {
                        errors.Add($"보상 {i + 1}의 Reward ID가 비어 있습니다: {path}");
                    }
                    else if (reward.amount <= 0)
                    {
                        errors.Add($"보상 {reward.rewardId}의 수량은 1 이상이어야 합니다: {path}");
                    }
                }
            }
        }

        private static void ValidateWave(StageWaveDefinition wave, string path, int waveIndex,
            ICollection<string> errors)
        {
            if (wave == null)
            {
                errors.Add($"WAVE {waveIndex + 1}이 null입니다: {path}");
                return;
            }

            if (wave.buildPhaseDuration < 0f) { errors.Add($"WAVE {waveIndex + 1} 준비 시간이 음수입니다: {path}"); }
            if (wave.healthMultiplier <= 0f) { errors.Add($"WAVE {waveIndex + 1} 체력 배율은 0보다 커야 합니다: {path}"); }
            if (wave.spawns == null || wave.spawns.Count == 0)
            {
                errors.Add($"WAVE {waveIndex + 1}에 적 편성이 없습니다: {path}");
                return;
            }

            for (int spawnIndex = 0; spawnIndex < wave.spawns.Count; spawnIndex++)
            {
                StageEnemySpawn spawn = wave.spawns[spawnIndex];
                if (spawn == null || spawn.enemy == null)
                {
                    errors.Add($"WAVE {waveIndex + 1} 편성 {spawnIndex + 1}의 적이 비어 있습니다: {path}");
                    continue;
                }

                if (spawn.count <= 0) { errors.Add($"WAVE {waveIndex + 1} 편성 수량은 1 이상이어야 합니다: {path}"); }
                if (spawn.interval < 0f || spawn.initialDelay < 0f)
                {
                    errors.Add($"WAVE {waveIndex + 1} 편성 시간은 음수일 수 없습니다: {path}");
                }
            }
        }

        private static string BuildReport(int count, IReadOnlyCollection<string> errors,
            IReadOnlyCollection<string> warnings)
        {
            string report = $"[StageAssetValidator] {count}개 검사 / 오류 {errors.Count} / 경고 {warnings.Count}";
            foreach (string error in errors) { report += $"\nERROR: {error}"; }
            foreach (string warning in warnings) { report += $"\nWARN: {warning}"; }
            return report;
        }
    }
}
