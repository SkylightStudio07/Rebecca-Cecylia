using RCCom.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 메인 로비와 리크루트 화면 사이의 표시 전환만 담당한다.
    /// 실제 모집 데이터와 재화 처리는 별도 기능이 준비될 때 주입한다.
    /// </summary>
    public sealed class LobbyShopPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuBackground;
        [SerializeField] private GameObject shopPanelBackground;
        [SerializeField] private Button recruitEntryButton;
        [SerializeField] private Button recruitOperatorButton;
        [SerializeField] private Button backButton;

        [Header("상점 사운드")]
        [SerializeField] private AudioClip shopBgmClip;

        private void Awake()
        {
            if (recruitEntryButton != null) { recruitEntryButton.onClick.AddListener(Open); }
            if (backButton != null) { backButton.onClick.AddListener(Close); }

            // TitleSceneController가 타이틀과 메인 로비의 초기 상태를 소유하므로 로비는 건드리지 않고,
            // 편집을 위해 켜 둔 Shop 패널만 런타임 진입 시 숨긴다.
            if (shopPanelBackground != null) { shopPanelBackground.SetActive(false); }
        }

        private void OnDestroy()
        {
            if (recruitEntryButton != null) { recruitEntryButton.onClick.RemoveListener(Open); }
            if (backButton != null) { backButton.onClick.RemoveListener(Close); }
        }

        public void Open()
        {
            UILoadingTransition.Run(OpenCovered);
        }

        public void Close()
        {
            UILoadingTransition.Run(CloseCovered);
        }

        private void OpenCovered()
        {
            if (mainMenuBackground != null) { mainMenuBackground.SetActive(false); }
            if (shopPanelBackground != null) { shopPanelBackground.SetActive(true); }

            // 화면이 로딩 연출에 완전히 덮인 뒤 음악을 교체해 시각·청각 전환이 어긋나지 않게 한다.
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayTemporaryLoopingBgm(shopBgmClip);
            }
        }

        private void CloseCovered()
        {
            if (shopPanelBackground != null) { shopPanelBackground.SetActive(false); }
            if (mainMenuBackground != null) { mainMenuBackground.SetActive(true); }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.RestoreBgmAfterTemporaryLoop();
            }
        }
    }
}
