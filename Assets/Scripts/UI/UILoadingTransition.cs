using System;
using System.Collections;
using TMPro;
using RCCom.Definitions.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace RCCom.UI
{
    /// <summary>
    /// 타이틀 패널 전환과 씬 로딩을 같은 소녀전선풍 와이프로 감싼다.
    /// 전환 캔버스는 씬 사이에서 유지되지만 게임 흐름을 결정하지 않고, 호출자가 넘긴
    /// 작업이 끝날 때까지 입력과 시각 전환만 소유한다.
    /// </summary>
    public sealed class UILoadingTransition : MonoBehaviour
    {
        public const string LoadingTipsAddress = "ui/loading-tips";

        [Header("Visual")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform loadingBackground;
        [SerializeField] private RectTransform loadingGeometry;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private TMP_Text loadingText;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float coverDuration = 0.6f;
        [SerializeField, Min(0.01f)] private float revealDuration = 0.6f;
        [SerializeField, Min(0f)] private float minimumCoveredDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float completionPulseDuration = 0.28f;
        [SerializeField] private float geometryRotationSpeed = 160f;
        [SerializeField, Min(1f)] private float completionScale = 1.35f;

        [Header("Addressables")]
        [SerializeField] private string loadingTipsAddress = LoadingTipsAddress;
        [SerializeField, TextArea(1, 3)] private string[] fallbackTips =
        {
            "당신은 최고의 지휘관입니다!",
            "오퍼레이터마다 사용할 수 있는 전술 로스터가 다릅니다.",
            "아군 유닛은 최종 랠리 포인트에서 출격합니다.",
            "타워와 아군 유닛의 역할을 조합해 방어선을 유지하십시오.",
        };

        private static UILoadingTransition _instance;

        private Vector2 _coveredPosition;
        private Vector2 _hiddenAbovePosition;
        private Vector3 _geometryBaseScale;
        private bool _isTransitioning;
        private float _loadingVisualTime;
        private LoadingTipSet _loadingTipSet;
        private AsyncOperationHandle<LoadingTipSet> _tipHandle;
        private bool _ownsTipHandle;

        public static bool IsTransitioning => _instance != null && _instance._isTransitioning;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveReferences();
            CacheLayout();
            HideImmediately();
        }

        private void Start()
        {
            StartCoroutine(PreloadTips());
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (_ownsTipHandle && _tipHandle.IsValid())
            {
                Addressables.Release(_tipHandle);
            }
        }

        /// <summary>
        /// 동기 UI 전환을 로딩 화면이 완전히 덮은 프레임에 실행한다.
        /// 전환 컴포넌트가 없는 개발 씬에서는 기존 동작을 즉시 실행한다.
        /// </summary>
        public static bool Run(Action coveredAction)
        {
            if (_instance == null)
            {
                coveredAction?.Invoke();
                return false;
            }

            return _instance.BeginTransition(null, coveredAction);
        }

        /// <summary>
        /// Addressables 등 비동기 작업을 화면이 덮인 동안 실행한다.
        /// </summary>
        public static bool Run(IEnumerator loadingOperation)
        {
            if (_instance == null)
            {
                return false;
            }

            return _instance.BeginTransition(loadingOperation, null);
        }

        public static void LoadScene(string sceneName)
        {
            if (_instance == null)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            _instance.BeginTransition(_instance.LoadSceneRoutine(sceneName), null);
        }

        private bool BeginTransition(IEnumerator loadingOperation, Action coveredAction)
        {
            if (_isTransitioning)
            {
                return false;
            }

            StartCoroutine(PlayTransition(loadingOperation, coveredAction));
            return true;
        }

        private IEnumerator PlayTransition(IEnumerator loadingOperation, Action coveredAction)
        {
            _isTransitioning = true;
            _loadingVisualTime = 0f;
            PreparePresentation();
            SetVisible(true);

            yield return MoveBackground(_hiddenAbovePosition, _coveredPosition, coverDuration);

            float coveredStartedAt = Time.realtimeSinceStartup;
            coveredAction?.Invoke();
            if (loadingOperation != null)
            {
                yield return loadingOperation;
            }

            float remainingHold = minimumCoveredDuration -
                                  (Time.realtimeSinceStartup - coveredStartedAt);
            if (remainingHold > 0f)
            {
                yield return HoldCovered(remainingHold);
            }

            yield return PulseGeometry();
            yield return MoveBackground(_coveredPosition, _hiddenAbovePosition, revealDuration);

            HideImmediately();
            _isTransitioning = false;
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                Debug.LogError($"[UILoadingTransition] 씬 로딩을 시작하지 못했습니다: {sceneName}", this);
                yield break;
            }

            while (!operation.isDone)
            {
                UpdateLoadingVisual(Time.unscaledDeltaTime);
                yield return null;
            }
        }

        private IEnumerator MoveBackground(Vector2 start, Vector2 destination, float duration)
        {
            if (loadingBackground == null)
            {
                yield break;
            }

            loadingBackground.anchoredPosition = start;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = EaseInOutCubic(Mathf.Clamp01(elapsed / duration));
                loadingBackground.anchoredPosition =
                    Vector2.LerpUnclamped(start, destination, t);
                UpdateLoadingVisual(Time.unscaledDeltaTime);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            loadingBackground.anchoredPosition = destination;
        }

        private IEnumerator HoldCovered(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                UpdateLoadingVisual(Time.unscaledDeltaTime);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator PulseGeometry()
        {
            if (loadingGeometry == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < completionPulseDuration)
            {
                float t = EaseOutBack(Mathf.Clamp01(elapsed / completionPulseDuration));
                loadingGeometry.localScale = Vector3.LerpUnclamped(
                    _geometryBaseScale,
                    _geometryBaseScale * completionScale,
                    t);
                UpdateLoadingVisual(Time.unscaledDeltaTime);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            loadingGeometry.localScale = _geometryBaseScale * completionScale;
        }

        private void UpdateLoadingVisual(float unscaledDeltaTime)
        {
            _loadingVisualTime += unscaledDeltaTime;
            if (loadingGeometry != null)
            {
                loadingGeometry.Rotate(0f, 0f, -geometryRotationSpeed * unscaledDeltaTime);
            }

            if (loadingText != null)
            {
                int dots = Mathf.FloorToInt(_loadingVisualTime * 2f) % 4;
                loadingText.text = $"LOADING{new string('.', dots)}";
            }
        }

        private void PreparePresentation()
        {
            if (tipText != null)
            {
                tipText.text = ResolveTip();
            }

            if (loadingText != null)
            {
                loadingText.text = "LOADING";
            }

            if (loadingGeometry != null)
            {
                loadingGeometry.localScale = _geometryBaseScale;
            }
        }

        private string ResolveTip()
        {
            if (_loadingTipSet != null && _loadingTipSet.TryGetRandom(out string addressableTip))
            {
                return addressableTip;
            }

            if (fallbackTips != null && fallbackTips.Length > 0)
            {
                for (int attempt = 0; attempt < fallbackTips.Length; attempt++)
                {
                    string tip = fallbackTips[UnityEngine.Random.Range(0, fallbackTips.Length)];
                    if (!string.IsNullOrWhiteSpace(tip))
                    {
                        return tip;
                    }
                }
            }

            return "작전 데이터를 확인하고 있습니다.";
        }

        private IEnumerator PreloadTips()
        {
            if (string.IsNullOrWhiteSpace(loadingTipsAddress))
            {
                yield break;
            }

            _tipHandle = Addressables.LoadAssetAsync<LoadingTipSet>(loadingTipsAddress);
            _ownsTipHandle = true;
            yield return _tipHandle;
            if (_tipHandle.Status == AsyncOperationStatus.Succeeded)
            {
                _loadingTipSet = _tipHandle.Result;
            }
            else
            {
                Debug.LogWarning("[UILoadingTransition] Addressable 로딩 팁을 읽지 못해 로컬 기본 문구를 사용합니다.", this);
            }
        }

        private void ResolveReferences()
        {
            if (canvasGroup == null) { canvasGroup = GetComponent<CanvasGroup>(); }
            if (loadingBackground == null) { loadingBackground = transform.Find("UILoadingImageBackground") as RectTransform; }
            if (loadingBackground != null)
            {
                if (loadingGeometry == null) { loadingGeometry = loadingBackground.Find("UILoadingImageGeometry") as RectTransform; }
                if (tipText == null) { tipText = loadingBackground.Find("LoadingTipText")?.GetComponent<TMP_Text>(); }
                if (loadingText == null) { loadingText = loadingBackground.Find("LoadingStateText")?.GetComponent<TMP_Text>(); }
            }
        }

        private void CacheLayout()
        {
            _coveredPosition = loadingBackground != null
                ? loadingBackground.anchoredPosition
                : Vector2.zero;
            float height = 1440f;
            if (loadingBackground != null && loadingBackground.parent is RectTransform parentRect)
            {
                height = Mathf.Max(parentRect.rect.height, 1f);
            }

            _hiddenAbovePosition = _coveredPosition + Vector2.up * height;
            _geometryBaseScale = loadingGeometry != null
                ? loadingGeometry.localScale
                : Vector3.one;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void HideImmediately()
        {
            if (loadingBackground != null)
            {
                loadingBackground.anchoredPosition = _hiddenAbovePosition;
            }

            if (loadingGeometry != null)
            {
                loadingGeometry.localScale = _geometryBaseScale;
            }

            SetVisible(false);
        }

        private static float EaseInOutCubic(float value)
        {
            return value < 0.5f
                ? 4f * value * value * value
                : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f;
        }

        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.70158f;
            float shifted = value - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted +
                   overshoot * shifted * shifted;
        }
    }
}
