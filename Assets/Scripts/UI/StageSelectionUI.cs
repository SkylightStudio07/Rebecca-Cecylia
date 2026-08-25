using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Enemy;
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
        [SerializeField] private OperatorCatalog operatorCatalog;
        [SerializeField] private EnemyCatalog enemyCatalog;
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
        [Header("스테이지 오퍼레이터 보상")]
        [SerializeField] private GameObject operatorRewardPanel;
        [SerializeField] private Image operatorRewardPortrait;
        [SerializeField] private TextMeshProUGUI operatorRewardNameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private StageSelectionRightPanelUI rightPanel;
        [SerializeField] private Button startStageButton;
        [SerializeField] private Button backButton;

        private readonly List<StageNodeView> _nodes = new();
        private IProfileStorage _profileStorage;
        private PlayerProfile _profile;
        private int _selectedIndex;
        private bool _isLoadingStage;

        private void Awake()
        {
            // 빌드 이후 원격으로 추가된 오퍼레이터까지 포함한 카탈로그로 바꾼다.
            // 원격 카탈로그가 아직 안 왔거나 추가분이 없으면 내장본을 그대로 돌려준다.
            operatorCatalog = LiveCatalogService.Resolve(operatorCatalog);
            catalog = LiveCatalogService.Resolve(catalog);
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
            operatorCatalog = LiveCatalogService.Resolve(operatorCatalog);
            catalog = LiveCatalogService.Resolve(catalog);
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
            UILoadingTransition.Run(CloseCovered);
        }

        private void CloseCovered()
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

            // 원격 스테이지는 이 시점에야 Definition을 내려받는다. 로딩 중 버튼을 다시 눌러
            // 코루틴이 겹치면 씬을 두 번 로드하게 되므로 게이트를 둔다.
            if (_isLoadingStage)
            {
                return;
            }

            _isLoadingStage = true;
            Time.timeScale = 1f;
            StartCoroutine(LoadStageAndEnter(entry));
        }

        private System.Collections.IEnumerator LoadStageAndEnter(StageCatalogEntry entry)
        {
            StageDefinition definition = null;
            string failure = null;
            yield return StageContentLoader.Load(
                entry,
                (message, _) => { if (statusText != null) { statusText.text = message; } },
                loaded => definition = loaded,
                error => failure = error);

            if (definition == null)
            {
                _isLoadingStage = false;
                if (statusText != null)
                {
                    statusText.text = failure ?? "스테이지를 불러오지 못했습니다.";
                }

                RenderSelection();
                yield break;
            }

            BattleSession.SelectStage(definition);
            // 스테이지 전용 적을 먼저 확보해야 원격 Definition도 첫 웨이브부터 즉시 해석할 수 있다.
            yield return BattleContentCache.PreloadEnemiesForStage(BattleSession.SelectedStage, null, null);
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
            // Definition은 읽지 않는다. 원격 스테이지는 선택을 확정해야 내려받으므로,
            // 목록과 브리핑은 카탈로그에 복사해 둔 경량 값만으로 그려져야 한다.
            if (recommendedLevelText != null)
            {
                recommendedLevelText.text = $"RECOMMENDED LV.  {entry.recommendedLevel}";
            }

            if (descriptionBackgroundImage != null)
            {
                // 원격 스테이지의 배경은 Definition과 다른 번들에 있어 한 장만 먼저 받을 수 있다.
                RemotePreviewSpriteLoader.LoadInto(
                    descriptionBackgroundImage, entry.descriptionBackground,
                    entry.descriptionBackgroundAddress, Color.clear);
            }

            if (rightPanel != null)
            {
                rightPanel.Render(entry, _profile, enemyCatalog, operatorCatalog);
                if (operatorRewardPanel != null) { operatorRewardPanel.SetActive(false); }
            }
            else
            {
                RenderOperatorReward(entry.stageId);
            }
            if (statusText != null)
            {
                statusText.text = "스테이지를 선택하면 작전 정보가 표시됩니다.";
            }

            if (startStageButton != null)
            {
                startStageButton.interactable = !_isLoadingStage && entry.IsPlayable(_profile?.bestWave ?? 0);
            }
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null) { _nodes[i].SetSelected(i == _selectedIndex); }
            }
        }

        private void RenderOperatorReward(string stageId)
        {
            OperatorCatalogEntry rewardEntry = FindOperatorReward(stageId);
            bool hasReward = rewardEntry != null;
            if (operatorRewardPanel != null) { operatorRewardPanel.SetActive(hasReward); }
            if (!hasReward)
            {
                return;
            }

            if (operatorRewardPortrait != null)
            {
                Sprite localSprite = rewardEntry.unlockRewardPortrait != null
                    ? rewardEntry.unlockRewardPortrait
                    : rewardEntry.previewPortrait;
                string remoteAddress = !string.IsNullOrEmpty(rewardEntry.unlockRewardPortraitAddress)
                    ? rewardEntry.unlockRewardPortraitAddress
                    : rewardEntry.previewPortraitAddress;
                RemotePreviewSpriteLoader.LoadInto(operatorRewardPortrait, localSprite, remoteAddress, Color.clear);
            }

            if (operatorRewardNameText != null)
            {
                string state = rewardEntry.IsUnlocked(_profile) ? "획득 완료" : "클리어 보상";
                operatorRewardNameText.text = $"{state}\n{rewardEntry.displayName}";
            }
        }

        private OperatorCatalogEntry FindOperatorReward(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId) || operatorCatalog == null || operatorCatalog.entries == null)
            {
                return null;
            }

            for (int i = 0; i < operatorCatalog.entries.Count; i++)
            {
                OperatorCatalogEntry entry = operatorCatalog.entries[i];
                if (entry != null && entry.IsStageRewardFor(stageId))
                {
                    return entry;
                }
            }

            return null;
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
            if (rightPanel != null) { rightPanel.gameObject.SetActive(visible); }
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
