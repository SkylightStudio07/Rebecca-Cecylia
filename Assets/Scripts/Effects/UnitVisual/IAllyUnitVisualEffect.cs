using RCCom.Runtime.Visuals;

namespace RCCom.Effects.UnitVisual
{
    /// <summary>
    /// 아군 유닛 Definition에 조립하는 표현 전용 계약. SO는 설정만 보유하고 유닛마다 달라지는
    /// 애니메이션 진행도와 Renderer 수명은 반환된 런타임 객체가 소유한다.
    /// </summary>
    public interface IAllyUnitVisualEffect
    {
        IAllyUnitVisualRuntime CreateRuntime(AllyUnitVisualContext ctx);
    }
}
