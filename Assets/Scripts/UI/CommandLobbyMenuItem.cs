using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 로비 메뉴의 패널 스프라이트와 TMP 색만 전환한다.
    /// 클릭 결과는 기존 TitleMenuTextButton에 남겨 시각 상태가 화면 흐름을 소유하지 않게 한다.
    /// </summary>
    public sealed class CommandLobbyMenuItem : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image panelImage;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite hoverSprite;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Color normalTitleColor = new(0.035f, 0.045f, 0.06f, 1f);
        [SerializeField] private Color normalSubtitleColor = new(0.18f, 0.2f, 0.23f, 1f);
        [SerializeField] private Color hoverTitleColor = Color.white;
        [SerializeField] private Color hoverSubtitleColor = new(0.84f, 0.94f, 1f, 1f);

        private bool _pointerInside;
        private bool _selected;

        private void Awake()
        {
            ApplyVisual(false);
        }

        private void OnDisable()
        {
            _pointerInside = false;
            _selected = false;
            ApplyVisual(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            ApplyVisual(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            ApplyVisual(_selected);
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            ApplyVisual(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            ApplyVisual(_pointerInside);
        }

        private void ApplyVisual(bool highlighted)
        {
            if (panelImage != null)
            {
                panelImage.sprite = highlighted && hoverSprite != null ? hoverSprite : normalSprite;
            }

            if (titleText != null)
            {
                titleText.color = highlighted ? hoverTitleColor : normalTitleColor;
            }

            if (subtitleText != null)
            {
                subtitleText.color = highlighted ? hoverSubtitleColor : normalSubtitleColor;
            }
        }
    }
}
