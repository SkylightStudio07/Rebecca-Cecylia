using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Unit
{
    /// <summary>
    /// 오퍼레이터가 배치할 수 있는 아군 유닛 Definition 목록. 로스터와 Definition은 런타임에
    /// 수정하지 않고, 체력·쿨다운·버프처럼 개체별로 달라지는 값은 AllyUnitInstance가 소유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Ally Unit Roster")]
    public class AllyUnitRoster : ScriptableObject
    {
        // Addressables 그룹이 Roster 에셋 안으로 모든 Definition을 끌고 들어가지 않도록
        // 편성 원본은 ID 목록으로 저장한다. 실제 Definition 목록은 Addressables 프리로드
        // 완료 후 세션 전용 인스턴스에 주입하므로 이 SO가 원격 콘텐츠를 직접 참조하지 않는다.
        public List<string> unitIds = new();
        [NonSerialized] public List<AllyUnitDefinition> units = new();

        public AllyUnitDefinition FindById(string unitId)
        {
            return units.Find(definition =>
                definition != null && string.Equals(definition.data.unitId, unitId, StringComparison.Ordinal));
        }
    }
}
