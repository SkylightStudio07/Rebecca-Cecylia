using System;
using System.Collections.Generic;
using RCCom.Definitions.PlayerPart;

namespace RCCom.EditorTools
{
    [Serializable]
    public sealed class PlayerPartAssetRecipe
    {
        public string partId;
        public PlayerPartSlot slot;
        public PlayerPartGrade grade;
        public string archetypeId;
        public string displayName;
        public string description;
        public int price;
        public PlayerPartStatOverride statOverride = new();
        public List<PlayerPartEffectRecipe> effects = new();
        public string iconPath;
    }
}
