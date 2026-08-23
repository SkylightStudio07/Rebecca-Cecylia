using RCCom.Core;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// EnemyInstance(순수 C#)의 시각 표현만 담당하는 MonoBehaviour. 로직은 갖지 않고,
    /// 매 프레임 EnemyInstance.position을 읽어 transform.position에 반영하고, Died/ReachedGoal
    /// 이벤트를 구독해 스스로를 파괴한다. WaveManager가 EnemyInstance를 스폰/Tick하고
    /// 이 View를 Bind해 붙여준다.
    ///
    /// 프리팹은 적 종류별로 따로 만들지 않고 1개만 두고 재사용한다 — Bind() 시점에
    /// EnemyDefinition.sprite를 읽어 자기 SpriteRenderer에 반영해 종류별 외형을 구분한다
    /// (GDD "툴백 라인": 로직/프리팹 재사용, 그래픽만 교체).
    ///
    /// 이 오브젝트의 Collider2D는 두 가지 용도로 쓰인다:
    /// 1) Tower의 Collider2D 트리거가 적을 감지하는 대상 (Tower 쪽에서 감지)
    /// 2) 자신이 플레이어/거점 등 IDamageable과 접촉했을 때 접촉 피해를 주는 판정 (아래 OnTriggerEnter2D)
    /// 트리거 감지엔 최소 한쪽에 Rigidbody2D도 필요 — 프리팹 설정 시 유의.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyView : MonoBehaviour
    {
        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField] private float hitFlashDuration = 0.1f;

        [Tooltip("자식 오브젝트로 둔 체력바(선택) — 회전은 EnemyView가 이동방향으로 매 프레임 돌리므로, 자식이면 그대로 두면 같이 돌아가 버려 여기서 역회전으로 상쇄한다")]
        [SerializeField] private EnemyHealthBar healthBar;

        [Header("회전 보간 (0 = 즉시 회전, 기존 동작)")]
        [Tooltip("목표 방향을 따라잡는 시간 상수(초). 0이면 기존처럼 즉시 스냅한다. 0.08~0.15 권장 — " +
                 "클수록 부드럽지만 코너에서 방향이 더 밀린다.")]
        [SerializeField] private float turnSmoothTime = 0f;

        private SpriteRenderer _spriteRenderer;
        private Color _baseColor;
        private float _hitFlashRemaining;
        private bool _hasFacing;

        public EnemyInstance Instance { get; private set; }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _baseColor = _spriteRenderer.color;
        }

        public void Bind(EnemyInstance instance)
        {
            Instance = instance;
            Instance.Died += HandleRemoved;
            Instance.ReachedGoal += HandleRemoved;
            Instance.Damaged += HandleDamaged;

            if (instance.definition.sprite != null)
            {
                _spriteRenderer.sprite = instance.definition.sprite;
            }
        }

        private void OnDestroy()
        {
            if (Instance != null)
            {
                Instance.Died -= HandleRemoved;
                Instance.ReachedGoal -= HandleRemoved;
                Instance.Damaged -= HandleDamaged;
            }
        }

        private void LateUpdate()
        {
            Vector2 currentPosition = Instance.position;
            UpdateFacing(currentPosition);

            transform.position = currentPosition;
            TickHitFlash();
            UpdateHealthBar();
        }

        /// <summary>
        /// healthBar가 자식 오브젝트라 위치는 자동으로 따라오지만, 이 오브젝트의 회전(이동방향
        /// 추적)까지 그대로 물려받으면 체력바가 같이 빙글빙글 돌아버린다. 그래서 매 프레임
        /// 월드 회전을 다시 identity로 되돌려 항상 수평으로 보이게 한다. 이 메서드가 자기
        /// transform.rotation을 이미 설정한 뒤에 호출되므로(같은 LateUpdate 안, 순서 보장됨)
        /// 타이밍 경쟁 없이 항상 정확히 상쇄된다.
        /// </summary>
        private void UpdateHealthBar()
        {
            if (healthBar == null)
            {
                return;
            }

            healthBar.transform.rotation = Quaternion.identity;
            healthBar.SetHealthPercent(Instance.currentHealth / Instance.Data.maxHealth);
        }

        /// <summary>
        /// 프레임 간 실제 이동량(델타) 대신 "지금 향하고 있는 다음 웨이포인트" 방향을 직접 써서
        /// 회전한다 — 델타 기반은 스폰 직후나 웨이포인트에 정확히 스냅되는 프레임처럼 실이동량이
        /// 0에 가까운 순간에 방향이 안 바뀌거나 흔들리는 문제가 있었다. 목적지 기준이면 실제로
        /// 아직 한 프레임도 안 움직인 상태에서도 항상 올바른 방향을 즉시 반영한다.
        /// </summary>
        private void UpdateFacing(Vector2 currentPosition)
        {
            Vector2? target = Instance.CurrentTargetWaypoint;
            if (!target.HasValue)
            {
                return;
            }

            Vector2 direction = target.Value - currentPosition;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + Instance.definition.spriteForwardOffsetDegrees;

            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);

            // 스폰 직후 첫 프레임은 보간하지 않는다 — Instantiate가 준 identity 회전에서
            // 서서히 돌아오면 등장하자마자 엉뚱한 방향을 보고 있게 된다.
            if (turnSmoothTime <= 0f || !_hasFacing)
            {
                transform.rotation = targetRotation;
                _hasFacing = true;
                return;
            }

            // 목표 각도는 웨이포인트 단위로 끊기는 계단 함수라(간격 0.5·곡률 반경 4 기준
            // 약 7도/스텝) 즉시 스냅하면 코너에서 각도가 딱딱 끊겨 보인다. 각속도 상한
            // (RotateTowards) 방식은 상한이 자연 회전 속도(이 경로 기준 약 42도/초)보다
            // 조금만 커도 한 프레임에 다 돌아버려 보간이 사실상 사라지는 튜닝 절벽이 있어서,
            // 스텝 크기와 무관하게 항상 부드러운 지수 감쇠(저역통과)를 쓴다.
            // 1 - exp(-dt/τ)는 프레임레이트가 변해도 같은 감쇠 속도를 유지한다(dt 비례 Lerp와 다름).
            // 등속 코너링 시 정상상태 지연은 ω×τ 정도라 τ=0.1이면 약 4도로 눈에 띄지 않는다.
            float t = 1f - Mathf.Exp(-Time.deltaTime / turnSmoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }

        /// <summary>
        /// 건설 프리뷰의 "설치 불가 = 빨간색"과 같은 방식 — 로직 없이 SpriteRenderer.color만
        /// 잠깐 바꿨다 되돌린다. 코루틴 대신 이 프로젝트 전반의 스타일(잔여시간 카운트다운 필드,
        /// TowerInstance.cooldownRemaining 등)에 맞춰 타이머 필드로 처리.
        /// </summary>
        private void TickHitFlash()
        {
            if (_hitFlashRemaining <= 0f)
            {
                return;
            }

            _hitFlashRemaining -= Time.deltaTime;
            _spriteRenderer.color = _hitFlashRemaining > 0f ? hitFlashColor : _baseColor;
        }

        private void HandleDamaged(float amount)
        {
            _hitFlashRemaining = hitFlashDuration;
            _spriteRenderer.color = hitFlashColor;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out IDamageable target))
            {
                Instance.DealContactDamageTo(target);
            }
        }

        private void HandleRemoved()
        {
            Destroy(gameObject);
        }
    }
}
