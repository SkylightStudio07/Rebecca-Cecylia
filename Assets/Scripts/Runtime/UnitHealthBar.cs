using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 적/아군 공용 체력바 — SpriteRenderer 2개(배경+채움)만으로 구성한 가벼운 표시. UI Canvas
    /// 없이 월드 스프라이트 스케일만으로 구현해 개체 수가 늘어나도 가볍다. 채움 스프라이트는
    /// Pivot이 왼쪽(Left)이어야 오른쪽부터 자연스럽게 줄어든다. 만피일 때는 자동으로 숨겨서,
    /// 스폰 직후의 잡몹까지 전부 바가 떠서 화면이 지저분해지는 걸 방지한다. 로직 없는 순수
    /// 렌더러(4계층 3번) — EnemyView/AllyUnitView가 매 프레임 SetHealthPercent만 호출해준다.
    /// (원래 이름 EnemyHealthBar — 아군에도 재사용하며 이름만 일반화했다. 클래스 자체엔 적/아군
    /// 어느 쪽에도 특화된 로직이 없어서 그대로 재사용 가능했다.)
    /// </summary>
    public class UnitHealthBar : MonoBehaviour
    {
        [SerializeField] private Transform fillTransform;

        public void SetHealthPercent(float percent)
        {
            percent = Mathf.Clamp01(percent);
            gameObject.SetActive(percent > 0f && percent < 1f);

            if (fillTransform != null)
            {
                Vector3 scale = fillTransform.localScale;
                scale.x = percent;
                fillTransform.localScale = scale;
            }
        }
    }
}
