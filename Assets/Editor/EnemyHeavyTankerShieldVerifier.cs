using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Effects.Enemy;
using RCCom.Effects.Enemy.Concrete;
using RCCom.Runtime;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>헤비탱커 정면 반원 피해 감소의 방향 경계와 에셋 배선을 검증한다.</summary>
    public static class EnemyHeavyTankerShieldVerifier
    {
        private const float Epsilon = 0.001f;

        [MenuItem("RCCom/Verify/Enemy Heavy Tanker Frontal Shield")]
        public static void Verify()
        {
            FrontalShieldEffect shieldEffect =
                AssetDatabase.LoadAssetAtPath<FrontalShieldEffect>(
                    EnemySpecialEffectAssetBuilder.HeavyTankerShieldEffectPath);
            EnemyDefinition heavyTanker =
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    "Assets/Data/Enemies/enemy-heavytanker/EnemyDefinition.asset");
            if (shieldEffect == null || heavyTanker == null)
            {
                throw new InvalidOperationException("헤비탱커 방어막 Effect 또는 Definition을 찾지 못했습니다.");
            }

            if (heavyTanker.data.kind != EnemyKind.HeavyTanker ||
                heavyTanker.effects == null || !heavyTanker.effects.Contains(shieldEffect))
            {
                throw new InvalidOperationException("헤비탱커 Definition에 정면 방어막 Effect가 연결되지 않았습니다.");
            }
            AssertApproximately(0.5f, shieldEffect.DamageMultiplier, "정면 피해 배율이 50%가 아닙니다.");
            AssertApproximately(2.15f, shieldEffect.ShieldRadius, "반원 방어막 반경이 다릅니다.");
            if (shieldEffect.Material == null || shieldEffect.ShieldColor.b <= shieldEffect.ShieldColor.r)
            {
                throw new InvalidOperationException("헤비탱커의 파란 반원 방어막 머티리얼 배선이 올바르지 않습니다.");
            }

            FrontalShieldEffect runtimeEffect = ScriptableObject.CreateInstance<FrontalShieldEffect>();
            EnemyDefinition runtimeDefinition = CreateDefinition(runtimeEffect);
            try
            {
                AssertDamage(runtimeDefinition, new Vector2(5f, 0f), 90f, 10f, "정면 피해가 50% 감소하지 않았습니다.");
                AssertDamage(runtimeDefinition, new Vector2(-5f, 0f), 80f, 20f, "후면 피해가 감소했습니다.");
                AssertDamage(runtimeDefinition, new Vector2(0f, 5f), 80f, 20f, "측면 피해가 감소했습니다.");
                AssertDamage(runtimeDefinition, null, 80f, 20f, "발신 위치 없는 지속 피해가 감소했습니다.");

                EnemyInstance upward = Spawn(
                    runtimeDefinition,
                    new[] { Vector2.zero, new Vector2(0f, 10f) });
                upward.TakeDamage(20f, new Vector2(0f, 5f));
                AssertApproximately(90f, upward.currentHealth, "경로 방향이 바뀐 정면 판정이 회전하지 않았습니다.");

                EnemyInstance corner = Spawn(
                    runtimeDefinition,
                    new[] { Vector2.zero, Vector2.right, Vector2.one });
                corner.Tick(0f);
                corner.Tick(1f);
                AssertApproximately(0f, corner.FacingDirection.x, "코너 도착 프레임에 다음 선분 방향이 갱신되지 않았습니다.");
                AssertApproximately(1f, corner.FacingDirection.y, "코너 도착 프레임의 방어막 방향이 올바르지 않습니다.");

                Debug.Log("[EnemyHeavyTankerShieldVerifier] PASS — 정면 180도 직접 피해 50% 감소, " +
                          "측면·후면·지속 피해 원본 유지, 경로 방향 회전, 파란 반원 배선 확인");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeDefinition);
                UnityEngine.Object.DestroyImmediate(runtimeEffect);
            }
        }

        public static void VerifyRegressionSuite()
        {
            Verify();
            EnemySelfDestructVerifier.Verify();
            EnemyDefenderVerifier.Verify();
            EndlessBossPromotionVerifier.Verify();
        }

        private static void AssertDamage(
            EnemyDefinition definition,
            Vector2? sourcePosition,
            float expectedHealth,
            float expectedAppliedDamage,
            string message)
        {
            EnemyInstance instance = Spawn(
                definition,
                new[] { Vector2.zero, new Vector2(10f, 0f) });
            float appliedDamage = -1f;
            instance.Damaged += damage => appliedDamage = damage;
            instance.TakeDamage(20f, sourcePosition);
            AssertApproximately(expectedHealth, instance.currentHealth, message);
            AssertApproximately(expectedAppliedDamage, appliedDamage, $"{message} Damaged 이벤트 값도 다릅니다.");
        }

        private static EnemyDefinition CreateDefinition(FrontalShieldEffect effect)
        {
            EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            definition.data = new EnemyData
            {
                enemyId = "verify-heavy-tanker",
                displayName = "verify-heavy-tanker",
                kind = EnemyKind.HeavyTanker,
                maxHealth = 100f,
                moveSpeed = 1f,
                waveCost = 1f,
            };
            definition.effects = new List<EnemyEffectBase> { effect };
            return definition;
        }

        private static EnemyInstance Spawn(
            EnemyDefinition definition,
            IReadOnlyList<Vector2> path)
        {
            var instance = new EnemyInstance
            {
                definition = definition,
                position = path[0],
            };
            instance.Spawn(path, null);
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
