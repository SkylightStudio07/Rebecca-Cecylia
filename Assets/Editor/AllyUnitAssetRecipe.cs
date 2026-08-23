using System;
using System.Collections.Generic;
using RCCom.Data;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// AllyUnitAssetBuilder가 읽는 JSON 한 파일의 모양.
    /// Sprite와 효과 SO는 경로 문자열로 저장하고 빌드 시 강타입 참조로 변환해,
    /// 신규 유닛을 추가할 때 런타임 C# 클래스를 만들지 않도록 한다.
    /// </summary>
    [Serializable]
    public sealed class AllyUnitAssetRecipe
    {
        public string unitId;
        public int catalogOrder;
        public string displayName;
        public string spritePath;
        public Color tint = Color.white;
        public float spriteForwardOffsetDegrees;
        public List<string> effectPaths = new();
        public bool remoteContent;
        public AllyUnitData data = new();
    }
}
