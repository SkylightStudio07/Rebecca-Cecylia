using System;
using RCCom.Definitions.Unit;
using RCCom.Effects.Unit.Concrete;
using RCCom.Effects.UnitVisual.Concrete;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 전술 중계 오라 공용 효과 에셋을 재현 가능한 기본값으로 만들고 Calliste 드론 레시피를
    /// 다시 빌드한다. 수작업으로 SO 필드와 Definition 참조가 엇갈리는 일을 막기 위한 도구다.
    /// </summary>
    public static class TacticalRelayAuraEffectBuilder
    {
        private const string GeneratedLabel = "RCCom.GeneratedAllyUnitEffect";
        private const string EffectFolder = "Assets/Data/Effects/Unit";
        private const string EffectPath = EffectFolder + "/TacticalRelayAuraEffect.asset";
        private const string VisualGeneratedLabel = "RCCom.GeneratedAllyUnitVisualEffect";
        private const string VisualFolder = EffectFolder + "/Visual";
        private const string ShaderPath = "Assets/Shaders/Unit/RangePulseAura.shader";
        private const string MaterialPath = VisualFolder + "/RangePulseAura.mat";
        private const string VisualEffectPath =
            VisualFolder + "/CallisteBuffRangePulseVisualEffect.asset";
        private const string DroneRecipePath =
            "Assets/Editor/AllyUnitRecipes/calliste-drone.json";
        private const string DroneDefinitionPath =
            "Assets/Data/AllyUnits/calliste-drone/AllyUnitDefinition.asset";

        [MenuItem("RCCom/Ally Units/Build Tactical Relay Aura Effect")]
        public static void BuildAndConnect()
        {
            EnsureFolder(EffectFolder);
            TacticalRelayAuraEffect effect = LoadOrCreateEffect();
            ConfigureEffect(effect);

            EnsureFolder(VisualFolder);
            Material material = LoadOrCreateMaterial();
            RangePulseVisualEffect visualEffect = LoadOrCreateVisualEffect();
            ConfigureVisualEffect(visualEffect, material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AllyUnitBuildReport report = AllyUnitAssetBuilder.BuildSingle(DroneRecipePath);
            AllyUnitDefinition drone =
                AssetDatabase.LoadAssetAtPath<AllyUnitDefinition>(DroneDefinitionPath);
            if (!report.validationPassed || drone == null || drone.effects == null ||
                drone.effects.Count != 1 || drone.effects[0] != effect ||
                drone.visualEffects == null || drone.visualEffects.Count != 1 ||
                drone.visualEffects[0] != visualEffect)
            {
                throw new InvalidOperationException(
                    "Calliste 서포트 드론에 전술 중계 오라와 범위 비주얼이 정확히 연결되지 않았습니다.");
            }

            Debug.Log(
                "[TacticalRelayAuraEffectBuilder] 전술 중계 오라·범위 비주얼 생성 및 Calliste 드론 연결 완료");
        }

        private static TacticalRelayAuraEffect LoadOrCreateEffect()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(EffectPath);
            if (existing != null)
            {
                if (existing is not TacticalRelayAuraEffect effect)
                {
                    throw new InvalidOperationException(
                        $"전술 중계 오라 경로에 다른 타입의 에셋이 있습니다: {EffectPath}");
                }

                if (Array.IndexOf(AssetDatabase.GetLabels(effect), GeneratedLabel) < 0)
                {
                    throw new InvalidOperationException(
                        $"자동 생성 라벨이 없는 기존 에셋은 덮어쓸 수 없습니다: {EffectPath}");
                }

                return effect;
            }

            var created = ScriptableObject.CreateInstance<TacticalRelayAuraEffect>();
            AssetDatabase.CreateAsset(created, EffectPath);
            AssetDatabase.SetLabels(created, new[] { GeneratedLabel });
            return created;
        }

        private static void ConfigureEffect(TacticalRelayAuraEffect effect)
        {
            var serializedEffect = new SerializedObject(effect);
            serializedEffect.FindProperty("moveSpeedMultiplier").floatValue = 1.2f;
            serializedEffect.FindProperty("attackSpeedMultiplier").floatValue = 1.2f;
            serializedEffect.FindProperty("refreshDuration").floatValue = 0.2f;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
        }

        private static Material LoadOrCreateMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported)
            {
                throw new InvalidOperationException(
                    $"범위 파동 셰이더를 찾을 수 없거나 현재 환경에서 지원하지 않습니다: {ShaderPath}");
            }

            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(MaterialPath);
            Material material;
            if (existing != null)
            {
                if (existing is not Material existingMaterial)
                {
                    throw new InvalidOperationException(
                        $"범위 파동 Material 경로에 다른 타입의 에셋이 있습니다: {MaterialPath}");
                }

                EnsureOwned(existingMaterial, VisualGeneratedLabel, MaterialPath);
                material = existingMaterial;
            }
            else
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SetLabels(material, new[] { VisualGeneratedLabel });
            }

            if (material.shader != shader)
            {
                material.shader = shader;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static RangePulseVisualEffect LoadOrCreateVisualEffect()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(VisualEffectPath);
            if (existing != null)
            {
                if (existing is not RangePulseVisualEffect visualEffect)
                {
                    throw new InvalidOperationException(
                        $"범위 파동 비주얼 경로에 다른 타입의 에셋이 있습니다: {VisualEffectPath}");
                }

                EnsureOwned(visualEffect, VisualGeneratedLabel, VisualEffectPath);
                return visualEffect;
            }

            var created = ScriptableObject.CreateInstance<RangePulseVisualEffect>();
            AssetDatabase.CreateAsset(created, VisualEffectPath);
            AssetDatabase.SetLabels(created, new[] { VisualGeneratedLabel });
            return created;
        }

        private static void ConfigureVisualEffect(
            RangePulseVisualEffect visualEffect,
            Material material)
        {
            var serializedEffect = new SerializedObject(visualEffect);
            serializedEffect.FindProperty("material").objectReferenceValue = material;
            serializedEffect.FindProperty("auraColor").colorValue =
                new Color(0.12f, 0.82f, 1.35f, 0.9f);
            serializedEffect.FindProperty("pulseDuration").floatValue = 0.85f;
            serializedEffect.FindProperty("pulseInterval").floatValue = 0.3f;
            serializedEffect.FindProperty("strokeWidth").floatValue = 0.012f;
            serializedEffect.FindProperty("glowIntensity").floatValue = 0.8f;
            serializedEffect.FindProperty("glossIntensity").floatValue = 0.35f;
            serializedEffect.FindProperty("opacity").floatValue = 0.8f;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visualEffect);
        }

        private static void EnsureOwned(UnityEngine.Object asset, string label, string path)
        {
            if (Array.IndexOf(AssetDatabase.GetLabels(asset), label) < 0)
            {
                throw new InvalidOperationException(
                    $"자동 생성 라벨이 없는 기존 에셋은 덮어쓸 수 없습니다: {path}");
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
