using RCCom.Runtime;

namespace RCCom.Effects.Unit
{
    /// <summary>
    /// 아군 유닛 효과 계약. 효과 SO 자체는 상태를 갖지 않고, 인스턴스별 쿨다운과 대상은
    /// AllyUnitInstance에 둔다. 근접·원거리·회복·버프는 이 훅의 조립으로 구현한다.
    /// </summary>
    public interface IAllyUnitEffect
    {
        void OnSpawn(AllyUnitContext ctx);
        void OnTick(AllyUnitContext ctx);
        void OnAttack(AllyUnitContext ctx, EnemyInstance target);
        void OnDeath(AllyUnitContext ctx);
    }
}
