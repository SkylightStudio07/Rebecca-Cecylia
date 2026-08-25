using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 콘텐츠 그룹 빌더 세 개(Operator/Enemy/AllyUnit)가 공유하는 Addressables 그룹 스키마 정책.
    ///
    /// 원래 같은 블록이 세 빌더에 그대로 복사되어 있었다. 그 상태로 정책을 하나 바꾸면 세 곳을
    /// 모두 고쳐야 하고, 한 곳을 빠뜨리면 "적 그룹만 다르게 묶이는" 식으로 조용히 어긋난다.
    /// 그룹 스키마는 배포 안전성과 직결되므로(아래 StaticContent 참고) 정본을 하나만 둔다.
    /// </summary>
    public static class AddressableGroupPolicy
    {
        /// <summary>
        /// 그룹을 찾거나 만들고 스키마를 기대 상태로 맞춘다. 실제로 바꾼 것이 있으면 changed를 올린다.
        /// 이미 같은 값이면 다시 쓰지 않아 Addressables 에셋이 불필요하게 갱신되지 않는다.
        /// </summary>
        public static AddressableAssetGroup EnsureGroup(
            AddressableAssetSettings settings,
            string groupName,
            bool remoteContent,
            BundledAssetGroupSchema.BundlePackingMode bundleMode,
            ref bool changed)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(
                    groupName,
                    false,
                    false,
                    true,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
                changed = true;
            }

            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null)
            {
                bundled = group.AddSchema<BundledAssetGroupSchema>();
                changed = true;
            }

            string buildPath = remoteContent
                ? AddressableAssetSettings.kRemoteBuildPath
                : AddressableAssetSettings.kLocalBuildPath;
            string loadPath = remoteContent
                ? AddressableAssetSettings.kRemoteLoadPath
                : AddressableAssetSettings.kLocalLoadPath;

            bool schemaChanged = false;
            if (bundled.BuildPath.GetName(settings) != buildPath)
            {
                bundled.BuildPath.SetVariableByName(settings, buildPath);
                schemaChanged = true;
            }

            if (bundled.LoadPath.GetName(settings) != loadPath)
            {
                bundled.LoadPath.SetVariableByName(settings, loadPath);
                schemaChanged = true;
            }

            if (bundled.BundleMode != bundleMode)
            {
                bundled.BundleMode = bundleMode;
                schemaChanged = true;
            }

            if (!bundled.IncludeInBuild)
            {
                bundled.IncludeInBuild = true;
                schemaChanged = true;
            }

            if (schemaChanged)
            {
                EditorUtility.SetDirty(bundled);
                changed = true;
            }

            if (ApplyUpdatePolicy(group, remoteContent))
            {
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(group);
            }

            return group;
        }

        /// <summary>
        /// 콘텐츠 업데이트 정책을 맞춘다. 실제로 바꿨으면 true.
        ///
        /// 로컬 그룹은 StaticContent(인스펙터의 "Prevent Updates")로 묶는다. 이 프로젝트는
        /// BuildRemoteCatalog가 켜져 있고 DisableCatalogUpdateOnStart가 꺼져 있어서, 이미 배포된
        /// 플레이어가 부팅할 때마다 원격 카탈로그를 받아 자기 카탈로그를 그것으로 갈아탄다.
        /// 그런데 원격 카탈로그에는 원격 그룹뿐 아니라 **로컬 그룹 엔트리까지 전부** 들어 있고,
        /// 그 엔트리의 로드 경로는 플레이어 자신의 StreamingAssets를 가리킨다. 로컬 그룹을
        /// static으로 묶어두지 않으면 콘텐츠 업데이트가 로컬 번들까지 새 해시로 다시 굽고,
        /// 새 카탈로그가 구 플레이어에 존재하지 않는 파일명을 가리키게 되어 이미 잘 돌던
        /// 기존 콘텐츠까지 통째로 깨진다. static으로 묶으면 업데이트 시 로컬 엔트리는 이전
        /// 빌드의 번들 참조를 그대로 유지한다.
        ///
        /// 원격 그룹은 반대로 static이면 안 된다 — 갱신 대상 자체이기 때문이다.
        /// </summary>
        public static bool ApplyUpdatePolicy(AddressableAssetGroup group, bool remoteContent)
        {
            if (group == null)
            {
                return false;
            }

            bool changed = false;
            ContentUpdateGroupSchema updateSchema = group.GetSchema<ContentUpdateGroupSchema>();
            if (updateSchema == null)
            {
                updateSchema = group.AddSchema<ContentUpdateGroupSchema>();
                changed = true;
            }

            bool desiredStatic = !remoteContent;
            if (updateSchema.StaticContent != desiredStatic)
            {
                updateSchema.StaticContent = desiredStatic;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(updateSchema);
                EditorUtility.SetDirty(group);
            }

            return changed;
        }

        /// <summary>
        /// 그룹이 원격인지 판정한다. 그룹 이름(-Remote 접미사)이 아니라 실제 빌드 경로 변수를
        /// 보는 이유는, 빌더가 만들지 않은 손수 만든 그룹도 같은 기준으로 다루기 위해서다.
        /// </summary>
        public static bool IsRemoteGroup(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            BundledAssetGroupSchema bundled = group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
            if (bundled == null)
            {
                return false;
            }

            return string.Equals(
                bundled.BuildPath.GetName(settings),
                AddressableAssetSettings.kRemoteBuildPath,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// 저장소에 이미 있는 모든 그룹에 업데이트 정책을 적용한다. 빌더가 관리하지 않는
        /// 그룹(Default Local Group, UI-Live-Remote 등)까지 한 번에 맞추기 위한 진입점이며,
        /// 빌더를 돌린 뒤에도 언제든 다시 실행할 수 있다(같은 값이면 아무것도 쓰지 않는다).
        /// </summary>
        [MenuItem("RCCom/Addressables/Apply Group Update Policy")]
        public static void ApplyToAllGroups()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables 설정을 찾을 수 없습니다.");
            }

            int changedCount = 0;
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null || group.ReadOnly)
                {
                    continue;
                }

                bool remote = IsRemoteGroup(settings, group);
                if (ApplyUpdatePolicy(group, remote))
                {
                    changedCount++;
                    Debug.Log($"[AddressableGroupPolicy] {group.Name} → StaticContent={!remote}");
                }
            }

            if (changedCount > 0)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[AddressableGroupPolicy] 그룹 {settings.groups.Count}개 검사, {changedCount}개 갱신");
        }
    }
}
