using RCCom.Runtime;

namespace RCCom.Effects.PlayerPart
{
    /// <summary>
    /// 포탑 슬롯의 주 공격 표식. 검증기가 한 Definition 안의 중복 조립을 막아 한 번의
    /// 자동 공격이 여러 판정 Effect로 중복 실행되는 사고를 차단한다.
    /// </summary>
    public interface IPlayerPrimaryAttackEffect
    {
        void OnAttack(PlayerAttackContext ctx, EnemyInstance target);
    }
}
