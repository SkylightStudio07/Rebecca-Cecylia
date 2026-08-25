using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Rechargeable Shield")]
    public sealed class RechargeableShieldEffect : PlayerPartEffectBase
    {
        [Min(0f)] public float barrierAmount = 8f;
        [Min(0.01f)] public float rechargeInterval = 10f;

        public override void OnSpawn(PlayerPartContext ctx)
        {
            PlayerPartRuntimeState state = ctx.GetState(this);
            state.value = barrierAmount;
            state.timer = rechargeInterval;
        }

        public override void OnTick(PlayerPartContext ctx)
        {
            PlayerPartRuntimeState state = ctx.GetState(this);
            if (state.value > 0f)
            {
                return;
            }

            state.timer -= ctx.deltaTime;
            if (state.timer <= 0f)
            {
                state.value = barrierAmount;
                state.timer = rechargeInterval;
            }
        }

        public override float ModifyIncomingDamage(PlayerPartContext ctx, float incomingDamage)
        {
            PlayerPartRuntimeState state = ctx.GetState(this);
            if (state.value <= 0f || incomingDamage <= 0f)
            {
                return incomingDamage;
            }

            float absorbed = Mathf.Min(state.value, incomingDamage);
            state.value -= absorbed;
            if (state.value <= 0f)
            {
                state.timer = rechargeInterval;
            }

            return incomingDamage - absorbed;
        }
    }
}
