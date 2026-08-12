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

        /// <summary>
        /// 결과 화면이 전달한 도달 웨이브를 누적 최고 기록에 반영한다.
        /// 저장 호출 여부를 소비자가 판단할 수 있게 실제로 기록이 갱신됐을 때만 true를 반환한다.
        /// </summary>
        public bool TryRecordBestWave(int reachedWave)
        {
            int normalizedWave = Math.Max(0, reachedWave);
            if (normalizedWave <= bestWave)
            {
                return false;
            }

            bestWave = normalizedWave;
            return true;
        }
    }
}
