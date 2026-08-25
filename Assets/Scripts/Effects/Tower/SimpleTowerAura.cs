namespace RCCom.Effects.Tower
{
    /// <summary>
    /// 데미지/공속 배율만 곱하는 가장 단순한 ITowerAura 구현. 원래 AuraBuffEffect(스킬 타워,
    /// 콜라이더 출입 이벤트로 상시 등록) 안의 private 중첩 클래스였는데, 유닛이 시전하는 임시
    /// 오라(TowerReinforcementAuraEffect, TowerInstance.ApplyTemporaryAura 경유)도 동일한 배율
    /// 계산이 필요해서 공유 가능한 위치로 추출했다.
    /// </summary>
    public sealed class SimpleTowerAura : ITowerAura
    {
        private readonly float _damageMultiplier;
        private readonly float _attackSpeedMultiplier;

        public SimpleTowerAura(float damageMultiplier, float attackSpeedMultiplier)
        {
            _damageMultiplier = damageMultiplier;
            _attackSpeedMultiplier = attackSpeedMultiplier;
        }

        public float ModifyOutgoingDamage(float baseDamage) => baseDamage * _damageMultiplier;

        public float ModifyAttackInterval(float baseInterval) => baseInterval / _attackSpeedMultiplier;
    }
}
