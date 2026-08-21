using System;
using UnityEngine;

namespace RCCom.Data
{
    /// <summary>
    /// 스테이지 클리어 보상 표시 계약. 실제 지급처는 이후 계정 재화·인벤토리 계약이 정해졌을 때
    /// rewardId로 연결하며, 현재는 UI와 콘텐츠 제작 데이터가 임의의 저장 구조를 만들지 않게 분리한다.
    /// </summary>
    [Serializable]
    public sealed class StageReward
    {
        public string rewardId = string.Empty;
        public string displayName = string.Empty;
        public Sprite icon;
        public int amount = 1;
    }
}
