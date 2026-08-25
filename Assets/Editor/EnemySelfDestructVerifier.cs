using System;
using System.Collections.Generic;
using System.Reflection;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Effects.Enemy;
using RCCom.Effects.Enemy.Concrete;
using RCCom.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RCCom.EditorTools
{
    /// <summary>자폭 적의 거점 피해·즉사·이벤트 단일 발생을 실제 런타임 인스턴스로 검증한다.</summary>
    public static class EnemySelfDestructVerifier
    {
        [MenuItem("RCCom/Enemies/Verify Exploder Special Effect")]
        public static void Verify()
        {
            GameObject baseObject = null;
            EnemyDefinition definition = null;
            Scene playerScene = default;

            try
            {
                baseObject = new GameObject("ExploderVerifier_Base");
                baseObject.SetActive(false);
                BaseController baseController = baseObject.AddComponent<BaseController>();
                baseController.maxHealth = 100f;
                baseObject.SetActive(true);
                PropertyInfo currentHealthProperty = typeof(BaseController).GetProperty(
                    nameof(BaseController.CurrentHealth),
                    BindingFlags.Instance | BindingFlags.Public);
                currentHealthProperty?.SetValue(baseController, 100f);

                ContactDamageEffect contactEffect =
                    AssetDatabase.LoadAssetAtPath<ContactDamageEffect>(
                        "Assets/Data/Effects/Enemy_ContactDamageEffect_Default.asset");
                SelfDestructOnCriticalContactEffect selfDestructEffect =
                    AssetDatabase.LoadAssetAtPath<SelfDestructOnCriticalContactEffect>(
                        EnemySpecialEffectAssetBuilder.SelfDestructEffectPath);
                if (contactEffect == null || selfDestructEffect == null)
                {
                    throw new InvalidOperationException("자폭 검증에 필요한 Effect 에셋이 없습니다.");
                }

                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                definition.data = new EnemyData
                {
                    enemyId = "verifier-exploder",
                    displayName = "자폭 검증체",
                    maxHealth = 25f,
                    moveSpeed = 1f,
                    contactDamage = 40f,
                    attackInterval = 1f,
                    waveCost = 1f,
                };
                definition.effects = new List<EnemyEffectBase> { contactEffect, selfDestructEffect };

                var instance = new EnemyInstance
                {
                    definition = definition,
                    position = Vector2.zero,
                };
                int diedCount = 0;
                int reachedGoalCount = 0;
                instance.Died += () => diedCount++;
                instance.ReachedGoal += () => reachedGoalCount++;
                instance.Spawn(new List<Vector2> { Vector2.zero }, baseController);
                instance.Tick(0f);

                AssertNear(baseController.CurrentHealth, 60f, "Studio contactDamage가 거점에 적용되지 않았습니다.");
                if (!instance.IsDead || instance.currentHealth != 0f)
                {
                    throw new InvalidOperationException("거점 접촉 후 자폭 적이 즉시 사망하지 않았습니다.");
                }

                if (diedCount != 1 || reachedGoalCount != 0)
                {
                    throw new InvalidOperationException(
                        $"자폭 종료 이벤트가 중복되거나 잘못됐습니다: Died={diedCount}, ReachedGoal={reachedGoalCount}");
                }

                playerScene = EditorSceneManager.OpenScene(
                    "Assets/Scenes/DefenseScene.unity",
                    OpenSceneMode.Additive);
                PlayerController player = FindComponentInScene<PlayerController>(playerScene);
                if (player == null)
                {
                    throw new InvalidOperationException("DefenseScene에서 PlayerController를 찾지 못했습니다.");
                }

                SetCurrentHealth(player, 100f);
                var playerContactInstance = new EnemyInstance
                {
                    definition = definition,
                    position = Vector2.zero,
                };
                int playerContactDiedCount = 0;
                playerContactInstance.Died += () => playerContactDiedCount++;
                playerContactInstance.Spawn(new List<Vector2> { Vector2.one }, baseController);
                playerContactInstance.DealContactDamageTo(player);

                AssertNear(player.CurrentHealth, 60f, "Studio contactDamage가 플레이어에 적용되지 않았습니다.");
                if (!playerContactInstance.IsDead || playerContactDiedCount != 1)
                {
                    throw new InvalidOperationException("플레이어 접촉 후 자폭 적이 즉시 한 번 사망하지 않았습니다.");
                }

                Debug.Log(
                    "[EnemySelfDestructVerifier] 거점·플레이어 피해 40, 즉사, Died 1회 검증 통과");
            }
            finally
            {
                if (playerScene.IsValid() && playerScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(playerScene, true);
                }

                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }

                if (baseObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(baseObject);
                }
            }
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void SetCurrentHealth(object target, float value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                "CurrentHealth",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(true);
            if (setter == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name}.CurrentHealth setter를 찾지 못했습니다.");
            }

            setter.Invoke(target, new object[] { value });
        }

        private static void AssertNear(float actual, float expected, string message)
        {
            if (Mathf.Abs(actual - expected) > 0.001f)
            {
                throw new InvalidOperationException($"{message} 기대값={expected}, 실제값={actual}");
            }
        }
    }
}
