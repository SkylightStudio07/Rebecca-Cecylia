using System.Collections;
using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 로비의 Operators 메뉴에서 활성 오퍼레이터를 관리한다. 전투 진입은 기존
    /// OperatorSelectionUI가 계속 담당하며, 이 화면의 Deploy는 프로필과 세션 선택만 바꾼다.
    /// </summary>
    public sealed class OperatorManagementUI : MonoBehaviour
    {
        [SerializeField] private OperatorCatalog catalog;
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup mainMenuGroup;
        [SerializeField] private Transform cardContent;
        [SerializeField] private OperatorManagementCardView cardPrefab;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI unlockText;
        [SerializeField] private TextMeshProUGUI affinityText;
        [SerializeField] private TextMeshProUGUI registeredText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Slider downloadProgress;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button deployButton;
        [SerializeField] private Button backButton;
        [SerializeField] private LobbyOperatorDialogueUI lobbyDialogueUI;

        private readonly List<OperatorManagementCardView> _cards = new();
        private IProfileStorage _profileStorage;
        private PlayerProfile _profile;
        private int _browsingIndex;
        private bool _isLoading;

        private void Awake()
        {
            _profileStorage = new PlayerPrefsProfileStorage();
            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            if (previousButton != null) { previousButton.onClick.AddListener(Previous); }
            if (nextButton != null) { nextButton.onClick.AddListener(Next); }
            if (deployButton != null) { deployButton.onClick.AddListener(Deploy); }
            if (backButton != null) { backButton.onClick.AddListener(Close); }
        }

        private void OnDisable()
        {
            if (previousButton != null) { previousButton.onClick.RemoveListener(Previous); }
            if (nextButton != null) { nextButton.onClick.RemoveListener(Next); }
            if (deployButton != null) { deployButton.onClick.RemoveListener(Deploy); }
            if (backButton != null) { backButton.onClick.RemoveListener(Close); }
        }

        public void Open()
        {
            if (catalog == null || catalog.entries == null)
            {
                Debug.LogError("[OperatorManagement] OperatorCatalog가 비어 있습니다.", this);
                return;
            }

            _profile = _profileStorage.Load();
            SelectSavedOrFirst();
            SetPanelVisible(true);
            RebuildCards();
            RenderSelection();
            FocusCurrentCard();
        }

        public void Close()
        {
            if (_isLoading) { return; }
            SetPanelVisible(false);
            if (EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(null); }
        }

        public void Previous() { MoveSelection(-1); }
        public void Next() { MoveSelection(1); }

        public void Deploy()
        {
            if (_isLoading || !TryGetBrowsingEntry(out OperatorCatalogEntry entry) ||
                !entry.IsUnlocked(_profile.bestWave))
            {
                return;
            }

            StartCoroutine(DeployRoutine(entry));
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) { return; }
            if (_isLoading || panel == null || !panel.activeInHierarchy) { return; }

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            if ((keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)) ||
                (gamepad != null && (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame)))
            {
                Previous();
                return;
            }

            if ((keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)) ||
                (gamepad != null && (gamepad.dpad.right.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame)))
            {
                Next();
                return;
            }

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
            {
                Close();
            }
        }

        private IEnumerator DeployRoutine(OperatorCatalogEntry entry)
        {
            _isLoading = true;
            UpdateButtons();
            bool succeeded = false;
            yield return OperatorContentLoader.LoadAndSelect(entry, SetLoading,
                _ => succeeded = true, FailLoading);

            if (!succeeded)
            {
                yield break;
            }

            _profile.selectedOperatorId = entry.operatorId;
            _profileStorage.Save(_profile);
            _isLoading = false;
            SetLoading("활성 오퍼레이터가 변경되었습니다.", 1f);
            RefreshCards();
            if (lobbyDialogueUI != null) { lobbyDialogueUI.RefreshOperator(); }
            UpdateButtons();
        }

        private void MoveSelection(int offset)
        {
            if (catalog == null || catalog.entries == null) { return; }
            int slotCount = GetSlotCount();
            _browsingIndex = (_browsingIndex + offset + slotCount) % slotCount;
            RenderSelection();
            FocusCurrentCard();
        }

        private void SelectCard(int index)
        {
            if (_isLoading || index < 0 || index >= _cards.Count) { return; }
            _browsingIndex = index;
            RenderSelection();
            FocusCurrentCard();
        }

        private void SelectSavedOrFirst()
        {
            int saved = catalog.FindIndex(_profile.selectedOperatorId);
            _browsingIndex = saved >= 0 ? saved : Mathf.Max(0, catalog.FindFirstUnlockedIndex(_profile.bestWave));
        }

        private void RebuildCards()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] != null) { Destroy(_cards[i].gameObject); }
            }
            _cards.Clear();

            if (cardContent == null || cardPrefab == null) { return; }
            for (int i = 0; i < GetSlotCount(); i++)
            {
                int capturedIndex = i;
                OperatorManagementCardView view = Instantiate(cardPrefab, cardContent);
                _cards.Add(view);
                SetupCard(view, i, capturedIndex);
            }
        }

        private void RefreshCards()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                SetupCard(_cards[i], i, i);
            }
        }

        private void SetupCard(OperatorManagementCardView view, int index, int callbackIndex)
        {
            if (view == null || index < 0 || index >= GetSlotCount()) { return; }
            OperatorCatalogEntry entry = index < catalog.entries.Count ? catalog.entries[index] : null;
            bool unlocked = entry != null && entry.IsUnlocked(_profile.bestWave);
            bool active = entry != null && entry.operatorId == _profile.selectedOperatorId;
            int affinity = entry != null ? _profile.GetOperatorAffinity(entry.operatorId) : 0;
            view.Setup(entry, index, unlocked, active, index == _browsingIndex, affinity,
                () => SelectCard(callbackIndex));
        }

        private void RenderSelection()
        {
            if (!TryGetBrowsingEntry(out OperatorCatalogEntry entry))
            {
                if (nameText != null) { nameText.text = "UNASSIGNED"; }
                if (descriptionText != null) { descriptionText.text = "RESERVED OPERATOR SLOT"; }
                if (unlockText != null) { unlockText.text = "LOCKED SLOT"; }
                if (affinityText != null) { affinityText.text = string.Empty; }
                if (statusText != null) { statusText.text = "아직 등록되지 않은 오퍼레이터 슬롯입니다."; }
                UpdateRegisteredText();
                UpdateBrowsingVisuals();
                UpdateButtons();
                return;
            }
            bool unlocked = entry.IsUnlocked(_profile.bestWave);
            if (nameText != null) { nameText.text = entry.displayName; }
            if (descriptionText != null) { descriptionText.text = entry.playStyleDescription; }
            if (unlockText != null)
            {
                unlockText.text = unlocked ? (entry.remoteContent ? "REMOTE CONTENT" : "LOCAL OPERATOR")
                    : $"BEST WAVE {entry.requiredBestWave} 달성 시 해금";
            }
            if (affinityText != null)
            {
                affinityText.text = $"AFFINITY  {_profile.GetOperatorAffinity(entry.operatorId):000} / 100";
            }
            if (registeredText != null)
            {
                int unlockedCount = 0;
                for (int i = 0; i < catalog.entries.Count; i++)
                {
                    if (catalog.entries[i] != null && catalog.entries[i].IsUnlocked(_profile.bestWave)) { unlockedCount++; }
                }
                registeredText.text = $"{unlockedCount:00} / {GetSlotCount():00}\nREGISTERED";
            }
            if (statusText != null)
            {
                statusText.text = unlocked ? "DEPLOY를 눌러 활성 오퍼레이터로 지정합니다." : "잠금 조건을 충족해야 배치할 수 있습니다.";
            }
            if (downloadProgress != null) { downloadProgress.value = 0f; }

            UpdateBrowsingVisuals();
            UpdateButtons();
        }

        private void UpdateRegisteredText()
        {
            if (registeredText == null || catalog == null || catalog.entries == null) { return; }
            int unlockedCount = 0;
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                if (catalog.entries[i] != null && catalog.entries[i].IsUnlocked(_profile.bestWave)) { unlockedCount++; }
            }
            registeredText.text = $"{unlockedCount:00} / {GetSlotCount():00}\nREGISTERED";
        }

        private void UpdateBrowsingVisuals()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] != null) { _cards[i].SetBrowsing(i == _browsingIndex); }
            }
        }

        private void SetLoading(string message, float progress)
        {
            if (statusText != null) { statusText.text = message; }
            if (downloadProgress != null) { downloadProgress.value = Mathf.Clamp01(progress); }
        }

        private void FailLoading(string message)
        {
            _isLoading = false;
            SetLoading(message, 0f);
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool hasEntry = TryGetBrowsingEntry(out OperatorCatalogEntry entry);
            bool unlocked = hasEntry && entry.IsUnlocked(_profile?.bestWave ?? 0);
            bool canNavigate = !_isLoading && catalog != null && GetSlotCount() > 1;
            bool alreadyActive = hasEntry && entry.operatorId == _profile?.selectedOperatorId;
            if (previousButton != null) { previousButton.interactable = canNavigate; }
            if (nextButton != null) { nextButton.interactable = canNavigate; }
            if (deployButton != null) { deployButton.interactable = !_isLoading && unlocked && !alreadyActive; }
            if (backButton != null) { backButton.interactable = !_isLoading; }
        }

        private bool TryGetBrowsingEntry(out OperatorCatalogEntry entry)
        {
            entry = null;
            if (catalog == null || catalog.entries == null || _browsingIndex < 0 ||
                _browsingIndex >= GetSlotCount() || _browsingIndex >= catalog.entries.Count) { return false; }
            entry = catalog.entries[_browsingIndex];
            return entry != null;
        }

        private int GetSlotCount()
        {
            // 잠금 슬롯은 실제 등록 오퍼레이터 수와 무관하게 항상 6개를 유지한다.
            // 화면용 예약 슬롯이므로 Catalog에 가짜 Definition을 추가하지 않는다.
            return catalog == null || catalog.entries == null ? 6 : catalog.entries.Count + 6;
        }

        private void FocusCurrentCard()
        {
            if (EventSystem.current == null || _browsingIndex < 0 || _browsingIndex >= _cards.Count) { return; }
            Button target = _cards[_browsingIndex] != null ? _cards[_browsingIndex].Button : null;
            if (target != null) { EventSystem.current.SetSelectedGameObject(target.gameObject); }
        }

        private void SetPanelVisible(bool visible)
        {
            if (panel != null) { panel.SetActive(visible); }
            if (mainMenuGroup != null)
            {
                mainMenuGroup.interactable = !visible;
                mainMenuGroup.blocksRaycasts = !visible;
            }
        }
    }
}
