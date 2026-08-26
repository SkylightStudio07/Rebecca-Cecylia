using System.Collections;
using RCCom.Core;
using RCCom.Data;
using RCCom.Managers;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace RCCom.UI
{
    public class TitleSceneController : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private GameObject titleBackground;
        [SerializeField] private GameObject mainMenuBackground;
        [SerializeField] private Graphic pressAnyButtonGraphic;

        [Header("Shared Lobby UI")]
        [Tooltip("메인 로비와 상점 화면에서만 표시하는 계정 재화 패널")]
        [SerializeField] private GameObject commodityPanel;
        [SerializeField] private GameObject shopPanelBackground;

        [Header("Timing")]
        [SerializeField] private float promptFlashDuration = 0.16f;
        [SerializeField] private float transitionDuration = 0.6f;

        [Header("Motion")]
        [SerializeField] private Vector2 titleExitOffset = new(0f, 180f);
        [SerializeField] private Vector2 mainMenuEnterOffset = new(0f, -140f);
        [SerializeField] private float backgroundScaleBump = 0.035f;

        [Header("Prompt Pulse")]
        [SerializeField] private float pulseSpeed = 3.4f;
        [SerializeField] private float minPulseAlpha = 0.55f;
        [SerializeField] private float maxPulseAlpha = 1f;
        [SerializeField] private float pulseScaleAmount = 0.025f;

        private RectTransform titleRect;
        private RectTransform mainMenuRect;
        private RectTransform promptRect;
        private CanvasGroup titleGroup;
        private CanvasGroup mainMenuGroup;
        private Vector2 titleStartPosition;
        private Vector2 mainMenuStartPosition;
        private Vector3 titleStartScale;
        private Vector3 mainMenuStartScale;
        private Vector3 promptStartScale;
        private Color promptStartColor;
        private bool isAnimating;
        private bool isMenuOpen;
        private IProfileStorage profileStorage;
        private TextMeshProUGUI commodityText;
        private LobbyOperatorDialogueUI lobbyOperatorDialogueUI;

        private void Awake()
        {
            profileStorage = new PlayerPrefsProfileStorage();
            ResolveReferences();
            ResolveCommodityText();
            RefreshCommodityText();
            CacheInitialState();
            ResetToTitle();
        }

        private void Update()
        {
            if (isAnimating || isMenuOpen)
            {
                return;
            }

            PulsePrompt();

            if (WasAnyInputPressedThisFrame())
            {
                BeginTransition();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            BeginTransition();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            BeginTransition();
        }

        public void ReturnToTitle()
        {
            if (isAnimating || !isMenuOpen)
            {
                return;
            }

            StartCoroutine(PlayReturnToTitle());
        }

        private void ResolveReferences()
        {
            if (titleBackground == null && transform.parent != null)
            {
                titleBackground = transform.parent.gameObject;
            }

            if (mainMenuBackground == null && titleBackground != null && titleBackground.transform.parent != null)
            {
                Transform sibling = titleBackground.transform.parent.Find("MainMenuBackground");
                if (sibling != null)
                {
                    mainMenuBackground = sibling.gameObject;
                }
            }

            if (pressAnyButtonGraphic == null)
            {
                pressAnyButtonGraphic = GetComponent<Graphic>();
            }

            if (mainMenuBackground != null)
            {
                lobbyOperatorDialogueUI = mainMenuBackground.GetComponentInChildren<LobbyOperatorDialogueUI>(true);
            }

            if (commodityPanel == null)
            {
                Transform panel = FindTransformByName("CommodityPanel");
                if (panel != null)
                {
                    commodityPanel = panel.gameObject;
                }
            }

            if (shopPanelBackground == null)
            {
                Transform panel = FindTransformByName("ShopPanelBackground");
                if (panel != null)
                {
                    shopPanelBackground = panel.gameObject;
                }
            }
        }

        private void ResolveCommodityText()
        {
            Transform panel = commodityPanel != null ? commodityPanel.transform : FindTransformByName("CommodityPanel");
            if (panel == null)
            {
                return;
            }

            Transform textTransform = FindChildByName(panel, "Text (TMP)");
            if (textTransform != null)
            {
                commodityText = textTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        public void RefreshCommodityText()
        {
            if (commodityText == null || profileStorage == null)
            {
                return;
            }

            PlayerProfile profile = profileStorage.Load();
            commodityText.text = profile.commodity.ToString();
        }

        private void RefreshCommodityVisibility()
        {
            if (commodityPanel == null)
            {
                return;
            }

            bool lobbyVisible = mainMenuBackground != null && mainMenuBackground.activeInHierarchy;
            bool shopVisible = shopPanelBackground != null && shopPanelBackground.activeInHierarchy;
            bool shouldShow = isMenuOpen && (lobbyVisible || shopVisible);
            if (commodityPanel.activeSelf == shouldShow)
            {
                return;
            }

            commodityPanel.SetActive(shouldShow);
            if (shouldShow)
            {
                // 상점 구매·강화 후 복귀한 경우에도 화면이 열리는 시점의 최신 계정값을 보인다.
                RefreshCommodityText();
            }
        }

        private static Transform FindTransformByName(string name)
        {
            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                if (allTransforms[i].name == name)
                {
                    return allTransforms[i];
                }
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void CacheInitialState()
        {
            titleRect = titleBackground != null ? titleBackground.GetComponent<RectTransform>() : null;
            mainMenuRect = mainMenuBackground != null ? mainMenuBackground.GetComponent<RectTransform>() : null;
            promptRect = pressAnyButtonGraphic != null ? pressAnyButtonGraphic.rectTransform : transform as RectTransform;
            titleGroup = GetOrAddCanvasGroup(titleBackground);
            mainMenuGroup = GetOrAddCanvasGroup(mainMenuBackground);

            titleStartPosition = titleRect != null ? titleRect.anchoredPosition : Vector2.zero;
            mainMenuStartPosition = mainMenuRect != null ? mainMenuRect.anchoredPosition : Vector2.zero;
            titleStartScale = titleRect != null ? titleRect.localScale : Vector3.one;
            mainMenuStartScale = mainMenuRect != null ? mainMenuRect.localScale : Vector3.one;
            promptStartScale = promptRect != null ? promptRect.localScale : Vector3.one;
            promptStartColor = pressAnyButtonGraphic != null ? pressAnyButtonGraphic.color : Color.white;
        }

        private void ResetToTitle()
        {
            isAnimating = false;
            isMenuOpen = false;
            RefreshCommodityVisibility();

            if (titleBackground != null)
            {
                titleBackground.SetActive(true);
            }

            if (mainMenuBackground != null)
            {
                mainMenuBackground.SetActive(false);
            }

            SetCanvasGroup(titleGroup, 1f, true);
            SetCanvasGroup(mainMenuGroup, 0f, false);

            if (titleRect != null)
            {
                titleRect.anchoredPosition = titleStartPosition;
                titleRect.localScale = titleStartScale;
            }

            if (mainMenuRect != null)
            {
                mainMenuRect.anchoredPosition = mainMenuStartPosition;
                mainMenuRect.localScale = mainMenuStartScale;
            }

            if (pressAnyButtonGraphic != null)
            {
                pressAnyButtonGraphic.raycastTarget = true;
                pressAnyButtonGraphic.color = promptStartColor;
            }

            if (promptRect != null)
            {
                promptRect.localScale = promptStartScale;
            }
        }

        private void PulsePrompt()
        {
            if (pressAnyButtonGraphic == null)
            {
                return;
            }

            float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            Color color = promptStartColor;
            color.a = Mathf.Lerp(minPulseAlpha, maxPulseAlpha, wave) * promptStartColor.a;
            pressAnyButtonGraphic.color = color;

            if (promptRect != null)
            {
                float scale = 1f + wave * pulseScaleAmount;
                promptRect.localScale = promptStartScale * scale;
            }
        }

        private void BeginTransition()
        {
            if (isAnimating || isMenuOpen)
            {
                return;
            }

            if (titleBackground == null || mainMenuBackground == null)
            {
                Debug.LogWarning("TitleSceneController needs TitleBackground and MainMenuBackground references.", this);
                return;
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayTitleClick();
            }

            isAnimating = true;
            StartCoroutine(PlayTransition());
        }

        private IEnumerator PlayTransition()
        {
            if (pressAnyButtonGraphic != null)
            {
                pressAnyButtonGraphic.raycastTarget = false;
            }

            mainMenuBackground.SetActive(true);
            RefreshCommodityText();
            SetCanvasGroup(mainMenuGroup, 0f, false);

            if (mainMenuRect != null)
            {
                mainMenuRect.anchoredPosition = mainMenuStartPosition + mainMenuEnterOffset;
                mainMenuRect.localScale = mainMenuStartScale * (1f + backgroundScaleBump);
            }

            yield return FlashPrompt();

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                float t = transitionDuration > 0f ? Mathf.Clamp01(elapsed / transitionDuration) : 1f;
                ApplyTransition(EaseOutCubic(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ApplyTransition(1f);
            SetCanvasGroup(titleGroup, 0f, false);
            SetCanvasGroup(mainMenuGroup, 1f, true);
            isAnimating = false;
            isMenuOpen = true;
            if (lobbyOperatorDialogueUI != null)
            {
                lobbyOperatorDialogueUI.PresentPendingReturn();
                lobbyOperatorDialogueUI.ShowTitleGreeting();
            }
        }

        private void LateUpdate()
        {
            RefreshCommodityVisibility();
        }

        private IEnumerator PlayReturnToTitle()
        {
            isAnimating = true;

            titleBackground.SetActive(true);
            SetCanvasGroup(titleGroup, 0f, false);
            SetCanvasGroup(mainMenuGroup, 1f, false);

            if (titleRect != null)
            {
                titleRect.anchoredPosition = titleStartPosition + titleExitOffset;
                titleRect.localScale = titleStartScale * (1f + backgroundScaleBump);
            }

            if (mainMenuRect != null)
            {
                mainMenuRect.anchoredPosition = mainMenuStartPosition;
                mainMenuRect.localScale = mainMenuStartScale;
            }

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                float linear = transitionDuration > 0f ? Mathf.Clamp01(elapsed / transitionDuration) : 1f;
                ApplyTransition(1f - EaseOutCubic(linear));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ResetToTitle();
        }

        private IEnumerator FlashPrompt()
        {
            if (pressAnyButtonGraphic == null || promptFlashDuration <= 0f)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < promptFlashDuration)
            {
                float t = Mathf.Clamp01(elapsed / promptFlashDuration);
                float flash = Mathf.Sin(t * Mathf.PI);
                Color color = promptStartColor;
                color.a = Mathf.Lerp(promptStartColor.a, 0.1f, flash);
                pressAnyButtonGraphic.color = color;

                if (promptRect != null)
                {
                    promptRect.localScale = promptStartScale * Mathf.Lerp(1f, 1.08f, flash);
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            pressAnyButtonGraphic.color = promptStartColor;
            if (promptRect != null)
            {
                promptRect.localScale = promptStartScale;
            }
        }

        private void ApplyTransition(float t)
        {
            if (titleRect != null)
            {
                titleRect.anchoredPosition = Vector2.LerpUnclamped(titleStartPosition, titleStartPosition + titleExitOffset, t);
                titleRect.localScale = Vector3.LerpUnclamped(titleStartScale, titleStartScale * (1f + backgroundScaleBump), t);
            }

            if (mainMenuRect != null)
            {
                mainMenuRect.anchoredPosition = Vector2.LerpUnclamped(mainMenuStartPosition + mainMenuEnterOffset, mainMenuStartPosition, t);
                mainMenuRect.localScale = Vector3.LerpUnclamped(mainMenuStartScale * (1f + backgroundScaleBump), mainMenuStartScale, t);
            }

            SetCanvasGroup(titleGroup, 1f - t, false);
            SetCanvasGroup(mainMenuGroup, t, false);
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

        private static bool WasAnyInputPressedThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                return true;
            }

            if (Mouse.current != null &&
                (Mouse.current.leftButton.wasPressedThisFrame ||
                 Mouse.current.rightButton.wasPressedThisFrame ||
                 Mouse.current.middleButton.wasPressedThisFrame))
            {
                return true;
            }

            if (Gamepad.current != null && HasPressedButton(Gamepad.current.allControls))
            {
                return true;
            }

            return Joystick.current != null && HasPressedButton(Joystick.current.allControls);
        }

        private static bool HasPressedButton(UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputControl> controls)
        {
            foreach (InputControl control in controls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
