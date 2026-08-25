using System;
using System.Collections.Generic;
using System.Reflection;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Runtime;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>무한 모드 보스 승급의 모드 경계·수식·SO 불변성을 순수 런타임 데이터로 검증한다.</summary>
    public static class EndlessBossPromotionVerifier
    {
        private const float Epsilon = 0.001f;

        [MenuItem("RCCom/Verify/Endless Boss Promotion")]
        public static void Verify()
        {
            GameObject viewObject = null;
            Sprite runtimeSprite = null;
            var definitions = new List<EnemyDefinition>
            {
                CreateDefinition("light", 100f, 1.2f, 10f, 8, 4),
                CreateDefinition("selected", 250f, 2.4f, 30f, 12, 5),
                CreateDefinition("reward-tie", 50f, 3.1f, 20f, 12, 9),
            };

            try
            {
                if (!EndlessBossPromotion.ShouldPromote(BattleMode.Endless, 5, 5) ||
                    EndlessBossPromotion.ShouldPromote(BattleMode.Endless, 4, 5) ||
                    EndlessBossPromotion.ShouldPromote(BattleMode.Stage, 5, 5))
                {
                    throw new InvalidOperationException("무한 모드 매 5웨이브 전용 승급 조건이 깨졌습니다.");
                }

                EnemyData selectedOriginal = definitions[1].data;
                EnemyData bossData = EndlessBossPromotion.CreateRuntimeData(definitions, 1);
                if (bossData == null || ReferenceEquals(bossData, selectedOriginal))
                {
                    throw new InvalidOperationException("보스 전투 데이터가 런타임 복제본으로 생성되지 않았습니다.");
                }

                AssertNear(700f, bossData.maxHealth, "웨이브 총 체력 400 × 1.75가 아닙니다.");
                AssertNear(0.75f, bossData.moveSpeed, "보스 고정 이동속도가 다릅니다.");
                AssertNear(52.5f, bossData.contactDamage, "최대 접촉 공격력 30 × 1.75가 아닙니다.");
                AssertEqual(36, bossData.goldReward, "최고 골드 보상의 3배가 아닙니다.");
                AssertEqual(27, bossData.expReward, "골드 동률 중 높은 EXP의 3배가 아닙니다.");
                AssertNear(selectedOriginal.attackRange, bossData.attackRange, "선택된 적의 공격 범위를 유지하지 않았습니다.");
                AssertNear(selectedOriginal.attackInterval, bossData.attackInterval, "선택된 적의 공격 주기를 유지하지 않았습니다.");

                var instance = new EnemyInstance
                {
                    definition = definitions[1],
                    position = Vector2.zero,
                };
                instance.Spawn(new[] { Vector2.zero, Vector2.right }, null, bossData, true);
                instance.ApplyHealthMultiplier(1.5f);

                if (!instance.IsBoss || !instance.IsPromotedBoss || instance.definition != definitions[1])
                {
                    throw new InvalidOperationException("승급 인스턴스가 선택된 EnemyDefinition을 유지하지 않았습니다.");
                }

                AssertNear(1050f, instance.MaxHealth, "무한 웨이브 체력 배율이 보스 체력에 한 번 적용되지 않았습니다.");
                AssertNear(250f, selectedOriginal.maxHealth, "EnemyDefinition 원본 체력이 변경되었습니다.");
                AssertNear(2.4f, selectedOriginal.moveSpeed, "EnemyDefinition 원본 이동속도가 변경되었습니다.");
                AssertEqual(5, selectedOriginal.expReward, "EnemyDefinition 원본 보상이 변경되었습니다.");

                runtimeSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
                definitions[1].sprite = runtimeSprite;
                viewObject = new GameObject("EndlessBossPromotionVerifier_View");
                CircleCollider2D circleCollider = viewObject.AddComponent<CircleCollider2D>();
                circleCollider.offset = new Vector2(0.1f, -0.2f);
                circleCollider.radius = 0.6f;
                viewObject.AddComponent<SpriteRenderer>();
                EnemyView view = viewObject.AddComponent<EnemyView>();
                MethodInfo awake = typeof(EnemyView).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (awake == null)
                {
                    throw new InvalidOperationException("EnemyView.Awake 초기화 경로를 찾지 못했습니다.");
                }

                // Edit Mode에서 AddComponent만으로는 Awake가 자동 호출되지 않는다. 실제 프리팹
                // Instantiate와 같은 초기화 순서를 재현한 뒤 Bind의 drawing-size 경로를 검증한다.
                awake.Invoke(view, null);
                view.Bind(instance);

                AssertNear(1.5f, viewObject.transform.localScale.x, "보스 drawing size가 1.5배가 아닙니다.");
                AssertNear(1.5f, viewObject.transform.localScale.y, "보스 drawing size가 균일한 1.5배가 아닙니다.");
                AssertNear(0.6f, circleCollider.radius * viewObject.transform.lossyScale.x,
                    "보스 시각 확대가 월드 접촉 반경까지 키웠습니다.");
                AssertNear(0.1f, circleCollider.offset.x * viewObject.transform.lossyScale.x,
                    "보스 시각 확대가 Collider 오프셋을 변경했습니다.");

                Debug.Log("[EndlessBossPromotionVerifier] PASS — 무한 모드 5웨이브 조건, 랜덤 슬롯 승급용 " +
                          "런타임 복제, 체력·속도·공격·골드/EXP 수식, drawing size 1.5배와 " +
                          "Collider 판정 불변성, 원본 SO 불변성 확인");
            }
            finally
            {
                if (viewObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(viewObject);
                }

                if (runtimeSprite != null)
                {
                    UnityEngine.Object.DestroyImmediate(runtimeSprite);
                }

                for (int i = 0; i < definitions.Count; i++)
                {
                    if (definitions[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(definitions[i]);
                    }
                }
            }
        }

        private static EnemyDefinition CreateDefinition(
            string enemyId,
            float maxHealth,
            float moveSpeed,
            float contactDamage,
            int goldReward,
            int expReward)
        {
            EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            definition.data = new EnemyData
            {
                enemyId = enemyId,
                displayName = enemyId,
                kind = EnemyKind.Normal,
                maxHealth = maxHealth,
                moveSpeed = moveSpeed,
                contactDamage = contactDamage,
                attackRange = 1.75f,
                attackInterval = 1.25f,
                waveCost = 2f,
                minWave = 1,
                goldReward = goldReward,
                expReward = expReward,
            };
            return definition;
        }

        private static void AssertNear(float expected, float actual, string message)
        {
            if (Mathf.Abs(expected - actual) > Epsilon)
            {
                throw new InvalidOperationException($"{message} expected={expected}, actual={actual}");
            }
        }

        private static void AssertEqual(int expected, int actual, string message)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException($"{message} expected={expected}, actual={actual}");
            }
        }
    }
}
