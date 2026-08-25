using System.Collections.Generic;
using RCCom.Effects.PlayerPart;
using UnityEngine;

namespace RCCom.Definitions.PlayerPart
{
    /// <summary>
    /// 플레이어 파츠 한 종류의 스탯·기믹·표현을 조립하는 SO. 신규 파츠는 기존 Effect 에셋을
    /// 재조합하는 데이터 추가로 끝나며 플레이어 전투 클래스는 파츠 종류를 알지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Player Part/Part Definition")]
    public sealed class PlayerPartDefinition : ScriptableObject
    {
        public string partId;
        public PlayerPartSlot slot;
        public PlayerPartGrade grade;
        public string archetypeId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        [Min(0)] public int price;
        public PlayerPartStatOverride statOverride = new();
        public List<PlayerPartEffectBase> effects = new();
        public Sprite icon;
    }
}
