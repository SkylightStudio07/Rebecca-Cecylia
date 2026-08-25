namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// 강화 modifier가 변경할 런타임 값. 신규 오퍼레이터는 이 목록의 기존 값을 조립하고,
    /// 완전히 새로운 전투 메커니즘을 추가할 때만 효과 SO와 함께 확장한다.
    /// </summary>
    public enum OperatorUpgradeTargetKind
    {
        AllyUnitMaxHealth,
        AllyUnitAttackDamage,
        AllyUnitAttackRange,
        AllyUnitMoveSpeed,
        AllyUnitDeployCost,
        DeployStartingCommandPoints,
        DeployMaxCommandPoints,
        DeployCommandPointRecoveryPerSecond,
        EffectTacticalRelayMultiplier,
        EffectVulnerableDamageMultiplier,
        EffectVulnerableRefreshDuration,
        EffectSlowMultiplier,
        EffectRepairPerSecond,
        EffectRepairPitStopMultiplier,
    }
}
