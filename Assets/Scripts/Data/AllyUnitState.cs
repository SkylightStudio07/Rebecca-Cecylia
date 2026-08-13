namespace RCCom.Data
{
    /// <summary>
    /// 아군 유닛의 최소 상태 계약. 대상이 사라지면 Engaging에서 Advancing으로 돌아가고,
    /// 체력이 소진되면 Dead에서 더 이상 Tick되지 않는다.
    /// </summary>
    public enum AllyUnitState
    {
        Advancing,
        Engaging,
        Dead,
    }
}
