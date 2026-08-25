using System;
using System.Collections.Generic;
using RCCom.Definitions.Operator;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// "빌드 이후에 추가된 오퍼레이터"만 담아 원격으로 배송되는 경량 카탈로그를 만든다.
    ///
    /// 정본 OperatorCatalog는 씬 UI가 직접 참조해 플레이어 빌드에 통째로 박히므로, 거기에
    /// 신규 항목을 넣어도 이미 배포된 빌드에는 영영 닿지 않는다. 그래서 원격 항목만 추린
    /// 사본을 따로 만들어 원격 그룹에 올리고, 런타임의 LiveCatalogService가 내장 카탈로그
    /// 위에 얹는다.
    ///
    /// 원격 항목만 담는 것이 핵심이다. 정본을 통째로 올리면 로컬 오퍼레이터가 직접 참조하는
    /// 스프라이트가 전부 이 원격 번들의 의존으로 딸려 들어가 번들이 비대해지고, static으로
    /// 묶어둔 로컬 그룹과 교차 의존이 생긴다. 원격 항목은 CreateEntry가 이미 모든 Sprite
    /// 참조를 null로 비우고 주소 문자열만 남기므로(원격 콘텐츠가 본체 빌드로 새어 나가지
    /// 않게 하려는 기존 규칙) 이 카탈로그는 어떤 로컬 에셋에도 의존하지 않는다.
    /// </summary>
    public static class OperatorLiveCatalogBuilder
    {
        public const string LiveCatalogPath = "Assets/Data/Operators/OperatorLiveCatalog.asset";
        public const string LiveGroupName = "Catalog-Live-Remote";
        public const string AddressablesLabel = "live-catalog";

        private const string GeneratedLabel = "RCCom.GeneratedOperator";

        /// <summary>
        /// 원격 항목 목록으로 라이브 카탈로그를 갱신하고 원격 그룹에 배선한다.
        /// entries에는 정본 카탈로그와 별개의 인스턴스를 넘겨야 한다 — 같은 객체를 두 에셋이
        /// 공유하면 한쪽을 고칠 때 다른 쪽이 조용히 따라 바뀐다.
        /// </summary>
        public static void Build(List<OperatorCatalogEntry> remoteEntries, List<string> changedAssets)
        {
            if (remoteEntries == null)
            {
                throw new ArgumentNullException(nameof(remoteEntries));
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings를 만들지 못했습니다.");
            }

            VerifyNoLocalAssetReferences(remoteEntries);

            OperatorCatalog catalog = GetOrCreateCatalog();
            string before = EditorJsonUtility.ToJson(catalog);
            catalog.entries = remoteEntries;
            if (EditorJsonUtility.ToJson(catalog) != before)
            {
                EditorUtility.SetDirty(catalog);
                OperatorAssetBuilder.RecordChange(LiveCatalogPath, changedAssets);
            }

            settings.AddLabel(AddressablesLabel, false);

            bool changed = false;
            AddressableAssetGroup group = AddressableGroupPolicy.EnsureGroup(
                settings,
                LiveGroupName,
                true,
                BundledAssetGroupSchema.BundlePackingMode.PackTogether,
                ref changed);

            if (AddressableGroupPolicy.AssignEntry(
                    settings, group, LiveCatalogPath, OperatorCatalog.LiveCatalogAddress, AddressablesLabel))
            {
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(settings);
                OperatorAssetBuilder.RecordChange(AssetDatabase.GetAssetPath(settings), changedAssets);
            }

            Debug.Log($"[OperatorLiveCatalogBuilder] 라이브 카탈로그 {remoteEntries.Count}명 갱신 완료");
        }

        /// <summary>
        /// 라이브 카탈로그가 로컬 에셋을 참조하면 원격 번들이 그 에셋을 끌어안는다. static으로
        /// 묶은 로컬 그룹과 교차 의존이 생겨 콘텐츠 업데이트가 깨지므로, 조용히 넘어가지 말고
        /// 빌드를 세운다. 정상 경로에서는 CreateEntry가 이미 전부 null로 비워 두므로 이 검사는
        /// 앞으로 누군가 CreateEntry를 고쳤을 때를 위한 안전망이다.
        /// </summary>
        private static void VerifyNoLocalAssetReferences(List<OperatorCatalogEntry> entries)
        {
            foreach (OperatorCatalogEntry entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (entry.previewPortrait != null || entry.managementPortrait != null ||
                    entry.shopPortrait != null || entry.shopUpperBodyPortrait != null ||
                    entry.shopUpperBodyPortraitDimmed != null || entry.unlockRewardPortrait != null)
                {
                    throw new InvalidOperationException(
                        $"라이브 카탈로그 항목이 로컬 스프라이트를 직접 참조합니다: {entry.operatorId}. " +
                        "원격 항목은 주소 문자열만 들고 있어야 합니다.");
                }

                if (entry.unitPreviews == null)
                {
                    continue;
                }

                foreach (var preview in entry.unitPreviews)
                {
                    if (preview != null && preview.previewIcon != null)
                    {
                        throw new InvalidOperationException(
                            $"라이브 카탈로그의 유닛 미리보기가 로컬 스프라이트를 직접 참조합니다: " +
                            $"{entry.operatorId}/{preview.unitId}");
                    }
                }
            }
        }

        private static OperatorCatalog GetOrCreateCatalog()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(LiveCatalogPath);
            if (existing != null)
            {
                if (existing is not OperatorCatalog catalog)
                {
                    throw new InvalidOperationException($"라이브 카탈로그 경로에 다른 타입의 에셋이 있습니다: {LiveCatalogPath}");
                }

                if (Array.IndexOf(AssetDatabase.GetLabels(catalog), GeneratedLabel) < 0)
                {
                    throw new InvalidOperationException($"자동 생성 라벨 없는 카탈로그는 덮어쓸 수 없습니다: {LiveCatalogPath}");
                }

                return catalog;
            }

            var created = ScriptableObject.CreateInstance<OperatorCatalog>();
            AssetDatabase.CreateAsset(created, LiveCatalogPath);
            AssetDatabase.SetLabels(created, new[] { GeneratedLabel });
            return created;
        }
    }
}
