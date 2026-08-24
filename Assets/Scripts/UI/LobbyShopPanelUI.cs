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
        [SerializeField] private Button enhanceEntryButton;
        [SerializeField] private Button shopRecruitTabButton;
        [SerializeField] private Button shopEnhanceTabButton;
        [SerializeField] private Button recruitOperatorButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button enhanceBackButton;
        [SerializeField] private GameObject recruitPanel;
        [SerializeField] private GameObject enhancePanel;
        [SerializeField] private OperatorRecruitShopUI recruitController;
        [SerializeField] private OperatorEnhanceShopUI enhanceController;
        [SerializeField] private Image shopRecruitTabImage;
        [SerializeField] private Image shopEnhanceTabImage;
        [SerializeField] private Sprite recruitNormalSprite;
        [SerializeField] private Sprite recruitSelectedSprite;
        [SerializeField] private Sprite enhanceNormalSprite;
        [SerializeField] private Sprite enhanceSelectedSprite;

        private bool _enhanceMode;

        private void Awake()
        {
            if (recruitEntryButton != null) { recruitEntryButton.onClick.AddListener(OpenRecruit); }
            if (enhanceEntryButton != null) { enhanceEntryButton.onClick.AddListener(OpenEnhance); }
            if (shopRecruitTabButton != null) { shopRecruitTabButton.onClick.AddListener(ShowRecruit); }
            if (shopEnhanceTabButton != null) { shopEnhanceTabButton.onClick.AddListener(ShowEnhance); }
            if (backButton != null) { backButton.onClick.AddListener(Close); }
            if (enhanceBackButton != null) { enhanceBackButton.onClick.AddListener(Close); }

            // TitleSceneController가 타이틀과 메인 로비의 초기 상태를 소유하므로 로비는 건드리지 않고,
            // 편집을 위해 켜 둔 Shop 패널만 런타임 진입 시 숨긴다.
            if (shopPanelBackground != null) { shopPanelBackground.SetActive(false); }
            ApplyMode();
        }

        private void OnDestroy()
        {
            if (recruitEntryButton != null) { recruitEntryButton.onClick.RemoveListener(OpenRecruit); }
            if (enhanceEntryButton != null) { enhanceEntryButton.onClick.RemoveListener(OpenEnhance); }
            if (shopRecruitTabButton != null) { shopRecruitTabButton.onClick.RemoveListener(ShowRecruit); }
            if (shopEnhanceTabButton != null) { shopEnhanceTabButton.onClick.RemoveListener(ShowEnhance); }
            if (backButton != null) { backButton.onClick.RemoveListener(Close); }
            if (enhanceBackButton != null) { enhanceBackButton.onClick.RemoveListener(Close); }
        }

        public void Open()
        {
            OpenRecruit();
        }

        public void OpenRecruit()
        {
            _enhanceMode = false;
            UILoadingTransition.Run(OpenCovered);
        }

        public void OpenEnhance()
        {
            _enhanceMode = true;
            UILoadingTransition.Run(OpenCovered);
        }

        public void ShowRecruit()
        {
            _enhanceMode = false;
            ApplyMode();
        }

        public void ShowEnhance()
        {
            _enhanceMode = true;
            ApplyMode();
        }

        public void Close()
        {
            UILoadingTransition.Run(CloseCovered);
        }

        private void OpenCovered()
        {
            if (mainMenuBackground != null) { mainMenuBackground.SetActive(false); }
            if (shopPanelBackground != null) { shopPanelBackground.SetActive(true); }
            ApplyMode();
        }

        private void CloseCovered()
        {
            if (shopPanelBackground != null) { shopPanelBackground.SetActive(false); }
            if (mainMenuBackground != null) { mainMenuBackground.SetActive(true); }
        }

        private void ApplyMode()
        {
            if (recruitPanel != null) { recruitPanel.SetActive(!_enhanceMode); }
            if (enhancePanel != null) { enhancePanel.SetActive(_enhanceMode); }
            if (recruitController != null) { recruitController.enabled = !_enhanceMode; }
            if (enhanceController != null) { enhanceController.enabled = _enhanceMode; }
            ApplyTabVisual(shopRecruitTabButton, shopRecruitTabImage, recruitNormalSprite,
                recruitSelectedSprite, !_enhanceMode);
            ApplyTabVisual(shopEnhanceTabButton, shopEnhanceTabImage, enhanceNormalSprite,
                enhanceSelectedSprite, _enhanceMode);
        }

        private static void ApplyTabVisual(Button button, Image image, Sprite normal, Sprite selected,
            bool isSelected)
        {
            if (button == null || image == null)
            {
                return;
            }

            image.sprite = isSelected ? selected : normal;
            if (isSelected)
            {
                button.transition = Selectable.Transition.None;
                return;
            }

            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.highlightedSprite = selected;
            state.pressedSprite = selected;
            state.selectedSprite = normal;
            state.disabledSprite = normal;
            button.spriteState = state;
        }
    }
}
