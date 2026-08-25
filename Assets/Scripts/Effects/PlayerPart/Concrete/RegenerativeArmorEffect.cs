using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Regenerative Armor")]
    public sealed class RegenerativeArmorEffect : PlayerPartEffectBase
    {
        [Min(0f)] public float delayAfterDamage = 3f;
        [Min(0f)] public float maxHealthPerSecond = 0.02f;

        public override void OnSpawn(PlayerPartContext ctx)
        {
            ctx.GetState(this).timer = delayAfterDamage;
        }

        public override void OnTick(PlayerPartContext ctx)
        {
            PlayerPartRuntimeState state = ctx.GetState(this);
            state.timer -= ctx.deltaTime;
            if (state.timer <= 0f)
            {
                ctx.self.Heal(ctx.self.MaxHealth * maxHealthPerSecond * ctx.deltaTime);
            }
        }

        public override void OnDamaged(PlayerPartContext ctx, float appliedDamage)
        {
            ctx.GetState(this).timer = delayAfterDamage;
        }
    }
}
