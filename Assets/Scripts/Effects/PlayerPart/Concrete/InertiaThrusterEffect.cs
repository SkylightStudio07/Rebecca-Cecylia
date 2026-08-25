using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.PlayerPart.Concrete
{
    [CreateAssetMenu(menuName = "RCCom/Player Part/Effects/Inertia Thruster")]
    public sealed class InertiaThrusterEffect : PlayerPartEffectBase
    {
        [Min(0f)] public float accelerationDelay = 1.5f;
        [Min(0.01f)] public float rampDuration = 1.5f;
        [Min(1f)] public float maximumSpeedMultiplier = 1.26f;
        [Range(-1f, 1f)] public float sameDirectionDot = 0.98f;

        public override float ModifyMoveSpeed(PlayerPartContext ctx, float currentSpeed)
        {
            PlayerPartRuntimeState state = ctx.GetState(this);
            Vector2 input = ctx.self.LastMoveInput;
            if (input.sqrMagnitude < 0.0001f)
            {
                state.timer = 0f;
                state.direction = Vector2.zero;
                return currentSpeed;
            }

            Vector2 direction = input.normalized;
            if (state.direction.sqrMagnitude > 0.0001f &&
                Vector2.Dot(state.direction, direction) < sameDirectionDot)
            {
                state.timer = 0f;
            }

            state.direction = direction;
            state.timer += ctx.deltaTime;
            float progress = Mathf.Clamp01((state.timer - accelerationDelay) / Mathf.Max(0.01f, rampDuration));
            return currentSpeed * Mathf.Lerp(1f, maximumSpeedMultiplier, progress);
        }
    }
}
