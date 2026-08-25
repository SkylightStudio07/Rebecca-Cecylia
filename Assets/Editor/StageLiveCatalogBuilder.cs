using System;
using System.Collections.Generic;
using RCCom.Definitions.Stage;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// "빌드 이후에 추가된 스테이지"만 담아 원격으로 배송되는 경량 카탈로그를 만든다.
    /// OperatorLiveCatalogBuilder와 같은 구조이며 같은 원격 그룹을 공유한다 —
    /// 두 카탈로그 모두 작고, 라이브 드랍 때 항상 함께 갱신되므로 번들을 나눌 이유가 없다.
    /// </summary>
    public static class StageLiveCatalogBuilder
    {
        public const string LiveCatalogPath = "Assets/Data/Stages/StageLiveCatalog.asset";
        public const string AddressablesLabel = "live-catalog";

        public static void Build(List<StageCatalogEntry> remoteEntries)
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

            StageCatalog catalog = GetOrCreateCatalog();
            string before = EditorJsonUtility.ToJson(catalog);
            catalog.entries = remoteEntries;
            if (EditorJsonUtility.ToJson(catalog) != before)
            {
                EditorUtility.SetDirty(catalog);
            }

            settings.AddLabel(AddressablesLabel, false);

            bool changed = false;
            AddressableAssetGroup group = AddressableGroupPolicy.EnsureGroup(
                settings,
                OperatorLiveCatalogBuilder.LiveGroupName,
                true,
                BundledAssetGroupSchema.BundlePackingMode.PackTogether,
                ref changed);

            AddressableGroupPolicy.AssignEntry(
                settings, group, LiveCatalogPath, StageCatalog.LiveCatalogAddress, AddressablesLabel);

            EditorUtility.SetDirty(settings);
            Debug.Log($"[StageLiveCatalogBuilder] 라이브 스테이지 카탈로그 {remoteEntries.Count}개 갱신 완료");
        }

        /// <summary>
        /// 라이브 카탈로그가 로컬 에셋을 참조하면 원격 번들이 그 에셋을 끌어안아, static으로 묶은
        /// 로컬 그룹과 교차 의존이 생기고 콘텐츠 업데이트가 깨진다. CreateEntry가 이미 원격
        /// 항목의 참조를 전부 비우므로 이 검사는 앞으로 그 규칙이 깨졌을 때를 위한 안전망이다.
        /// </summary>
        private static void VerifyNoLocalAssetReferences(List<StageCatalogEntry> entries)
        {
            foreach (StageCatalogEntry entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (entry.stageDefinition != null)
                {
                    throw new InvalidOperationException(
                        $"라이브 스테이지 항목이 StageDefinition을 직접 참조합니다: {entry.stageId}. " +
                        "원격 항목은 주소만 들고 있어야 합니다.");
                }

                if (entry.descriptionBackground != null)
                {
                    throw new InvalidOperationException(
                        $"라이브 스테이지 항목이 로컬 배경 스프라이트를 직접 참조합니다: {entry.stageId}.");
                }
            }
        }

        private static StageCatalog GetOrCreateCatalog()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(LiveCatalogPath);
            if (existing != null)
            {
                if (existing is not StageCatalog catalog)
                {
                    throw new InvalidOperationException($"라이브 카탈로그 경로에 다른 타입의 에셋이 있습니다: {LiveCatalogPath}");
                }

                return catalog;
            }

            var created = ScriptableObject.CreateInstance<StageCatalog>();
            AssetDatabase.CreateAsset(created, LiveCatalogPath);
            return created;
        }
    }
}
