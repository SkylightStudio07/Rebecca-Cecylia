using RCCom.Core;
using RCCom.Data;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// TitleScene에서만 사용하는 호감도 테스트 패널.
    /// 실제 프로필 저장소와 로비 대사 호출을 그대로 사용해 디버그 경로가
    /// 본편 흐름과 달라지는 것을 막는다.
    /// </summary>
    public sealed class OperatorAffinityDebugPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private TMP_InputField operatorIdInput;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Slider affinitySlider;
        [SerializeField] private Button applyAffinityButton;
        [SerializeField] private Button decreaseAffinityButton;
        [SerializeField] private Button increaseAffinityButton;
        [SerializeField] private Button setUnfamiliarButton;
        [SerializeField] private Button setFavorableButton;
        [SerializeField] private Button setJoyButton;
        [SerializeField] private Button setLoveButton;
        [SerializeField] private Button setExButton;
        [SerializeField] private Button queueParticipatedReturnButton;
        [SerializeField] private Button queueOtherReturnButton;
        [SerializeField] private Button clearReturnButton;
        [SerializeField] private Button showDialogueButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private LobbyOperatorDialogueUI lobbyDialogueUi;

        private const string DebugOtherOperatorId = "__debug_other_operator__";
        private const float RefreshInterval = 0.25f;

        private IProfileStorage _storage;
        private float _refreshTimer;

        private void Awake()
        {
#if !UNITY_EDITOR
            // 디버그 UI는 빌드에 보이면 안 되므로 플레이어에서 첫 프레임 전에 비활성화한다.
            gameObject.SetActive(false);
            return;
#else
            _storage = new PlayerPrefsProfileStorage();
            if (lobbyDialogueUi == null)
            {
                lobbyDialogueUi = FindFirstObjectByType<LobbyOperatorDialogueUI>(
                    FindObjectsInactive.Include);
            }

            EnsureOperatorIdInput();
            RefreshFromProfile();
#endif
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (applyAffinityButton != null) { applyAffinityButton.onClick.AddListener(ApplySliderAffinity); }
            if (decreaseAffinityButton != null) { decreaseAffinityButton.onClick.AddListener(DecreaseAffinity); }
            if (increaseAffinityButton != null) { increaseAffinityButton.onClick.AddListener(IncreaseAffinity); }
            if (setUnfamiliarButton != null) { setUnfamiliarButton.onClick.AddListener(SetUnfamiliar); }
            if (setFavorableButton != null) { setFavorableButton.onClick.AddListener(SetFavorable); }
            if (setJoyButton != null) { setJoyButton.onClick.AddListener(SetJoy); }
            if (setLoveButton != null) { setLoveButton.onClick.AddListener(SetLove); }
            if (setExButton != null) { setExButton.onClick.AddListener(SetEx); }
            if (queueParticipatedReturnButton != null) { queueParticipatedReturnButton.onClick.AddListener(QueueParticipatedReturn); }
            if (queueOtherReturnButton != null) { queueOtherReturnButton.onClick.AddListener(QueueOtherReturn); }
            if (clearReturnButton != null) { clearReturnButton.onClick.AddListener(ClearReturn); }
            if (showDialogueButton != null) { showDialogueButton.onClick.AddListener(ShowDialogue); }
            if (refreshButton != null) { refreshButton.onClick.AddListener(RefreshFromProfile); }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (applyAffinityButton != null) { applyAffinityButton.onClick.RemoveListener(ApplySliderAffinity); }
            if (decreaseAffinityButton != null) { decreaseAffinityButton.onClick.RemoveListener(DecreaseAffinity); }
            if (increaseAffinityButton != null) { increaseAffinityButton.onClick.RemoveListener(IncreaseAffinity); }
            if (setUnfamiliarButton != null) { setUnfamiliarButton.onClick.RemoveListener(SetUnfamiliar); }
            if (setFavorableButton != null) { setFavorableButton.onClick.RemoveListener(SetFavorable); }
            if (setJoyButton != null) { setJoyButton.onClick.RemoveListener(SetJoy); }
            if (setLoveButton != null) { setLoveButton.onClick.RemoveListener(SetLove); }
            if (setExButton != null) { setExButton.onClick.RemoveListener(SetEx); }
            if (queueParticipatedReturnButton != null) { queueParticipatedReturnButton.onClick.RemoveListener(QueueParticipatedReturn); }
            if (queueOtherReturnButton != null) { queueOtherReturnButton.onClick.RemoveListener(QueueOtherReturn); }
            if (clearReturnButton != null) { clearReturnButton.onClick.RemoveListener(ClearReturn); }
            if (showDialogueButton != null) { showDialogueButton.onClick.RemoveListener(ShowDialogue); }
            if (refreshButton != null) { refreshButton.onClick.RemoveListener(RefreshFromProfile); }
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Time.timeScale <= 0f)
            {
                return;
            }

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f)
            {
                return;
            }

            _refreshTimer = RefreshInterval;
            RefreshStatusOnly();
