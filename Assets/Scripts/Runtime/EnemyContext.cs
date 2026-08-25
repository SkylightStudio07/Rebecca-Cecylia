using System.Collections.Generic;

namespace RCCom.Runtime
{
    /// <summary>
    /// IEnemyEffect 훅에 전달되는 컨텍스트. TowerContext와 대응되는 개념.
    /// </summary>
    public class EnemyContext
    {
        public EnemyInstance self;
        public float deltaTime;
        public IReadOnlyList<EnemyInstance> activeEnemies;
    }
}
