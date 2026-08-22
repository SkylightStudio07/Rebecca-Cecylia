using System;
using RCCom.Data;

namespace RCCom.EditorTools
{
    /// <summary>
    /// OperatorAssetBuilder가 읽는 JSON 한 파일의 모양. Unity 에셋 참조는 경로로 받아
    /// 에디터에서 강타입 참조로 변환한다 — 신규 오퍼레이터 추가 시 C# 클래스 대신
    /// 레시피와 아트만 추가하도록 만들기 위한 편집 전용 데이터다.
    /// </summary>
    [Serializable]
    public sealed class OperatorAssetRecipe
    {
        public string operatorId;
        public string displayName;
        public string playStyleDescription;
        public string selectionPortraitPath;
        public string managementPortraitPath;
        public string sourceTowerRosterPath;
        public string sourceCardRosterPath;
        public string sourceAllyUnitRosterPath;
        public string dialogueSetPath;
        public bool remoteContent;
        public PlayerData playerData = new();
        public int requiredBestWave;
    }
}
