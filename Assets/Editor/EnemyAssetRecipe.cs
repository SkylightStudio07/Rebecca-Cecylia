using System;
using System.Collections.Generic;
using RCCom.Data;

namespace RCCom.EditorTools
{
    /// <summary>
    /// EnemyAssetBuilder가 읽는 JSON 한 파일의 모양. OperatorAssetRecipe와 동일한 이유로
    /// Unity 에셋 참조는 경로 문자열로 받아 에디터에서 강타입 참조로 변환한다 — 신규 적 추가
    /// 시 C# 클래스 대신 레시피와 아트만 추가하도록 만들기 위한 편집 전용 데이터다.
    ///
    /// enemyId/displayName이 최상위와 data(EnemyData) 양쪽에 존재하는 것은 EnemyData가
    /// 이미 저작용 필드로 이 둘을 갖고 있기 때문이다(기존 EnemyDefinition 구조를 유지).
    /// 빌더는 최상위 값을 정본으로 삼아 복제 시 data 쪽을 덮어써 둘이 어긋나지 않게 한다.
    /// </summary>
    [Serializable]
    public sealed class EnemyAssetRecipe
    {
        public string enemyId;
        public int catalogOrder;
        public string displayName;
        public string spritePath;
        public float spriteForwardOffsetDegrees = 90f;
        public List<string> effectPaths = new();
        public bool remoteContent;
        public EnemyData data = new();
    }
}
