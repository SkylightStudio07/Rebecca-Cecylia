using System.Collections;
using System.Collections.Generic;
using RCCom.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    public class TitleConfigurationController : MonoBehaviour
    {
        private const string ScreenModeKey = "Settings.ScreenMode";
        private const string VSyncKey = "Settings.VSync";
        private const string ResolutionWidthKey = "Settings.ResolutionWidth";
        private const string ResolutionHeightKey = "Settings.ResolutionHeight";

        private enum ScreenMode
        {
            Windowed,
            Fullscreen
        }

        [Header("Panels")]
        [SerializeField] private GameObject configurationBackground;
        [SerializeField] private GameObject mainMenuBackground;

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Display Text")]
        [SerializeField] private TextMeshProUGUI screenModeText;
        [SerializeField] private TextMeshProUGUI resolutionText;
        [SerializeField] private TextMeshProUGUI vSyncText;

        [Header("Transition")]
        [SerializeField] private float transitionDuration = 0.45f;
        [SerializeField] private Vector2 menuExitOffset = new(-90f, 0f);
        [SerializeField] private Vector2 configurationEnterOffset = new(90f, 0f);
        [SerializeField] private float scaleBump = 0.025f;

        private readonly List<Vector2Int> availableResolutions = new();
        private ScreenMode pendingScreenMode;
        private int pendingResolutionIndex;
        private bool pendingVSync;
        private bool initialized;
        private bool isConfigurationOpen;
        private bool isTransitioning;
        private bool isOpeningFromInactive;
        private RectTransform configurationRect;
        private RectTransform mainMenuRect;
        private CanvasGroup configurationGroup;
        private CanvasGroup mainMenuGroup;
        private Vector2 configurationStartPosition;
        private Vector2 mainMenuStartPosition;
        private Vector3 configurationStartScale;
        private Vector3 mainMenuStartScale;

        private void Awake()
        {
            Initialize();

            if (!isOpeningFromInactive)
            {
                HideImmediately();
            }
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            if (configurationBackground == null)
            {
                configurationBackground = gameObject;
            }

            configurationRect = configurationBackground != null ? configurationBackground.GetComponent<RectTransform>() : null;
            mainMenuRect = mainMenuBackground != null ? mainMenuBackground.GetComponent<RectTransform>() : null;
            configurationGroup = GetOrAddCanvasGroup(configurationBackground);
            mainMenuGroup = GetOrAddCanvasGroup(mainMenuBackground);

            configurationStartPosition = configurationRect != null ? configurationRect.anchoredPosition : Vector2.zero;
            mainMenuStartPosition = mainMenuRect != null ? mainMenuRect.anchoredPosition : Vector2.zero;
            configurationStartScale = configurationRect != null ? configurationRect.localScale : Vector3.one;
            mainMenuStartScale = mainMenuRect != null ? mainMenuRect.localScale : Vector3.one;

            LoadSettings();
            initialized = true;
        }

        public void Open()
        {
            Initialize();

            if (isConfigurationOpen || isTransitioning)
            {
                return;
            }

            RefreshLabels();

            if (configurationBackground != null)
            {
                isOpeningFromInactive = !configurationBackground.activeInHierarchy;
                configurationBackground.SetActive(true);
                isOpeningFromInactive = false;
            }

            StartCoroutine(PlayOpenTransition());
        }

        public void Close()
        {
            Initialize();

            if (!isConfigurationOpen || isTransitioning)
            {
                return;
            }

            CloseCovered();
        }

        private void CloseCovered()
        {
            if (mainMenuBackground != null)
            {
                mainMenuBackground.SetActive(true);
            }

            StartCoroutine(PlayCloseTransition());
        }

        public void Apply()
        {
            Initialize();

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SaveVolumeSettings();
            }

            QualitySettings.vSyncCount = pendingVSync ? 1 : 0;

            // 해상도와 창모드는 반드시 Screen.SetResolution 한 번에 같이 넘겨야 함 — Screen.fullScreenMode만
            // 따로 바꾸면 해상도가 그대로 남아 창모드 전환 시 화면이 어긋나는 경우가 있음(유니티 공식 권장 방식).
            Vector2Int resolution = availableResolutions.Count > 0
                ? availableResolutions[pendingResolutionIndex]
                : new Vector2Int(Screen.width, Screen.height);
            FullScreenMode fullScreenMode = pendingScreenMode == ScreenMode.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(resolution.x, resolution.y, fullScreenMode);

            PlayerPrefs.SetInt(ScreenModeKey, (int)pendingScreenMode);
            PlayerPrefs.SetInt(VSyncKey, pendingVSync ? 1 : 0);
            PlayerPrefs.SetInt(ResolutionWidthKey, resolution.x);
            PlayerPrefs.SetInt(ResolutionHeightKey, resolution.y);
            PlayerPrefs.Save();

            RefreshLabels();
        }

        public void PreviousScreenMode()
        {
            ToggleScreenMode();
        }

        public void NextScreenMode()
        {
            ToggleScreenMode();
        }

        public void PreviousVSync()
        {
            ToggleVSync();
        }

        public void NextVSync()
        {
            ToggleVSync();
        }

        public void PreviousResolution()
        {
            if (availableResolutions.Count == 0)
            {
                return;
            }

            pendingResolutionIndex = Mathf.Max(0, pendingResolutionIndex - 1);
            RefreshLabels();
        }

        public void NextResolution()
        {
            if (availableResolutions.Count == 0)
            {
                return;
            }

            pendingResolutionIndex = Mathf.Min(availableResolutions.Count - 1, pendingResolutionIndex + 1);
            RefreshLabels();
        }

        private void LoadSettings()
        {
            if (SoundManager.Instance != null)
            {
                SetSliderValue(masterVolumeSlider, SoundManager.Instance.MasterVolume);
                SetSliderValue(bgmVolumeSlider, SoundManager.Instance.BgmVolume);
                SetSliderValue(sfxVolumeSlider, SoundManager.Instance.SfxVolume);

                if (masterVolumeSlider != null)
                {
                    masterVolumeSlider.onValueChanged.AddListener(SoundManager.Instance.SetMasterVolume);
                }

                if (bgmVolumeSlider != null)
                {
                    bgmVolumeSlider.onValueChanged.AddListener(SoundManager.Instance.SetBgmVolume);
                }

                if (sfxVolumeSlider != null)
                {
                    sfxVolumeSlider.onValueChanged.AddListener(SoundManager.Instance.SetSfxVolume);
                }
            }

            pendingScreenMode = (ScreenMode)PlayerPrefs.GetInt(ScreenModeKey, Screen.fullScreen ? (int)ScreenMode.Fullscreen : (int)ScreenMode.Windowed);
            pendingVSync = PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;

            BuildResolutionList();
            int savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
            int savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);
            pendingResolutionIndex = availableResolutions.FindIndex(r => r.x == savedWidth && r.y == savedHeight);
            if (pendingResolutionIndex < 0)
            {
                // 저장된 해상도가 이 모니터의 지원 목록에 없으면(다른 모니터에서 저장된 값 등)
                // 목록의 마지막(가장 높은 해상도, 보통 네이티브)으로 대체.
                pendingResolutionIndex = availableResolutions.Count - 1;
            }

            RefreshLabels();
        }

        /// <summary>
        /// Screen.resolutions는 갱신주파수(refresh rate)별로 같은 해상도가 중복 등록돼있어,
        /// 너비×높이 기준으로만 중복 제거한 목록을 만든다. 이미 오름차순으로 들어오므로 정렬은 불필요.
        /// </summary>
        private void BuildResolutionList()
        {
            availableResolutions.Clear();

            foreach (Resolution resolution in Screen.resolutions)
            {
                Vector2Int size = new(resolution.width, resolution.height);
                if (!availableResolutions.Contains(size))
                {
                    availableResolutions.Add(size);
                }
            }

            if (availableResolutions.Count == 0)
            {
                availableResolutions.Add(new Vector2Int(Screen.width, Screen.height));
            }
        }

        private void ToggleScreenMode()
        {
            pendingScreenMode = pendingScreenMode == ScreenMode.Windowed ? ScreenMode.Fullscreen : ScreenMode.Windowed;
            RefreshLabels();
        }

        private void ToggleVSync()
        {
            pendingVSync = !pendingVSync;
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (screenModeText != null)
            {
                screenModeText.text = pendingScreenMode == ScreenMode.Fullscreen ? "Fullscreen" : "Windowed";
            }

            if (resolutionText != null)
            {
                Vector2Int size = availableResolutions.Count > 0
                    ? availableResolutions[pendingResolutionIndex]
                    : new Vector2Int(Screen.width, Screen.height);
                resolutionText.text = $"{size.x} x {size.y}";
            }

            if (vSyncText != null)
            {
                vSyncText.text = pendingVSync ? "ON" : "OFF";
            }
        }

        private void HideImmediately()
        {
            isConfigurationOpen = false;
            isTransitioning = false;

            SetCanvasGroup(configurationGroup, 0f, false);
            SetCanvasGroup(mainMenuGroup, 1f, true);

            if (configurationRect != null)
            {
                configurationRect.anchoredPosition = configurationStartPosition;
                configurationRect.localScale = configurationStartScale;
            }

            if (mainMenuRect != null)
            {
                mainMenuRect.anchoredPosition = mainMenuStartPosition;
                mainMenuRect.localScale = mainMenuStartScale;
            }

            if (configurationBackground != null)
            {
                configurationBackground.SetActive(false);
            }

            if (mainMenuBackground != null)
            {
                mainMenuBackground.SetActive(true);
            }
        }

        private IEnumerator PlayOpenTransition()
        {
            isTransitioning = true;

            if (mainMenuBackground != null)
            {
                mainMenuBackground.SetActive(true);
            }

            SetCanvasGroup(mainMenuGroup, 1f, false);
            SetCanvasGroup(configurationGroup, 0f, false);

            if (mainMenuRect != null)
            {
                mainMenuRect.anchoredPosition = mainMenuStartPosition;
                mainMenuRect.localScale = mainMenuStartScale;
            }

            if (configurationRect != null)
            {
                configurationRect.anchoredPosition = configurationStartPosition + configurationEnterOffset;
                configurationRect.localScale = configurationStartScale * (1f + scaleBump);
            }

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                float t = transitionDuration > 0f ? Mathf.Clamp01(elapsed / transitionDuration) : 1f;
                ApplyTransition(EaseOutCubic(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ApplyTransition(1f);
            SetCanvasGroup(mainMenuGroup, 0f, false);
            SetCanvasGroup(configurationGroup, 1f, true);

            if (mainMenuBackground != null)
            {
                mainMenuBackground.SetActive(false);
            }

            isConfigurationOpen = true;
            isTransitioning = false;
        }

        private IEnumerator PlayCloseTransition()
        {
            isTransitioning = true;

            if (mainMenuBackground != null)
            {
                mainMenuBackground.SetActive(true);
            }

            SetCanvasGroup(mainMenuGroup, 0f, false);
            SetCanvasGroup(configurationGroup, 1f, false);

            if (mainMenuRect != null)
            {
                mainMenuRect.anchoredPosition = mainMenuStartPosition + menuExitOffset;
                mainMenuRect.localScale = mainMenuStartScale * (1f + scaleBump);
            }

            if (configurationRect != null)
            {
                configurationRect.anchoredPosition = configurationStartPosition;
                configurationRect.localScale = configurationStartScale;
            }

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                float t = transitionDuration > 0f ? Mathf.Clamp01(elapsed / transitionDuration) : 1f;
                ApplyTransition(1f - EaseOutCubic(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ApplyTransition(0f);
            SetCanvasGroup(mainMenuGroup, 1f, true);
            SetCanvasGroup(configurationGroup, 0f, false);

            if (configurationBackground != null)
            {
                configurationBackground.SetActive(false);
            }

            isConfigurationOpen = false;
            isTransitioning = false;
        }

        private void ApplyTransition(float t)
        {
            if (mainMenuRect != null)
            {
                mainMenuRect.anchoredPosition = Vector2.LerpUnclamped(mainMenuStartPosition, mainMenuStartPosition + menuExitOffset, t);
                mainMenuRect.localScale = Vector3.LerpUnclamped(mainMenuStartScale, mainMenuStartScale * (1f + scaleBump), t);
            }

            if (configurationRect != null)
            {
                configurationRect.anchoredPosition = Vector2.LerpUnclamped(configurationStartPosition + configurationEnterOffset, configurationStartPosition, t);
                configurationRect.localScale = Vector3.LerpUnclamped(configurationStartScale * (1f + scaleBump), configurationStartScale, t);
            }

            SetCanvasGroup(mainMenuGroup, 1f - t, false);
            SetCanvasGroup(configurationGroup, t, false);
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            if (!target.TryGetComponent(out CanvasGroup group))
            {
                group = target.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private static void SetCanvasGroup(CanvasGroup group, float alpha, bool interactive)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = alpha;
            group.interactable = interactive;
            group.blocksRaycasts = interactive;
        }

        private static float EaseOutCubic(float t)
        {
            float inverse = 1f - Mathf.Clamp01(t);
            return 1f - inverse * inverse * inverse;
        }

        private static void SetSliderValue(Slider slider, float value)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(value);
        }
    }
}
