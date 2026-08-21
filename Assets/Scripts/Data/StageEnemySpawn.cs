using System;
using RCCom.Definitions.Enemy;

namespace RCCom.Data
{
    /// <summary>
    /// 스테이지 한 웨이브에서 같은 적을 몇 마리, 어떤 간격으로 소환할지 정의한다.
    /// 적의 전투 수치는 EnemyDefinition이 소유하므로 이 데이터는 편성 정보만 가진다.
    /// </summary>
    [Serializable]
    public sealed class StageEnemySpawn
    {
        public EnemyDefinition enemy;
        public int count = 1;
        public float interval = 1f;
        public float initialDelay;
    }
}
