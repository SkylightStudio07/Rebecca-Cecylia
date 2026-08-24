using UnityEngine;

namespace RCCom.Core
{
    /// <summary>
    /// 피해를 받을 수 있는 대상의 최소 계약. 적(EnemyInstance)이 구현하며, 플레이어/거점도
    /// 컨트롤러 단계에서 구현.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// sourcePosition: 이 피해를 입힌 주체(타워/유닛/플레이어)의 월드 위치. 사망 넉백
        /// 연출(설계안 §4 고도화)이 "공격받은 반대 방향"을 계산하는 데 쓴다 — 선택 인자라
        /// 넘기지 않아도(예: 독 틱처럼 순간 소스가 모호한 경우) 기존처럼 동작한다(EnemyInstance/
        /// AllyUnitInstance는 null이면 사망 연출 쪽에서 랜덤 방향으로 대체).
        /// </summary>
        void TakeDamage(float amount, Vector2? sourcePosition = null);
    }
}
