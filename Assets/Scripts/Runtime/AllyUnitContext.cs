using System.Collections.Generic;

namespace RCCom.Runtime
{
    /// <summary>
    /// IAllyUnitEffect 훅에 전달되는 읽기 전용 후보 집합과 런타임 자기 참조.
    /// 효과가 씬 매니저나 SO의 전역 상태를 직접 찾지 않게 하는 경계다.
    /// </summary>
    public class AllyUnitContext
    {
        public AllyUnitInstance self;
        public float deltaTime;
        public IReadOnlyList<EnemyInstance> activeEnemies;
        public IReadOnlyList<AllyUnitInstance> activeAllies;
    }
}
