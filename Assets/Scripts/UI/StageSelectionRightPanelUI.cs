using System;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Stage;
using TMPro;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>StageSelectionRightPanel의 경량 카탈로그 정보 렌더러.</summary>
    public sealed class StageSelectionRightPanelUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI stageTitleText;
        [SerializeField] private TextMeshProUGUI recommendedLevelText;
        [SerializeField] private TextMeshProUGUI missionBriefingText;
        [SerializeField] private StageSelectionPreviewSlot[] enemySlots = Array.Empty<StageSelectionPreviewSlot>();
        [SerializeField] private StageSelectionPreviewSlot[] rewardSlots = Array.Empty<StageSelectionPreviewSlot>();
        [SerializeField] private Sprite goldSprite;
        [SerializeField, Min(0)] private int defaultGoldReward = 100;

        public void Render(StageCatalogEntry stage, PlayerProfile profile,
            EnemyCatalog enemyCatalog, OperatorCatalog operatorCatalog)
        {
            if (stage == null) { return; }
            if (stageTitleText != null) { stageTitleText.text = $"{stage.displayName}  {stage.subtitle}"; }
            if (recommendedLevelText != null)
            {
                recommendedLevelText.text = $"RECOMMENDED LV.  {stage.recommendedLevel}";
            }
            if (missionBriefingText != null) { missionBriefingText.text = stage.description; }

            RenderEnemies(stage, enemyCatalog);
            RenderRewards(stage, profile, operatorCatalog);
        }

        private void RenderEnemies(StageCatalogEntry stage, EnemyCatalog enemyCatalog)
        {
            HideAll(enemySlots);
            if (stage.enemyPreviews == null) { return; }
            int count = Mathf.Min(stage.enemyPreviews.Count, enemySlots.Length);
            for (int i = 0; i < count; i++)
            {
                StageEnemyPreview preview = stage.enemyPreviews[i];
                EnemyCatalogEntry enemy = enemyCatalog != null ? enemyCatalog.FindById(preview.enemyId) : null;
                string name = enemy != null && !string.IsNullOrWhiteSpace(enemy.displayName)
                    ? enemy.displayName
                    : preview.enemyId;
                enemySlots[i].Show(enemy != null ? enemy.previewSprite : null, name, $"x{preview.totalCount}");
            }
        }

        private void RenderRewards(StageCatalogEntry stage, PlayerProfile profile, OperatorCatalog operatorCatalog)
        {
            HideAll(rewardSlots);
            int index = 0;
            if (!ContainsGoldReward(stage) && index < rewardSlots.Length)
            {
                // 결과 화면의 기본 100 + 생존 시간 보너스는 선택 시점에 확정할 수 없으므로
                // 최소 보장량과 추가 보상이 있음을 함께 표시한다.
                rewardSlots[index++].Show(goldSprite, "GOLD", $"{defaultGoldReward}+");
            }
            if (stage.rewards != null)
            {
                for (int i = 0; i < stage.rewards.Count && index < rewardSlots.Length; i++)
                {
                    StageReward reward = stage.rewards[i];
                    if (reward == null) { continue; }
                    Sprite icon = IsGold(reward.rewardId) ? goldSprite : reward.icon;
                    rewardSlots[index++].Show(icon, reward.displayName, $"x{reward.amount}");
                }
            }

            OperatorCatalogEntry operatorReward = FindOperatorReward(stage.stageId, operatorCatalog);
            if (operatorReward == null || index >= rewardSlots.Length) { return; }
            Sprite portrait = operatorReward.unlockRewardPortrait != null
                ? operatorReward.unlockRewardPortrait
                : operatorReward.previewPortrait;
            string portraitAddress = !string.IsNullOrEmpty(operatorReward.unlockRewardPortraitAddress)
                ? operatorReward.unlockRewardPortraitAddress
                : operatorReward.previewPortraitAddress;
            string state = operatorReward.IsUnlocked(profile) ? "획득 완료" : "합류";
            rewardSlots[index].ShowRemote(portrait, portraitAddress, operatorReward.displayName, state);
        }

        private static bool IsGold(string rewardId)
        {
            return string.Equals(rewardId, "gold", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rewardId, "commodity", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsGoldReward(StageCatalogEntry stage)
        {
            if (stage.rewards == null) { return false; }
            for (int i = 0; i < stage.rewards.Count; i++)
            {
                StageReward reward = stage.rewards[i];
                if (reward != null && IsGold(reward.rewardId)) { return true; }
            }
            return false;
        }

        private static OperatorCatalogEntry FindOperatorReward(string stageId, OperatorCatalog catalog)
        {
            if (catalog == null || catalog.entries == null) { return null; }
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                OperatorCatalogEntry entry = catalog.entries[i];
                if (entry != null && entry.IsStageRewardFor(stageId)) { return entry; }
            }
            return null;
        }

        private static void HideAll(StageSelectionPreviewSlot[] slots)
        {
            if (slots == null) { return; }
            foreach (StageSelectionPreviewSlot slot in slots)
            {
                if (slot != null) { slot.Hide(); }
            }
        }
    }
}
