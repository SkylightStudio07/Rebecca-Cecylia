using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Stage;
using RCCom.Effects.Enemy;
using RCCom.Effects.Enemy.Concrete;
using RCCom.Runtime;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>방어 적의 10% 최대 체력 오라, 범위 경계, 만료, 에셋 배선을 회귀 검증한다.</summary>
    public static class EnemyDefenderVerifier
    {
        private const float Epsilon = 0.001f;

        [MenuItem("RCCom/Verify/Enemy Defender")]
        public static void Verify()
        {
            MaxHealthAuraEffect auraEffect =
                AssetDatabase.LoadAssetAtPath<MaxHealthAuraEffect>(
                    EnemySpecialEffectAssetBuilder.DefenderEffectPath);
            EnemyDefinition defenderDefinition =
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    "Assets/Data/Enemies/enemy-defend/EnemyDefinition.asset");
            if (auraEffect == null || defenderDefinition == null)
            {
                throw new InvalidOperationException("생성된 방어 Effect 또는 Definition 에셋을 찾지 못했습니다.");
            }

            if (defenderDefinition.data.kind != EnemyKind.Defend ||
                defenderDefinition.effects == null || defenderDefinition.effects.Count != 1 ||
                defenderDefinition.effects[0] != auraEffect)
            {
                throw new InvalidOperationException("방어 Definition의 Kind 또는 Effect 배선이 올바르지 않습니다.");
            }

            AssertApproximately(4f, defenderDefinition.data.attackRange, "방어 오라 범위가 4가 아닙니다.");
            AssertApproximately(0f, defenderDefinition.data.contactDamage, "방어 적의 접촉 공격력은 0이어야 합니다.");
            AssertApproximately(1.1f, auraEffect.HealthMultiplier, "최대 체력 증가량이 10%가 아닙니다.");
            if (AssetImporter.GetAtPath("Assets/Art/Enemies/defend.png") is not TextureImporter importer)
            {
                throw new InvalidOperationException("Defend 스프라이트 Import 설정을 읽을 수 없습니다.");
            }
            AssertApproximately(160f, importer.spritePixelsPerUnit, "Defend 스프라이트 PPU가 크기 보정값과 다릅니다.");
            float longestSpriteAxis = Mathf.Max(
                defenderDefinition.sprite.bounds.size.x,
                defenderDefinition.sprite.bounds.size.y);
            if (longestSpriteAxis < 2f || longestSpriteAxis > 3f)
            {
                throw new InvalidOperationException(
                    $"Defend 스프라이트 실제 월드 크기가 특수 적 기준 범위를 벗어났습니다: {longestSpriteAxis}");
            }
            if (auraEffect.IncludeSelf || auraEffect.Material == null || auraEffect.AuraColor.g <= auraEffect.AuraColor.r)
            {
                throw new InvalidOperationException("방어 오라는 자신을 제외하고 초록 범위 머티리얼을 사용해야 합니다.");
            }

            VerifyStageIntroduction();

            MaxHealthAuraEffect runtimeEffect = ScriptableObject.CreateInstance<MaxHealthAuraEffect>();
            EnemyDefinition providerDefinition = CreateDefinition(
                "verify-defender", 100f, 4f, runtimeEffect);
            EnemyDefinition targetDefinition = CreateDefinition(
                "verify-defender-target", 100f, 0f);

            try
            {
                EnemyInstance provider = Spawn(providerDefinition, Vector2.zero);
                EnemyInstance nearTarget = Spawn(targetDefinition, new Vector2(3f, 0f));
                EnemyInstance edgeTarget = Spawn(targetDefinition, new Vector2(4f, 0f));
                EnemyInstance farTarget = Spawn(targetDefinition, new Vector2(4.01f, 0f));
                nearTarget.ApplyHealthMultiplier(1.5f);
                nearTarget.TakeDamage(75f);

                var activeEnemies = new List<EnemyInstance>
                {
                    provider,
                    nearTarget,
                    edgeTarget,
                    farTarget,
                };

                provider.Tick(0f, activeEnemies);
                AssertApproximately(165f, nearTarget.MaxHealth, "웨이브 배율 뒤 10% 오라 상한이 다릅니다.");
                AssertApproximately(82.5f, nearTarget.currentHealth, "오라 진입 시 현재 체력 비율이 보존되지 않았습니다.");
                AssertApproximately(110f, edgeTarget.MaxHealth, "범위 경계 대상이 오라를 받지 못했습니다.");
                AssertApproximately(100f, farTarget.MaxHealth, "범위 밖 대상이 오라를 받았습니다.");
                AssertApproximately(100f, provider.MaxHealth, "기본 설정에서 방어 유닛 자신이 오라를 받았습니다.");

                provider.position = new Vector2(20f, 0f);
                nearTarget.Tick(auraEffect.RefreshDuration + 0.01f, activeEnemies);
                AssertApproximately(150f, nearTarget.MaxHealth, "범위 이탈 뒤 최대 체력 오라가 만료되지 않았습니다.");
                AssertApproximately(75f, nearTarget.currentHealth, "오라 만료 시 현재 체력 비율이 보존되지 않았습니다.");

                Debug.Log("[EnemyDefenderVerifier] PASS — Defend Kind, 범위 4, 최대 체력 +10%, " +
                          "자기 제외, 범위 이탈 만료, 체력 비율 보존, 초록 지속 링 배선 확인");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(providerDefinition);
                UnityEngine.Object.DestroyImmediate(targetDefinition);
                UnityEngine.Object.DestroyImmediate(runtimeEffect);
            }
        }

        /// <summary>최대 체력 경로를 공유하는 기존 힐러·무한 보스까지 함께 확인한다.</summary>
        public static void VerifyRegressionSuite()
        {
            Verify();
            EnemyHealerVerifier.Verify();
            EndlessBossPromotionVerifier.Verify();
        }

        private static void VerifyStageIntroduction()
        {
            StageDefinition previousStage = AssetDatabase.LoadAssetAtPath<StageDefinition>(
                "Assets/Data/Stages/CH1/ch1-01.asset");
            if (previousStage == null)
            {
                throw new InvalidOperationException("방어 적의 첫 등장 이전 스테이지 1-1을 찾지 못했습니다.");
            }
            for (int waveIndex = 0; waveIndex < previousStage.waves.Count; waveIndex++)
            {
                bool alreadySpawned = previousStage.waves[waveIndex].spawns?.Exists(
                    spawn => string.Equals(spawn.enemyId, "enemy-defend", StringComparison.Ordinal)) == true;
                if (alreadySpawned)
                {
                    throw new InvalidOperationException("방어 적은 첫 등장 스테이지 1-2보다 앞서 나오면 안 됩니다.");
                }
            }

            StageDefinition stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(
                "Assets/Data/Stages/CH1/ch1-02.asset");
            if (stage == null || stage.waves == null || stage.waves.Count == 0)
            {
                throw new InvalidOperationException("방어 적의 첫 등장 스테이지 1-2를 찾지 못했습니다.");
            }

            for (int waveIndex = 0; waveIndex < stage.waves.Count; waveIndex++)
            {
                StageEnemySpawn defenderSpawn = stage.waves[waveIndex].spawns?.Find(
                    spawn => string.Equals(spawn.enemyId, "enemy-defend", StringComparison.Ordinal));
                int expectedCount = waveIndex < 2 ? 1 : 2;
                if (defenderSpawn == null || defenderSpawn.count != expectedCount)
                {
                    throw new InvalidOperationException(
                        $"1-2 웨이브 {waveIndex + 1}의 방어 적 편성이 올바르지 않습니다.");
                }
            }
        }

        private static EnemyDefinition CreateDefinition(
            string enemyId,
            float maxHealth,
            float attackRange,
            params EnemyEffectBase[] effects)
        {
            EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            definition.data = new EnemyData
            {
                enemyId = enemyId,
                displayName = enemyId,
                maxHealth = maxHealth,
                moveSpeed = 0f,
                attackRange = attackRange,
            };
            definition.effects = new List<EnemyEffectBase>(effects);
            return definition;
        }

        private static EnemyInstance Spawn(EnemyDefinition definition, Vector2 position)
        {
            var instance = new EnemyInstance
            {
                definition = definition,
                position = position,
            };
            instance.Spawn(new[] { position, position + Vector2.right * 100f }, null);
            return instance;
        }

        private static void AssertApproximately(float expected, float actual, string message)
        {
            if (Mathf.Abs(expected - actual) > Epsilon)
            {
                throw new InvalidOperationException($"{message} expected={expected}, actual={actual}");
            }
        }
    }
}
