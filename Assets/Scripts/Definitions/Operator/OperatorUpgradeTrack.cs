using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// 플레이어가 한 번에 구매하고 UI에서 한 행으로 보는 강화 트랙. 정확한 레벨별 수치와 비용을
    /// 데이터로 보존해 정수 반올림 때문에 기획표와 실제 전투 값이 달라지지 않게 한다.
    /// </summary>
    [Serializable]
    public sealed class OperatorUpgradeTrack
    {
        public string trackId = string.Empty;
        public string displayName = string.Empty;

        [TextArea(2, 4)]
        public string description = string.Empty;

        public OperatorUpgradeCategory category = OperatorUpgradeCategory.Core;
        [Min(1)] public int maxLevel = 8;

        [Tooltip("Lv1부터 순서대로 해당 레벨을 구매할 때 필요한 계정 골드.")]
        public List<int> levelCosts = new();

        [Tooltip("Lv1부터 순서대로 필요한 호감도. 모두 0이면 호감도 게이팅을 사용하지 않는다.")]
        public List<int> requiredAffinityByLevel = new();

        public List<OperatorUpgradeModifier> modifiers = new();

        public int GetCostForLevel(int level)
        {
            if (level <= 0 || levelCosts == null || levelCosts.Count == 0)
            {
                return 0;
            }

            return Mathf.Max(0, levelCosts[Mathf.Clamp(level, 1, levelCosts.Count) - 1]);
        }

        public int GetRequiredAffinityForLevel(int level)
        {
            if (level <= 0 || requiredAffinityByLevel == null || requiredAffinityByLevel.Count == 0)
            {
                return 0;
            }

            return Mathf.Clamp(requiredAffinityByLevel[
                Mathf.Clamp(level, 1, requiredAffinityByLevel.Count) - 1], 0,
                RCCom.Data.PlayerProfile.MaxOperatorAffinity);
        }
    }
}
