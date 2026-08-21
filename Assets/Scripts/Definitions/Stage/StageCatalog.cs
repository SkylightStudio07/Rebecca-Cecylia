using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Stage
{
    /// <summary>
    /// 챕터 맵을 구성하는 스테이지 목록. UI는 이 카탈로그만 읽으며 전투 웨이브 데이터는 별도 Definition이 소유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Stage/Stage Catalog")]
    public sealed class StageCatalog : ScriptableObject
    {
        public List<StageCatalogEntry> entries = new();

        public StageCatalogEntry FindById(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId) || entries == null)
            {
                return null;
            }

            foreach (StageCatalogEntry entry in entries)
            {
                if (entry != null && entry.stageId == stageId)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
