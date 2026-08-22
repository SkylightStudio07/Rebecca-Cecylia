using System;

namespace RCCom.Data
{
    /// <summary>
    /// OperatorDefinition과 분리해 저장하는 오퍼레이터별 플레이어 상태.
    /// operatorId만 보유하므로 Addressable 콘텐츠가 아직 내려오지 않은 상태에서도
    /// 호감도 기록이 특정 에셋 참조에 묶이지 않는다.
    /// </summary>
    [Serializable]
    public sealed class OperatorAffinityRecord
    {
        public string operatorId = string.Empty;
        public int affinity;
    }
}
