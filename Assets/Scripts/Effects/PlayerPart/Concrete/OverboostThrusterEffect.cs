using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Overboost Thruster")]
    public sealed class OverboostThrusterEffect : PlayerPartEffectBase
    {
        [Min(0f)] public float dashDuration = 0.6f;

        public override void OnSkillUsed(PlayerPartContext ctx)
        {
            ctx.self.BeginForcedDash(dashDuration);
        }
    }
}
