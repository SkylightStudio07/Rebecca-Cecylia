using System.Collections.Generic;
using RCCom.Data;
using UnityEngine;

namespace RCCom.Definitions.Stage
{
    /// <summary>
    /// 챕터 맵을 구성하는 스테이지 목록. UI는 이 카탈로그만 읽으며 전투 웨이브 데이터는 별도 Definition이 소유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Stage/Stage Catalog")]
    public sealed class StageCatalog : ScriptableObject
    {
        /// <summary>
        /// 빌드 이후에 추가된 스테이지만 담아 원격으로 배송되는 카탈로그의 Addressables 주소.
        /// 런타임과 에디터가 같은 문자열을 봐야 하는데 런타임은 에디터 어셈블리를 참조할 수
        /// 없으므로 정본을 여기 둔다(OperatorCatalog.LiveCatalogAddress와 같은 이유).
        /// </summary>
        public const string LiveCatalogAddress = "catalog/stage";

        public List<StageCatalogEntry> entries = new();

        public StageCatalogEntry FindById(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId) || entries == null)
            {
                return null;
            }

            foreach (StageCatalogEntry entry in entries)
            {
                if (entry != null && entry.stageId == stageId)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// 유한 스테이지의 해금 정본은 같은 챕터의 클리어 기록이다. 이미 클리어한 노드와
        /// 그 바로 다음 노드는 열어 두고, 클리어 기록이 전혀 없는 구버전 저장 데이터만
        /// bestWave 기준으로 복구한다.
        /// </summary>
        public bool IsUnlocked(StageCatalogEntry target, PlayerProfile profile)
        {
            if (target == null)
            {
                return false;
            }

            if (profile == null)
            {
                return target.requiredBestWave <= 0;
            }

            if (profile.HasClearedStage(target.stageId))
            {
                return true;
            }

            StageCatalogEntry previous = FindPreviousInChapter(target);
            if (previous == null || profile.HasClearedStage(previous.stageId))
            {
                return true;
            }

            bool hasStageHistory = profile.clearedStageIds != null && profile.clearedStageIds.Count > 0;
            if (!hasStageHistory)
            {
                // clearedStageIds 도입 이전 저장은 진행도를 잃지 않도록 한 번만 레거시 값을 읽는다.
                return target.IsUnlocked(profile.bestWave);
            }

            // 뒤쪽 스테이지의 클리어 기록만 남은 저장에서도 앞 노드 재도전을 막지 않는다.
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                StageCatalogEntry cleared = entries[i];
                if (cleared != null && cleared.order > target.order &&
                    string.Equals(cleared.chapterId, target.chapterId, System.StringComparison.OrdinalIgnoreCase) &&
                    profile.HasClearedStage(cleared.stageId))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsPlayable(StageCatalogEntry target, PlayerProfile profile)
        {
            return IsUnlocked(target, profile) && target != null && target.HasBattleData;
        }

        private StageCatalogEntry FindPreviousInChapter(StageCatalogEntry target)
        {
            StageCatalogEntry previous = null;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                StageCatalogEntry candidate = entries[i];
                if (candidate == null || candidate.order >= target.order ||
                    !string.Equals(candidate.chapterId, target.chapterId,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (previous == null || candidate.order > previous.order)
                {
                    previous = candidate;
                }
            }

            return previous;
        }
    }
}
