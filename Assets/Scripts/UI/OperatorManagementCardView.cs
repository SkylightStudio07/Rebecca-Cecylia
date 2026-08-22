using System;
using RCCom.Definitions.Operator;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 오퍼레이터 관리 화면의 카드 한 장을 표현한다. 잠금·활성·포커스 상태만 받아 그리며
    /// 프로필 저장과 Addressables 로드는 상위 화면에 남겨 View를 데이터와 분리한다.
    /// </summary>
    public sealed class OperatorManagementCardView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image stateImage;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Sprite unlockedSprite;
        [SerializeField] private Sprite hoverSprite;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private TextMeshProUGUI indexText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI affinityText;
        [SerializeField] private GameObject activeBadge;
        [SerializeField] private float highlightedScale = 1.08f;
        [SerializeField] private OperatorManagementCardVisual normalVisual;
        [SerializeField] private OperatorManagementCardVisual hoverVisual;
        [SerializeField] private OperatorManagementCardVisual lockedVisual;

        private OperatorCatalogEntry _entry;
        private int _displayIndex;
        private int _affinity;
        private bool _active;
        private bool _unlocked;
        private bool _browsing;
        private bool _pointerInside;
        private bool _uiSelected;
        private Action _onClick;

        public Button Button => button;

        public void Setup(OperatorCatalogEntry entry, int displayIndex, bool unlocked, bool active,
            bool browsing, int affinity, Action onClick)
        {
            _entry = entry;
            _displayIndex = displayIndex;
            _affinity = affinity;
            _active = active;
            _unlocked = unlocked;
            _browsing = browsing;
            _onClick = onClick;

            if (indexText != null) { indexText.text = (displayIndex + 1).ToString("00"); }
            if (nameText != null) { nameText.text = entry != null ? entry.displayName : "UNASSIGNED"; }
            if (stateText != null)
            {
                stateText.text = unlocked ? (active ? "ACTIVE" : "AVAILABLE") : "LOCKED";
                stateText.color = unlocked
                    ? new Color(0.12f, 0.7f, 1f, 1f)
                    : new Color(0.58f, 0.61f, 0.65f, 1f);
            }
            if (affinityText != null) { affinityText.text = unlocked ? $"AFFINITY {affinity:000}" : string.Empty; }
            if (activeBadge != null) { activeBadge.SetActive(active); }

            if (portraitImage != null)
            {
                portraitImage.sprite = entry != null && entry.managementPortrait != null
                    ? entry.managementPortrait
                    : entry != null ? entry.previewPortrait : null;
                portraitImage.enabled = portraitImage.sprite != null;
            }

            if (button != null)
            {
                // 잠긴 카드도 해금 조건을 살펴볼 수 있어야 하므로 클릭 자체는 막지 않는다.
                button.interactable = true;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(InvokeClick);
            }

            if (normalVisual != null) { normalVisual.Apply(_entry, _displayIndex, _unlocked, _active, _affinity); }
            if (hoverVisual != null) { hoverVisual.Apply(_entry, _displayIndex, _unlocked, _active, _affinity); }
            if (lockedVisual != null) { lockedVisual.Apply(_entry, _displayIndex, _unlocked, _active, _affinity); }
            ApplyVisual();
        }

        public void SetBrowsing(bool browsing)
        {
            _browsing = browsing;
            ApplyVisual();
        }

        public void SetActive(bool active)
        {
            _active = active;
            if (normalVisual != null) { normalVisual.Apply(_entry, _displayIndex, _unlocked, _active, _affinity); }
            if (hoverVisual != null) { hoverVisual.Apply(_entry, _displayIndex, _unlocked, _active, _affinity); }
            if (lockedVisual != null) { lockedVisual.Apply(_entry, _displayIndex, _unlocked, _active, _affinity); }
            if (activeBadge != null) { activeBadge.SetActive(active); }
            if (stateText != null && _unlocked) { stateText.text = active ? "ACTIVE" : "AVAILABLE"; }
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
            _uiSelected = true;
            ApplyVisual();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _uiSelected = false;
            ApplyVisual();
        }

        private void InvokeClick()
        {
            _onClick?.Invoke();
        }

        private void ApplyVisual()
        {
            if (normalVisual != null && hoverVisual != null && lockedVisual != null)
            {
                bool useHover = _unlocked && (_browsing || _pointerInside || _uiSelected);
                normalVisual.gameObject.SetActive(_unlocked && !useHover);
                hoverVisual.gameObject.SetActive(_unlocked && useHover);
                lockedVisual.gameObject.SetActive(!_unlocked);
                transform.localScale = Vector3.one;
                return;
            }

            if (stateImage == null)
            {
                return;
            }

            bool highlighted = _unlocked && (_browsing || _pointerInside || _uiSelected);
            stateImage.sprite = _unlocked ? (highlighted ? hoverSprite : unlockedSprite) : lockedSprite;
            // 카드 루트까지 확대하면 초상화와 TMP도 함께 움직여 호버 전후의 시각 중심이 흔들린다.
            // 상태 Image만 확대하고 콘텐츠 좌표는 항상 같은 위치를 유지한다.
            stateImage.preserveAspect = false;
            transform.localScale = Vector3.one;
            stateImage.rectTransform.localScale = highlighted ? Vector3.one * highlightedScale : Vector3.one;
        }
    }
}
