using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// 빌드에 항상 포함되어 오퍼레이터 목록과 Addressables 주소를 제공하는 로컬 카탈로그.
    /// 각 Definition 자체는 로컬 또는 원격 그룹에 있을 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Operator/Operator Catalog")]
    public sealed class OperatorCatalog : ScriptableObject
    {
        public List<OperatorCatalogEntry> entries = new();

        public int FindIndex(string operatorId)
        {
            if (string.IsNullOrWhiteSpace(operatorId))
            {
                return -1;
            }

            return entries.FindIndex(entry =>
                entry != null && string.Equals(entry.operatorId, operatorId, StringComparison.Ordinal));
        }

        public int FindFirstUnlockedIndex(int bestWave)
        {
            return entries.FindIndex(entry => entry != null && entry.IsUnlocked(bestWave));
        }
    }
}
