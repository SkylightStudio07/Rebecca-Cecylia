using System.Collections;
using TMPro;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>
    /// "LiveContent" 버튼을 누르면 화면을 덮는 단순 블로커 패널.
    /// UILoadingTransition(씬 전환용 와이프)과 달리 이 패널은 화면 전환을 소유하지 않고,
    /// 원격 카탈로그 조회가 끝날 때까지 입력만 막아 두는 용도라 CanvasGroup 알파만 조절한다.
    /// </summary>
    public sealed class LiveContentLoadingPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text messageText;
        [Tooltip("다운로드 속도 표시용 보조 텍스트. 비워 두면 속도 표시 없이 안내 문구만 보인다.")]
        [SerializeField] private TMP_Text progressText;
        [SerializeField, Min(0.01f)] private float fadeInDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.25f;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            if (canvasGroup == null) { canvasGroup = GetComponent<CanvasGroup>(); }
            HideImmediately();
        }

        public IEnumerator FadeIn(string message)
        {
            if (messageText != null && !string.IsNullOrEmpty(message))
            {
                messageText.text = message;
            }

            SetProgressText(string.Empty);
            IsVisible = true;
            SetBlocking(true);
            yield return Fade(1f, fadeInDuration);
        }

        public IEnumerator FadeOut()
        {
            yield return Fade(0f, fadeOutDuration);
            SetBlocking(false);
            SetProgressText(string.Empty);
            IsVisible = false;
        }

        public void SetMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
        }

        /// <summary>다운로드 속도/용량 같은 보조 정보 한 줄. progressText가 없으면 조용히 무시한다.</summary>
        public void SetProgressText(string text)
        {
            if (progressText != null)
            {
                progressText.text = text ?? string.Empty;
            }
        }

        private IEnumerator Fade(float target, float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            float from = canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                canvasGroup.alpha = Mathf.Lerp(from, target, elapsed / duration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            canvasGroup.alpha = target;
        }

        private void SetBlocking(bool blocking)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.interactable = blocking;
            canvasGroup.blocksRaycasts = blocking;
        }

        private void HideImmediately()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            SetBlocking(false);
            IsVisible = false;
        }
    }
}
