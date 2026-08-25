using System;
using System.Collections.Generic;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Unit;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
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

            ValidateGroupUpdatePolicy(settings);
            ValidateLiveCatalogs(settings);

            Debug.Log($"[AddressablesBuildValidator] {target} 사전 검증 통과");
        }

        /// <summary>
        /// 로컬 그룹이 static인지, 원격 그룹이 static이 아닌지 검사한다.
        ///
        /// 이 프로젝트는 원격 카탈로그를 켜 두고 시작 시 카탈로그 갱신도 막지 않아서, 이미
        /// 배포된 플레이어가 부팅할 때마다 원격 카탈로그로 갈아탄다. 그 카탈로그에는 로컬 그룹
        /// 엔트리까지 들어 있고 로드 경로가 플레이어 자신의 StreamingAssets를 가리키므로, 로컬
        /// 그룹이 static이 아니면 콘텐츠 업데이트가 로컬 번들을 새 해시로 다시 굽고 새 카탈로그가
        /// 구 플레이어에 없는 파일명을 가리키게 된다 — 잘 돌던 기존 콘텐츠까지 통째로 깨진다.
        /// 플레이어를 굽기 전에 세우는 편이 배포 후에 겪는 것보다 압도적으로 싸다.
        /// </summary>
        private static void ValidateGroupUpdatePolicy(AddressableAssetSettings settings)
        {
            var wrong = new List<string>();
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null || group.ReadOnly)
                {
                    continue;
                }

                ContentUpdateGroupSchema updateSchema = group.GetSchema<ContentUpdateGroupSchema>();
                if (updateSchema == null)
                {
                    wrong.Add($"{group.Name}: ContentUpdateGroupSchema 없음");
                    continue;
                }

                bool remote = AddressableGroupPolicy.IsRemoteGroup(settings, group);
                if (updateSchema.StaticContent == !remote)
                {
                    continue;
                }

                wrong.Add(remote
                    ? $"{group.Name}: 원격 그룹인데 Prevent Updates가 켜져 있음"
                    : $"{group.Name}: 로컬 그룹인데 Prevent Updates가 꺼져 있음");
            }

            if (wrong.Count > 0)
            {
                throw new InvalidOperationException(
                    "Addressables 그룹 업데이트 정책이 어긋났습니다:\n  " + string.Join("\n  ", wrong) +
                    "\nRCCom/Addressables/Apply Group Update Policy를 실행하세요.");
            }
        }

        /// <summary>
        /// 원격 콘텐츠가 있는데 라이브 카탈로그가 없으면, 그 콘텐츠는 배포해도 이미 나간 빌드의
        /// 화면에 끝내 나타나지 않는다. 빌드는 성공하고 CDN 업로드도 성공하는데 결과만 없는
        /// 상황이라 원인을 찾기가 특히 어려우므로 여기서 잡는다.
        /// </summary>
        private static void ValidateLiveCatalogs(AddressableAssetSettings settings)
        {
            OperatorCatalog liveOperators = AssetDatabase.LoadAssetAtPath<OperatorCatalog>(
                OperatorLiveCatalogBuilder.LiveCatalogPath);
            if (liveOperators == null)
            {
                throw new InvalidOperationException(
                    $"라이브 오퍼레이터 카탈로그가 없습니다: {OperatorLiveCatalogBuilder.LiveCatalogPath}\n" +
                    "RCCom/Operators/Build Operator Catalog And Addressables를 실행하세요.");
            }

            AddressableAssetEntry entry = settings.FindAssetEntry(
                AssetDatabase.AssetPathToGUID(OperatorLiveCatalogBuilder.LiveCatalogPath));
            if (entry == null || entry.address != OperatorCatalog.LiveCatalogAddress)
            {
                throw new InvalidOperationException(
                    $"라이브 오퍼레이터 카탈로그가 {OperatorCatalog.LiveCatalogAddress} 주소로 등록되지 않았습니다.");
            }

            if (entry.parentGroup == null || !AddressableGroupPolicy.IsRemoteGroup(settings, entry.parentGroup))
            {
                throw new InvalidOperationException(
                    "라이브 오퍼레이터 카탈로그가 원격 그룹에 있지 않습니다. 로컬 그룹에 있으면 " +
                    "이미 배포된 빌드는 이 카탈로그를 영영 받지 못합니다.");
            }
        }
    }
}
