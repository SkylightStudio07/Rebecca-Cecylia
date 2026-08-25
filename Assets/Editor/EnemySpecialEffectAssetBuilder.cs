using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RCCom.Effects.Enemy.Concrete;
using RCCom.Runtime;
using RCCom.Runtime.Visuals;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 특수 적 Effect SO와 레시피 배선을 재현 가능하게 만든다. Effect 파일과 JSON 경로를
    /// 손으로 각각 연결하면 한쪽만 저장되는 실수가 생기므로 단일 메뉴에서 함께 처리한다.
    /// </summary>
    public static class EnemySpecialEffectAssetBuilder
    {
        public const string SelfDestructEffectPath =
            "Assets/Data/Effects/Enemy_SelfDestructOnCriticalContactEffect.asset";
        public const string HealerEffectPath =
            "Assets/Data/Effects/Enemy_HealNearbyEnemiesEffect.asset";
        public const string HealerPulseVisualPath =
            "Assets/Data/Effects/Enemy_HealingPulseVisual.asset";

        private const string ContactDamageEffectPath =
            "Assets/Data/Effects/Enemy_ContactDamageEffect_Default.asset";
        private const string ExploderRecipePath =
            "Assets/Editor/EnemyRecipes/enemy-explode.json";
        private const string HealerRecipePath =
            "Assets/Editor/EnemyRecipes/enemy-heal.json";

        [MenuItem("RCCom/Enemies/Build Exploder Special Effect")]
        public static void BuildExploder()
        {
            SelfDestructOnCriticalContactEffect effect =
                AssetDatabase.LoadAssetAtPath<SelfDestructOnCriticalContactEffect>(SelfDestructEffectPath);
            if (effect == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(SelfDestructEffectPath) != null)
                {
                    throw new InvalidOperationException(
                        $"자폭 Effect 경로에 다른 타입의 에셋이 있습니다: {SelfDestructEffectPath}");
                }

                effect = ScriptableObject.CreateInstance<SelfDestructOnCriticalContactEffect>();
                AssetDatabase.CreateAsset(effect, SelfDestructEffectPath);
            }

            TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ExploderRecipePath);
            if (recipeAsset == null)
            {
                throw new InvalidOperationException($"자폭 적 레시피를 찾지 못했습니다: {ExploderRecipePath}");
            }

            EnemyAssetRecipe recipe = JsonUtility.FromJson<EnemyAssetRecipe>(recipeAsset.text);
            if (recipe == null || !string.Equals(recipe.enemyId, "enemy-explode", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("자폭 적 레시피의 enemyId가 올바르지 않습니다.");
            }

            recipe.effectPaths ??= new List<string>();
            EnsureEffectOrder(recipe.effectPaths);
            File.WriteAllText(
                Path.GetFullPath(ExploderRecipePath),
                JsonUtility.ToJson(recipe, true),
                new UTF8Encoding(false));

            AssetDatabase.ImportAsset(ExploderRecipePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnemyAssetBuilder.BuildSingle(ExploderRecipePath);

            Debug.Log("[EnemySpecialEffectAssetBuilder] 자폭 적 Effect 생성·레시피 배선 완료");
        }

        public static void BuildAndVerifyExploder()
        {
            BuildExploder();
            EnemySelfDestructVerifier.Verify();
        }

        [MenuItem("RCCom/Enemies/Build Healer Special Effect")]
        public static void BuildHealer()
        {
            HealNearbyEnemiesEffect effect =
                AssetDatabase.LoadAssetAtPath<HealNearbyEnemiesEffect>(HealerEffectPath);
            if (effect == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(HealerEffectPath) != null)
                {
                    throw new InvalidOperationException(
                        $"힐러 Effect 경로에 다른 타입의 에셋이 있습니다: {HealerEffectPath}");
                }

                effect = ScriptableObject.CreateInstance<HealNearbyEnemiesEffect>();
                AssetDatabase.CreateAsset(effect, HealerEffectPath);
            }

            GameObject pulsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CombatVfxAssetBuilder.ShockwaveRingPrefabPath);
            if (pulsePrefab == null || pulsePrefab.GetComponent<ShockwaveRing>() == null)
            {
                throw new InvalidOperationException(
                    $"공용 파장 프리팹을 찾지 못했거나 설정이 올바르지 않습니다: " +
                    CombatVfxAssetBuilder.ShockwaveRingPrefabPath);
            }

            ShockwaveRingVisualEffect pulseVisual = BuildHealerPulseVisual();
            var serializedEffect = new SerializedObject(effect);
            serializedEffect.FindProperty("healingPulsePrefab").objectReferenceValue = pulsePrefab;
            serializedEffect.FindProperty("healingPulseVisual").objectReferenceValue = pulseVisual;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);

            TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(HealerRecipePath);
            if (recipeAsset == null)
            {
                throw new InvalidOperationException($"힐러 적 레시피를 찾지 못했습니다: {HealerRecipePath}");
            }

            EnemyAssetRecipe recipe = JsonUtility.FromJson<EnemyAssetRecipe>(recipeAsset.text);
            if (recipe == null || !string.Equals(recipe.enemyId, "enemy-heal", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("힐러 적 레시피의 enemyId가 올바르지 않습니다.");
            }

            recipe.effectPaths ??= new List<string>();
            recipe.effectPaths.RemoveAll(path =>
                string.Equals(path, ContactDamageEffectPath, StringComparison.Ordinal) ||
                string.Equals(path, HealerEffectPath, StringComparison.Ordinal));
            recipe.effectPaths.Insert(0, HealerEffectPath);

            File.WriteAllText(
                Path.GetFullPath(HealerRecipePath),
                JsonUtility.ToJson(recipe, true),
                new UTF8Encoding(false));

            AssetDatabase.ImportAsset(HealerRecipePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnemyAssetBuilder.BuildSingle(HealerRecipePath);

            Debug.Log("[EnemySpecialEffectAssetBuilder] 힐러 적 Effect 생성·레시피 배선 완료");
        }

        public static void BuildAndVerifyHealer()
        {
            BuildHealer();
            EnemyHealerVerifier.Verify();
        }

        private static ShockwaveRingVisualEffect BuildHealerPulseVisual()
        {
            ShockwaveRingVisualEffect visual =
                AssetDatabase.LoadAssetAtPath<ShockwaveRingVisualEffect>(HealerPulseVisualPath);
            if (visual != null)
            {
                // 사람이 인스펙터에서 조정한 색·두께·속도를 빌더 재실행이 덮어쓰지 않는다.
                return visual;
            }

            if (AssetDatabase.LoadMainAssetAtPath(HealerPulseVisualPath) != null)
            {
                throw new InvalidOperationException(
                    $"힐러 파장 시각 설정 경로에 다른 타입의 에셋이 있습니다: {HealerPulseVisualPath}");
            }

            visual = ScriptableObject.CreateInstance<ShockwaveRingVisualEffect>();
            var serializedVisual = new SerializedObject(visual);
            serializedVisual.FindProperty("color").colorValue = new Color(0.25f, 1.25f, 0.55f, 0.85f);
            serializedVisual.FindProperty("expandDuration").floatValue = 0.45f;
            serializedVisual.FindProperty("strokeWidth").floatValue = 0.012f;
            serializedVisual.FindProperty("glowIntensity").floatValue = 0.8f;
            serializedVisual.FindProperty("glossIntensity").floatValue = 0.25f;
            serializedVisual.FindProperty("opacity").floatValue = 0.8f;
            serializedVisual.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(visual, HealerPulseVisualPath);
            return visual;
        }

        private static void EnsureEffectOrder(List<string> effectPaths)
        {
            effectPaths.RemoveAll(path =>
                string.Equals(path, ContactDamageEffectPath, StringComparison.Ordinal) ||
                string.Equals(path, SelfDestructEffectPath, StringComparison.Ordinal));

            // 피해를 먼저 적용하고 자폭시키면 PlayerController의 피격 이벤트도 정상적으로
            // 발생하며, 두 Effect가 같은 damage 값을 중복 적용하지 않는다.
            effectPaths.Insert(0, SelfDestructEffectPath);
            effectPaths.Insert(0, ContactDamageEffectPath);
        }
    }
}
