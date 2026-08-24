using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// 강화 트랙 카테고리. 비용/구매 로직은 아직 없으므로(상점 미구현) 순수 표시용 그룹핑이다.
    /// </summary>
    public enum OperatorUpgradeCategory
    {
        Core,
        Support,
    }

    /// <summary>
    /// 강화 트랙 1개가 어떤 런타임 값을 가산하는지 지정한다. AllyUnit* 계열은 targetUnitId로
    /// 대상 유닛을 지정하고, Effect* 계열은 4종 오라 효과 SO(모두 무상태 공유 자산)의 복제본에
    /// 오버라이드를 주입할 때 사용한다. Player* 계열은 아직 없음 — 파일럿 공용 스탯 강화(설계안
    /// §5)는 이번 범위에서 제외했고, 필요해지면 이 enum에 값만 추가하면 된다.
    /// </summary>
    public enum OperatorUpgradeTargetKind
    {
        AllyUnitMaxHealth,
        AllyUnitAttackDamage,
        AllyUnitAttackRange,
        AllyUnitMoveSpeed,
        AllyUnitDeployCost,
        DeployStartingCommandPoints,
        DeployMaxCommandPoints,
        DeployCommandPointRecoveryPerSecond,
        EffectTacticalRelayMultiplier,
        EffectVulnerableDamageMultiplier,
        EffectVulnerableRefreshDuration,
        EffectSlowMultiplier,
        EffectRepairPerSecond,
        EffectRepairPitStopMultiplier,
    }

    /// <summary>
    /// 강화 트랙 1개의 데이터. SO가 아닌 순수 직렬화 클래스라 Recipe JSON
    /// (OperatorAssetRecipe.upgradeTracks)과 런타임 OperatorUpgradeTrackSet.tracks가 같은 타입을
    /// 그대로 공유한다. Lv0 기준값은 여기 중복 저장하지 않고, 적용 시점에 실제 Definition/컴포넌트의
    /// 현재 값을 기준선으로 읽어 perLevelDelta × 레벨을 더한다.
    /// </summary>
    [Serializable]
    public class OperatorUpgradeTrack
    {
        public string trackId = string.Empty;
        public string displayName = string.Empty;
        public OperatorUpgradeCategory category = OperatorUpgradeCategory.Core;
        public OperatorUpgradeTargetKind targetKind = OperatorUpgradeTargetKind.AllyUnitMaxHealth;

        [Tooltip("AllyUnit*/Effect* 대상일 때 유닛 ID. 여러 유닛에 동일 delta를 적용하려면 " +
                 "';'로 구분해서 나열한다 (예: \"aurora-debuff-drone;aurora-slow-drone\").")]
        public string targetUnitId = string.Empty;

        [Min(1)] public int maxLevel = 8;
        public float perLevelDelta;

        [Tooltip("true면 반올림 후 정수로 적용한다 (CP, 배치 비용 등).")]
        public bool isInteger;

        public bool hasMinValue;
        public float minValue;
        public bool hasMaxValue;
        public float maxValue;

        /// <summary>세미콜론으로 구분된 targetUnitId 목록에 해당 유닛이 포함되는지 확인한다.</summary>
        public bool TargetsUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId) || string.IsNullOrWhiteSpace(targetUnitId))
            {
                return false;
            }

            string[] parts = targetUnitId.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i].Trim(), unitId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>가산 결과에 트랙에 설정된 클램프(및 정수 반올림)를 적용한다.</summary>
        public float ClampResult(float value)
        {
            if (isInteger)
            {
                value = Mathf.Round(value);
            }

            if (hasMinValue)
            {
                value = Mathf.Max(minValue, value);
            }

            if (hasMaxValue)
            {
                value = Mathf.Min(maxValue, value);
            }

            return value;
        }
    }

    /// <summary>
    /// 오퍼레이터 1명이 가진 강화 트랙 묶음. towerRoster/cardRoster와 동일하게
    /// OperatorDefinition이 직접 필드로 참조해, 오퍼레이터 Addressable 그룹에 함께 딸려간다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Operator/Operator Upgrade Track Set")]
    public class OperatorUpgradeTrackSet : ScriptableObject
    {
        public List<OperatorUpgradeTrack> tracks = new();

        public OperatorUpgradeTrack FindByTrackId(string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId) || tracks == null)
            {
                return null;
            }

            for (int i = 0; i < tracks.Count; i++)
            {
                OperatorUpgradeTrack track = tracks[i];
                if (track != null && string.Equals(track.trackId, trackId, StringComparison.Ordinal))
                {
                    return track;
                }
            }

            return null;
        }
    }
}
