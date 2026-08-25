using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.PlayerPart;
using RCCom.Effects.PlayerPart;
using RCCom.Runtime;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    public static class PlayerPartFoundationVerifier
    {
        [MenuItem("RCCom/Player Parts/Verify Runtime Composition")]
        public static void Verify()
        {
            PlayerPartCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayerPartCatalog>(
                PlayerPartAssetBuilder.CatalogPath);
            if (catalog == null || catalog.parts == null || catalog.parts.Count != 38)
            {
                throw new InvalidOperationException("38개 파츠가 포함된 PlayerPartCatalog가 필요합니다.");
            }

            PlayerPartDebugSession.SetCatalog(catalog);
            PlayerPartDebugSession.ClearEquipment();
            VerifyCommonFallback(catalog);
            VerifyEveryDefinition(catalog);
            VerifyRepresentativeCombination(catalog);
            PlayerPartDebugSession.ClearEquipment();
            Debug.Log("[PlayerPartFoundationVerifier] 38개 파츠 조립 및 대표 조합 검증을 통과했습니다.");
        }

        private static void VerifyCommonFallback(PlayerPartCatalog catalog)
        {
            foreach (PlayerPartSlot slot in Enum.GetValues(typeof(PlayerPartSlot)))
            {
                if (slot == PlayerPartSlot.Special)
                {
                    continue;
                }

                if (catalog.FindCommon(slot) == null || PlayerPartDebugSession.Resolve(slot) == null)
                {
                    throw new InvalidOperationException($"{slot} 슬롯의 Common 폴백이 없습니다.");
                }
            }
        }

        private static void VerifyEveryDefinition(PlayerPartCatalog catalog)
        {
            var source = new PlayerData();
            foreach (PlayerPartDefinition part in catalog.parts)
            {
                if (part == null || !PlayerPartDebugSession.TryEquip(part.slot, part.partId))
                {
                    throw new InvalidOperationException("카탈로그 파츠를 테스트 세션에 장착하지 못했습니다.");
                }

                PlayerLoadoutResult result = PlayerLoadoutBuilder.Compose(source, catalog);
                if (result.data == null)
                {
                    throw new InvalidOperationException($"{part.partId} 조립 결과가 비어 있습니다.");
                }
            }
        }

        private static void VerifyRepresentativeCombination(PlayerPartCatalog catalog)
        {
            PlayerPartDebugSession.ClearEquipment();
            string[] ids =
            {
                "thruster-overboost",
                "turret-core-overload",
                "body-reactive",
                "driver-dual-core",
                "special-core-overload",
            };

            foreach (string id in ids)
            {
                PlayerPartDefinition part = catalog.FindById(id);
                if (part == null || !PlayerPartDebugSession.TryEquip(part.slot, part.partId))
                {
                    throw new InvalidOperationException($"대표 조합 파츠를 찾지 못했습니다: {id}");
                }
            }

            PlayerLoadoutResult result = PlayerLoadoutBuilder.Compose(new PlayerData(), catalog);
            var effectTypes = new HashSet<Type>();
            foreach (PlayerPartEffectBase effect in result.effects)
            {
                if (effect != null)
                {
                    effectTypes.Add(effect.GetType());
                }
            }

            if (result.data.skillChargeCapacity != 2 || result.effects.Count < 3 || effectTypes.Count < 3)
            {
                throw new InvalidOperationException("대표 조합의 스탯 오버라이드 또는 Effect 조립이 누락되었습니다.");
            }
        }
    }
}
