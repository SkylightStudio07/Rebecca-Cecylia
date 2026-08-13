using System.Collections.Generic;
using RCCom.Data;
using RCCom.Effects.Unit;
using UnityEngine;

namespace RCCom.Definitions.Unit
{
    /// <summary>
    /// 아군 유닛 한 종류의 데이터·효과·표현을 조립하는 SO. AllyUnitView 프리팹은 하나만 두고
    /// Bind 시 이 Definition을 주입해 종류별 프리팹 복제를 피한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Ally Unit Definition")]
    public class AllyUnitDefinition : ScriptableObject
    {
        public AllyUnitData data = new();
        public List<AllyUnitEffectBase> effects = new();
        public Sprite sprite;

        [Tooltip("스프라이트가 기본적으로 바라보는 방향 보정각. 오른쪽 기준 0도.")]
        public float spriteForwardOffsetDegrees;
    }
}
