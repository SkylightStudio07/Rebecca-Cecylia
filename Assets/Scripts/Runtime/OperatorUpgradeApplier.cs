using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Unit;
using RCCom.Effects.Unit;
using RCCom.Effects.Unit.Concrete;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// PlayerProfile에 저장된 오퍼레이터 강화 레벨을 실제 전투 값으로 계산·적용하는 순수 헬퍼.
    /// MonoBehaviour가 아니며, 원본 SO 에셋은 절대 수정하지 않는다 — 값이 바뀌는 대상은 항상
    /// 호출자가 만든 런타임 복제본(AllyUnitDefinition, AllyUnitEffectBase, 씬 컴포넌트 필드)이다.
    /// </summary>
    public static class OperatorUpgradeApplier
    {
        /// <summary>여러 호출부가 저장소 생성 코드를 중복하지 않도록 감싼 진입점.</summary>
        public static PlayerProfile LoadProfile()
        {
            return new PlayerPrefsProfileStorage().Load();
        }

        /// <summary>저장된 레벨을 트랙의 maxLevel로 클램프한다. 상한은 PlayerProfile이 모르는 값이라 여기서 강제한다.</summary>
        public static int GetEffectiveLevel(PlayerProfile profile, string operatorId, OperatorUpgradeTrack track)
        {
            if (profile == null || track == null || string.IsNullOrWhiteSpace(operatorId))
            {
                return 0;
            }

            int rawLevel = profile.GetUpgradeLevel(operatorId, track.trackId);
            return Mathf.Clamp(rawLevel, 0, Mathf.Max(1, track.maxLevel));
        }

        /// <summary>
        /// unitId가 null이면 유닛과 무관한 트랙(Deploy* 계열)만, 아니면 해당 유닛을 대상으로 하는
        /// 트랙만 찾아 perLevelDelta × 유효 레벨의 합을 반환한다. 일치하는 트랙이 여럿이면 합산한다.
        /// </summary>
        public static float GetTotalDelta(
            OperatorUpgradeTrackSet trackSet,
            PlayerProfile profile,
            string operatorId,
            OperatorUpgradeTargetKind targetKind,
            string unitId)
        {
            if (trackSet == null || trackSet.tracks == null || profile == null ||
                string.IsNullOrWhiteSpace(operatorId))
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < trackSet.tracks.Count; i++)
            {
                OperatorUpgradeTrack track = trackSet.tracks[i];
                if (track == null || track.targetKind != targetKind)
                {
                    continue;
                }

                bool matchesUnit = unitId == null
                    ? string.IsNullOrWhiteSpace(track.targetUnitId)
                    : track.TargetsUnit(unitId);
                if (!matchesUnit)
                {
                    continue;
                }

                int level = GetEffectiveLevel(profile, operatorId, track);
                if (level <= 0)
                {
                    continue;
                }

                total += track.perLevelDelta * level;
            }

            return total;
        }

        /// <summary>
        /// 유닛 하나의 AllyUnitDefinition에 해당 유닛을 대상으로 하는 모든 트랙을 적용한 런타임
        /// 복제본을 만든다. 바뀐 값이 하나도 없으면 불필요한 할당 없이 원본을 그대로 반환한다.
        /// </summary>
        public static AllyUnitDefinition CreateUpgradedUnitDefinition(
            AllyUnitDefinition source,
            string operatorId,
            OperatorUpgradeTrackSet trackSet,
            PlayerProfile profile)
        {
            if (source == null || source.data == null || trackSet == null || trackSet.tracks == null ||
                profile == null || string.IsNullOrWhiteSpace(operatorId))
            {
                return source;
            }

            string unitId = source.data.unitId;
            bool changed = false;

            AllyUnitData data = CloneAllyUnitData(source.data);
            changed |= TryApplyDataField(trackSet, profile, operatorId, unitId,
                OperatorUpgradeTargetKind.AllyUnitMaxHealth, data.maxHealth, value => data.maxHealth = value);
            changed |= TryApplyDataField(trackSet, profile, operatorId, unitId,
                OperatorUpgradeTargetKind.AllyUnitAttackDamage, data.attackDamage, value => data.attackDamage = value);
            changed |= TryApplyDataField(trackSet, profile, operatorId, unitId,
                OperatorUpgradeTargetKind.AllyUnitAttackRange, data.attackRange, value => data.attackRange = value);
            changed |= TryApplyDataField(trackSet, profile, operatorId, unitId,
                OperatorUpgradeTargetKind.AllyUnitMoveSpeed, data.moveSpeed, value => data.moveSpeed = value);
            changed |= TryApplyDataField(trackSet, profile, operatorId, unitId,
                OperatorUpgradeTargetKind.AllyUnitDeployCost, data.deployCost, value => data.deployCost = Mathf.RoundToInt(value));

            var effects = new List<AllyUnitEffectBase>(source.effects.Count);
            for (int i = 0; i < source.effects.Count; i++)
            {
                AllyUnitEffectBase original = source.effects[i];
                AllyUnitEffectBase upgraded = TryUpgradeEffect(original, unitId, operatorId, trackSet, profile);
                if (!ReferenceEquals(upgraded, original))
                {
                    changed = true;
                }

                effects.Add(upgraded ?? original);
            }

            if (!changed)
            {
                return source;
            }

            var clone = ScriptableObject.CreateInstance<AllyUnitDefinition>();
            clone.name = $"{source.name} (Upgraded: {operatorId})";
            clone.hideFlags = HideFlags.DontSave;
            clone.data = data;
            clone.effects = effects;
            clone.visualEffects = source.visualEffects;
            clone.sprite = source.sprite;
            clone.tint = source.tint;
            clone.spriteForwardOffsetDegrees = source.spriteForwardOffsetDegrees;
            return clone;
        }

        private static bool TryApplyDataField(
            OperatorUpgradeTrackSet trackSet,
            PlayerProfile profile,
            string operatorId,
            string unitId,
            OperatorUpgradeTargetKind targetKind,
            float baseValue,
            System.Action<float> apply)
        {
            OperatorUpgradeTrack matched = FindMatchingTrack(trackSet, targetKind, unitId);
            if (matched == null)
            {
                return false;
            }

            int level = GetEffectiveLevel(profile, operatorId, matched);
            if (level <= 0)
            {
                return false;
            }

            float value = matched.ClampResult(baseValue + matched.perLevelDelta * level);
            apply(value);
            return true;
        }

        private static OperatorUpgradeTrack FindMatchingTrack(
            OperatorUpgradeTrackSet trackSet, OperatorUpgradeTargetKind targetKind, string unitId)
        {
            for (int i = 0; i < trackSet.tracks.Count; i++)
            {
                OperatorUpgradeTrack track = trackSet.tracks[i];
                if (track != null && track.targetKind == targetKind && track.TargetsUnit(unitId))
                {
                    return track;
                }
            }

            return null;
        }

        /// <summary>
        /// 효과 SO 한 개를 대상 유닛/트랙 정보로 검사해, 강화 델타가 있으면 Instantiate한
        /// 복제본에 오버라이드를 적용해 반환한다. 없으면 원본을 그대로 반환해 공유 인스턴스를 유지한다.
        /// </summary>
        private static AllyUnitEffectBase TryUpgradeEffect(
            AllyUnitEffectBase source,
            string unitId,
            string operatorId,
            OperatorUpgradeTrackSet trackSet,
            PlayerProfile profile)
        {
            if (source == null)
            {
                return source;
            }

            switch (source)
            {
                case TacticalRelayAuraEffect tacticalRelay:
                {
                    float delta = GetTotalDelta(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectTacticalRelayMultiplier, unitId);
                    if (delta == 0f)
                    {
                        return source;
                    }

                    var clone = Object.Instantiate(tacticalRelay);
                    clone.hideFlags = HideFlags.DontSave;
                    clone.ApplyRuntimeOverride(
                        tacticalRelay.MoveSpeedMultiplier + delta,
                        tacticalRelay.AttackSpeedMultiplier + delta);
                    return clone;
                }

                case VulnerableAuraEffect vulnerable:
                {
                    float multiplierDelta = GetTotalDelta(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectVulnerableDamageMultiplier, unitId);
                    float refreshDelta = GetTotalDelta(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectVulnerableRefreshDuration, unitId);
                    if (multiplierDelta == 0f && refreshDelta == 0f)
                    {
                        return source;
                    }

                    var clone = Object.Instantiate(vulnerable);
                    clone.hideFlags = HideFlags.DontSave;
                    clone.ApplyRuntimeOverride(
                        vulnerable.DamageTakenMultiplier + multiplierDelta,
                        vulnerable.RefreshDuration + refreshDelta);
                    return clone;
                }

                case SlowAuraEffect slow:
                {
                    float delta = GetTotalDelta(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectSlowMultiplier, unitId);
                    if (delta == 0f)
                    {
                        return source;
                    }

                    var clone = Object.Instantiate(slow);
                    clone.hideFlags = HideFlags.DontSave;
                    clone.ApplyRuntimeOverride(slow.SpeedMultiplier + delta);
                    return clone;
                }

                case PitCrewRepairAuraEffect pitCrew:
                {
                    float repairDelta = GetTotalDelta(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectRepairPerSecond, unitId);
                    float pitStopDelta = GetTotalDelta(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectRepairPitStopMultiplier, unitId);
                    if (repairDelta == 0f && pitStopDelta == 0f)
                    {
                        return source;
                    }

                    var clone = Object.Instantiate(pitCrew);
                    clone.hideFlags = HideFlags.DontSave;
                    clone.ApplyRuntimeOverride(
                        pitCrew.RepairPerSecond + repairDelta,
                        pitCrew.PitStopMultiplier + pitStopDelta);
                    return clone;
                }

                default:
                    return source;
            }
        }

        private static AllyUnitData CloneAllyUnitData(AllyUnitData source)
        {
            return new AllyUnitData
            {
                unitId = source.unitId,
                displayName = source.displayName,
                deployCost = source.deployCost,
                maxHealth = source.maxHealth,
                moveSpeed = source.moveSpeed,
                attackDamage = source.attackDamage,
                attackInterval = source.attackInterval,
                attackRange = source.attackRange,
                detectionRange = source.detectionRange,
                projectileSpeed = source.projectileSpeed,
            };
        }
    }
}
