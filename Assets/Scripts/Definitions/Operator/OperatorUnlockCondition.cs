using System;
using RCCom.Data;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// 오퍼레이터 획득 경로 하나. 같은 오퍼레이터에 여러 항목을 두면 어느 하나만 만족해도
    /// 해금되는 OR 조건으로 사용해, 스테이지 보상과 상점 구매를 동시에 제공할 수 있게 한다.
    /// </summary>
    [Serializable]
    public sealed class OperatorUnlockCondition
    {
        public OperatorUnlockType type = OperatorUnlockType.InitiallyAvailable;

        [Min(0)] public int requiredBestWave;
        [Min(0)] public int purchasePrice;
        public string requiredStageId = string.Empty;

        public bool IsSatisfied(PlayerProfile profile, string operatorId)
        {
            if (profile == null)
            {
                return false;
            }

            return type switch
            {
                OperatorUnlockType.InitiallyAvailable => true,
                OperatorUnlockType.BestWave => profile.bestWave >= requiredBestWave,
                OperatorUnlockType.CommodityPurchase => profile.HasAcquiredOperator(operatorId),
                OperatorUnlockType.StageClearReward => profile.HasClearedStage(requiredStageId),
                _ => false,
            };
        }

        public string GetDescription()
        {
            return type switch
            {
                OperatorUnlockType.BestWave => $"최고 웨이브 {requiredBestWave} 달성 시 해금",
                OperatorUnlockType.CommodityPurchase => $"{purchasePrice} 골드로 영입",
                OperatorUnlockType.StageClearReward =>
                    $"스테이지 {FormatStageLabel(requiredStageId)} 클리어 보상",
                _ => "사용 가능",
            };
        }

        private static string FormatStageLabel(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId))
            {
                return "미지정";
            }

            string[] parts = stageId.Split('-');
            if (parts.Length == 2 && parts[0].StartsWith("ch", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[0].Substring(2), out int chapter) &&
                int.TryParse(parts[1], out int stage))
            {
                return $"{chapter}-{stage}";
            }

            return stageId;
        }
    }
}
