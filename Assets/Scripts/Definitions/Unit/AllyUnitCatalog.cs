using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Unit
{
    /// <summary>
    /// 빌드에 항상 포함되는 아군 유닛 카탈로그. Definition 본체는 유닛별 Local/Remote
    /// 그룹에 놓이고, 이 에셋은 ID·주소·미리보기만 제공한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Ally Unit Catalog")]
    public sealed class AllyUnitCatalog : ScriptableObject
    {
        public List<AllyUnitCatalogEntry> entries = new();

        public int FindIndex(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return -1;
            }

            return entries.FindIndex(entry =>
                entry != null && string.Equals(entry.unitId, unitId, StringComparison.Ordinal));
        }

        public AllyUnitCatalogEntry FindById(string unitId)
        {
            int index = FindIndex(unitId);
            return index < 0 ? null : entries[index];
        }
    }
}
