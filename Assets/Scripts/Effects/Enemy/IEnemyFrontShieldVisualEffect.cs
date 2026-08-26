using UnityEngine;

namespace RCCom.Effects.Enemy
{
    /// <summary>이동 방향 앞쪽에 지속 반원 방어막을 표시하는 적 Effect의 공용 시각 계약.</summary>
    public interface IEnemyFrontShieldVisualEffect
    {
        Material Material { get; }
        Color ShieldColor { get; }
        float ShieldRadius { get; }
        float StrokeWidth { get; }
        float GlowIntensity { get; }
        float FillOpacity { get; }
        float Opacity { get; }
    }
}
