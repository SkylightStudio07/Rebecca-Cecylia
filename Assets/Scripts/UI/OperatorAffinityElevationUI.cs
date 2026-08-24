using TMPro;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>로비 귀환 시 실제로 증가한 오퍼레이터 친밀도를 짧은 상승 토스트로 표시한다.</summary>
    public sealed class OperatorAffinityElevationUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform messageRect;
        [SerializeField, Min(0.1f)] private float duration = 1.4f;
        [SerializeField, Min(0f)] private float riseDistance = 90f;
        [SerializeField, Range(0f, 0.9f)] private float fadeStart = 0.38f;
        [SerializeField, Range(1f, 1.3f)] private float popScale = 1.08f;

        private Vector2 _startPosition;
        private Vector3 _startScale;
        private float _elapsed;
        private bool _isVisible;

        private void Awake()
        {
            ResolveReferences();
            _startPosition = messageRect != null ? messageRect.anchoredPosition : Vector2.zero;
            _startScale = messageRect != null ? messageRect.localScale : Vector3.one;
            HideImmediately();
        }

        private void OnDisable()
        {
            HideImmediately();
        }

        private void Update()
        {
            if (!_isVisible)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(_elapsed / Mathf.Max(0.1f, duration));
            float easedRise = 1f - Mathf.Pow(1f - normalized, 3f);

            if (messageRect != null)
            {
                messageRect.anchoredPosition = _startPosition + Vector2.up * (riseDistance * easedRise);
                float scale = normalized < 0.2f
                    ? Mathf.Lerp(0.82f, popScale, normalized / 0.2f)
                    : Mathf.Lerp(popScale, 1f, (normalized - 0.2f) / 0.8f);
                messageRect.localScale = _startScale * scale;
            }

            if (canvasGroup != null)
            {
                float fade = Mathf.InverseLerp(fadeStart, 1f, normalized);
                canvasGroup.alpha = 1f - fade * fade;
            }

            if (normalized >= 1f)
            {
                HideImmediately();
            }
        }

        public void ShowAffinityIncrease(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            ResolveReferences();
            if (messageText == null || canvasGroup == null || messageRect == null)
            {
                return;
            }

            messageText.text = $"친밀도 {amount} 상승!";
            messageText.raycastTarget = false;
            messageRect.anchoredPosition = _startPosition;
            messageRect.localScale = _startScale * 0.82f;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            _elapsed = 0f;
            _isVisible = true;
        }

        private void ResolveReferences()
        {
            if (messageText == null)
            {
                messageText = GetComponent<TextMeshProUGUI>();
            }

            if (messageRect == null)
            {
                messageRect = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private void HideImmediately()
        {
            _isVisible = false;
            _elapsed = 0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (messageRect != null)
            {
                messageRect.anchoredPosition = _startPosition;
                messageRect.localScale = _startScale;
            }
        }
    }
}
