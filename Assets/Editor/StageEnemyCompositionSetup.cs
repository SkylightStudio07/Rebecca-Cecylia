using System;
using System.Linq;
using RCCom.Data;
using RCCom.Definitions.Stage;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>지형 콘셉트에 맞춘 CH1 특수 적 편성을 반복 실행해도 중복 없이 적용한다.</summary>
    public static class StageEnemyCompositionSetup
    {
        private const string ExploderId = "enemy-explode";
        private const string HealerId = "enemy-heal";
        private const string DefenderId = "enemy-defend";
        private const string HeavyTankerId = "enemy-heavytanker";

        [MenuItem("RCCom/Stages/Apply CH1 Special Enemy Encounters")]
        public static void ApplySpecialEnemyEncounters()
        {
            ApplyDefenderIntroduction();
            ApplyToAllWaves("ch1-06", HealerId, ExploderId);
            ApplyToAllWaves("ch1-07", ExploderId, HealerId);
            ApplyHeavyTankerEncounter();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            StageCatalogBuilder.BuildCatalog();

            if (!StageAssetValidator.ValidateAll(out string report))
            {
                throw new InvalidOperationException(report);
            }

            VerifyHeavyTankerEncounter();
            Debug.Log($"[StageEnemyCompositionSetup] 1-2 방어·1-6 힐러·1-7 자폭 드론·" +
                      $"1-8 헤비탱커 편성 완료\n{report}");
        }

        private static void ApplyHeavyTankerEncounter()
        {
            const string stagePath = "Assets/Data/Stages/CH1/ch1-08.asset";
            StageDefinition stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
            if (stage == null || stage.waves == null || stage.waves.Count == 0)
            {
                throw new InvalidOperationException($"편성 가능한 1-8 StageDefinition을 찾지 못했습니다: {stagePath}");
            }

            Undo.RecordObject(stage, "Apply Heavy Tanker Encounter");
            for (int waveIndex = 0; waveIndex < stage.waves.Count; waveIndex++)
            {
                StageWaveDefinition wave = stage.waves[waveIndex];
                wave.spawns ??= new System.Collections.Generic.List<StageEnemySpawn>();
                StageEnemySpawn spawn = wave.spawns.FirstOrDefault(item => item.enemyId == HeavyTankerId);
                if (spawn == null)
                {
                    spawn = new StageEnemySpawn { enemyId = HeavyTankerId };
                    wave.spawns.Add(spawn);
                }

                // 정면 피해 50% 감소와 높은 체력을 함께 가진 적이라 마지막 웨이브 전까지는 한 기로
                // 측면 대응을 학습시키고, 마지막 웨이브에서만 두 기로 늘려 난이도 급등을 제한한다.
                spawn.count = waveIndex < 2 ? 1 : 2;
                spawn.interval = 2.5f;
                spawn.initialDelay = waveIndex == 0 ? 1.5f : 2.2f;
            }

            stage.schemaVersion = StageDefinition.CurrentSchemaVersion;
            EditorUtility.SetDirty(stage);
        }

        private static void VerifyHeavyTankerEncounter()
        {
            StageDefinition stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(
                "Assets/Data/Stages/CH1/ch1-08.asset");
            if (stage == null || stage.waves == null || stage.waves.Count == 0)
            {
                throw new InvalidOperationException("1-8 헤비탱커 편성 검증 대상이 없습니다.");
            }

            for (int waveIndex = 0; waveIndex < stage.waves.Count; waveIndex++)
            {
                StageWaveDefinition wave = stage.waves[waveIndex];
                StageEnemySpawn[] matches = wave.spawns?
                    .Where(item => item.enemyId == HeavyTankerId)
                    .ToArray() ?? Array.Empty<StageEnemySpawn>();
                int expectedCount = waveIndex < 2 ? 1 : 2;
                if (matches.Length != 1 || matches[0].count != expectedCount)
                {
                    throw new InvalidOperationException(
                        $"1-8 웨이브 {waveIndex + 1}의 헤비탱커 편성이 올바르지 않습니다.");
                }
            }

            Debug.Log("[StageEnemyCompositionSetup] PASS — 1-8 헤비탱커 첫 두 웨이브 1기, 이후 2기 편성 확인");
        }

        private static void ApplyDefenderIntroduction()
        {
            const string stagePath = "Assets/Data/Stages/CH1/ch1-02.asset";
            StageDefinition stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
            if (stage == null || stage.waves == null || stage.waves.Count == 0)
            {
                throw new InvalidOperationException($"편성 가능한 1-2 StageDefinition을 찾지 못했습니다: {stagePath}");
            }

            Undo.RecordObject(stage, "Apply Defender Introduction");
            for (int waveIndex = 0; waveIndex < stage.waves.Count; waveIndex++)
            {
                StageWaveDefinition wave = stage.waves[waveIndex];
                wave.spawns ??= new System.Collections.Generic.List<StageEnemySpawn>();
                StageEnemySpawn spawn = wave.spawns.FirstOrDefault(item => item.enemyId == DefenderId);
                if (spawn == null)
                {
                    spawn = new StageEnemySpawn { enemyId = DefenderId };
                    wave.spawns.Add(spawn);
                }

                // 초반 스테이지에서 오라 중첩이 급격히 커지지 않도록 처음 두 웨이브는 한 기로
                // 능력을 소개하고, 세 번째 웨이브부터 두 기까지만 등장시킨다.
                spawn.count = waveIndex < 2 ? 1 : 2;
                spawn.interval = 2f;
                spawn.initialDelay = waveIndex == 0 ? 1.5f : 2.2f;
            }

            stage.schemaVersion = StageDefinition.CurrentSchemaVersion;
            EditorUtility.SetDirty(stage);
        }

        private static void ApplyToAllWaves(string stageId, string enemyId, string excludedEnemyId)
        {
            string stagePath = $"Assets/Data/Stages/CH1/{stageId}.asset";
            StageDefinition stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
            if (stage == null)
            {
                throw new InvalidOperationException($"StageDefinition을 찾을 수 없습니다: {stagePath}");
            }
            if (stage.waves == null || stage.waves.Count == 0)
            {
                throw new InvalidOperationException($"편성할 웨이브가 없습니다: {stagePath}");
            }

            Undo.RecordObject(stage, "Apply Stage Special Enemy Encounters");
            for (int waveIndex = 0; waveIndex < stage.waves.Count; waveIndex++)
            {
                StageWaveDefinition wave = stage.waves[waveIndex];
                wave.spawns ??= new System.Collections.Generic.List<StageEnemySpawn>();

                // 지형별 대표 특수 적을 분명하게 유지한다. 이전 자동화가 실행된 뒤 기획이 바뀌어도
                // 반대 지형의 특수 적 행을 함께 제거해야 Studio와 실제 편성이 다시 일치한다.
                wave.spawns.RemoveAll(item => item.enemyId == excludedEnemyId);
                StageEnemySpawn spawn = wave.spawns.FirstOrDefault(item => item.enemyId == enemyId);
                if (spawn == null)
                {
                    spawn = new StageEnemySpawn { enemyId = enemyId };
                    wave.spawns.Add(spawn);
                }

                // 특수 능력 적은 첫 세 웨이브까지만 수량을 늘린다. 10웨이브 규모에서 인덱스를
                // 그대로 수량으로 쓰면 힐러·자폭 드론이 한 웨이브에 10기까지 쌓여 역할 학습이
                // 아니라 능력 물량전이 되므로, 이후 난이도는 일반 적과 체력 배율이 담당한다.
                spawn.count = Mathf.Min(waveIndex + 1, 3);
                spawn.interval = enemyId == HealerId ? 2f : 1.5f;
                spawn.initialDelay = waveIndex == 0 ? 1.5f : 2.2f;
            }
            stage.schemaVersion = StageDefinition.CurrentSchemaVersion;
            EditorUtility.SetDirty(stage);
        }
    }
}
