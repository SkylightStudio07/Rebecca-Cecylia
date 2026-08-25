using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Passive Regeneration")]
    public sealed class PassiveRegenerationEffect : PlayerPartEffectBase
    {
        [Min(0f)] public float maxHealthPerSecond = 0.03f;
        [Min(0f)] public float healOnSkillRatio;

        public override void OnTick(PlayerPartContext ctx)
        {
            ctx.self.Heal(ctx.self.MaxHealth * maxHealthPerSecond * ctx.deltaTime);
        }

        public override void OnSkillUsed(PlayerPartContext ctx)
        {
            ctx.self.Heal(ctx.self.MaxHealth * healOnSkillRatio);
        }
    }
}
