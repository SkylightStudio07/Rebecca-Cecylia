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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 로컬 카탈로그를 순회하고 선택한 Definition만 Addressables로 내려받아 게임을 시작한다.
    /// 미리보기 초상화는 선택 사항이라 아트가 늦어져도 코드·씬 배선을 먼저 끝낼 수 있다.
    /// </summary>
    public sealed class OperatorSelectionUI : MonoBehaviour
    {
        [SerializeField] private OperatorCatalog catalog;
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup mainMenuGroup;
        [SerializeField] private Transform cardContent;
        [SerializeField] private OperatorSelectionCard cardPrefab;
        [SerializeField] private Transform rosterContent;
        [SerializeField] private OperatorRosterPreviewItem rosterItemPrefab;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI unlockText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Slider downloadProgress;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;
        [SerializeField] private ModeSelectionUI modeSelectionUI;
        [SerializeField] private string defenseSceneName = "DefenseScene";

        private IProfileStorage _profileStorage;
        private PlayerProfile _profile;
        private int _selectedIndex;
        private bool _isLoading;
        private readonly List<OperatorSelectionCard> _cards = new();
        private readonly List<OperatorRosterPreviewItem> _rosterItems = new();

        private void Awake()
        {
            _profileStorage = new PlayerPrefsProfileStorage();
            _profile = _profileStorage.Load();
            SelectSavedOrFirstUnlocked();
            SetPanelVisible(false);
        }

        public void Open()
        {
            if (catalog == null || catalog.entries == null || catalog.entries.Count == 0)
            {
                Debug.LogError("[OperatorSelection] OperatorCatalog가 비어 있어 선택 화면을 열 수 없습니다.", this);
                return;
            }

            _profile = _profileStorage.Load();
            SelectSavedOrFirstUnlocked();
            SetPanelVisible(true);
            RenderSelection();
            FocusDefaultButton();
        }

        public void Close()
        {
            if (_isLoading)
            {
                return;
            }

            SetPanelVisible(false);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void Previous()
        {
            MoveSelection(-1);
        }

        public void Next()
        {
            MoveSelection(1);
        }

        public void Confirm()
        {
            if (_isLoading || !TryGetSelectedEntry(out OperatorCatalogEntry entry) ||
                !entry.IsUnlocked(_profile.bestWave))
            {
                return;
            }

            StartCoroutine(LoadAndStart(entry));
        }

        private void Update()
        {
            // 타이틀 화면에서도 향후 일시정지 오버레이가 추가될 수 있으므로 입력 폴링은
            // 프로젝트 공통 규칙대로 최상단에서 차단한다.
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (_isLoading || panel == null || !panel.activeInHierarchy)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            if ((keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)) ||
                (gamepad != null && (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame)))
            {
                Previous();
                FocusDefaultButton();
                return;
            }

            if ((keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)) ||
                (gamepad != null && (gamepad.dpad.right.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame)))
            {
                Next();
                FocusDefaultButton();
                return;
            }

            if ((keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                                      keyboard.spaceKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
            {
                Confirm();
                return;
            }

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
            {
                Close();
            }
        }

        private IEnumerator LoadAndStart(OperatorCatalogEntry entry)
        {
            _isLoading = true;
            UpdateButtons();
            yield return OperatorContentLoader.LoadAndSelect(entry,
                (message, progress) => SetLoading(true, message, progress),
                _ => SaveSelectionAndOpenModeSelection(entry.operatorId), FailLoading);
        }

        private void SaveSelectionAndOpenModeSelection(string operatorId)
        {
            _profile.selectedOperatorId = operatorId;
            _profileStorage.Save(_profile);
            Time.timeScale = 1f;

            _isLoading = false;
            SetPanelVisible(false);
            if (modeSelectionUI != null)
            {
                modeSelectionUI.Open(operatorId);
                return;
            }

            // 기존 배선이 남아 있는 씬에서도 선택 기능이 완전히 끊기지 않도록 레거시 폴백을 둔다.
            SceneManager.LoadScene(defenseSceneName);
        }

        private void FailLoading(string message)
        {
            _isLoading = false;
            if (statusText != null)
            {
                statusText.text = message;
            }

            UpdateButtons();
        }

        private void MoveSelection(int offset)
        {
            if (_isLoading || catalog == null || catalog.entries == null || catalog.entries.Count == 0)
            {
                return;
            }

            _selectedIndex = (_selectedIndex + offset + catalog.entries.Count) % catalog.entries.Count;
            RenderSelection();
        }

        private void RebuildCards()
        {
            ClearCards();

            if (cardContent == null || cardPrefab == null || !TryGetSelectedEntry(out OperatorCatalogEntry entry))
            {
                return;
            }

            OperatorSelectionCard card = Instantiate(cardPrefab, cardContent);
            card.Setup(entry, entry.IsUnlocked(_profile?.bestWave ?? 0), true, FocusDefaultButton);
            _cards.Add(card);
        }

        private void ClearCards()
        {
            foreach (OperatorSelectionCard card in _cards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }

            _cards.Clear();
        }

        private void RebuildRosterPreview(OperatorCatalogEntry entry)
        {
            ClearRosterPreview();
            if (entry == null || rosterContent == null || rosterItemPrefab == null || entry.unitPreviews == null)
            {
                return;
            }

            foreach (OperatorUnitPreview preview in entry.unitPreviews)
            {
                if (preview == null)
                {
                    continue;
                }

                OperatorRosterPreviewItem item = Instantiate(rosterItemPrefab, rosterContent);
                item.Setup(preview);
                _rosterItems.Add(item);
            }
        }

        private void ClearRosterPreview()
        {
            foreach (OperatorRosterPreviewItem item in _rosterItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            _rosterItems.Clear();
        }

        private void SelectSavedOrFirstUnlocked()
        {
            if (catalog == null || catalog.entries == null || catalog.entries.Count == 0)
            {
                _selectedIndex = 0;
                return;
            }

            int savedIndex = catalog.FindIndex(_profile?.selectedOperatorId);
            if (savedIndex >= 0 && catalog.entries[savedIndex].IsUnlocked(_profile.bestWave))
            {
                _selectedIndex = savedIndex;
                return;
            }

            int firstUnlocked = catalog.FindFirstUnlockedIndex(_profile?.bestWave ?? 0);
            _selectedIndex = firstUnlocked >= 0 ? firstUnlocked : 0;
        }

        private void RenderSelection()
        {
            if (!TryGetSelectedEntry(out OperatorCatalogEntry entry))
            {
                return;
            }

            bool unlocked = entry.IsUnlocked(_profile.bestWave);
            if (portraitImage != null)
            {
                portraitImage.sprite = entry.previewPortrait;
                portraitImage.enabled = entry.previewPortrait != null;
            }

            if (nameText != null) { nameText.text = entry.displayName; }
            if (descriptionText != null) { descriptionText.text = entry.playStyleDescription; }
            if (unlockText != null)
            {
                unlockText.text = unlocked ? (entry.remoteContent ? "다운로드 콘텐츠" : "사용 가능") :
                    $"최고 웨이브 {entry.requiredBestWave} 도달 시 해금";
            }

            if (statusText != null) { statusText.text = unlocked ? "선택하면 필요한 콘텐츠를 확인합니다." : "잠긴 오퍼레이터입니다."; }
            if (downloadProgress != null) { downloadProgress.value = 0f; }
            RebuildCards();
            RebuildRosterPreview(entry);
            RefreshCardSelection();
            UpdateButtons();
        }

        private void RefreshCardSelection()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] != null)
                {
                    _cards[i].SetSelected(true);
                }
            }
        }

        private bool TryGetSelectedEntry(out OperatorCatalogEntry entry)
        {
            entry = null;
            if (catalog == null || catalog.entries == null ||
                _selectedIndex < 0 || _selectedIndex >= catalog.entries.Count)
            {
                return false;
            }

            entry = catalog.entries[_selectedIndex];
            return entry != null;
        }

        private void SetLoading(bool loading, string message, float progress)
        {
            _isLoading = loading;
            if (statusText != null) { statusText.text = message; }
            if (downloadProgress != null) { downloadProgress.value = Mathf.Clamp01(progress); }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool hasEntry = TryGetSelectedEntry(out OperatorCatalogEntry entry);
            bool unlocked = hasEntry && entry.IsUnlocked(_profile?.bestWave ?? 0);
            bool canNavigate = !_isLoading && catalog != null && catalog.entries.Count > 1;

            if (previousButton != null) { previousButton.interactable = canNavigate; }
            if (nextButton != null) { nextButton.interactable = canNavigate; }
            if (confirmButton != null) { confirmButton.interactable = !_isLoading && unlocked; }
            if (backButton != null) { backButton.interactable = !_isLoading; }
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

        private void FocusDefaultButton()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            Button target = confirmButton != null && confirmButton.interactable ? confirmButton : backButton;
            if (target != null && target.interactable)
            {
                EventSystem.current.SetSelectedGameObject(target.gameObject);
            }
        }
    }
}
