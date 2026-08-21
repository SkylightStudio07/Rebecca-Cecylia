using System;
using RCCom.Definitions.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 챕터 맵의 스테이지 노드 하나를 표현한다. 잠금·선택 표현만 담당하고 진행도 저장은 상위 UI가 소유한다.
    /// </summary>
    public sealed class StageNodeView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private RectTransform backgroundRect;
        [SerializeField] private Sprite availableSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Vector2 availableSize = new Vector2(188f, 253f);
        [SerializeField] private Vector2 selectedSize = new Vector2(210f, 276f);
        [SerializeField] private Vector2 lockedSize = new Vector2(168f, 260f);
        [SerializeField, Min(1f)] private float selectedScale = 1.15f;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI stateText;

        private Action _onClick;
        private bool _unlocked;
        private bool _selected;

        public Button Button => button;

        public void Setup(StageCatalogEntry entry, bool unlocked, bool selected, Action onClick)
        {
            _onClick = onClick;
            _unlocked = unlocked;

            if (titleText != null) { titleText.text = entry != null ? entry.displayName : "--"; }
            if (subtitleText != null) { subtitleText.text = entry != null ? entry.subtitle : string.Empty; }
            if (stateText != null) { stateText.gameObject.SetActive(false); }

            if (button != null)
            {
                button.interactable = unlocked;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(InvokeClick);
            }

            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            // 레이아웃 칸은 고정한 채 표시만 확대해야 주변 노드가 선택할 때마다 밀리지 않는다.
            transform.localScale = _unlocked && _selected
                ? Vector3.one * selectedScale
                : Vector3.one;
            if (background == null)
            {
                return;
            }

            if (!_unlocked)
            {
                ApplyVisual(lockedSprite, lockedSize);
                ApplyTextColors(new Color(0.55f, 0.58f, 0.62f, 1f), new Color(0.4f, 0.44f, 0.48f, 1f));
            }
            else if (_selected)
            {
                ApplyVisual(selectedSprite, selectedSize);
                ApplyTextColors(Color.white, new Color(0.25f, 0.8f, 1f, 1f));
            }
            else
            {
                ApplyVisual(availableSprite, availableSize);
                ApplyTextColors(Color.white, new Color(0.18f, 0.72f, 1f, 1f));
            }
        }

        private void ApplyVisual(Sprite sprite, Vector2 size)
        {
            background.sprite = sprite;
            background.color = Color.white;
            background.preserveAspect = true;
            if (backgroundRect != null)
            {
                backgroundRect.sizeDelta = size;
            }
        }

        private void ApplyTextColors(Color titleColor, Color subtitleColor)
        {
            if (titleText != null) { titleText.color = titleColor; }
            if (subtitleText != null) { subtitleText.color = subtitleColor; }
        }

        private void InvokeClick()
        {
            _onClick?.Invoke();
        }
    }
}
