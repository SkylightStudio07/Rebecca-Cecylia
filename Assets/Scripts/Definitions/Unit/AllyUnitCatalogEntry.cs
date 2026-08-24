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

        // previewIcon이 원격이라 비어 있을 때 RemotePreviewSpriteLoader가 내려받을 독립
        // 번들 주소. Definition(프리팹·이펙트 포함) 전체를 당기지 않고 아이콘 한 장만 받는다.
        [Tooltip("previewIcon이 원격이라 비어 있을 때 내려받을 Addressables 주소")]
        public string previewIconAddress;

        public Color fallbackColor = Color.white;
    }
}
