using RCCom.Managers;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 타이틀 로비의 오퍼레이터 클릭 대사만 담당한다. 전투용 OperatorDialogueUI와 달리
    /// 플레이어·거점·건설 흐름을 참조하지 않아 TitleScene에서도 독립적으로 동작한다.
    /// </summary>
    public sealed class LobbyOperatorDialogueUI : MonoBehaviour
    {
        [SerializeField] private OperatorDialogueSet dialogueSet;
        [SerializeField] private Button operatorButton;
        [SerializeField] private Button dialogueButton;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private CanvasGroup dialogueGroup;
        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private float fadeDuration = 0.35f;

        private float _remainingDisplay;
        private float _remainingFade;
        private bool _isFading;

        private void Awake()
        {
            dialogueSet = OperatorLoadoutSession.ResolveDialogueSet(dialogueSet);
            Hide();
        }

        private void OnEnable()
        {
            if (operatorButton != null)
            {
                operatorButton.onClick.AddListener(ShowInteraction);
            }

            if (dialogueButton != null)
            {
                dialogueButton.onClick.AddListener(Hide);
            }
        }

        private void OnDisable()
        {
            if (operatorButton != null)
            {
                operatorButton.onClick.RemoveListener(ShowInteraction);
            }

            if (dialogueButton != null)
            {
                dialogueButton.onClick.RemoveListener(Hide);
            }

            Hide();
        }

        private void Update()
        {
            if (_isFading)
            {
                TickFade();
                return;
            }

            if (_remainingDisplay <= 0f)
            {
                return;
            }

            // 타이틀 로비는 게임플레이 일시정지와 무관하게 계속 반응해야 한다.
            _remainingDisplay -= Time.unscaledDeltaTime;
            if (_remainingDisplay <= 0f)
            {
                if (fadeDuration <= 0f)
                {
                    Hide();
                    return;
                }

                _isFading = true;
                _remainingFade = fadeDuration;
            }
        }

        public void ShowInteraction()
        {
            OperatorLineSet lineSet = ResolveLobbyLineSet();
            if (!HasLines(lineSet) || dialogueText == null || dialogueGroup == null)
            {
                return;
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayMainMenuClick();
            }

            dialogueText.text = lineSet.lines[Random.Range(0, lineSet.lines.Length)];
            dialogueGroup.alpha = 1f;
            dialogueGroup.interactable = true;
            dialogueGroup.blocksRaycasts = true;
            _remainingDisplay = Mathf.Max(0f, displayDuration);
            _remainingFade = 0f;
            _isFading = false;
        }

        public void Hide()
        {
            if (dialogueGroup != null)
            {
                dialogueGroup.alpha = 0f;
                dialogueGroup.interactable = false;
                dialogueGroup.blocksRaycasts = false;
            }

            _remainingDisplay = 0f;
            _remainingFade = 0f;
            _isFading = false;
        }

        private void TickFade()
        {
            _remainingFade -= Time.unscaledDeltaTime;
            dialogueGroup.alpha = Mathf.Clamp01(_remainingFade / fadeDuration);

            if (_remainingFade <= 0f)
            {
                Hide();
            }
        }

        private OperatorLineSet ResolveLobbyLineSet()
        {
            if (dialogueSet == null)
            {
                return null;
            }

            // 기존 에셋을 즉시 사용할 수 있게 하고, 최종 로비 대사는 데이터만 채워 교체한다.
            return HasLines(dialogueSet.lobbyInteraction)
                ? dialogueSet.lobbyInteraction
                : dialogueSet.gameStart;
        }

        private static bool HasLines(OperatorLineSet lineSet)
        {
            return lineSet != null && lineSet.lines != null && lineSet.lines.Length > 0;
        }
    }
}
