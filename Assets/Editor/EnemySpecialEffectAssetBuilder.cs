using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RCCom.Data;
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
        public const string DefenderEffectPath =
            "Assets/Data/Effects/Enemy_MaxHealthAuraEffect.asset";
        public const string DefenderRangeMaterialPath =
            "Assets/Data/Effects/Enemy_DefenderRangeAura.mat";
        public const string HeavyTankerShieldEffectPath =
            "Assets/Data/Effects/Enemy_HeavyTankerFrontalShieldEffect.asset";
        public const string HeavyTankerShieldMaterialPath =
            "Assets/Data/Effects/Enemy_HeavyTankerFrontalShield.mat";

        private const string ContactDamageEffectPath =
            "Assets/Data/Effects/Enemy_ContactDamageEffect_Default.asset";
        private const string ExploderRecipePath =
            "Assets/Editor/EnemyRecipes/enemy-explode.json";
        private const string HealerRecipePath =
            "Assets/Editor/EnemyRecipes/enemy-heal.json";
        private const string DefenderRecipePath =
            "Assets/Editor/EnemyRecipes/enemy-defend.json";
        private const string DefenderSpritePath =
            "Assets/Art/Enemies/defend.png";
        private const string HeavyTankerRecipePath =
            "Assets/Editor/EnemyRecipes/enemy-heavytanker.json";

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

        [MenuItem("RCCom/Enemies/Build Defender Special Effect")]
        public static void BuildDefender()
        {
            ConfigureDefenderSpriteImporter();

            MaxHealthAuraEffect effect =
                AssetDatabase.LoadAssetAtPath<MaxHealthAuraEffect>(DefenderEffectPath);
            bool createdEffect = effect == null;
            if (createdEffect)
            {
                if (AssetDatabase.LoadMainAssetAtPath(DefenderEffectPath) != null)
                {
                    throw new InvalidOperationException(
                        $"방어 오라 Effect 경로에 다른 타입의 에셋이 있습니다: {DefenderEffectPath}");
                }

                effect = ScriptableObject.CreateInstance<MaxHealthAuraEffect>();
                AssetDatabase.CreateAsset(effect, DefenderEffectPath);
            }

            Material rangeMaterial = BuildDefenderRangeMaterial();
            var serializedEffect = new SerializedObject(effect);
            SerializedProperty materialProperty = serializedEffect.FindProperty("material");
            if (createdEffect || materialProperty.objectReferenceValue == null)
            {
                materialProperty.objectReferenceValue = rangeMaterial;
                serializedEffect.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(effect);
            }

            TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefenderRecipePath);
            if (recipeAsset == null)
            {
                throw new InvalidOperationException($"방어 적 레시피를 찾지 못했습니다: {DefenderRecipePath}");
            }

            EnemyAssetRecipe recipe = JsonUtility.FromJson<EnemyAssetRecipe>(recipeAsset.text);
            if (recipe == null || !string.Equals(recipe.enemyId, "enemy-defend", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("방어 적 레시피의 enemyId가 올바르지 않습니다.");
            }

            recipe.effectPaths ??= new List<string>();
            recipe.effectPaths.RemoveAll(path =>
                string.Equals(path, ContactDamageEffectPath, StringComparison.Ordinal) ||
                string.Equals(path, DefenderEffectPath, StringComparison.Ordinal));
            recipe.effectPaths.Insert(0, DefenderEffectPath);
            recipe.data.kind = EnemyKind.Defend;
            recipe.data.attackRange = 4f;
            recipe.data.contactDamage = 0f;

            File.WriteAllText(
                Path.GetFullPath(DefenderRecipePath),
                JsonUtility.ToJson(recipe, true),
                new UTF8Encoding(false));

            AssetDatabase.ImportAsset(DefenderRecipePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnemyAssetBuilder.BuildSingle(DefenderRecipePath);

            Debug.Log("[EnemySpecialEffectAssetBuilder] 방어 적 최대 체력 오라·초록 범위 배선 완료");
        }

        public static void BuildAndVerifyDefender()
        {
            BuildDefender();
            EnemyDefenderVerifier.Verify();
        }

        /// <summary>배치형 실행에서 Definition 생성 뒤 1-2 편성까지 참조 순서대로 한 번에 적용한다.</summary>
        public static void BuildDefenderStageAndVerify()
        {
            BuildDefender();
            StageEnemyCompositionSetup.ApplySpecialEnemyEncounters();
            EnemyDefenderVerifier.Verify();
        }

        private static Material BuildDefenderRangeMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(DefenderRangeMaterialPath);
            if (material != null)
            {
                return material;
            }

            if (AssetDatabase.LoadMainAssetAtPath(DefenderRangeMaterialPath) != null)
            {
                throw new InvalidOperationException(
                    $"방어 오라 머티리얼 경로에 다른 타입의 에셋이 있습니다: {DefenderRangeMaterialPath}");
            }

            Shader shader = Shader.Find("RCCom/Enemy Visuals/Persistent Range Aura");
            if (shader == null)
            {
                throw new InvalidOperationException("방어 오라 셰이더를 찾지 못했습니다.");
            }

            material = new Material(shader) { name = "Enemy_DefenderRangeAura" };
            AssetDatabase.CreateAsset(material, DefenderRangeMaterialPath);
            return material;
        }

        [MenuItem("RCCom/Enemies/Build Heavy Tanker Frontal Shield")]
        public static void BuildHeavyTankerShield()
        {
            FrontalShieldEffect effect =
                AssetDatabase.LoadAssetAtPath<FrontalShieldEffect>(HeavyTankerShieldEffectPath);
            bool createdEffect = effect == null;
            if (createdEffect)
            {
                if (AssetDatabase.LoadMainAssetAtPath(HeavyTankerShieldEffectPath) != null)
                {
                    throw new InvalidOperationException(
                        $"헤비탱커 방어막 Effect 경로에 다른 타입의 에셋이 있습니다: {HeavyTankerShieldEffectPath}");
                }

                effect = ScriptableObject.CreateInstance<FrontalShieldEffect>();
                AssetDatabase.CreateAsset(effect, HeavyTankerShieldEffectPath);
            }

            Material shieldMaterial = BuildHeavyTankerShieldMaterial();
            var serializedEffect = new SerializedObject(effect);
            SerializedProperty materialProperty = serializedEffect.FindProperty("material");
            if (createdEffect || materialProperty.objectReferenceValue == null)
            {
                materialProperty.objectReferenceValue = shieldMaterial;
                serializedEffect.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(effect);
            }

            TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(HeavyTankerRecipePath);
            if (recipeAsset == null)
            {
                throw new InvalidOperationException($"헤비탱커 레시피를 찾지 못했습니다: {HeavyTankerRecipePath}");
            }

            EnemyAssetRecipe recipe = JsonUtility.FromJson<EnemyAssetRecipe>(recipeAsset.text);
            if (recipe == null || !string.Equals(recipe.enemyId, "enemy-heavytanker", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("헤비탱커 레시피의 enemyId가 올바르지 않습니다.");
            }

            recipe.effectPaths ??= new List<string>();
            recipe.effectPaths.RemoveAll(path =>
                string.Equals(path, HeavyTankerShieldEffectPath, StringComparison.Ordinal));
            if (!recipe.effectPaths.Exists(path =>
                    string.Equals(path, ContactDamageEffectPath, StringComparison.Ordinal)))
            {
                recipe.effectPaths.Insert(0, ContactDamageEffectPath);
            }
            recipe.effectPaths.Add(HeavyTankerShieldEffectPath);

            File.WriteAllText(
                Path.GetFullPath(HeavyTankerRecipePath),
                JsonUtility.ToJson(recipe, true),
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(HeavyTankerRecipePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnemyAssetBuilder.BuildSingle(HeavyTankerRecipePath);

            Debug.Log("[EnemySpecialEffectAssetBuilder] 헤비탱커 정면 50% 피해 감소·파란 반원 방어막 배선 완료");
        }

        public static void BuildAndVerifyHeavyTankerShield()
        {
            BuildHeavyTankerShield();
            EnemyHeavyTankerShieldVerifier.VerifyRegressionSuite();
        }

        private static Material BuildHeavyTankerShieldMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(HeavyTankerShieldMaterialPath);
            if (material != null)
            {
                return material;
            }

            if (AssetDatabase.LoadMainAssetAtPath(HeavyTankerShieldMaterialPath) != null)
            {
                throw new InvalidOperationException(
                    $"헤비탱커 방어막 머티리얼 경로에 다른 타입의 에셋이 있습니다: {HeavyTankerShieldMaterialPath}");
            }

            Shader shader = Shader.Find("RCCom/Enemy Visuals/Frontal Shield");
            if (shader == null)
            {
                throw new InvalidOperationException("헤비탱커 정면 방어막 셰이더를 찾지 못했습니다.");
            }

            material = new Material(shader) { name = "Enemy_HeavyTankerFrontalShield" };
            AssetDatabase.CreateAsset(material, HeavyTankerShieldMaterialPath);
            return material;
        }

        private static void ConfigureDefenderSpriteImporter()
        {
            if (AssetImporter.GetAtPath(DefenderSpritePath) is not TextureImporter importer)
            {
                throw new InvalidOperationException(
                    $"Defend 이미지를 TextureImporter로 열 수 없습니다: {DefenderSpritePath}");
            }

            const float targetPixelsPerUnit = 160f;
            if (Mathf.Approximately(importer.spritePixelsPerUnit, targetPixelsPerUnit))
            {
                return;
            }

            // 445px 원본을 기존 100 PPU로 읽으면 다른 특수 적보다 지나치게 크다. 160 PPU에서는
            // 투명 여백을 제외한 Tight Sprite의 실제 폭이 약 2.16이 되어 공용 View에서도 같은 체급으로 보인다.
            importer.spritePixelsPerUnit = targetPixelsPerUnit;
            importer.SaveAndReimport();
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
