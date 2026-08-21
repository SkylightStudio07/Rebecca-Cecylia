using System;

namespace RCCom.Definitions.Stage
{
    /// <summary>
    /// 챕터 맵에서 즉시 표시할 수 있는 가벼운 스테이지 메타데이터.
    /// UI 표시 정보와 실행 Definition을 분리해, 추후 Definition 참조만 Addressables 키로 교체할 수 있게 한다.
    /// </summary>
    [Serializable]
    public sealed class StageCatalogEntry
    {
        public string stageId = string.Empty;
        public string chapterId = "ch1";
        public string displayName = "1-1";
        public string subtitle = "FIRST CONTACT";
        public string description = string.Empty;
        public int order;
        public int requiredBestWave;
        public StageDefinition stageDefinition;

        public bool IsUnlocked(int bestWave)
        {
            return bestWave >= requiredBestWave;
        }

        public bool IsPlayable(int bestWave)
        {
            return IsUnlocked(bestWave) && stageDefinition != null && stageDefinition.IsPlayable;
        }
    }
}
