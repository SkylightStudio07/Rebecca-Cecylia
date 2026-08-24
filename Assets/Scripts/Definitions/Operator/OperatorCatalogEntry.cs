using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Unit;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// Definition을 내려받기 전 선택 화면이 보여줘야 하는 최소 메타데이터.
    /// 원격 Definition 안에 이 정보를 넣으면 다운로드 전에는 선택 화면을 만들 수 없으므로
    /// 로컬 카탈로그에 별도로 둔다.
    /// </summary>
    [Serializable]
    public sealed class OperatorCatalogEntry
    {
        public string operatorId;
        public string displayName;

        [TextArea(2, 4)]
        public string playStyleDescription;

        [Tooltip("다운로드 전 선택 화면에 표시할 작은 초상화. 미완성 콘텐츠는 비워도 된다.")]
        public Sprite previewPortrait;

        [Tooltip("오퍼레이터 관리 카드에 표시할 전신·반신 초상화. 원격 콘텐츠는 다운로드 전 비워둔다.")]
        public Sprite managementPortrait;

        [Tooltip("리크루트 화면 좌측의 큰 오퍼레이터 이미지. 원격 콘텐츠는 다운로드 전 비워둔다.")]
        public Sprite shopPortrait;

        [Tooltip("리크루트 화면 하단 카드에 표시할 상반신 이미지. 원격 콘텐츠는 다운로드 전 비워둔다.")]
        public Sprite shopUpperBodyPortrait;

        public string alternateName;

        [TextArea(2, 4)]
        public string shopDialogue;

        [Tooltip("OperatorDefinition의 Addressables 주소")]
        public string address;

        [Tooltip("선택 화면에서 다운로드 콘텐츠임을 안내하기 위한 표시값")]
        public bool remoteContent;

        public OperatorUnlockType unlockType = OperatorUnlockType.InitiallyAvailable;

        [Min(0)]
        public int requiredBestWave;

        [Min(0)]
        public int purchasePrice;

        public string requiredStageId;

        [Tooltip("스테이지 보상 화면에 표시할 경량 초상화")]
        public Sprite unlockRewardPortrait;

        [Tooltip("Definition을 내려받기 전 로스터 패널에 표시할 경량 유닛 정보")]
        // 유닛 전용 카탈로그와 같은 엔트리 타입을 사용해 선택 화면이 별도 미리보기
        // 데이터 모델을 다시 정의하지 않게 한다.
        public List<AllyUnitCatalogEntry> unitPreviews = new();

        public bool IsUnlocked(PlayerProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            return unlockType switch
            {
                OperatorUnlockType.InitiallyAvailable => true,
                OperatorUnlockType.BestWave => profile.bestWave >= requiredBestWave,
                OperatorUnlockType.CommodityPurchase => profile.HasAcquiredOperator(operatorId),
                OperatorUnlockType.StageClearReward => profile.HasClearedStage(requiredStageId),
                _ => false,
            };
        }

        public string GetLockedDescription()
        {
            return unlockType switch
            {
                OperatorUnlockType.BestWave => $"최고 웨이브 {requiredBestWave} 달성 시 해금",
                OperatorUnlockType.CommodityPurchase => $"{purchasePrice} 골드로 영입",
                OperatorUnlockType.StageClearReward => $"스테이지 {FormatStageLabel(requiredStageId)} 클리어 보상",
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
