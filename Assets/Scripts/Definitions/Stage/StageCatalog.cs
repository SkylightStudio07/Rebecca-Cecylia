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
        /// <summary>
        /// 빌드 이후에 추가된 스테이지만 담아 원격으로 배송되는 카탈로그의 Addressables 주소.
        /// 런타임과 에디터가 같은 문자열을 봐야 하는데 런타임은 에디터 어셈블리를 참조할 수
        /// 없으므로 정본을 여기 둔다(OperatorCatalog.LiveCatalogAddress와 같은 이유).
        /// </summary>
        public const string LiveCatalogAddress = "catalog/stage";

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
