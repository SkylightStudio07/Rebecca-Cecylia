using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Multi Barrel Attack")]
    public sealed class PlayerMultiBarrelAttackEffect : PlayerPartEffectBase, IPlayerPrimaryAttackEffect
    {
        [Min(1)] public int shotCount = 2;
        [Min(0f)] public float shotInterval = 0.12f;
        [Min(0f)] public float damageMultiplierPerShot = 0.65f;

        public void OnAttack(PlayerAttackContext ctx, EnemyInstance target)
        {
            for (int index = 0; index < Mathf.Max(1, shotCount); index++)
            {
                ctx.Fire(target, damageMultiplierPerShot, index * shotInterval);
            }
        }
    }
}
