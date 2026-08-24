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
    /// 기본값 주입은 에셋을 "이번 실행에서 새로 생성했을 때"만 한다 — 기획자가 인스펙터에서
    /// 배율·색상 등을 밸런싱해 둔 뒤 이 메뉴가 다시 실행되면(다른 유닛 온보딩 등) 그 값을
    /// 말없이 하드코딩 기본값으로 되돌려버리는 사고를 막기 위해서다. 반면 Material 참조 같은
    /// "배선"은 밸런싱 값이 아니므로 매번 재확인해 끊어지지 않게 한다.
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
            TacticalRelayAuraEffect effect = LoadOrCreateEffect(out bool effectCreated);
            if (effectCreated)
            {
                ConfigureEffect(effect);
            }

            EnsureFolder(VisualFolder);
            Material material = LoadOrCreateMaterial();
            RangePulseVisualEffect visualEffect = LoadOrCreateVisualEffect(out bool visualEffectCreated);
            ConnectMaterial(visualEffect, material);
            if (visualEffectCreated)
            {
                ConfigureVisualEffectDefaults(visualEffect);
            }

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

        private static TacticalRelayAuraEffect LoadOrCreateEffect(out bool created)
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

                created = false;
                return effect;
            }

            var instance = ScriptableObject.CreateInstance<TacticalRelayAuraEffect>();
            AssetDatabase.CreateAsset(instance, EffectPath);
            AssetDatabase.SetLabels(instance, new[] { GeneratedLabel });
            created = true;
            return instance;
        }

        /// <summary>
        /// 배율 기본값. 에셋을 이번 실행에서 새로 만들었을 때만 호출한다(위 클래스 주석 참고).
        /// </summary>
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

        private static RangePulseVisualEffect LoadOrCreateVisualEffect(out bool created)
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
                created = false;
                return visualEffect;
            }

            var instance = ScriptableObject.CreateInstance<RangePulseVisualEffect>();
            AssetDatabase.CreateAsset(instance, VisualEffectPath);
            AssetDatabase.SetLabels(instance, new[] { VisualGeneratedLabel });
            created = true;
            return instance;
        }

        /// <summary>
        /// Material 참조는 밸런싱 값이 아니라 배선이므로, 기존 에셋이어도 매번 재확인해
        /// 끊어져 있으면 다시 연결한다. 이미 올바르게 연결돼 있으면 손대지 않는다(불필요한
        /// dirty 마킹 방지).
        /// </summary>
        private static void ConnectMaterial(RangePulseVisualEffect visualEffect, Material material)
        {
            var serializedEffect = new SerializedObject(visualEffect);
            SerializedProperty materialProperty = serializedEffect.FindProperty("material");
            if (materialProperty.objectReferenceValue == material)
            {
                return;
            }

            materialProperty.objectReferenceValue = material;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visualEffect);
        }

        /// <summary>
        /// 색상·주기 등 표현 밸런싱 값의 초기 기본값. 에셋을 이번 실행에서 새로 만들었을 때만
        /// 호출한다 — 이미 있는 에셋에 매번 다시 쓰면 기획자가 인스펙터에서 바꿔 둔 값을 이
        /// 메뉴를 재실행할 때마다 되돌리게 된다.
        /// </summary>
        private static void ConfigureVisualEffectDefaults(RangePulseVisualEffect visualEffect)
        {
            var serializedEffect = new SerializedObject(visualEffect);
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
