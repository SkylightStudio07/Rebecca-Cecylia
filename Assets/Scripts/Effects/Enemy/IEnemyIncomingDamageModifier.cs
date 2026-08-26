using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Effects.Enemy
{
    /// <summary>
    /// 적이 받는 피해를 데이터 Effect로 수정하는 선택 계약. 모든 적 Effect 훅을 늘리지 않고
    /// 피해 보정이 필요한 Effect만 구현하며, 공격 방향 판정용 발신 위치도 함께 받는다.
    /// </summary>
    public interface IEnemyIncomingDamageModifier
    {
        float ModifyIncomingDamage(
            EnemyContext ctx,
            float incomingDamage,
            Vector2? sourcePosition);
    }
}
