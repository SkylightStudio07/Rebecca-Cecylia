using System;
using System.Collections.Generic;
using RCCom.Definitions.PlayerPart;
using RCCom.Effects.PlayerPart;
using RCCom.Effects.PlayerPart.Concrete;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    public static class PlayerPartAssetValidator
    {
        [MenuItem("RCCom/Player Parts/Validate Player Part Assets")]
        public static void ValidateAll()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var commonSlots = new HashSet<PlayerPartSlot>();
            PlayerPartCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayerPartCatalog>(
                PlayerPartAssetBuilder.CatalogPath);
            if (catalog == null || catalog.parts == null)
            {
                throw new InvalidOperationException("PlayerPartCatalog가 없거나 parts가 null입니다.");
            }

            foreach (PlayerPartDefinition part in catalog.parts)
            {
                if (part == null || string.IsNullOrWhiteSpace(part.partId))
                {
                    errors.Add("카탈로그에 null 또는 ID가 빈 파츠가 있습니다.");
                    continue;
                }

                if (!ids.Add(part.partId))
                {
                    errors.Add($"partId가 중복됩니다: {part.partId}");
                }

                if (part.grade == PlayerPartGrade.Common)
                {
                    if (part.slot == PlayerPartSlot.Special || part.price != 0 || !commonSlots.Add(part.slot))
                    {
                        errors.Add($"Common 폴백 규칙이 잘못되었습니다: {part.partId}");
                    }
                }

                ValidateStatOverride(part, errors);

                int primaryCount = 0;
                if (part.effects == null)
                {
                    errors.Add($"Effect 목록이 null입니다: {part.partId}");
                }
                else
                {
                    foreach (PlayerPartEffectBase effect in part.effects)
                    {
                        if (effect == null)
                        {
                            errors.Add($"Effect 목록에 null이 있습니다: {part.partId}");
                        }
                        else if (effect is IPlayerPrimaryAttackEffect)
                        {
                            primaryCount++;
                        }
                    }
                }

                ValidateAttackVisuals(part, errors);

                if (part.slot == PlayerPartSlot.Turret && primaryCount != 1)
                {
                    errors.Add($"포탑 파츠는 주 공격 Effect가 정확히 1개여야 합니다: {part.partId}");
                }
                else if (part.slot != PlayerPartSlot.Turret && primaryCount != 0)
                {
                    errors.Add($"포탑 외 슬롯에 주 공격 Effect가 있습니다: {part.partId}");
                }

                if (part.icon == null)
                {
                    errors.Add($"상점 아이콘이 비어 있습니다: {part.partId}");
                }
            }

            foreach (PlayerPartSlot slot in new[]
                     {
                         PlayerPartSlot.Thruster,
                         PlayerPartSlot.Turret,
                         PlayerPartSlot.Body,
                         PlayerPartSlot.Driver,
                     })
            {
                if (!commonSlots.Contains(slot))
                {
                    errors.Add($"Common 폴백 파츠가 없습니다: {slot}");
                }
            }

            foreach (string warning in warnings) { Debug.LogWarning($"[PlayerPartValidator] {warning}"); }
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }

            Debug.Log($"[PlayerPartValidator] {catalog.parts.Count}개 검증 통과, 아트 경고 {warnings.Count}건.");
        }

        private static void ValidateAttackVisuals(PlayerPartDefinition part, List<string> errors)
        {
            if (part.effects == null)
            {
                return;
            }

            foreach (PlayerPartEffectBase effect in part.effects)
            {
                if (effect is PlayerPierceAttackEffect or PlayerChainAttackEffect or PlayerOverloadAttackEffect)
                {
                    ValidateVisualReference(
                        effect,
                        "laserBeamPrefab",
                        CombatVfxAssetBuilder.LaserBeamPrefabPath,
                        errors,
                        part.partId);
                    ValidateVisualReference(
                        effect,
                        "laserBeamVisual",
                        CombatVfxAssetBuilder.LaserBeamVisualEffectPath,
                        errors,
                        part.partId);
                }

                if (effect is PlayerSplashAttackEffect)
                {
                    ValidateVisualReference(
                        effect,
                        "fakeProjectilePrefab",
                        CombatVfxAssetBuilder.FakeProjectilePrefabPath,
                        errors,
                        part.partId);
                    ValidateVisualReference(
                        effect,
                        "explosionBurstPrefab",
                        CombatVfxAssetBuilder.DeathBurstPrefabPath,
                        errors,
                        part.partId);
                    ValidateVisualReference(
                        effect,
                        "shockwaveRingPrefab",
                        CombatVfxAssetBuilder.ShockwaveRingPrefabPath,
                        errors,
                        part.partId);
                    ValidateVisualReference(
                        effect,
                        "shockwaveVisual",
                        CombatVfxAssetBuilder.ShockwaveRingVisualEffectPath,
                        errors,
                        part.partId);
                    ValidateVisualReference(
                        effect,
                        "scorchDecalPrefab",
                        CombatVfxAssetBuilder.ScorchDecalPrefabPath,
                        errors,
                        part.partId);
                }
            }
        }

        private static void ValidateVisualReference(
            PlayerPartEffectBase effect,
            string propertyName,
            string expectedPath,
            List<string> errors,
            string partId)
        {
            UnityEngine.Object expected = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(expectedPath);
            var serialized = new SerializedObject(effect);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (expected == null || property == null || property.objectReferenceValue != expected)
            {
                errors.Add($"공용 VFX 배선이 올바르지 않습니다: {partId}/{propertyName}");
            }
        }

        private static void ValidateStatOverride(PlayerPartDefinition part, List<string> errors)
        {
            PlayerPartStatOverride value = part.statOverride;
            if (value == null)
            {
                errors.Add($"스탯 오버라이드가 null입니다: {part.partId}");
                return;
            }

            bool hasThruster = NonZero(value.moveSpeed) || NonZero(value.skillOverdriveMoveSpeedMultiplier);
            bool hasTurret = NonZero(value.attackDamage) || NonZero(value.attackRange) ||
                             NonZero(value.attackInterval) || NonZero(value.projectileSpeed);
            bool hasBody = NonZero(value.maxHealth) || NonZero(value.hitInvulnerabilityDuration);
            bool hasDriver = NonZero(value.skillCooldown) || NonZero(value.skillRange) ||
                             NonZero(value.skillDamage) || value.skillBurstCount != 0 ||
                             NonZero(value.skillBurstInterval) || value.skillChargeCapacity != 0;

            bool hasForeignFields;
            bool requiredFieldsInvalid;
            switch (part.slot)
            {
                case PlayerPartSlot.Thruster:
                    hasForeignFields = hasTurret || hasBody || hasDriver;
                    requiredFieldsInvalid = value.moveSpeed <= 0f;
                    break;
                case PlayerPartSlot.Turret:
                    hasForeignFields = hasThruster || hasBody || hasDriver;
                    requiredFieldsInvalid = value.attackDamage <= 0f || value.attackRange <= 0f ||
                                            value.attackInterval <= 0f || value.projectileSpeed <= 0f;
                    break;
                case PlayerPartSlot.Body:
                    hasForeignFields = hasThruster || hasTurret || hasDriver;
                    requiredFieldsInvalid = value.maxHealth <= 0f;
                    break;
                case PlayerPartSlot.Driver:
                    hasForeignFields = hasThruster || hasTurret || hasBody;
                    requiredFieldsInvalid = value.skillCooldown <= 0f || value.skillRange <= 0f ||
                                            value.skillDamage <= 0f || value.skillBurstCount <= 0 ||
                                            value.skillBurstInterval <= 0f || value.skillChargeCapacity <= 0;
                    break;
                case PlayerPartSlot.Special:
                    hasForeignFields = hasThruster || hasTurret || hasBody || hasDriver;
                    requiredFieldsInvalid = false;
                    break;
                default:
                    hasForeignFields = true;
                    requiredFieldsInvalid = true;
                    break;
            }

            if (hasForeignFields)
            {
                errors.Add($"슬롯이 소유하지 않는 스탯 필드가 설정되었습니다: {part.partId}");
            }

            if (requiredFieldsInvalid)
            {
                errors.Add($"슬롯의 필수 스탯이 0 이하입니다: {part.partId}");
            }
        }

        private static bool NonZero(float value)
        {
            return !Mathf.Approximately(value, 0f);
        }
    }
}
