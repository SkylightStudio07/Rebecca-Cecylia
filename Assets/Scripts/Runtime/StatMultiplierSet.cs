namespace RCCom.Runtime
{
    /// <summary>
    /// 아군 유닛에게 적용되는 짧은 지속시간 버프의 3축 배율. AllyUnitInstance.ApplyStatMultipliers가
    /// (source, effect) 키로 RefreshableAuraBag에 저장하고, 서로 다른 (source, effect) 조합은
    /// 축별로 곱연산 중첩된다(CalculateMoveSpeedMultiplier 등 참고).
    /// </summary>
    public readonly struct StatMultiplierSet
    {
        public readonly float moveSpeedMultiplier;
        public readonly float attackSpeedMultiplier;
        public readonly float damageMultiplier;

        public StatMultiplierSet(float moveSpeedMultiplier, float attackSpeedMultiplier, float damageMultiplier)
        {
            this.moveSpeedMultiplier = moveSpeedMultiplier;
            this.attackSpeedMultiplier = attackSpeedMultiplier;
            this.damageMultiplier = damageMultiplier;
        }
    }
}
