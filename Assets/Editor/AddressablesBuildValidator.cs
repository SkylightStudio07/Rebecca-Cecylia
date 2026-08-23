using System;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Unit;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 플레이어 빌드를 시작하기 전에 오퍼레이터 패키지와 활성 원격 프로필을 함께 검사한다.
    /// 실제 콘텐츠 빌드는 하지 않으므로 사람이 최종 빌드 전에 반복 실행해도 안전하다.
    /// </summary>
    public static class AddressablesBuildValidator
    {
        [MenuItem("RCCom/Addressables/Validate Active Build Configuration")]
        public static void ValidateMenu()
        {
            ValidateOrThrow(EditorUserBuildSettings.activeBuildTarget);
        }

        public static void ValidateOrThrow(BuildTarget target)
        {
            if (!OperatorAssetValidator.ValidateAll(false))
            {
                throw new InvalidOperationException("오퍼레이터/Addressables 에셋 검증에 실패했습니다.");
            }

            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(targetGroup, target))
            {
                throw new InvalidOperationException($"설치되지 않은 빌드 타깃입니다: {target}");
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || string.IsNullOrEmpty(settings.activeProfileId))
            {
                throw new InvalidOperationException("활성 Addressables 프로필이 없습니다.");
            }

            OperatorCatalog operatorCatalog = AssetDatabase.LoadAssetAtPath<OperatorCatalog>(OperatorCatalogBuilder.CatalogPath);
            bool hasRemoteContent = operatorCatalog != null && operatorCatalog.entries != null &&
                                    operatorCatalog.entries.Exists(entry => entry != null && entry.remoteContent);

            // EnemyCatalog는 이 시점엔 아직 존재하지 않을 수 있다(마이그레이션 전 단계) —
            // 없거나 비어 있는 것을 오류로 취급하면 이 커밋만으로 기존 빌드가 막힌다.
            EnemyCatalog enemyCatalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(EnemyCatalogBuilder.CatalogPath);
            hasRemoteContent |= enemyCatalog != null && enemyCatalog.entries != null &&
                                enemyCatalog.entries.Exists(entry => entry != null && entry.remoteContent);

            AllyUnitCatalog allyUnitCatalog = AssetDatabase.LoadAssetAtPath<AllyUnitCatalog>(
                AllyUnitCatalogBuilder.CatalogPath);
            hasRemoteContent |= allyUnitCatalog != null && allyUnitCatalog.entries != null &&
                                allyUnitCatalog.entries.Exists(entry => entry != null && entry.remoteContent);

            if (hasRemoteContent)
            {
                string remoteLoadPath = settings.profileSettings.GetValueByName(
                    settings.activeProfileId,
                    AddressableAssetSettings.kRemoteLoadPath);
                string normalized = AddressablesRemoteProfileConfigurator.NormalizeRemoteLoadPath(remoteLoadPath);
                if (target == BuildTarget.WebGL &&
                    (normalized.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) ||
                     normalized.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("WebGL 최종 빌드에는 localhost 원격 주소를 사용할 수 없습니다.");
                }
            }

            Debug.Log($"[AddressablesBuildValidator] {target} 사전 검증 통과");
        }
    }
}
