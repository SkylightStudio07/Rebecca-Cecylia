using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 로비 메뉴의 패널 스프라이트만 전환한다.
    /// 클릭 결과는 기존 TitleMenuTextButton에 남겨 시각 상태가 화면 흐름을 소유하지 않게 한다.
    /// </summary>
    public sealed class CommandLobbyMenuItem : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image panelImage;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite hoverSprite;
        [Tooltip("패널 PNG의 투명 여백은 클릭을 통과시키고, 보이는 패널만 메뉴로 판정한다.")]
        [SerializeField, Range(0.01f, 1f)] private float alphaHitTestThreshold = 0.08f;

        private bool _pointerInside;
        private bool _selected;

        private void Awake()
        {
            ApplyRaycastShape();
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
        }

        private void ApplyRaycastShape()
        {
            if (panelImage == null)
            {
                return;
            }

            // alphaHitTestMinimumThreshold는 0을 대입하는 경우에도 Read/Write가 꺼진 텍스처면
            // 예외를 던진다. 읽기 가능한 아트에만 값을 쓰고, 나머지는 Image 기본 사각 판정을
            // 그대로 두어 로비 메뉴 하나의 아트 설정이 전체 초기화를 중단하지 않게 한다.
            Texture2D texture = panelImage.sprite != null ? panelImage.sprite.texture : null;
            if (texture != null && texture.isReadable)
            {
                panelImage.alphaHitTestMinimumThreshold = alphaHitTestThreshold;
            }
        }
    }
}
