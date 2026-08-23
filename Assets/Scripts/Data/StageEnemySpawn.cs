using System;

namespace RCCom.Data
{
    /// <summary>
    /// 스테이지 한 웨이브에서 같은 적을 몇 마리, 어떤 간격으로 소환할지 정의한다.
    /// 적의 전투 수치는 EnemyDefinition이 소유하므로 이 데이터는 편성 정보만 가진다.
    ///
    /// enemy(EnemyDefinition 하드 참조) 대신 enemyId 문자열을 쓴다 — 하드 참조가 있으면
    /// 이 StageDefinition을 포함한 빌드 그룹에 참조된 모든 EnemyDefinition이 딸려 들어가,
    /// 적을 개별 Addressable로 분리해도 로컬 빌드에서 절대 빠지지 않는다.
    /// </summary>
    [Serializable]
    public sealed class StageEnemySpawn
    {
        public string enemyId;
        public int count = 1;
        public float interval = 1f;
        public float initialDelay;
    }
}
