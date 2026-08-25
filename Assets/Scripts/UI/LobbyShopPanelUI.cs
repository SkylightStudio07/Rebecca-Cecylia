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
        [SerializeField] private Button exchangeEntryButton;
        [SerializeField] private Button enhanceEntryButton;
        [SerializeField] private Button materialEntryButton;
        [SerializeField] private Button shopRecruitTabButton;
        [SerializeField] private Button shopExchangeTabButton;
        [SerializeField] private Button shopEnhanceTabButton;
        [SerializeField] private Button shopMaterialTabButton;
        [SerializeField] private Button recruitOperatorButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button enhanceBackButton;
        [SerializeField] private Button exchangeBackButton;
        [SerializeField] private GameObject recruitPanel;
        [SerializeField] private GameObject exchangePanel;
        [SerializeField] private GameObject enhancePanel;
        [SerializeField] private GameObject dossierPanel;
        [SerializeField] private OperatorRecruitShopUI recruitController;
        [SerializeField] private PlayerPartShopUI exchangeController;
        [SerializeField] private OperatorEnhanceShopUI enhanceController;
        [SerializeField] private OperatorDossierUI dossierController;
        [SerializeField] private Image shopRecruitTabImage;
        [SerializeField] private Image shopExchangeTabImage;
        [SerializeField] private Image shopEnhanceTabImage;
        [SerializeField] private Image shopMaterialTabImage;
        [SerializeField] private Sprite recruitNormalSprite;
        [SerializeField] private Sprite recruitSelectedSprite;
        [SerializeField] private Sprite exchangeNormalSprite;
        [SerializeField] private Sprite exchangeSelectedSprite;
        [SerializeField] private Sprite enhanceNormalSprite;
        [SerializeField] private Sprite enhanceSelectedSprite;
        [SerializeField] private Sprite materialNormalSprite;
        [SerializeField] private Sprite materialSelectedSprite;

        [Header("상점 사운드")]
        [SerializeField] private AudioClip shopBgmClip;

        private ShopMode _mode;

        private enum ShopMode
        {
            Recruit,
            Exchange,
            Enhance,
            Material,
        }

        private void Awake()
        {
            if (recruitEntryButton != null) { recruitEntryButton.onClick.AddListener(OpenRecruit); }
            if (exchangeEntryButton != null) { exchangeEntryButton.onClick.AddListener(OpenExchange); }
            if (enhanceEntryButton != null) { enhanceEntryButton.onClick.AddListener(OpenEnhance); }
            if (materialEntryButton != null) { materialEntryButton.onClick.AddListener(OpenMaterial); }
            if (shopRecruitTabButton != null) { shopRecruitTabButton.onClick.AddListener(ShowRecruit); }
            if (shopExchangeTabButton != null) { shopExchangeTabButton.onClick.AddListener(ShowExchange); }
            if (shopEnhanceTabButton != null) { shopEnhanceTabButton.onClick.AddListener(ShowEnhance); }
            if (shopMaterialTabButton != null) { shopMaterialTabButton.onClick.AddListener(ShowMaterial); }
            if (backButton != null) { backButton.onClick.AddListener(Close); }
            if (enhanceBackButton != null) { enhanceBackButton.onClick.AddListener(Close); }
            if (exchangeBackButton != null) { exchangeBackButton.onClick.AddListener(Close); }

            // TitleSceneController가 타이틀과 메인 로비의 초기 상태를 소유하므로 로비는 건드리지 않고,
            // 편집을 위해 켜 둔 Shop 패널만 런타임 진입 시 숨긴다.
            if (shopPanelBackground != null) { shopPanelBackground.SetActive(false); }
            ApplyMode();
        }

        private void OnDestroy()
        {
            if (recruitEntryButton != null) { recruitEntryButton.onClick.RemoveListener(OpenRecruit); }
            if (exchangeEntryButton != null) { exchangeEntryButton.onClick.RemoveListener(OpenExchange); }
            if (enhanceEntryButton != null) { enhanceEntryButton.onClick.RemoveListener(OpenEnhance); }
            if (materialEntryButton != null) { materialEntryButton.onClick.RemoveListener(OpenMaterial); }
            if (shopRecruitTabButton != null) { shopRecruitTabButton.onClick.RemoveListener(ShowRecruit); }
            if (shopExchangeTabButton != null) { shopExchangeTabButton.onClick.RemoveListener(ShowExchange); }
            if (shopEnhanceTabButton != null) { shopEnhanceTabButton.onClick.RemoveListener(ShowEnhance); }
            if (shopMaterialTabButton != null) { shopMaterialTabButton.onClick.RemoveListener(ShowMaterial); }
            if (backButton != null) { backButton.onClick.RemoveListener(Close); }
            if (enhanceBackButton != null) { enhanceBackButton.onClick.RemoveListener(Close); }
            if (exchangeBackButton != null) { exchangeBackButton.onClick.RemoveListener(Close); }
        }

        public void Open()
        {
            OpenRecruit();
        }

        public void OpenRecruit()
        {
            _mode = ShopMode.Recruit;
            UILoadingTransition.Run(OpenCovered);
        }

        public void OpenEnhance()
        {
            _mode = ShopMode.Enhance;
            UILoadingTransition.Run(OpenCovered);
        }

        public void OpenExchange()
        {
            _mode = ShopMode.Exchange;
            UILoadingTransition.Run(OpenCovered);
        }

        public void OpenMaterial()
        {
            _mode = ShopMode.Material;
            UILoadingTransition.Run(OpenCovered);
        }

        public void ShowRecruit()
        {
            _mode = ShopMode.Recruit;
            ApplyMode();
        }

        public void ShowEnhance()
        {
            _mode = ShopMode.Enhance;
            ApplyMode();
        }

        public void ShowExchange()
        {
            _mode = ShopMode.Exchange;
            ApplyMode();
        }

        public void ShowMaterial()
        {
            _mode = ShopMode.Material;
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

        private void ApplyMode()
        {
            bool recruit = _mode == ShopMode.Recruit;
            bool exchange = _mode == ShopMode.Exchange;
            bool enhance = _mode == ShopMode.Enhance;
            bool material = _mode == ShopMode.Material;
            if (recruitPanel != null) { recruitPanel.SetActive(recruit); }
            if (exchangePanel != null) { exchangePanel.SetActive(exchange); }
            if (enhancePanel != null) { enhancePanel.SetActive(enhance); }
            if (dossierPanel != null) { dossierPanel.SetActive(material); }
            if (recruitController != null) { recruitController.enabled = recruit; }
            if (exchangeController != null) { exchangeController.enabled = exchange; }
            if (enhanceController != null) { enhanceController.enabled = enhance; }
            if (dossierController != null) { dossierController.enabled = material; }
            ApplyTabVisual(shopRecruitTabButton, shopRecruitTabImage, recruitNormalSprite,
                recruitSelectedSprite, recruit);
            ApplyTabVisual(shopExchangeTabButton, shopExchangeTabImage, exchangeNormalSprite,
                exchangeSelectedSprite, exchange);
            ApplyTabVisual(shopEnhanceTabButton, shopEnhanceTabImage, enhanceNormalSprite,
                enhanceSelectedSprite, enhance);
            ApplyTabVisual(shopMaterialTabButton, shopMaterialTabImage, materialNormalSprite,
                materialSelectedSprite, material);
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
