using RCCom.Runtime;

namespace RCCom.Effects.PlayerPart
{
    public interface IPlayerPartEffect
    {
        void OnSpawn(PlayerPartContext ctx);
        void OnTick(PlayerPartContext ctx);
        float ModifyMoveSpeed(PlayerPartContext ctx, float currentSpeed);
        float ModifyIncomingDamage(PlayerPartContext ctx, float incomingDamage);
        void OnDamaged(PlayerPartContext ctx, float appliedDamage);
        void OnSkillUsed(PlayerPartContext ctx);
        bool TryPreventPlayerDeath(PlayerPartContext ctx);
        bool TryPreventBaseDefeat(PlayerPartContext ctx, BaseController baseController);
    }
}
