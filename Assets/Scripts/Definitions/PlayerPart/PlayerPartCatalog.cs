using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.PlayerPart
{
    /// <summary>
    /// 빌드에 포함되는 로컬 파츠 카탈로그. 상점 도입 전에는 Home 디버그 UI와 전투 조립기가
    /// 함께 사용하고, 이후 원격 엔트리 계층을 추가해도 Definition 계약은 그대로 유지한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Player Part/Part Catalog")]
    public sealed class PlayerPartCatalog : ScriptableObject
    {
        public List<PlayerPartDefinition> parts = new();

        public PlayerPartDefinition FindById(string partId)
        {
            if (string.IsNullOrWhiteSpace(partId))
            {
                return null;
            }

            return parts.Find(part => part != null &&
                string.Equals(part.partId, partId, StringComparison.Ordinal));
        }

        public PlayerPartDefinition FindCommon(PlayerPartSlot slot)
        {
            return parts.Find(part => part != null && part.slot == slot &&
                part.grade == PlayerPartGrade.Common);
        }
    }
}
