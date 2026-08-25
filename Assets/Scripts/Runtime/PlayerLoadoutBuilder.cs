using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.PlayerPart;
using RCCom.Effects.PlayerPart;

namespace RCCom.Runtime
{
    public static class PlayerLoadoutBuilder
    {
        public static PlayerLoadoutResult Compose(PlayerData source, PlayerPartCatalog catalog)
        {
            PlayerData data = CloneAndNormalize(source);
            var effects = new List<PlayerPartEffectBase>();

            if (catalog == null)
            {
                return new PlayerLoadoutResult(data, effects);
            }

            foreach (PlayerPartSlot slot in System.Enum.GetValues(typeof(PlayerPartSlot)))
            {
                PlayerPartDefinition part = PlayerPartDebugSession.Resolve(slot);
                if (part == null)
                {
                    continue;
                }

                ApplyStatOverride(data, part);
                if (part.effects != null)
                {
                    foreach (PlayerPartEffectBase effect in part.effects)
                    {
                        if (effect != null)
                        {
                            effects.Add(effect);
                        }
                    }
                }

            }

            return new PlayerLoadoutResult(data, effects);
        }

        public static PlayerData CloneAndNormalize(PlayerData source)
        {
            var data = new PlayerData
            {
                maxHealth = source.maxHealth,
                moveSpeed = source.moveSpeed,
                hitInvulnerabilityDuration = source.hitInvulnerabilityDuration,
                attackDamage = source.attackDamage,
                attackRange = source.attackRange,
                attackInterval = source.attackInterval,
                projectileSpeed = source.projectileSpeed,
                skillCooldown = source.skillCooldown,
                skillRange = source.skillRange,
                skillDamage = source.skillDamage,
                skillBurstCount = source.skillBurstCount > 0 ? source.skillBurstCount : 4,
                skillBurstInterval = source.skillBurstInterval > 0f ? source.skillBurstInterval : 0.3f,
                skillOverdriveMoveSpeedMultiplier = source.skillOverdriveMoveSpeedMultiplier > 0f
                    ? source.skillOverdriveMoveSpeedMultiplier
                    : 1.5f,
                skillChargeCapacity = source.skillChargeCapacity > 0 ? source.skillChargeCapacity : 1,
            };
            return data;
        }

        private static void ApplyStatOverride(PlayerData data, PlayerPartDefinition part)
        {
            PlayerPartStatOverride value = part.statOverride;
            if (value == null)
            {
                return;
            }

            switch (part.slot)
            {
                case PlayerPartSlot.Thruster:
                    data.moveSpeed = value.moveSpeed;
                    if (value.skillOverdriveMoveSpeedMultiplier > 0f)
                    {
                        data.skillOverdriveMoveSpeedMultiplier = value.skillOverdriveMoveSpeedMultiplier;
                    }
                    break;
                case PlayerPartSlot.Turret:
                    data.attackDamage = value.attackDamage;
                    data.attackRange = value.attackRange;
                    data.attackInterval = value.attackInterval;
                    data.projectileSpeed = value.projectileSpeed;
                    break;
                case PlayerPartSlot.Body:
                    data.maxHealth = value.maxHealth;
                    data.hitInvulnerabilityDuration = value.hitInvulnerabilityDuration;
                    break;
                case PlayerPartSlot.Driver:
                    data.skillCooldown = value.skillCooldown;
                    data.skillRange = value.skillRange;
                    data.skillDamage = value.skillDamage;
                    data.skillBurstCount = value.skillBurstCount;
                    data.skillBurstInterval = value.skillBurstInterval;
                    data.skillChargeCapacity = value.skillChargeCapacity > 0 ? value.skillChargeCapacity : 1;
                    break;
            }
        }
    }
}
