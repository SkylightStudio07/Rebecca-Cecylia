using System;

namespace RCCom.Data
{
    /// <summary>Definition을 받기 전 스테이지 선택 화면에 보여줄 적 편성 요약.</summary>
    [Serializable]
    public sealed class StageEnemyPreview
    {
        public string enemyId = string.Empty;
        public int totalCount;
    }
}
