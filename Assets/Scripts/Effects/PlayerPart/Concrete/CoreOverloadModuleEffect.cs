using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Core Overload Module")]
    public sealed class CoreOverloadModuleEffect : PlayerPartEffectBase
    {
        [Min(0f)] public float maxHealthPerSecond = 0.035f;
        [Range(0f, 1f)] public float triggerHealthRatio = 0.3f;
        [Range(0f, 1f)] public float instantHealRatio = 0.25f;
        [Min(0f)] public float invulnerabilityDuration = 3f;
        [Min(0.01f)] public float cooldown = 45f;

        public override void OnTick(PlayerPartContext ctx)
        {
            PlayerPartRuntimeState state = ctx.GetState(this);
            state.timer = Mathf.Max(0f, state.timer - ctx.deltaTime);
            ctx.self.Heal(ctx.self.MaxHealth * maxHealthPerSecond * ctx.deltaTime);
        }

        public override void OnDamaged(PlayerPartContext ctx, float appliedDamage)
        {
            PlayerPartRuntimeState state = ctx.GetState(this);
            if (state.timer > 0f || ctx.self.CurrentHealth > ctx.self.MaxHealth * triggerHealthRatio)
            {
                return;
            }

            state.timer = cooldown;
            ctx.self.Heal(ctx.self.MaxHealth * instantHealRatio);
            ctx.self.GrantInvulnerability(invulnerabilityDuration);
        }
    }
}
