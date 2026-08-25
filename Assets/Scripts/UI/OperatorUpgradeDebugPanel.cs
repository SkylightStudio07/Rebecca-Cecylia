using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// TitleScene 전용 오퍼레이터 강화 테스트 패널. 상점/재화 투자 UI가 아직 없는 동안
    /// PlayerProfile.operatorUpgrades를 재화 소비 없이 직접 조작하기 위한 임시 디버그 도구다.
    /// OperatorAffinityDebugPanel과 동일하게 실제 저장소(PlayerPrefsProfileStorage)를 그대로
    /// 사용해 디버그 경로가 본편 흐름과 달라지지 않게 한다.
    /// </summary>
    public sealed class OperatorUpgradeDebugPanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField operatorIdInput;
        [SerializeField] private TMP_InputField trackIdInput;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Slider levelSlider;
        [SerializeField] private Button applyLevelButton;
        [SerializeField] private Button increaseLevelButton;
        [SerializeField] private Button decreaseLevelButton;
        [SerializeField] private Button maxOutOperatorButton;
        [SerializeField] private Button resetOperatorButton;
        [SerializeField] private Button refreshButton;

        private IProfileStorage _storage;
        private CanvasGroup _panelGroup;
        private bool _isPanelVisible;

        private void Awake()
        {
#if !UNITY_EDITOR
            // 디버그 UI는 빌드에 보이면 안 되므로 플레이어에서 첫 프레임 전에 비활성화한다.
            gameObject.SetActive(false);
            return;
#else
            _storage = new PlayerPrefsProfileStorage();
            _panelGroup = GetComponent<CanvasGroup>();
            if (_panelGroup == null)
            {
                // 루트 오브젝트를 끄면 이 컴포넌트도 Home 입력을 받을 수 없으므로
                // 표시 상태만 제어할 CanvasGroup을 런타임에 보장한다.
                _panelGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _isPanelVisible = _panelGroup.alpha > 0f;
            ApplyPanelVisibility();
            EnsureOperatorIdInput();
            RefreshFromProfile();
#endif
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (applyLevelButton != null) { applyLevelButton.onClick.AddListener(ApplySliderLevel); }
            if (increaseLevelButton != null) { increaseLevelButton.onClick.AddListener(IncreaseLevel); }
            if (decreaseLevelButton != null) { decreaseLevelButton.onClick.AddListener(DecreaseLevel); }
            if (maxOutOperatorButton != null) { maxOutOperatorButton.onClick.AddListener(MaxOutOperator); }
            if (resetOperatorButton != null) { resetOperatorButton.onClick.AddListener(ResetOperator); }
            if (refreshButton != null) { refreshButton.onClick.AddListener(RefreshFromProfile); }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (applyLevelButton != null) { applyLevelButton.onClick.RemoveListener(ApplySliderLevel); }
            if (increaseLevelButton != null) { increaseLevelButton.onClick.RemoveListener(IncreaseLevel); }
            if (decreaseLevelButton != null) { decreaseLevelButton.onClick.RemoveListener(DecreaseLevel); }
            if (maxOutOperatorButton != null) { maxOutOperatorButton.onClick.RemoveListener(MaxOutOperator); }
            if (resetOperatorButton != null) { resetOperatorButton.onClick.RemoveListener(ResetOperator); }
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

            if (Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame)
            {
                _isPanelVisible = !_isPanelVisible;
                ApplyPanelVisibility();
                if (_isPanelVisible)
                {
                    RefreshFromProfile();
                }
            }
#endif
        }

#if UNITY_EDITOR
        private void ApplyPanelVisibility()
        {
            if (_panelGroup == null)
            {
                return;
            }

            _panelGroup.alpha = _isPanelVisible ? 1f : 0f;
            _panelGroup.interactable = _isPanelVisible;
            _panelGroup.blocksRaycasts = _isPanelVisible;
        }

        private void EnsureOperatorIdInput()
        {
            if (operatorIdInput == null || !string.IsNullOrWhiteSpace(operatorIdInput.text))
            {
                return;
            }

            PlayerProfile profile = _storage.Load();
            operatorIdInput.SetTextWithoutNotify(ResolveCurrentOperatorId(profile));
        }

        private void ApplySliderLevel()
        {
            SetLevel(Mathf.RoundToInt(levelSlider != null ? levelSlider.value : 0f));
        }

        private void IncreaseLevel()
        {
            SetLevel(GetCurrentLevel() + 1);
        }

        private void DecreaseLevel()
        {
            SetLevel(GetCurrentLevel() - 1);
        }

        private void SetLevel(int value)
        {
            string trackId = ResolveTrackId();
            if (string.IsNullOrWhiteSpace(trackId))
            {
                if (statusText != null) { statusText.text = "Track ID를 입력하세요."; }
                return;
            }

            PlayerProfile profile = _storage.Load();
            string operatorId = ResolveOperatorId(profile);
            OperatorUpgradeTrack track = FindTrack(operatorId, trackId);
            int maxLevel = track != null ? track.maxLevel : 8;
            profile.SetUpgradeLevel(operatorId, trackId, Mathf.Clamp(value, 0, maxLevel));
            _storage.Save(profile);
            RefreshFromProfile();
        }

        /// <summary>입력된 operatorId가 가진 모든 트랙을 각자의 maxLevel로 세팅한다.</summary>
        private void MaxOutOperator()
        {
            PlayerProfile profile = _storage.Load();
            string operatorId = ResolveOperatorId(profile);
            OperatorUpgradeTrackSet trackSet = FindTrackSet(operatorId);
            if (trackSet == null || trackSet.tracks == null)
            {
                if (statusText != null) { statusText.text = $"'{operatorId}'의 강화 트랙을 찾지 못했습니다."; }
                return;
            }

            foreach (OperatorUpgradeTrack track in trackSet.tracks)
            {
                if (track != null && !string.IsNullOrWhiteSpace(track.trackId))
                {
                    profile.SetUpgradeLevel(operatorId, track.trackId, track.maxLevel);
                }
            }

            _storage.Save(profile);
            RefreshFromProfile();
        }

        /// <summary>입력된 operatorId의 저장된 강화 레코드를 전부 레벨 0으로 되돌린다.</summary>
        private void ResetOperator()
        {
            PlayerProfile profile = _storage.Load();
            string operatorId = ResolveOperatorId(profile);
            profile.operatorUpgrades ??= new List<OperatorUpgradeRecord>();
            foreach (OperatorUpgradeRecord record in profile.operatorUpgrades)
            {
                if (record != null && string.Equals(record.operatorId, operatorId, System.StringComparison.Ordinal))
                {
                    record.level = 0;
                }
            }

            _storage.Save(profile);
            RefreshFromProfile();
        }

        private void RefreshFromProfile()
        {
            if (_storage == null)
            {
                return;
            }

            PlayerProfile profile = _storage.Load();
            string operatorId = ResolveOperatorId(profile);
            if (levelSlider != null)
            {
                levelSlider.SetValueWithoutNotify(GetCurrentLevelFor(profile, operatorId));
            }

            RefreshStatus(profile, operatorId);
        }

        private void RefreshStatus(PlayerProfile profile, string operatorId)
        {
            if (statusText == null)
            {
                return;
            }

            OperatorUpgradeTrackSet trackSet = FindTrackSet(operatorId);
            if (trackSet == null || trackSet.tracks == null || trackSet.tracks.Count == 0)
            {
                statusText.text = $"오퍼레이터  {operatorId}\n강화 트랙 없음 (upgradeTracks 미배선)";
                return;
            }

            var lines = new List<string> { $"오퍼레이터  {operatorId}" };
            foreach (OperatorUpgradeTrack track in trackSet.tracks)
            {
                if (track == null || string.IsNullOrWhiteSpace(track.trackId))
                {
                    continue;
                }

                int level = profile.GetUpgradeLevel(operatorId, track.trackId);
                lines.Add($"{track.trackId}  Lv{Mathf.Clamp(level, 0, track.maxLevel)}/{track.maxLevel}" +
                          $"  ({track.displayName})");
            }

            statusText.text = string.Join("\n", lines);
        }

        private int GetCurrentLevel()
        {
            PlayerProfile profile = _storage.Load();
            return GetCurrentLevelFor(profile, ResolveOperatorId(profile));
        }

        private int GetCurrentLevelFor(PlayerProfile profile, string operatorId)
        {
            string trackId = ResolveTrackId();
            return string.IsNullOrWhiteSpace(trackId) ? 0 : profile.GetUpgradeLevel(operatorId, trackId);
        }

        private string ResolveOperatorId(PlayerProfile profile)
        {
            string input = operatorIdInput != null ? operatorIdInput.text : string.Empty;
            return string.IsNullOrWhiteSpace(input) ? ResolveCurrentOperatorId(profile) : input.Trim();
        }

        private string ResolveTrackId()
        {
            return trackIdInput != null ? trackIdInput.text.Trim() : string.Empty;
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

        private static OperatorUpgradeTrack FindTrack(string operatorId, string trackId)
        {
            return FindTrackSet(operatorId)?.FindByTrackId(trackId);
        }

        /// <summary>
        /// 에디터 전용 디버그 경로라 씬에 로드된 OperatorDefinition을 직접 훑어 operatorId가
        /// 일치하는 강화 트랙 세트를 찾는다 (Addressables 다운로드를 새로 트리거하지 않음).
        /// </summary>
        private static OperatorUpgradeTrackSet FindTrackSet(string operatorId)
        {
            if (string.IsNullOrWhiteSpace(operatorId))
            {
                return null;
            }

            OperatorDefinition[] definitions =
                Resources.FindObjectsOfTypeAll<OperatorDefinition>();
            foreach (OperatorDefinition definition in definitions)
            {
                if (definition != null &&
                    string.Equals(definition.operatorId, operatorId, System.StringComparison.Ordinal))
                {
                    return definition.upgradeTracks;
                }
            }

            return null;
        }
#endif
    }
}
