using System;

namespace RCCom.EditorTools
{
    [Serializable]
    public sealed class PlayerPartEffectRecipe
    {
        public string type;
        public int shotCount;
        public float shotInterval;
        public float damageMultiplier;
        public int maxTargets;
        public float damageFalloff;
        public float beamHalfAngleDegrees;
        public float radius;
        public float splashDamageMultiplier;
        public int jumpCount;
        public float accelerationDelay;
        public float rampDuration;
        public float maximumSpeedMultiplier;
        public float dashDuration;
        public float delayAfterDamage;
        public float maxHealthPerSecond;
        public float healOnSkillRatio;
        public float barrierAmount;
        public float rechargeInterval;
        public float restoredHealthRatio;
        public float invulnerabilityDuration;
        public float triggerHealthRatio;
        public float instantHealRatio;
        public float cooldown;
    }
}
