namespace RCCom.Data
{
    /// <summary>
    /// 호감도 수치를 UI와 대사 데이터가 공유하는 네 단계. 100은 사랑 단계의
    /// 최고치이지만, 별도 EX 대사 풀을 선택하기 위한 경계로 취급한다.
    /// </summary>
    public enum OperatorAffinityTier
    {
        Unfamiliar,
        Favorable,
        Joy,
        Love,
    }
}
