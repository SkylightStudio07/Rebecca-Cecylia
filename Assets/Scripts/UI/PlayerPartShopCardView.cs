using System;
using RCCom.Definitions.PlayerPart;
using TMPro;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>Exchange 하단의 고정 5칸 카드 한 칸. 데이터 판단은 상위 컨트롤러가 소유한다.</summary>
    public sealed class PlayerPartShopCardView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button button;
        [SerializeField] private UnityEngine.UI.Image background;
        [SerializeField] private UnityEngine.UI.Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text gradeText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text priceText;

        private Action<int> _clicked;
        private int _index;

        private UISpriteHoverSwap _backgroundSpriteSwap;

        private void Awake()
        {
            if (background != null)
            {
                _backgroundSpriteSwap = background.GetComponent<UISpriteHoverSwap>();
            }

            if (button != null) { button.onClick.AddListener(HandleClick); }
        }

        private void OnDestroy()
        {
            if (button != null) { button.onClick.RemoveListener(HandleClick); }
        }

        private void OnDisable()
        {
            if (_backgroundSpriteSwap != null) { _backgroundSpriteSwap.SetHighlighted(false); }
        }

        public void Bind(PlayerPartDefinition part, bool owned, bool equipped, bool selected,
            int index, Action<int> clicked)
        {
            _index = index;
            _clicked = clicked;
            gameObject.SetActive(part != null);
            if (part == null) { return; }

            if (icon != null)
            {
                icon.sprite = part.icon;
                icon.preserveAspect = true;
                icon.color = part.icon != null ? Color.white : new Color(0f, 0.55f, 0.9f, 0.3f);
            }

            SetText(nameText, part.displayName);
            SetText(gradeText, part.grade.ToString().ToUpperInvariant());
            SetText(stateText, equipped ? "EQUIPPED" : owned ? "OWNED" : "LOCKED");
            SetText(priceText, part.grade == PlayerPartGrade.Common ? "BASIC" : part.price.ToString());
            if (background != null)
            {
                // 카드 아트가 Normal/Hover 스프라이트 안에 선택 상태를 포함하므로,
                // 색상 곱으로 새 테두리와 발광을 다시 어둡게 만들지 않는다.
                background.color = Color.white;
                if (_backgroundSpriteSwap != null)
                {
                    _backgroundSpriteSwap.SetHighlighted(selected);
                }
            }
        }

        private void HandleClick()
        {
            _clicked?.Invoke(_index);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) { label.text = value ?? string.Empty; }
        }
    }
}
