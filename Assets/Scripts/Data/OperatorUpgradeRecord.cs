using System;

namespace RCCom.Data
{
    /// <summary>
    /// 오퍼레이터 강화 트랙 1개의 저장된 레벨. OperatorAffinityRecord와 동일한 이유로
    /// operatorId·trackId 문자열만 보유해, 아직 내려받지 않은 원격 오퍼레이터의 트랙 정의
    /// (OperatorUpgradeTrackSet)와도 저장 데이터가 결합되지 않는다.
    /// </summary>
    [Serializable]
    public sealed class OperatorUpgradeRecord
    {
        public string operatorId = string.Empty;
        public string trackId = string.Empty;
        public int level;
    }
}
