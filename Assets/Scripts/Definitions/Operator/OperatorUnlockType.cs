namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// 오퍼레이터 획득 경로. 조건별 수치는 Definition과 로컬 카탈로그가 소유하고,
    /// 실제 진행 상태는 PlayerProfile에서 판정한다.
    /// </summary>
    public enum OperatorUnlockType
    {
        InitiallyAvailable = 0,
        BestWave = 1,
        CommodityPurchase = 2,
        StageClearReward = 3,
    }
}
