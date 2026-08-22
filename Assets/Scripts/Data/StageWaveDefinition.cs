using System;
using System.Collections.Generic;

namespace RCCom.Data
{
    /// <summary>
    /// 스테이지의 한 웨이브 편성. 빌드 페이즈와 적 체력 배율을 웨이브마다 조절할 수 있다.
    /// </summary>
    [Serializable]
    public sealed class StageWaveDefinition
    {
        public string displayName = "WAVE";
        public float buildPhaseDuration = 3f;
        public float healthMultiplier = 1f;
        public List<StageEnemySpawn> spawns = new();
    }
}
