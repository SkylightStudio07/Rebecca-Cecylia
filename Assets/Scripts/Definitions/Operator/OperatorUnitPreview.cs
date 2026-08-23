using System;
using RCCom.Definitions.Unit;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// 이전 카탈로그 에셋과의 소스 호환성을 위한 이름만 남긴다.
    /// 실제 필드와 정본은 AllyUnitCatalogEntry가 소유하며 새 코드에서는 그 타입을 사용한다.
    /// </summary>
    [Serializable]
    [Obsolete("AllyUnitCatalogEntry를 사용하세요.")]
    public sealed class OperatorUnitPreview : AllyUnitCatalogEntry
    {
    }
}
