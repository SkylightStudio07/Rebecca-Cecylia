using System.Collections.Generic;
using RCCom.Data;
using RCCom.Effects.PlayerPart;

namespace RCCom.Runtime
{
    public readonly struct PlayerLoadoutResult
    {
        public readonly PlayerData data;
        public readonly List<PlayerPartEffectBase> effects;

        public PlayerLoadoutResult(
            PlayerData data,
            List<PlayerPartEffectBase> effects)
        {
            this.data = data;
            this.effects = effects;
        }
    }
}
