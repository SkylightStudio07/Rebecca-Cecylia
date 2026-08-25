using System;

namespace RCCom.Definitions.PlayerPart
{
    /// <summary>
    /// 슬롯별 스탯의 절대값. 파츠는 슬롯당 하나뿐이라 델타를 누적하지 않고 현재 파츠가
    /// 책임지는 축을 통째로 교체해야 상점의 전/후 비교와 런타임 조립이 같은 계산을 쓴다.
    /// </summary>
    [Serializable]
    public sealed class PlayerPartStatOverride
    {
        public float moveSpeed;
        public float skillOverdriveMoveSpeedMultiplier;

        public float attackDamage;
        public float attackRange;
        public float attackInterval;
        public float projectileSpeed;

        public float maxHealth;
        public float hitInvulnerabilityDuration;

        public float skillCooldown;
        public float skillRange;
        public float skillDamage;
        public int skillBurstCount;
        public float skillBurstInterval;
        public int skillChargeCapacity;
    }
}
