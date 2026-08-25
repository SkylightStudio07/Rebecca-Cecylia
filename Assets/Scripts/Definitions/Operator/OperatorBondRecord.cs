using System;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// 호감도 단계에서 공개되는 오퍼레이터 인연 기록 한 건.
    /// 단계명과 요구 수치는 전 오퍼레이터가 공유하므로 런타임 UI가 소유하고,
    /// 원격 콘텐츠로 교체할 수 있어야 하는 본문만 Definition에 둔다.
    /// </summary>
    [Serializable]
    public sealed class OperatorBondRecord
    {
        [TextArea(3, 8)]
        public string description = string.Empty;
    }
}
