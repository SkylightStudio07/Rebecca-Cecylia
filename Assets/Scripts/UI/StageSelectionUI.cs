using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Stage;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>챕터별 스테이지 맵 UGUI. 선택한 StageDefinition을 DefenseScene 세션으로 넘긴다.</summary>
    public sealed class StageSelectionUI : MonoBehaviour
    {
        [SerializeField] private StageCatalog catalog;
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup mainMenuGroup;
        [SerializeField] private ModeSelectionUI modeSelectionUI;
        [SerializeField] private ScrollRect nodeScrollRect;
        [SerializeField] private Transform nodeContent;
        [SerializeField] private StageNodeView nodePrefab;
        [SerializeField] private Button previousNodeButton;
        [SerializeField] private Button nextNodeButton;
        [SerializeField, Min(1)] private int visibleNodeCount = 5;
        [SerializeField] private TextMeshProUGUI chapterText;
        [SerializeField] private TextMeshProUGUI selectedTitleText;
        [SerializeField] private TextMeshProUGUI selectedSubtitleText;
        [SerializeField] private TextMeshProUGUI selectedDescriptionText;
        [SerializeField] private TextMeshProUGUI recommendedLevelText;
        [SerializeField] private Image descriptionBackgroundImage;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button startStageButton;
        [SerializeField] private Button backButton;

        private readonly List<StageNodeView> _nodes = new();
        private IProfileStorage _profileStorage;
        private PlayerProfile _profile;
        private int _selectedIndex;

        private void Awake()
        {
            _profileStorage = new PlayerPrefsProfileStorage();
            if (nodeScrollRect != null) { nodeScrollRect.onValueChanged.AddListener(HandleScrollChanged); }
            SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            if (nodeScrollRect != null) { nodeScrollRect.onValueChanged.RemoveListener(HandleScrollChanged); }
        }

        public void Open()
        {
            _profile = _profileStorage.Load();
            _selectedIndex = FindLatestUnlockedIndex();
            SetPanelVisible(true);
            RebuildNodes();
            RenderSelection();
            ScrollToSelected();
            FocusDefaultButton();
        }

        public void Close()
        {
            SetPanelVisible(false);
            if (modeSelectionUI != null)
            {
                modeSelectionUI.Open();
                return;
            }

            SetMainMenuVisible(true);
        }

        public void StartSelectedStage()
        {
            StageCatalogEntry entry = TryGetSelectedEntry();
            if (entry == null || !entry.IsPlayable(_profile?.bestWave ?? 0))
            {
                if (statusText != null) { statusText.text = "선택한 스테이지의 전투 데이터가 준비되지 않았습니다."; }
                return;
            }

            BattleSession.SelectStage(entry.stageDefinition);
            Time.timeScale = 1f;
            SceneManager.LoadScene("DefenseScene");
        }

        public void ScrollPrevious()
        {
            ScrollBy(-1);
        }

        public void ScrollNext()
        {
            ScrollBy(1);
        }

        private void SelectStage(int index)
        {
            if (catalog == null || catalog.entries == null || index < 0 || index >= catalog.entries.Count)
            {
                return;
            }

            StageCatalogEntry entry = catalog.entries[index];
            if (entry == null || !entry.IsUnlocked(_profile?.bestWave ?? 0))
            {
                return;
            }

            _selectedIndex = index;
            RenderSelection();
            FocusDefaultButton();
        }

        private void RebuildNodes()
        {
            ClearNodes();
            if (catalog == null || catalog.entries == null || nodeContent == null || nodePrefab == null)
            {
                return;
            }

            for (int i = 0; i < catalog.entries.Count; i++)
            {
                int index = i;
                StageCatalogEntry entry = catalog.entries[i];
                bool unlocked = entry != null && entry.IsUnlocked(_profile?.bestWave ?? 0);
                StageNodeView node = Instantiate(nodePrefab, nodeContent);
                node.Setup(entry, unlocked, index == _selectedIndex, () => SelectStage(index));
                _nodes.Add(node);
            }

            Canvas.ForceUpdateCanvases();
            if (nodeContent is RectTransform contentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }
            UpdateNavigationButtons();
        }

        private void ClearNodes()
        {
            foreach (StageNodeView node in _nodes)
            {
                if (node != null)
                {
                    Destroy(node.gameObject);
                }
            }

            _nodes.Clear();
        }

        private void RenderSelection()
        {
            StageCatalogEntry entry = TryGetSelectedEntry();
            if (entry == null)
            {
                return;
            }

            if (chapterText != null) { chapterText.text = $"CHAPTER 01  /  {entry.chapterId.ToUpperInvariant()}"; }
            if (selectedTitleText != null) { selectedTitleText.text = entry.displayName; }
            if (selectedSubtitleText != null) { selectedSubtitleText.text = entry.subtitle; }
            if (selectedDescriptionText != null) { selectedDescriptionText.text = entry.description; }
            StageDefinition definition = entry.stageDefinition;
            if (recommendedLevelText != null)
            {
                recommendedLevelText.text = definition != null
                    ? $"RECOMMENDED LV.  {definition.recommendedLevel}"
                    : string.Empty;
            }

            if (descriptionBackgroundImage != null)
            {
                Sprite background = definition != null ? definition.descriptionBackground : null;
                descriptionBackgroundImage.sprite = background;
                descriptionBackgroundImage.enabled = background != null;
            }
            if (statusText != null)
            {
                statusText.text = "스테이지를 선택하면 작전 정보가 표시됩니다.";
            }

            if (startStageButton != null)
            {
                startStageButton.interactable = entry.IsPlayable(_profile?.bestWave ?? 0);
            }
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null) { _nodes[i].SetSelected(i == _selectedIndex); }
            }
        }

        private StageCatalogEntry TryGetSelectedEntry()
        {
            if (catalog == null || catalog.entries == null || _selectedIndex < 0 ||
                _selectedIndex >= catalog.entries.Count)
            {
                return null;
            }

            StageCatalogEntry entry = catalog.entries[_selectedIndex];
            return entry != null && entry.IsUnlocked(_profile?.bestWave ?? 0) ? entry : null;
        }

        private int FindLatestUnlockedIndex()
        {
            if (catalog == null || catalog.entries == null)
            {
                return 0;
            }

            // 선형 CH1 진행에서는 가장 뒤의 해금 노드가 현재 작전 지점이다.
            for (int i = catalog.entries.Count - 1; i >= 0; i--)
            {
                if (catalog.entries[i] != null && catalog.entries[i].IsUnlocked(_profile?.bestWave ?? 0))
                {
                    return i;
                }
            }

            return 0;
        }

        private void ScrollBy(int direction)
        {
            int maxSteps = GetMaxScrollSteps();
            if (nodeScrollRect == null || maxSteps <= 0)
            {
                return;
            }

            float step = 1f / maxSteps;
            nodeScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
                nodeScrollRect.horizontalNormalizedPosition + direction * step);
            UpdateNavigationButtons();
        }

        private void ScrollToSelected()
        {
            int maxSteps = GetMaxScrollSteps();
            if (nodeScrollRect == null || maxSteps <= 0)
            {
                if (nodeScrollRect != null) { nodeScrollRect.horizontalNormalizedPosition = 0f; }
                UpdateNavigationButtons();
                return;
            }

            int firstVisibleIndex = Mathf.Clamp(_selectedIndex - visibleNodeCount + 1, 0, maxSteps);
            nodeScrollRect.horizontalNormalizedPosition = firstVisibleIndex / (float)maxSteps;
            UpdateNavigationButtons();
        }

        private int GetMaxScrollSteps()
        {
            return Mathf.Max(0, _nodes.Count - visibleNodeCount);
        }

        private void HandleScrollChanged(Vector2 _)
        {
            UpdateNavigationButtons();
        }

        private void UpdateNavigationButtons()
        {
            int maxSteps = GetMaxScrollSteps();
            bool canScroll = nodeScrollRect != null && maxSteps > 0;
            if (previousNodeButton != null)
            {
                previousNodeButton.gameObject.SetActive(canScroll);
                previousNodeButton.interactable = canScroll && nodeScrollRect.horizontalNormalizedPosition > 0.001f;
            }
            if (nextNodeButton != null)
            {
                nextNodeButton.gameObject.SetActive(canScroll);
                nextNodeButton.interactable = canScroll && nodeScrollRect.horizontalNormalizedPosition < 0.999f;
            }
        }

        private void SetPanelVisible(bool visible)
        {
            if (panel != null) { panel.SetActive(visible); }
            SetMainMenuVisible(!visible);
        }

        private void SetMainMenuVisible(bool visible)
        {
            if (mainMenuGroup == null)
            {
                return;
            }

            mainMenuGroup.interactable = visible;
            mainMenuGroup.blocksRaycasts = visible;
        }

        private void FocusDefaultButton()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            Button target = backButton;
            if (startStageButton != null && startStageButton.interactable)
            {
                target = startStageButton;
            }

            if (target != null && target.interactable)
            {
                EventSystem.current.SetSelectedGameObject(target.gameObject);
            }
        }
    }
}
