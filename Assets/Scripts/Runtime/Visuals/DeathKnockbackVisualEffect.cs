using UnityEngine;

namespace RCCom.Runtime.Visuals
{
    /// <summary>
    /// 사망 연출 고도화(폭발 넉백) 튜닝값. ShockwaveRingVisualEffect/LaserBeamVisualEffect와 같은
    /// 이유로 데이터 SO로 뺀다 — EnemyView와 AllyUnitView가 이 SO 하나를 공유해서 쓴다(둘 다
    /// 같은 "폭발에 밀려나는" 연출이라 굳이 따로 둘 이유가 없음).
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/VFX/Death Knockback Visual Effect")]
    public class DeathKnockbackVisualEffect : ScriptableObject
    {
        [Header("Knockback (Ease Out, 랜덤 바깥 방향)")]
        [SerializeField, Min(0f)] private float knockbackDistance = 0.25f;
        [SerializeField, Min(0.01f)] private float knockbackDuration = 0.2f;
        [SerializeField, Range(0f, 90f)] private float minRotationDegrees = 15f;
        [SerializeField, Range(0f, 90f)] private float maxRotationDegrees = 30f;

        [Header("넉백과 동시에 적용되는 틴트/알파")]
        [SerializeField, Range(0f, 1f)] private float targetAlpha = 0.75f;
        [Tooltip("원본 색상 RGB에 곱해지는 밝기 배율. 1=원래 밝기, 0=검정.")]
        [SerializeField, Range(0f, 1f)] private float tintBrightness = 0.55f;

        [Header("정지 유지 → 페이드아웃")]
        [SerializeField, Min(0f)] private float holdDuration = 1.25f;
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.35f;

        public float KnockbackDistance => Mathf.Max(0f, knockbackDistance);
        public float KnockbackDuration => Mathf.Max(0.01f, knockbackDuration);
        public float MinRotationDegrees => Mathf.Min(minRotationDegrees, maxRotationDegrees);
        public float MaxRotationDegrees => Mathf.Max(minRotationDegrees, maxRotationDegrees);
        public float TargetAlpha => Mathf.Clamp01(targetAlpha);
        public float TintBrightness => Mathf.Clamp01(tintBrightness);
        public float HoldDuration => Mathf.Max(0f, holdDuration);
        public float FadeOutDuration => Mathf.Max(0.01f, fadeOutDuration);
    }
}
