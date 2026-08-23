using System;
using UnityEngine;

namespace RCCom.Definitions.Unit
{
    /// <summary>
    /// AllyUnitDefinition을 내려받기 전에도 배치 메뉴와 오퍼레이터 선택 화면이 사용할
    /// 최소 메타데이터. 원격 유닛은 실제 Sprite를 비우고 tint 값만 복사한다.
    /// </summary>
    [Serializable]
    public class AllyUnitCatalogEntry
    {
        public string unitId;
        public string displayName;
        public int deployCost;
        public string address;
        public bool remoteContent;
        public Sprite previewIcon;
        public Color fallbackColor = Color.white;
    }
}
