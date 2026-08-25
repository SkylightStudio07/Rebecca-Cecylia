using System.Collections.Generic;
using RCCom.Definitions.PlayerPart;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 상점/영속 장착을 만들기 전 TitleScene의 Home 패널이 사용하는 세션 테스트 드라이버.
    /// 선택은 씬 전환과 Retry를 넘지만 앱 재실행에는 남지 않아 실제 프로필을 오염시키지 않는다.
    /// </summary>
    public static class PlayerPartDebugSession
    {
        private static readonly Dictionary<PlayerPartSlot, string> _equippedPartIds = new();
        public static PlayerPartCatalog Catalog { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewApplicationRun()
        {
            Catalog = null;
            _equippedPartIds.Clear();
        }

        public static void SetCatalog(PlayerPartCatalog catalog)
        {
            Catalog = catalog;
        }

        public static bool TryEquip(PlayerPartSlot slot, string partId)
        {
            if (Catalog == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(partId))
            {
                _equippedPartIds.Remove(slot);
                return slot == PlayerPartSlot.Special;
            }

            PlayerPartDefinition definition = Catalog.FindById(partId);
            if (definition == null || definition.slot != slot)
            {
                return false;
            }

            _equippedPartIds[slot] = definition.partId;
            return true;
        }

        public static PlayerPartDefinition Resolve(PlayerPartSlot slot)
        {
            if (Catalog == null)
            {
                return null;
            }

            if (_equippedPartIds.TryGetValue(slot, out string partId))
            {
                PlayerPartDefinition equipped = Catalog.FindById(partId);
                if (equipped != null && equipped.slot == slot)
                {
                    return equipped;
                }
            }

            return slot == PlayerPartSlot.Special ? null : Catalog.FindCommon(slot);
        }

        public static string GetEquippedId(PlayerPartSlot slot)
        {
            return Resolve(slot)?.partId ?? string.Empty;
        }

        public static void ClearEquipment()
        {
            _equippedPartIds.Clear();
        }
    }
}
