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
        public List<AllyUnitDefinition> units = new();

        public AllyUnitDefinition FindById(string unitId)
        {
            return units.Find(definition =>
                definition != null && string.Equals(definition.data.unitId, unitId, StringComparison.Ordinal));
        }
    }
}
