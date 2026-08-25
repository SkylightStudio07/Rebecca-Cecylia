using System;
using System.Collections.Generic;
using RCCom.Data;
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
        /// <summary>
        /// 빌드 이후에 추가된 오퍼레이터만 담아 원격으로 배송되는 카탈로그의 Addressables 주소.
        /// 런타임(LiveCatalogService)과 에디터(OperatorLiveCatalogBuilder)가 같은 문자열을
        /// 봐야 하는데 런타임 코드는 에디터 어셈블리를 참조할 수 없으므로 정본을 여기 둔다.
        /// </summary>
        public const string LiveCatalogAddress = "catalog/operator";

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

        public int FindFirstUnlockedIndex(PlayerProfile profile)
        {
            return entries.FindIndex(entry => entry != null && entry.IsUnlocked(profile));
        }
    }
}
