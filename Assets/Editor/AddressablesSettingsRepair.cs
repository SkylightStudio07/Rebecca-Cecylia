using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 삭제 또는 병합 도중 남은 Addressables 그룹의 끊어진 참조만 제거한다.
    /// 정상 그룹을 다시 만들지 않는 이유는 그룹별 주소와 스키마를 보존하기 위해서다.
    /// </summary>
    public static class AddressablesSettingsRepair
    {
        [MenuItem("RCCom/Addressables/Repair Null Group References")]
        public static void RepairNullGroupReferences()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings를 찾지 못했습니다.");
            }

            int removedCount = 0;
            for (int index = settings.groups.Count - 1; index >= 0; index--)
            {
                if (settings.groups[index] != null)
                {
                    continue;
                }

                settings.groups.RemoveAt(index);
                removedCount++;
            }

            if (removedCount == 0)
            {
                Debug.Log("[AddressablesSettingsRepair] 제거할 null 그룹 참조가 없습니다.");
                return;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AddressablesSettingsRepair] null 그룹 참조 {removedCount}개를 제거했습니다.");
        }
    }
}
