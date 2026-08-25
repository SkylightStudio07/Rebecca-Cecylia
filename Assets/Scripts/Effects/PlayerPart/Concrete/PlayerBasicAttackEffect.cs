using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Basic Attack")]
    public sealed class PlayerBasicAttackEffect : PlayerPartEffectBase, IPlayerPrimaryAttackEffect
    {
        public void OnAttack(PlayerAttackContext ctx, EnemyInstance target)
        {
            ctx.Fire(target);
        }
    }
}
