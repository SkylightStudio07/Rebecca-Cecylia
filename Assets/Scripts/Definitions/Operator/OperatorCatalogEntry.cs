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

        [Tooltip("리크루트 화면 하단 좌우 카드에 표시할 전처리된 어두운 상반신 이미지")]
        public Sprite shopUpperBodyPortraitDimmed;

        // 아래 *Address 필드들은 위 Sprite가 원격 콘텐츠라 비어 있을 때만 채워진다.
        // Definition 전체(대사·이펙트 포함)를 당기지 않고 이 초상화 한 장만 담긴 독립
        // 번들을 RemotePreviewSpriteLoader가 내려받을 수 있게 하는 Addressables 주소다.
        // 로컬 오퍼레이터는 위 Sprite가 이미 채워져 있어 이 주소들이 쓰이지 않는다.
        [Tooltip("previewPortrait이 원격이라 비어 있을 때 내려받을 Addressables 주소")]
        public string previewPortraitAddress;

        [Tooltip("managementPortrait이 원격이라 비어 있을 때 내려받을 Addressables 주소")]
        public string managementPortraitAddress;

        [Tooltip("shopPortrait이 원격이라 비어 있을 때 내려받을 Addressables 주소")]
        public string shopPortraitAddress;

        [Tooltip("shopUpperBodyPortrait이 원격이라 비어 있을 때 내려받을 Addressables 주소")]
        public string shopUpperBodyPortraitAddress;

        [Tooltip("shopUpperBodyPortraitDimmed이 원격이라 비어 있을 때 내려받을 Addressables 주소")]
        public string shopUpperBodyPortraitDimmedAddress;

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

        [Tooltip("여러 항목 중 하나만 만족해도 해금된다. 비어 있으면 구버전 단일 조건 필드를 사용한다.")]
        public List<OperatorUnlockCondition> unlockConditions = new();

        [Tooltip("스테이지 보상 화면에 표시할 경량 초상화")]
        public Sprite unlockRewardPortrait;

        [Tooltip("unlockRewardPortrait이 원격이라 비어 있을 때 내려받을 Addressables 주소")]
        public string unlockRewardPortraitAddress;

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

            if (unlockConditions != null && unlockConditions.Count > 0)
            {
                for (int i = 0; i < unlockConditions.Count; i++)
                {
                    if (unlockConditions[i] != null && unlockConditions[i].IsSatisfied(profile, operatorId))
                    {
                        return true;
                    }
                }

                return false;
            }

            return CreateLegacyCondition().IsSatisfied(profile, operatorId);
        }

        public string GetLockedDescription()
        {
            if (unlockConditions != null && unlockConditions.Count > 0)
            {
                var descriptions = new List<string>();
                for (int i = 0; i < unlockConditions.Count; i++)
                {
                    if (unlockConditions[i] != null)
                    {
                        descriptions.Add(unlockConditions[i].GetDescription());
                    }
                }

                return descriptions.Count > 0 ? string.Join(" 또는 ", descriptions) : "해금 조건 미지정";
            }

            return CreateLegacyCondition().GetDescription();
        }

        public bool HasUnlockCondition(OperatorUnlockType type)
        {
            if (unlockConditions != null && unlockConditions.Count > 0)
            {
                return unlockConditions.Exists(condition => condition != null && condition.type == type);
            }

            return unlockType == type;
        }

        public int GetPurchasePrice()
        {
            OperatorUnlockCondition condition = FindCondition(OperatorUnlockType.CommodityPurchase);
            return condition != null ? condition.purchasePrice : purchasePrice;
        }

        public bool IsStageRewardFor(string stageId)
        {
            if (unlockConditions != null && unlockConditions.Count > 0)
            {
                return unlockConditions.Exists(condition => condition != null &&
                    condition.type == OperatorUnlockType.StageClearReward &&
                    string.Equals(condition.requiredStageId, stageId, StringComparison.OrdinalIgnoreCase));
            }

            return unlockType == OperatorUnlockType.StageClearReward &&
                string.Equals(requiredStageId, stageId, StringComparison.OrdinalIgnoreCase);
        }

        private OperatorUnlockCondition FindCondition(OperatorUnlockType type)
        {
            return unlockConditions != null
                ? unlockConditions.Find(condition => condition != null && condition.type == type)
                : null;
        }

        private OperatorUnlockCondition CreateLegacyCondition()
        {
            return new OperatorUnlockCondition
            {
                type = unlockType,
                requiredBestWave = requiredBestWave,
                purchasePrice = purchasePrice,
                requiredStageId = requiredStageId,
            };
        }
    }
}
