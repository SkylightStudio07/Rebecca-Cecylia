using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Enemy
{
    /// <summary>
    /// 빌드에 항상 포함되어 적 목록과 Addressables 주소를 제공하는 로컬 카탈로그.
    /// OperatorCatalog와 동일한 역할 — 각 EnemyDefinition 자체는 로컬 또는 원격 그룹에 있을 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Enemy/Enemy Catalog")]
    public sealed class EnemyCatalog : ScriptableObject
    {
        public List<EnemyCatalogEntry> entries = new();

        public int FindIndex(string enemyId)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                return -1;
            }

            return entries.FindIndex(entry =>
                entry != null && string.Equals(entry.enemyId, enemyId, StringComparison.Ordinal));
        }

        public EnemyCatalogEntry FindById(string enemyId)
        {
            int index = FindIndex(enemyId);
            return index < 0 ? null : entries[index];
        }
    }
}
