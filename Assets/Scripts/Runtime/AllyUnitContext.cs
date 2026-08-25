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

        /// <summary>
        /// 현재 씬에 지어진 모든 타워(TowerInstance.All을 그대로 대입). 유닛→타워 버프
        /// (TowerReinforcementAuraEffect 등)가 사거리 필터링을 스스로 하도록 가공 없이 넘긴다 —
        /// activeAllies와 동일하게 사거리로 미리 걸러져 있지 않다.
        /// </summary>
        public IReadOnlyList<TowerInstance> activeTowers;
    }
}
