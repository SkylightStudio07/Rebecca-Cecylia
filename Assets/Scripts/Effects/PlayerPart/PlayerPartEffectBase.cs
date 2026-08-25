using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.PlayerPart
{
    public abstract class PlayerPartEffectBase : ScriptableObject, IPlayerPartEffect
    {
        public virtual void OnSpawn(PlayerPartContext ctx) { }
        public virtual void OnTick(PlayerPartContext ctx) { }
        public virtual float ModifyMoveSpeed(PlayerPartContext ctx, float currentSpeed) => currentSpeed;
        public virtual float ModifyIncomingDamage(PlayerPartContext ctx, float incomingDamage) => incomingDamage;
        public virtual void OnDamaged(PlayerPartContext ctx, float appliedDamage) { }
        public virtual void OnSkillUsed(PlayerPartContext ctx) { }
        public virtual bool TryPreventPlayerDeath(PlayerPartContext ctx) => false;
        public virtual bool TryPreventBaseDefeat(PlayerPartContext ctx, BaseController baseController) => false;
    }
}
