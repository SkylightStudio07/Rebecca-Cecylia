using System;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 자식 오브젝트의 트리거 콜라이더 이벤트를 부모 컴포넌트로 중계하는 최소 헬퍼.
    /// 플레이어처럼 한 오브젝트에 서로 다른 반경의 콜라이더 2개(몸통 히트박스 vs 원거리
    /// 사거리)가 필요할 때, Unity가 OnTriggerEnter2D에서 "내 콜라이더 중 어느 것"인지
    /// 구분해주지 않아서 큰 쪽(사거리)을 자식 오브젝트로 분리하고 이 릴레이로 이어붙인다.
    /// 이 오브젝트의 Collider2D 반경은 PlayerController가 현재 오퍼레이터의 공격 사거리에
    /// 맞춰 Awake에서 동기화한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AttackRangeTrigger : MonoBehaviour
    {
        public event Action<Collider2D> EnteredRange;
        public event Action<Collider2D> ExitedRange;

        /// <summary>
        /// 오퍼레이터 로드아웃이 플레이어 공격 사거리를 바꾸면 물리 감지 반경도 함께
        /// 바뀌어야 한다. 데이터만 교체하면 실제 트리거는 씬의 옛 값에 머무르는 불일치를
        /// 막기 위해 PlayerController.Awake에서 호출한다.
        /// </summary>
        public void SetWorldRadius(float radius)
        {
            if (!TryGetComponent(out CircleCollider2D circle))
            {
                Debug.LogWarning("[Player] AttackRangeTrigger에 CircleCollider2D가 없어 로드아웃 사거리를 반영하지 못했습니다.", this);
                return;
            }

            float scale = Mathf.Abs(transform.lossyScale.x);
            circle.radius = scale > 0f ? Mathf.Max(0f, radius) / scale : Mathf.Max(0f, radius);
        }

        private void OnTriggerEnter2D(Collider2D other) => EnteredRange?.Invoke(other);

        private void OnTriggerExit2D(Collider2D other) => ExitedRange?.Invoke(other);
    }
}
