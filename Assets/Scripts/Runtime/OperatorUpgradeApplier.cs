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
    /// PlayerProfile의 영구 강화 레벨을 전투용 복제본에 적용하는 순수 헬퍼. 원본 Definition과
    /// 효과 SO는 수정하지 않으며, 트랙 하나의 여러 modifier도 한 번의 레벨로 함께 계산한다.
    /// </summary>
    public static class OperatorUpgradeApplier
    {
        public static PlayerProfile LoadProfile()
        {
            return new PlayerPrefsProfileStorage().Load();
        }

        public static int GetEffectiveLevel(
            PlayerProfile profile,
            string operatorId,
            OperatorUpgradeTrack track)
        {
            if (profile == null || track == null || string.IsNullOrWhiteSpace(operatorId))
            {
                return 0;
            }

            return Mathf.Clamp(
                profile.GetUpgradeLevel(operatorId, track.trackId),
                0,
                Mathf.Max(1, track.maxLevel));
        }

        /// <summary>
        /// 기준값에 해당 대상의 모든 modifier를 순서대로 적용한다. Studio 검증은 동일 대상 중복을
        /// 막지만, 손상 데이터에서도 결정적으로 동작하도록 런타임은 모든 일치 항목을 처리한다.
        /// </summary>
        public static float ResolveValue(
            OperatorUpgradeTrackSet trackSet,
            PlayerProfile profile,
            string operatorId,
            OperatorUpgradeTargetKind targetKind,
            string unitId,
            float baseValue)
        {
            if (trackSet == null || trackSet.tracks == null || profile == null ||
                string.IsNullOrWhiteSpace(operatorId))
            {
                return baseValue;
            }

            float value = baseValue;
            for (int trackIndex = 0; trackIndex < trackSet.tracks.Count; trackIndex++)
            {
                OperatorUpgradeTrack track = trackSet.tracks[trackIndex];
                if (track == null || track.modifiers == null)
                {
                    continue;
                }

                int level = GetEffectiveLevel(profile, operatorId, track);
                if (level <= 0)
                {
                    continue;
                }

                for (int modifierIndex = 0; modifierIndex < track.modifiers.Count; modifierIndex++)
                {
                    OperatorUpgradeModifier modifier = track.modifiers[modifierIndex];
                    if (!Matches(modifier, targetKind, unitId))
                    {
                        continue;
                    }

                    value = modifier.ClampResult(value + modifier.GetDelta(level));
                }
            }

            return value;
        }

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
            AllyUnitData data = CloneAllyUnitData(source.data);
            bool changed = false;

            changed |= ApplyDataValue(trackSet, profile, operatorId, unitId,
                OperatorUpgradeTargetKind.AllyUnitMaxHealth, data.maxHealth, value => data.maxHealth = value);
            changed |= ApplyDataValue(trackSet, profile, operatorId, unitId,
                OperatorUpgradeTargetKind.AllyUnitAttackDamage, data.attackDamage, value => data.attackDamage = value);
            changed |= ApplyDataValue(trackSet, profile, operatorId, unitId,
                OperatorUpgradeTargetKind.AllyUnitAttackRange, data.attackRange, value => data.attackRange = value);
            changed |= ApplyDataValue(trackSet, profile, operatorId, unitId,
                OperatorUpgradeTargetKind.AllyUnitMoveSpeed, data.moveSpeed, value => data.moveSpeed = value);
            changed |= ApplyDataValue(trackSet, profile, operatorId, unitId,
                OperatorUpgradeTargetKind.AllyUnitDeployCost, data.deployCost,
                value => data.deployCost = Mathf.RoundToInt(value));

            List<AllyUnitEffectBase> sourceEffects = source.effects ?? new List<AllyUnitEffectBase>();
            var effects = new List<AllyUnitEffectBase>(sourceEffects.Count);
            for (int i = 0; i < sourceEffects.Count; i++)
            {
                AllyUnitEffectBase original = sourceEffects[i];
                AllyUnitEffectBase upgraded = UpgradeEffect(original, unitId, operatorId, trackSet, profile);
                changed |= !ReferenceEquals(upgraded, original);
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

        private static bool ApplyDataValue(
            OperatorUpgradeTrackSet trackSet,
            PlayerProfile profile,
            string operatorId,
            string unitId,
            OperatorUpgradeTargetKind targetKind,
            float baseValue,
            System.Action<float> apply)
        {
            float resolved = ResolveValue(trackSet, profile, operatorId, targetKind, unitId, baseValue);
            if (Mathf.Approximately(resolved, baseValue))
            {
                return false;
            }

            apply(resolved);
            return true;
        }

        private static AllyUnitEffectBase UpgradeEffect(
            AllyUnitEffectBase source,
            string unitId,
            string operatorId,
            OperatorUpgradeTrackSet trackSet,
            PlayerProfile profile)
        {
            if (source == null)
            {
                return null;
            }

            switch (source)
            {
                case TacticalRelayAuraEffect tacticalRelay:
                {
                    float move = ResolveValue(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectTacticalRelayMultiplier, unitId,
                        tacticalRelay.MoveSpeedMultiplier);
                    float attack = ResolveValue(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectTacticalRelayMultiplier, unitId,
                        tacticalRelay.AttackSpeedMultiplier);
                    if (Mathf.Approximately(move, tacticalRelay.MoveSpeedMultiplier) &&
                        Mathf.Approximately(attack, tacticalRelay.AttackSpeedMultiplier))
                    {
                        return source;
                    }

                    var clone = Object.Instantiate(tacticalRelay);
                    clone.hideFlags = HideFlags.DontSave;
                    clone.ApplyRuntimeOverride(move, attack);
                    return clone;
                }
                case VulnerableAuraEffect vulnerable:
                {
                    float multiplier = ResolveValue(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectVulnerableDamageMultiplier, unitId,
                        vulnerable.DamageTakenMultiplier);
                    float refresh = ResolveValue(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectVulnerableRefreshDuration, unitId,
                        vulnerable.RefreshDuration);
                    if (Mathf.Approximately(multiplier, vulnerable.DamageTakenMultiplier) &&
                        Mathf.Approximately(refresh, vulnerable.RefreshDuration))
                    {
                        return source;
                    }

                    var clone = Object.Instantiate(vulnerable);
                    clone.hideFlags = HideFlags.DontSave;
                    clone.ApplyRuntimeOverride(multiplier, refresh);
                    return clone;
                }
                case SlowAuraEffect slow:
                {
                    float multiplier = ResolveValue(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectSlowMultiplier, unitId,
                        slow.SpeedMultiplier);
                    if (Mathf.Approximately(multiplier, slow.SpeedMultiplier))
                    {
                        return source;
                    }

                    var clone = Object.Instantiate(slow);
                    clone.hideFlags = HideFlags.DontSave;
                    clone.ApplyRuntimeOverride(multiplier);
                    return clone;
                }
                case PitCrewRepairAuraEffect pitCrew:
                {
                    float repair = ResolveValue(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectRepairPerSecond, unitId,
                        pitCrew.RepairPerSecond);
                    float pitStop = ResolveValue(trackSet, profile, operatorId,
                        OperatorUpgradeTargetKind.EffectRepairPitStopMultiplier, unitId,
                        pitCrew.PitStopMultiplier);
                    if (Mathf.Approximately(repair, pitCrew.RepairPerSecond) &&
                        Mathf.Approximately(pitStop, pitCrew.PitStopMultiplier))
                    {
                        return source;
                    }

                    var clone = Object.Instantiate(pitCrew);
                    clone.hideFlags = HideFlags.DontSave;
                    clone.ApplyRuntimeOverride(repair, pitStop);
                    return clone;
                }
                default:
                    return source;
            }
        }

        private static bool Matches(
            OperatorUpgradeModifier modifier,
            OperatorUpgradeTargetKind targetKind,
            string unitId)
        {
            if (modifier == null || modifier.targetKind != targetKind)
            {
                return false;
            }

            return unitId == null
                ? string.IsNullOrWhiteSpace(modifier.targetUnitId)
                : string.Equals(modifier.targetUnitId, unitId, System.StringComparison.Ordinal);
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
