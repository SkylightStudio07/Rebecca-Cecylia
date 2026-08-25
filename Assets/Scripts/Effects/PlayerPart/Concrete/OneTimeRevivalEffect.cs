using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/One Time Revival")]
    public sealed class OneTimeRevivalEffect : PlayerPartEffectBase
    {
        [Range(0f, 1f)] public float restoredHealthRatio = 0.5f;
        [Min(0f)] public float invulnerabilityDuration = 1.5f;

        public override bool TryPreventPlayerDeath(PlayerPartContext ctx)
        {
            return TryConsume(ctx, null);
        }

        public override bool TryPreventBaseDefeat(PlayerPartContext ctx, BaseController baseController)
        {
            return TryConsume(ctx, baseController);
        }

        private bool TryConsume(PlayerPartContext ctx, BaseController baseController)
        {
            PlayerPartRuntimeState state = ctx.GetState(this);
            if (state.flag)
            {
                return false;
            }

            state.flag = true;
            ctx.self.RestoreFromPartEffect(restoredHealthRatio, invulnerabilityDuration);
            if (baseController != null && baseController.CurrentHealth <= 0f)
            {
                baseController.Revive(1f);
            }

            return true;
        }
    }
}
