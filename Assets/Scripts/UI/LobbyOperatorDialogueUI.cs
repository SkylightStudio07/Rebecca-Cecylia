using RCCom.Core;
using RCCom.Data;
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
        [SerializeField] private Image lobbyOperatorImage;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private CanvasGroup dialogueGroup;
        [SerializeField] private string fallbackOperatorId = "cassia";
        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private float fadeDuration = 0.35f;

        private float _remainingDisplay;
        private float _remainingFade;
        private bool _isFading;
        private IProfileStorage _profileStorage;
        private Sprite _sceneLobbyIdleSprite;
        private OperatorDialogueSet _sceneDialogueSet;

        private void Awake()
        {
            _sceneDialogueSet = dialogueSet;
            dialogueSet = OperatorLoadoutSession.ResolveDialogueSet(dialogueSet);
            _profileStorage = new PlayerPrefsProfileStorage();
            if (lobbyOperatorImage != null)
            {
                _sceneLobbyIdleSprite = lobbyOperatorImage.sprite;
            }
            Hide();
        }

        public void RefreshOperator()
        {
            // 관리 화면에서 활성 오퍼레이터를 바꿔도 TitleScene은 재로드되지 않으므로
            // Awake에만 의존하지 않고 현재 세션의 대사·로비 전신을 즉시 다시 해석한다.
            dialogueSet = OperatorLoadoutSession.ResolveDialogueSet(_sceneDialogueSet);
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
            PlayerProfile profile = _profileStorage.Load();
            string operatorId = ResolveOperatorId(profile);
            bool claimedReturn = profile.TryClaimBattleReturn(operatorId, out _, out bool participated);
            if (claimedReturn)
            {
                // 결과 화면에서 예약한 보상은 이 클릭에서만 소비한다. 로비 재진입이나
                // WebGL 새로고침 뒤에도 중복 수령되지 않도록 정산 직후 저장한다.
                _profileStorage.Save(profile);
            }

            OperatorLineSet lineSet = claimedReturn
                ? ResolveReturnLineSet(participated, profile, operatorId)
                : ResolveTouchLineSet(profile, operatorId);
            if (!HasLines(lineSet) || dialogueText == null || dialogueGroup == null)
            {
                return;
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayMainMenuClick();
            }

            if (!lineSet.TryGetRandomLobby(out string text, out Sprite lobbySprite))
            {
                return;
            }

            dialogueText.text = text;
            if (lobbyOperatorImage != null)
            {
                // 로비 터치 표정은 전투 포트레잇과 별개다. 문장별 전신 스프라이트가
                // 비어 있으면 오퍼레이터의 로비 기본 전신으로 되돌린다.
                lobbyOperatorImage.sprite = lobbySprite != null ? lobbySprite : ResolveLobbyIdleSprite();
            }
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

            if (lobbyOperatorImage != null)
            {
                lobbyOperatorImage.sprite = ResolveLobbyIdleSprite();
            }
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

        private OperatorLineSet ResolveReturnLineSet(bool participated, PlayerProfile profile,
            string operatorId)
        {
            OperatorLineSet preferred = participated
                ? dialogueSet.lobbyReturnTogether
                : dialogueSet.lobbyReturn;
            return HasLines(preferred) ? preferred : ResolveTouchLineSet(profile, operatorId);
        }

        private OperatorLineSet ResolveTouchLineSet(PlayerProfile profile, string operatorId)
        {
            if (dialogueSet == null || profile == null)
            {
                return null;
            }

            int affinity = profile.GetOperatorAffinity(operatorId);
            if (affinity >= PlayerProfile.MaxOperatorAffinity && HasLines(dialogueSet.lobbyTouchEx))
            {
                return dialogueSet.lobbyTouchEx;
            }

            OperatorAffinityTier tier = profile.GetOperatorAffinityTier(operatorId);
            OperatorLineSet[] fallbackOrder;
            switch (tier)
            {
                case OperatorAffinityTier.Love:
                    fallbackOrder = new[]
                    {
                        dialogueSet.lobbyTouchLove, dialogueSet.lobbyTouchJoy,
                        dialogueSet.lobbyTouchFavorable, dialogueSet.lobbyTouchUnfamiliar,
                    };
                    break;
                case OperatorAffinityTier.Joy:
                    fallbackOrder = new[]
                    {
                        dialogueSet.lobbyTouchJoy, dialogueSet.lobbyTouchFavorable,
                        dialogueSet.lobbyTouchUnfamiliar,
                    };
                    break;
                case OperatorAffinityTier.Favorable:
                    fallbackOrder = new[]
                    {
                        dialogueSet.lobbyTouchFavorable, dialogueSet.lobbyTouchUnfamiliar,
                    };
                    break;
                default:
                    fallbackOrder = new[] { dialogueSet.lobbyTouchUnfamiliar };
                    break;
            }

            for (int i = 0; i < fallbackOrder.Length; i++)
            {
                if (HasLines(fallbackOrder[i]))
                {
                    return fallbackOrder[i];
                }
            }

            // 기존 Cassia 에셋이 새 호감도 슬롯을 채우기 전에도 로비가 동작해야 하므로
            // 기존 lobbyInteraction → gameStart 순서의 폴백을 마지막에 유지한다.
            return HasLines(dialogueSet.lobbyInteraction) ? dialogueSet.lobbyInteraction : dialogueSet.gameStart;
        }

        private string ResolveOperatorId(PlayerProfile profile)
        {
            if (OperatorLoadoutSession.SelectedDefinition != null &&
                !string.IsNullOrWhiteSpace(OperatorLoadoutSession.SelectedDefinition.operatorId))
            {
                return OperatorLoadoutSession.SelectedDefinition.operatorId;
            }

            if (profile != null && !string.IsNullOrWhiteSpace(profile.selectedOperatorId))
            {
                return profile.selectedOperatorId;
            }

            return fallbackOperatorId;
        }

        private static bool HasLines(OperatorLineSet lineSet)
        {
            return lineSet != null && lineSet.HasContent;
        }

        private Sprite ResolveLobbyIdleSprite()
        {
            return dialogueSet != null && dialogueSet.lobbyIdleSprite != null
                ? dialogueSet.lobbyIdleSprite
                : _sceneLobbyIdleSprite;
        }
    }
}
