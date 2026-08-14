using RCCom.Runtime;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>
    /// 선택된 오퍼레이터가 유닛 로스터를 제공할 때만 배치 UI를 노출한다.
    /// 로스터가 없는 타워형 오퍼레이터도 정상 로드아웃이므로 오류 화면을 띄우지 않고,
    /// CanvasGroup으로 시각 요소와 포인터 입력을 함께 차단한다.
    /// </summary>
    public class UnitDeployMenuUI : MonoBehaviour
    {
        [SerializeField] private UnitDeployController deployController;
        [SerializeField] private CanvasGroup panelGroup;

        public bool IsVisible { get; private set; }

        private void Start()
        {
            // Unity는 오브젝트 사이 Awake 순서를 보장하지 않는다. 모든 Awake에서 오퍼레이터
            // 로드아웃 해석이 끝난 뒤 실행되는 Start에서 최종 노출 상태를 결정한다.
            RefreshAvailability();
        }

        /// <summary>씬 재사용이나 에디터 검증에서 현재 로스터 상태를 다시 반영한다.</summary>
        public void RefreshAvailability()
        {
            bool shouldShow = deployController != null && deployController.IsAvailable;
            SetVisible(shouldShow);
        }

        private void SetVisible(bool visible)
        {
            IsVisible = visible;

            if (panelGroup == null)
            {
                Debug.LogError("[UnitDeployUI] 배치 패널 CanvasGroup 참조가 비어 있습니다.", this);
                return;
            }

            // GameObject를 끄면 같은 오브젝트의 UI 스크립트도 비활성화되어 후속 갱신을
            // 받을 수 없으므로, 기존 CardSelectionUI와 같은 CanvasGroup 계약을 사용한다.
            panelGroup.alpha = visible ? 1f : 0f;
            panelGroup.interactable = visible;
            panelGroup.blocksRaycasts = visible;
        }
    }
}
