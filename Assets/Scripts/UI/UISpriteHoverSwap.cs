using UnityEngine;
using UnityEngine.EventSystems;

namespace RCCom.UI
{
    /// <summary>
    /// uGUI Image의 Normal/Hover 스프라이트를 공통으로 전환한다.
    /// 버튼의 화면 흐름은 Button에 남기고, 이 컴포넌트는 시각 상태만 소유한다.
    /// </summary>
    public sealed class UISpriteHoverSwap : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private UnityEngine.UI.Image targetImage;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite hoverSprite;
        [SerializeField] private bool startHighlighted;
        [SerializeField] private Vector2 highlightedPositionOffset;

        private bool _pointerInside;
        private bool _selected;
        private bool _forcedHighlighted;
        private RectTransform _targetRectTransform;
        private Vector2 _normalAnchoredPosition;
        private bool _positionCaptured;

        public bool IsHighlighted => _forcedHighlighted || _pointerInside || _selected;

        private void Awake()
        {
            ResolveTargetImage();
            CaptureNormalPosition();
            if (targetImage != null && normalSprite == null)
            {
                // 기존 Image에 이미 지정된 Normal을 인스펙터에서 다시 고르게 만들지 않는다.
                normalSprite = targetImage.sprite;
            }

            _forcedHighlighted = startHighlighted;
            ApplyVisual();
        }

        private void OnEnable()
        {
            _pointerInside = false;
            _selected = false;
            _forcedHighlighted = startHighlighted;
            ApplyVisual();
        }

        private void OnDisable()
        {
            _pointerInside = false;
            _selected = false;
            _forcedHighlighted = false;
            ApplyVisual();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            ApplyVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            ApplyVisual();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            ApplyVisual();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            ApplyVisual();
        }

        /// <summary>
        /// Recruit처럼 포인터가 없어도 선택 상태를 유지해야 하는 화면에서 사용한다.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            _forcedHighlighted = highlighted;
            ApplyVisual();
        }

        public void SetSprites(Sprite normal, Sprite hover)
        {
            normalSprite = normal;
            hoverSprite = hover;
            ApplyVisual();
        }

        public void SetHighlightedPositionOffset(Vector2 offset)
        {
            highlightedPositionOffset = offset;
            ApplyVisual();
        }

        private void ResolveTargetImage()
        {
            if (targetImage == null)
            {
                targetImage = GetComponent<UnityEngine.UI.Image>();
            }
        }

        private void CaptureNormalPosition()
        {
            if (_positionCaptured || targetImage == null)
            {
                return;
            }

            _targetRectTransform = targetImage.rectTransform;
            if (_targetRectTransform != null)
            {
                _normalAnchoredPosition = _targetRectTransform.anchoredPosition;
                _positionCaptured = true;
            }
        }

        private void ApplyVisual()
        {
            if (targetImage == null)
            {
                return;
            }

            bool highlighted = IsHighlighted;
            targetImage.sprite = highlighted && hoverSprite != null
                ? hoverSprite
                : normalSprite;

            CaptureNormalPosition();
            if (_targetRectTransform != null)
            {
                // 상태 이미지 자체의 투명 여백이 비대칭이어도 카드가 흔들려 보이지 않도록 보정한다.
                _targetRectTransform.anchoredPosition = _normalAnchoredPosition
                    + (highlighted ? highlightedPositionOffset : Vector2.zero);
            }
        }
    }
}
