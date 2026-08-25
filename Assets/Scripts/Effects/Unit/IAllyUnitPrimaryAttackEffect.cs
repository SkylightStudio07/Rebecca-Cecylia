namespace RCCom.Effects.Unit
{
    /// <summary>
    /// 한 번의 공격 훅에서 기본 피해를 완결하는 주 공격 Effect 표식 계약.
    /// AllyUnitInstance는 모든 Effect의 OnAttack을 호출하므로 한 Definition에 둘 이상 조립하면
    /// 피해가 중복된다. 런타임 분기를 늘리는 용도가 아니라 에디터 검증기가 데이터 조립 실수를
    /// 빌드 전에 차단하기 위한 역할 표식이다.
    /// </summary>
    public interface IAllyUnitPrimaryAttackEffect
    {
    }
}
