using System;
using System.Collections.Generic;
using System.Linq;
using RCCom.Data;
using RCCom.Definitions.Stage;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 기존에 사람이 조정한 세 웨이브를 보존하면서 CH1 후반 스테이지의 플레이 시간을 늘린다.
    /// 새 웨이브는 직전 편성을 초안으로 삼아 현재 스테이지의 난이도 흐름을 끊지 않는다.
    /// </summary>
    public static class StageWaveCountExpander
    {
        private const int FirstStageNumber = 1;
        private const int LastStageNumber = 8;
        private const int FirstStageWaveCount = 3;
        private const float HealthGrowthPerWave = 0.08f;
        private const string NormalEnemyId = "enemy-normal";

        [MenuItem("RCCom/Stages/Expand CH1 Wave Counts")]
        public static void ExpandChapterOne()
        {
            for (int stageNumber = FirstStageNumber; stageNumber <= LastStageNumber; stageNumber++)
            {
                StageDefinition stage = LoadStage(stageNumber);
                int targetCount = GetTargetWaveCount(stageNumber);
                if (stage.waves == null || stage.waves.Count == 0)
                {
                    throw new InvalidOperationException($"1-{stageNumber}에 복제할 기존 웨이브가 없습니다.");
                }
                if (stage.waves.Count > targetCount)
                {
                    // 이미 사람이 더 길게 제작한 스테이지를 자동화가 잘라내면 편성 데이터가 유실된다.
                    throw new InvalidOperationException(
                        $"1-{stageNumber}은 목표 {targetCount}보다 많은 {stage.waves.Count}웨이브입니다. " +
                        "자동으로 삭제하지 않습니다.");
                }

                Undo.RecordObject(stage, "Expand Chapter One Wave Counts");
                while (stage.waves.Count < targetCount)
                {
                    stage.waves.Add(CloneNextWave(stage.waves[^1], stage.waves.Count));
                }

                stage.schemaVersion = StageDefinition.CurrentSchemaVersion;
                EditorUtility.SetDirty(stage);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 확장된 웨이브에도 각 지형의 대표 특수 적을 동일한 상한 규칙으로 다시 배치한다.
            StageEnemyCompositionSetup.ApplySpecialEnemyEncounters();
            VerifyChapterOne();
        }

        [MenuItem("RCCom/Verify/CH1 Wave Counts")]
        public static void VerifyChapterOne()
        {
            var summary = new List<string>();
            for (int stageNumber = FirstStageNumber; stageNumber <= LastStageNumber; stageNumber++)
            {
                StageDefinition stage = LoadStage(stageNumber);
                int expected = GetTargetWaveCount(stageNumber);
                int actual = stage.waves?.Count ?? 0;
                if (actual != expected)
                {
                    throw new InvalidOperationException(
                        $"1-{stageNumber} 웨이브 수가 다릅니다. expected={expected}, actual={actual}");
                }

                for (int waveIndex = 0; waveIndex < actual; waveIndex++)
                {
                    StageWaveDefinition wave = stage.waves[waveIndex];
                    string expectedName = $"WAVE {waveIndex + 1:00}";
                    if (wave == null || wave.displayName != expectedName ||
                        wave.spawns == null || wave.spawns.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"1-{stageNumber}의 {expectedName} 데이터가 비어 있거나 이름이 다릅니다.");
                    }
                }

                summary.Add($"1-{stageNumber}={actual}");
            }

            Debug.Log($"[StageWaveCountExpander] PASS — {string.Join(", ", summary)}");
        }

        private static StageDefinition LoadStage(int stageNumber)
        {
            string path = $"Assets/Data/Stages/CH1/ch1-{stageNumber:00}.asset";
            StageDefinition stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
            if (stage == null)
            {
                throw new InvalidOperationException($"StageDefinition을 찾지 못했습니다: {path}");
            }
            return stage;
        }

        private static int GetTargetWaveCount(int stageNumber)
        {
            return stageNumber == FirstStageNumber ? FirstStageWaveCount : stageNumber + 3;
        }

        private static StageWaveDefinition CloneNextWave(StageWaveDefinition previous, int nextWaveIndex)
        {
            if (previous == null)
            {
                throw new InvalidOperationException($"WAVE {nextWaveIndex:00} 앞 웨이브가 null입니다.");
            }

            var next = new StageWaveDefinition
            {
                displayName = $"WAVE {nextWaveIndex + 1:00}",
                buildPhaseDuration = previous.buildPhaseDuration,
                healthMultiplier = previous.healthMultiplier + HealthGrowthPerWave,
                spawns = previous.spawns?
                    .Where(spawn => spawn != null)
                    .Select(CloneSpawn)
                    .ToList() ?? new List<StageEnemySpawn>()
            };

            StageEnemySpawn normal = next.spawns.FirstOrDefault(spawn => spawn.enemyId == NormalEnemyId);
            if (normal != null)
            {
                normal.count++;
            }
            return next;
        }

        private static StageEnemySpawn CloneSpawn(StageEnemySpawn source)
        {
            return new StageEnemySpawn
            {
                enemyId = source.enemyId,
                count = source.count,
                interval = source.interval,
                initialDelay = source.initialDelay
            };
        }
    }
}
