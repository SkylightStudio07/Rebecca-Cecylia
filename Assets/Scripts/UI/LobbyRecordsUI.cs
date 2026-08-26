using System;
using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Stage;
using RCCom.Runtime;
using TMPro;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>
    /// 로비에서 계정의 스테이지 클리어 기록을 읽어 표시한다.
    /// 전투 결과는 PlayerProfile이 기록하고 이 화면은 읽기만 해 UI→데이터 단방향을 유지한다.
    /// </summary>
    public sealed class LobbyRecordsUI : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuBackground;
        [SerializeField] private GameObject recordsPanelBackground;
        [SerializeField] private UnityEngine.UI.Button recordsEntryButton;
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField] private StageCatalog stageCatalog;
        [SerializeField] private StageRecordItemView itemPrefab;
        [SerializeField] private RectTransform itemContent;
        [SerializeField] private TMP_Text completionText;
        [SerializeField] private TMP_Text bestWaveText;
        [SerializeField] private TMP_Text currentObjectiveText;
        [SerializeField] private TMP_Text archiveStatusText;
        [SerializeField] private UnityEngine.UI.Image completionFillImage;

        private readonly List<StageRecordItemView> _items = new();
        private IProfileStorage _profileStorage;

        private void Awake()
        {
            _profileStorage = new PlayerPrefsProfileStorage();
            if (recordsEntryButton != null) { recordsEntryButton.onClick.AddListener(Open); }
            if (backButton != null) { backButton.onClick.AddListener(Close); }
            if (recordsPanelBackground != null) { recordsPanelBackground.SetActive(false); }
        }

        private void OnDestroy()
        {
            if (recordsEntryButton != null) { recordsEntryButton.onClick.RemoveListener(Open); }
            if (backButton != null) { backButton.onClick.RemoveListener(Close); }
        }

        public void Open()
        {
            UILoadingTransition.Run(OpenCovered);
        }

        public void Close()
        {
            UILoadingTransition.Run(CloseCovered);
        }

        private void OpenCovered()
        {
            if (mainMenuBackground != null) { mainMenuBackground.SetActive(false); }
            if (recordsPanelBackground != null)
            {
                recordsPanelBackground.SetActive(true);
                recordsPanelBackground.transform.SetAsLastSibling();
            }

            Render();
        }

        private void CloseCovered()
        {
            if (recordsPanelBackground != null) { recordsPanelBackground.SetActive(false); }
            if (mainMenuBackground != null) { mainMenuBackground.SetActive(true); }
        }

        private void Render()
        {
            _profileStorage ??= new PlayerPrefsProfileStorage();
            PlayerProfile profile = _profileStorage.Load();
            ClearItems();

            int totalCount = 0;
            int clearedCount = 0;
            StageCatalogEntry nextObjective = null;
            if (stageCatalog != null && stageCatalog.entries != null)
            {
                for (int i = 0; i < stageCatalog.entries.Count; i++)
                {
                    StageCatalogEntry entry = stageCatalog.entries[i];
                    if (entry == null || !string.Equals(entry.chapterId, "ch1",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool cleared = profile.HasClearedStage(entry.stageId);
                    bool unlocked = entry.IsUnlocked(profile.bestWave);
                    totalCount++;
                    if (cleared) { clearedCount++; }
                    else if (nextObjective == null && unlocked) { nextObjective = entry; }

                    if (itemPrefab != null && itemContent != null)
                    {
                        StageRecordItemView item = Instantiate(itemPrefab, itemContent);
                        item.Bind(entry, cleared, unlocked);
                        _items.Add(item);
                    }
                }
            }

            float ratio = totalCount > 0 ? (float)clearedCount / totalCount : 0f;
            if (completionText != null) { completionText.text = $"{clearedCount:00} / {totalCount:00}"; }
            if (bestWaveText != null) { bestWaveText.text = $"WAVE {Mathf.Max(0, profile.bestWave):00}"; }
            if (completionFillImage != null) { completionFillImage.fillAmount = ratio; }
            if (currentObjectiveText != null)
            {
                currentObjectiveText.text = nextObjective != null
                    ? $"{nextObjective.displayName}  {nextObjective.subtitle}"
                    : totalCount > 0 && clearedCount >= totalCount
                        ? "CHAPTER 01 COMPLETE"
                        : "NO AVAILABLE OPERATION";
            }

            if (archiveStatusText != null)
            {
                archiveStatusText.text = totalCount == 0
                    ? "NO STAGE CATALOG DATA"
                    : $"LOCAL PROFILE SYNCHRONIZED  //  {Mathf.RoundToInt(ratio * 100f):000}%";
            }
        }

        private void ClearItems()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null) { Destroy(_items[i].gameObject); }
            }

            _items.Clear();
        }
    }
}
