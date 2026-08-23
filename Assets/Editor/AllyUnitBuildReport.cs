using System.Collections.Generic;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 단일 아군 유닛 빌드에서 실제로 바뀐 에셋과 검증 결과를 전달한다.
    /// </summary>
    public sealed class AllyUnitBuildReport
    {
        public string unitId;
        public readonly List<string> changedAssets = new();
        public bool validationPassed;
    }
}
