using System;
using RCCom.Definitions.Unit;
using RCCom.Effects.Unit.Concrete;
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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AllyUnitBuildReport report = AllyUnitAssetBuilder.BuildSingle(DroneRecipePath);
            AllyUnitDefinition drone =
                AssetDatabase.LoadAssetAtPath<AllyUnitDefinition>(DroneDefinitionPath);
            if (!report.validationPassed || drone == null || drone.effects == null ||
                drone.effects.Count != 1 || drone.effects[0] != effect)
            {
                throw new InvalidOperationException(
                    "Calliste 서포트 드론에 전술 중계 오라 효과가 정확히 연결되지 않았습니다.");
            }

            Debug.Log("[TacticalRelayAuraEffectBuilder] 전술 중계 오라 생성 및 Calliste 드론 연결 완료");
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
