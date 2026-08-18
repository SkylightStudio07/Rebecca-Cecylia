using System;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// Definition을 내려받기 전 선택 화면에 표시할 유닛의 경량 미리보기 데이터다.
    /// 실제 전투 수치는 AllyUnitDefinition에만 남기고, 카탈로그 빌더가 표시값만 자동 파생한다.
    /// </summary>
    [Serializable]
    public sealed class OperatorUnitPreview
    {
        public string displayName;
        public int deployCost;
        public Sprite previewIcon;
        public Color fallbackColor = Color.white;
    }
}
