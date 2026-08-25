using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Effects.Enemy.Concrete;
using RCCom.Runtime;
using RCCom.Runtime.Visuals;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>힐러의 주기·범위·자기 제외·최대 체력 캡을 에셋 생성 뒤 회귀 검증한다.</summary>
    public static class EnemyHealerVerifier
    {
        private const float Epsilon = 0.001f;

        [MenuItem("RCCom/Verify/Enemy Healer")]
        public static void Verify()
        {
            HealNearbyEnemiesEffect healEffect =
                AssetDatabase.LoadAssetAtPath<HealNearbyEnemiesEffect>(
                    EnemySpecialEffectAssetBuilder.HealerEffectPath);
            EnemyDefinition healerDefinition =
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    "Assets/Data/Enemies/enemy-heal/EnemyDefinition.asset");
            if (healEffect == null || healerDefinition == null)
            {
                throw new InvalidOperationException("생성된 힐러 Effect 또는 Definition 에셋을 찾지 못했습니다.");
            }

            if (healerDefinition.effects == null || healerDefinition.effects.Count != 1 ||
                healerDefinition.effects[0] != healEffect)
            {
                throw new InvalidOperationException("힐러 Definition에는 치유 Effect 하나만 연결되어야 합니다.");
            }

            AssertApproximately(3f, healerDefinition.data.attackRange, "힐러 공격 범위 레시피 값이 다릅니다.");
            AssertApproximately(1.5f, healerDefinition.data.attackInterval, "힐러 회복 주기가 1.5초가 아닙니다.");
            AssertApproximately(0f, healerDefinition.data.contactDamage, "힐러의 접촉 공격력은 0이어야 합니다.");

            GameObject pulsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CombatVfxAssetBuilder.ShockwaveRingPrefabPath);
            ShockwaveRingVisualEffect pulseVisual =
                AssetDatabase.LoadAssetAtPath<ShockwaveRingVisualEffect>(
                    EnemySpecialEffectAssetBuilder.HealerPulseVisualPath);
            if (pulsePrefab == null || pulsePrefab.GetComponent<ShockwaveRing>() == null ||
                pulseVisual == null)
            {
                throw new InvalidOperationException("힐러 회복 파장 프리팹 또는 시각 설정 SO가 올바르지 않습니다.");
            }

            var serializedEffect = new SerializedObject(healEffect);
            AssertReference(
                serializedEffect, "healingPulsePrefab", pulsePrefab, "힐러 Effect에 공용 파장 프리팹이 연결되지 않았습니다.");
            AssertReference(
                serializedEffect, "healingPulseVisual", pulseVisual, "힐러 Effect에 회복 파장 시각 설정이 연결되지 않았습니다.");

            // 실제 에셋 참조는 유지하되 이동만 0인 런타임 Definition을 만들어, 범위 경계 검증이
            // 힐러의 웨이포인트 이동량에 흔들리지 않게 한다. 회복 계산 검증 중 Edit Mode 씬에
            // VFX 프리팹이 생성되지 않도록 시각 참조가 비어 있는 임시 Effect를 사용한다.
            HealNearbyEnemiesEffect runtimeHealEffect =
                ScriptableObject.CreateInstance<HealNearbyEnemiesEffect>();
            EnemyDefinition healerRuntimeDefinition = CreateDefinition(
                "verify-healer", 100f, 3f, 1.5f, runtimeHealEffect);
            EnemyDefinition targetDefinition = CreateDefinition(
                "verify-heal-target", 50f, 0f, 1f);

            try
            {
                EnemyInstance healer = Spawn(healerRuntimeDefinition, Vector2.zero);
                EnemyInstance nearTarget = Spawn(targetDefinition, new Vector2(2f, 0f));
                EnemyInstance edgeTarget = Spawn(targetDefinition, new Vector2(3f, 0f));
                EnemyInstance farTarget = Spawn(targetDefinition, new Vector2(3.01f, 0f));

                healer.TakeDamage(20f);
                nearTarget.TakeDamage(10f);
                edgeTarget.TakeDamage(2f);
                farTarget.TakeDamage(10f);

                var activeEnemies = new List<EnemyInstance>
                {
                    healer,
                    nearTarget,
                    edgeTarget,
                    farTarget,
                };

                healer.Tick(1.49f, activeEnemies);
                AssertApproximately(40f, nearTarget.currentHealth, "1.5초 전에는 회복되면 안 됩니다.");

                healer.Tick(0.01f, activeEnemies);
                AssertApproximately(45f, nearTarget.currentHealth, "범위 안 대상의 1회 회복량이 다릅니다.");
                AssertApproximately(50f, edgeTarget.currentHealth, "경계 대상은 회복되고 최대 체력을 넘지 않아야 합니다.");
                AssertApproximately(40f, farTarget.currentHealth, "범위 밖 대상이 회복되었습니다.");
                AssertApproximately(80f, healer.currentHealth, "기본 설정에서 힐러 자신이 회복되었습니다.");

                healer.Tick(1.5f, activeEnemies);
                AssertApproximately(50f, nearTarget.currentHealth, "두 번째 1.5초 주기 회복이 실행되지 않았습니다.");

                Debug.Log("[EnemyHealerVerifier] PASS — 1.5초 주기, 범위 3, 회복량 5, 자기 제외, " +
                          "최대 체력 캡, 회복 파장 프리팹·시각 SO 배선 확인");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(healerRuntimeDefinition);
                UnityEngine.Object.DestroyImmediate(targetDefinition);
                UnityEngine.Object.DestroyImmediate(runtimeHealEffect);
            }
        }

        private static EnemyDefinition CreateDefinition(
            string enemyId,
            float maxHealth,
            float attackRange,
            float attackInterval,
            params HealNearbyEnemiesEffect[] effects)
        {
            EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            definition.data = new EnemyData
            {
                enemyId = enemyId,
                displayName = enemyId,
                maxHealth = maxHealth,
                moveSpeed = 0f,
                attackRange = attackRange,
                attackInterval = attackInterval,
            };
            definition.effects = new List<RCCom.Effects.Enemy.EnemyEffectBase>(effects);
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

        private static void AssertReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object expected,
            string message)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != expected)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
