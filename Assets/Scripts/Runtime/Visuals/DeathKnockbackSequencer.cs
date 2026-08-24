using UnityEngine;

namespace RCCom.Runtime.Visuals
{
    /// <summary>
    /// 사망 넉백 연출(폭발에 밀려나며 회전+반투명+어두운 틴트 → 정지 유지 → 페이드아웃)의 3단계
    /// 진행을 순수 계산만 한다. EnemyView/AllyUnitView 둘 다 거의 동일한 사망 연출이 필요한데
    /// (이전엔 이런 이유로 "두 파일 모두 동일 패턴으로 확장"해왔지만, 이번엔 상태 필드가 많이
    /// 늘어나 그대로 중복하기엔 부담스러워 계산 부분만 분리했다) — MonoBehaviour에는 의존하지
    /// 않고 Position/RotationDegrees/TintColor 값만 반환하니, 실제 SpriteRenderer/Transform
    /// 적용은 호출하는 View가 직접 한다(RangePulseVisualRuntime과 그 SO의 관계와 같은 구조).
    /// </summary>
    public sealed class DeathKnockbackSequencer
    {
        private enum Phase
        {
            Knockback,
            Hold,
            FadeOut,
            Done,
        }

        private const float DefaultKnockbackDistance = 0.25f;
        private const float DefaultKnockbackDuration = 0.2f;
        private const float DefaultMinRotationDegrees = 15f;
        private const float DefaultMaxRotationDegrees = 30f;
        private const float DefaultTargetAlpha = 0.75f;
        private const float DefaultTintBrightness = 0.55f;
        private const float DefaultHoldDuration = 1.25f;
        private const float DefaultFadeOutDuration = 0.35f;

        private Phase _phase = Phase.Done;
        private float _phaseDuration;
        private float _phaseRemaining;
        private float _holdDuration;
        private float _fadeOutDuration;
        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private float _targetRotationDegrees;
        private Color _baseColor;
        private Color _tintColor;

        public Vector3 Position { get; private set; }
        public float RotationDegrees { get; private set; }
        public Color TintColor { get; private set; }
        public bool IsFinished => _phase == Phase.Done;

        /// <summary>
        /// 사망 확정 순간 1회 호출. config가 null이면(아직 SO를 안 꽂았으면) 기본값으로 진행한다 —
        /// 시각 효과 미배치가 사망 처리 자체를 막으면 안 된다는 원칙과 같은 이유.
        /// </summary>
        public void Begin(Vector3 startPosition, Color baseColor, DeathKnockbackVisualEffect config)
        {
            _startPosition = startPosition;
            _baseColor = baseColor;
            Position = startPosition;
            RotationDegrees = 0f;
            TintColor = baseColor;

            float distance = config != null ? config.KnockbackDistance : DefaultKnockbackDistance;
            float minDegrees = config != null ? config.MinRotationDegrees : DefaultMinRotationDegrees;
            float maxDegrees = config != null ? config.MaxRotationDegrees : DefaultMaxRotationDegrees;
            float targetAlpha = config != null ? config.TargetAlpha : DefaultTargetAlpha;
            float tintBrightness = config != null ? config.TintBrightness : DefaultTintBrightness;
            _holdDuration = config != null ? config.HoldDuration : DefaultHoldDuration;
            _fadeOutDuration = config != null ? config.FadeOutDuration : DefaultFadeOutDuration;

            // "바깥으로" 밀려나는 방향 — 폭발이 유닛 자기 자신의 위치에서 터지는 것이라 별도
            // 기준점이 없으니 랜덤 각도를 쓴다(여러 개체가 동시에 죽어도 전부 같은 방향으로
            // 밀리지 않게).
            float randomAngle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle), 0f);
            _targetPosition = _startPosition + direction * distance;

            float rotationMagnitude = Random.Range(minDegrees, maxDegrees);
            _targetRotationDegrees = Random.value < 0.5f ? -rotationMagnitude : rotationMagnitude;
            _tintColor = new Color(
                baseColor.r * tintBrightness, baseColor.g * tintBrightness, baseColor.b * tintBrightness, targetAlpha);

            SetPhase(Phase.Knockback, config != null ? config.KnockbackDuration : DefaultKnockbackDuration);
        }

        /// <summary>매 프레임 호출 — 내부 상태와 Position/RotationDegrees/TintColor를 갱신한다.</summary>
        public void Tick(float deltaTime)
        {
            if (_phase == Phase.Done)
            {
                return;
            }

            _phaseRemaining -= deltaTime;
            float elapsed = _phaseDuration > 0f ? Mathf.Clamp01(1f - _phaseRemaining / _phaseDuration) : 1f;

            switch (_phase)
            {
                case Phase.Knockback:
                    // OutQuad 이징 — ShockwaveRing과 같은 계산식(초반에 빠르게, 끝에서 느려짐).
                    float eased = 1f - (1f - elapsed) * (1f - elapsed);
                    Position = Vector3.LerpUnclamped(_startPosition, _targetPosition, eased);
                    RotationDegrees = Mathf.LerpUnclamped(0f, _targetRotationDegrees, eased);
                    TintColor = Color.Lerp(_baseColor, _tintColor, eased);
                    break;
                case Phase.Hold:
                    // Position/RotationDegrees/TintColor는 넉백 종료 시점 값 그대로 유지.
                    break;
                case Phase.FadeOut:
                    Color faded = _tintColor;
                    faded.a = Mathf.Lerp(_tintColor.a, 0f, elapsed);
                    TintColor = faded;
                    break;
            }

            if (_phaseRemaining <= 0f)
            {
                Advance();
            }
        }

        private void Advance()
        {
            switch (_phase)
            {
                case Phase.Knockback:
                    SetPhase(Phase.Hold, _holdDuration);
                    break;
                case Phase.Hold:
                    SetPhase(Phase.FadeOut, _fadeOutDuration);
                    break;
                case Phase.FadeOut:
                    _phase = Phase.Done;
                    break;
            }
        }

        private void SetPhase(Phase phase, float duration)
        {
            _phase = phase;
            _phaseDuration = Mathf.Max(0.0001f, duration);
            _phaseRemaining = _phaseDuration;
        }
    }
}
