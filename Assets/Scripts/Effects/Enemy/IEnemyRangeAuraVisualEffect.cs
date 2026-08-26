using UnityEngine;

namespace RCCom.Effects.Enemy
{
    /// <summary>
    /// 적 Effect가 지속 사거리 표시를 요청하는 공용 계약. EnemyView는 구체 효과 타입을 모르고
    /// 이 계약만 읽으므로 이후 다른 적 오라도 같은 시각 런타임을 데이터 조립으로 재사용한다.
    /// </summary>
    public interface IEnemyRangeAuraVisualEffect
    {
        Material Material { get; }
        Color AuraColor { get; }
        float StrokeWidth { get; }
        float GlowIntensity { get; }
        float Opacity { get; }
    }
}
