namespace RCCom.Runtime.Visuals
{
    /// <summary>AllyUnitView 한 개가 소유하는 표현 효과의 가변 상태와 수명 계약.</summary>
    public interface IAllyUnitVisualRuntime
    {
        void Tick(float deltaTime);
        void Dispose();
    }
}
