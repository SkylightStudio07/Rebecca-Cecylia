using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// 트랙 하나가 변경하는 실제 값 하나. 트랙 안에 여러 modifier를 둘 수 있어, 한 번의 구매로
    /// 서로 다른 유닛에 서로 다른 증가량을 적용하면서도 UI와 경제에서는 한 트랙으로 유지한다.
    /// </summary>
    [Serializable]
    public sealed class OperatorUpgradeModifier
    {
        public OperatorUpgradeTargetKind targetKind = OperatorUpgradeTargetKind.AllyUnitMaxHealth;

        [Tooltip("아군 유닛 또는 아군 효과 대상일 때의 영구 유닛 ID. 지휘 포인트 대상은 비워 둔다.")]
        public string targetUnitId = string.Empty;

        [Tooltip("Lv1부터 순서대로 적용할 기준값 대비 델타. 트랙 maxLevel과 길이가 같아야 한다.")]
        public List<float> levelDeltas = new();

        [Tooltip("최종 결과가 정수인 필드(CP, 배치 비용 등)에 사용한다.")]
        public bool isInteger;

        public bool hasMinValue;
        public float minValue;
        public bool hasMaxValue;
        public float maxValue;

        public float GetDelta(int level)
        {
            if (level <= 0 || levelDeltas == null || levelDeltas.Count == 0)
            {
                return 0f;
            }

            int index = Mathf.Clamp(level, 1, levelDeltas.Count) - 1;
            return levelDeltas[index];
        }

        public float ClampResult(float value)
        {
            if (isInteger)
            {
                value = Mathf.Round(value);
            }

            if (hasMinValue)
            {
                value = Mathf.Max(minValue, value);
            }

            if (hasMaxValue)
            {
                value = Mathf.Min(maxValue, value);
            }

            return value;
        }
    }
}
