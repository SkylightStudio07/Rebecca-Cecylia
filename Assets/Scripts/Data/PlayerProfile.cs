using System;

namespace RCCom.Data
{
    /// <summary>
    /// 씬 재시작으로 사라지는 전투 세션과 분리해 보존하는 계정 데이터의 모양.
    /// 해금 여부는 bestWave와 OperatorDefinition.requiredBestWave로 계산하므로 같은 상태를
    /// 목록으로 중복 저장하지 않는다 — 두 값이 어긋나 잠금 상태가 모순되는 일을 막기 위함이다.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int bestWave;
        public string selectedOperatorId = string.Empty;
    }
}
