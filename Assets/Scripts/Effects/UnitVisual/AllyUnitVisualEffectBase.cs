using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Effects.UnitVisual
{
    /// <summary>
    /// 게임플레이 효과와 독립된 View 전용 무상태 SO. 신규 유닛은 이 에셋을 Definition에
    /// 조립하며 AllyUnitInstance는 표현 효과의 존재를 알지 않는다.
    /// </summary>
    public abstract class AllyUnitVisualEffectBase : ScriptableObject, IAllyUnitVisualEffect
    {
        public abstract IAllyUnitVisualRuntime CreateRuntime(AllyUnitVisualContext ctx);
    }
}