#endif
        }

#if UNITY_EDITOR
        private void EnsureOperatorIdInput()
        {
            if (operatorIdInput == null || !string.IsNullOrWhiteSpace(operatorIdInput.text))
            {
                return;
            }

            PlayerProfile profile = _storage.Load();
            operatorIdInput.SetTextWithoutNotify(ResolveCurrentOperatorId(profile));
        }

        private void ApplySliderAffinity()
        {
            SetAffinity(Mathf.RoundToInt(affinitySlider != null ? affinitySlider.value : 0f));
        }

        private void DecreaseAffinity()
        {
            SetAffinity(GetCurrentAffinity() - 1);
        }

        private void IncreaseAffinity()
        {
            SetAffinity(GetCurrentAffinity() + 1);
        }

        private void SetUnfamiliar() { SetAffinity(0); }
        private void SetFavorable() { SetAffinity(25); }
        private void SetJoy() { SetAffinity(50); }
        private void SetLove() { SetAffinity(75); }
        private void SetEx() { SetAffinity(PlayerProfile.MaxOperatorAffinity); }

        private void SetAffinity(int value)
        {
            PlayerProfile profile = _storage.Load();
            string operatorId = ResolveInputOperatorId(profile);
            profile.SetOperatorAffinity(operatorId, value);
            _storage.Save(profile);
            RefreshFromProfile();
        }

        private void QueueParticipatedReturn()
        {
            PlayerProfile profile = _storage.Load();
            profile.QueueBattleReturn(ResolveInputOperatorId(profile));
            _storage.Save(profile);
            RefreshFromProfile();
        }

        private void QueueOtherReturn()
        {
            PlayerProfile profile = _storage.Load();
            profile.QueueBattleReturn(DebugOtherOperatorId);
            _storage.Save(profile);
            RefreshFromProfile();
        }

        private void ClearReturn()
        {
            PlayerProfile profile = _storage.Load();
            profile.pendingReturnOperatorId = string.Empty;
            profile.pendingReturnCount = 0;
            _storage.Save(profile);
            RefreshFromProfile();
        }

        private void ShowDialogue()
        {
            if (lobbyDialogueUi == null)
            {
                lobbyDialogueUi = FindFirstObjectByType<LobbyOperatorDialogueUI>(
                    FindObjectsInactive.Include);
            }

            if (lobbyDialogueUi != null)
            {
                lobbyDialogueUi.ShowInteraction();
            }

            RefreshFromProfile();
        }

        private void RefreshFromProfile()
        {
            if (_storage == null)
            {
                return;
            }

            PlayerProfile profile = _storage.Load();
            string operatorId = ResolveInputOperatorId(profile);
            int affinity = profile.GetOperatorAffinity(operatorId);
            if (affinitySlider != null)
            {
                affinitySlider.SetValueWithoutNotify(affinity);
            }

            RefreshStatus(profile, operatorId, affinity);
            _refreshTimer = RefreshInterval;
        }

        private void RefreshStatusOnly()
        {
            if (_storage == null)
            {
                return;
            }

            PlayerProfile profile = _storage.Load();
            string operatorId = ResolveInputOperatorId(profile);
            RefreshStatus(profile, operatorId, profile.GetOperatorAffinity(operatorId));
        }

        private void RefreshStatus(PlayerProfile profile, string operatorId, int affinity)
        {
            if (statusText == null)
            {
                return;
            }

            string pending = string.IsNullOrWhiteSpace(profile.pendingReturnOperatorId)
                ? "없음"
                : $"{profile.pendingReturnOperatorId} × {profile.pendingReturnCount}";
            statusText.text =
                $"ID  {operatorId}\n" +
                $"호감도  {affinity}/100\n" +
                $"등급  {profile.GetOperatorAffinityTier(operatorId)}\n" +
                $"귀환 예약  {pending}";
        }

        private int GetCurrentAffinity()
        {
            PlayerProfile profile = _storage.Load();
            return profile.GetOperatorAffinity(ResolveInputOperatorId(profile));
        }

        private string ResolveInputOperatorId(PlayerProfile profile)
        {
            string input = operatorIdInput != null ? operatorIdInput.text : string.Empty;
            if (!string.IsNullOrWhiteSpace(input))
            {
                return input.Trim();
            }

            return ResolveCurrentOperatorId(profile);
        }

        private static string ResolveCurrentOperatorId(PlayerProfile profile)
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

            return "cassia";
        }
#endif
    }
}
