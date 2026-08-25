using System.Collections.Generic;
using RCCom.Effects.PlayerPart;

namespace RCCom.Runtime
{
    public sealed class PlayerPartContext
    {
        public PlayerController self;
        public float deltaTime;
        public IReadOnlyList<EnemyInstance> activeEnemies;

        public PlayerPartRuntimeState GetState(PlayerPartEffectBase effect)
        {
            return self.GetPartRuntimeState(effect);
        }
    }
}
