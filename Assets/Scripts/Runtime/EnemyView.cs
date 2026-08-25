using RCCom.Core;
using RCCom.Runtime.Visuals;
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

        [Header("사망 연출")]
        [Tooltip("사망 시 재생할 폭발 플립북(SpriteFlipbook, Assets/Art/VFX/explosion) 프리팹. 비워두면 넉백/페이드만 재생된다.")]
        [SerializeField] private GameObject deathExplosionPrefab;
        [Tooltip("폭발에 밀려나며 회전+반투명+어두운 틴트 → 정지 유지 → 페이드아웃하는 연출의 튜닝값. " +
                 "비워두면 DeathKnockbackSequencer의 기본값으로 재생된다.")]
        [SerializeField] private DeathKnockbackVisualEffect deathKnockbackVisual;

        [Tooltip("자식 오브젝트로 둔 체력바(선택) — 회전은 EnemyView가 이동방향으로 매 프레임 돌리므로, 자식이면 그대로 두면 같이 돌아가 버려 여기서 역회전으로 상쇄한다")]
        [SerializeField] private UnitHealthBar healthBar;

        [Header("회전 보간 (0 = 즉시 회전, 기존 동작)")]
        [Tooltip("목표 방향을 따라잡는 시간 상수(초). 0이면 기존처럼 즉시 스냅한다. 0.08~0.15 권장 — " +
                 "클수록 부드럽지만 코너에서 방향이 더 밀린다.")]
        [SerializeField] private float turnSmoothTime = 0f;

        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider;
        private Color _baseColor;
        private float _hitFlashRemaining;
        private bool _hasFacing;
        private float _boundMaxHealth;
        private bool _isDying;
        private Vector3 _baseLocalScale;
        private Vector3 _healthBarBaseLocalScale;
        private CircleCollider2D _circleCollider;
        private Vector2 _circleColliderBaseOffset;
        private float _circleColliderBaseRadius;
        private readonly DeathKnockbackSequencer _deathSequencer = new();

        public EnemyInstance Instance { get; private set; }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
            _circleCollider = _collider as CircleCollider2D;
            _baseColor = _spriteRenderer.color;
            _baseLocalScale = transform.localScale;
            if (healthBar != null)
            {
                _healthBarBaseLocalScale = healthBar.transform.localScale;
            }

            if (_circleCollider != null)
            {
                _circleColliderBaseOffset = _circleCollider.offset;
                _circleColliderBaseRadius = _circleCollider.radius;
            }
        }

        public void Bind(EnemyInstance instance)
        {
            Instance = instance;
            // WaveManager가 웨이브/스테이지 체력 배율을 적용한 뒤 View를 Bind한다. 원본
            // Definition의 maxHealth를 분모로 쓰면 배율로 늘어난 체력이 100%를 초과해,
            // 실제로 피해를 받아도 체력바가 한동안 만피로 Clamp되어 숨겨진다.
            _boundMaxHealth = Mathf.Max(instance.currentHealth, Mathf.Epsilon);
            Instance.Died += HandleDied;
            Instance.ReachedGoal += HandleReachedGoal;
            Instance.Damaged += HandleDamaged;

            // 재사용을 대비한 방어적 초기화 — 지금은 사망 시 실제로 Destroy까지 가지만,
            // 향후 풀링이 추가되더라도 이전 개체의 사망 연출 상태가 새 개체에 새어나가지 않게.
            _isDying = false;
            transform.rotation = Quaternion.identity;
            if (_collider != null)
            {
                _collider.enabled = true;
            }

            Color restoredColor = _baseColor;
            restoredColor.a = 1f;
            _spriteRenderer.color = restoredColor;

            if (instance.definition.sprite != null)
            {
                _spriteRenderer.sprite = instance.definition.sprite;
            }

            ApplyVisualSize(_spriteRenderer.sprite);
        }

        /// <summary>
        /// 보스 확대는 그리기만 바꾸는 연출이므로 접촉 판정까지 커지면 안 된다. 공용 SpriteFit으로
        /// 현재 스프라이트의 원래 drawing size를 기준값으로 삼고 승급 보스에만 1.5배를 적용한 뒤,
        /// 같은 루트에 붙은 CircleCollider2D는 역보정해 월드 반경과 오프셋을 그대로 유지한다.
        /// </summary>
        private void ApplyVisualSize(Sprite sprite)
        {
            float visualMultiplier = Instance.IsPromotedBoss
                ? EndlessBossPromotion.VisualSizeMultiplier
                : 1f;
            float nativeTargetSize = sprite != null
                ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)
                : 0f;
            float fittedScale = SpriteFit.CalculateUniformScale(
                sprite,
                nativeTargetSize * visualMultiplier);
            transform.localScale = new Vector3(
                _baseLocalScale.x * fittedScale,
                _baseLocalScale.y * fittedScale,
                _baseLocalScale.z);

            if (_circleCollider != null)
            {
                _circleCollider.offset = _circleColliderBaseOffset / visualMultiplier;
                _circleCollider.radius = _circleColliderBaseRadius / visualMultiplier;
            }
        }

        private void OnDestroy()
        {
            if (Instance != null)
            {
                Instance.Died -= HandleDied;
                Instance.ReachedGoal -= HandleReachedGoal;
                Instance.Damaged -= HandleDamaged;
            }
        }

        private void LateUpdate()
        {
            if (_isDying)
            {
                TickDeath();
                return;
            }

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
            float visualMultiplier = Instance.IsPromotedBoss
                ? EndlessBossPromotion.VisualSizeMultiplier
                : 1f;
            healthBar.transform.localScale = new Vector3(
                _healthBarBaseLocalScale.x / visualMultiplier,
                _healthBarBaseLocalScale.y / visualMultiplier,
                _healthBarBaseLocalScale.z);
            healthBar.SetHealthPercent(Instance.currentHealth / _boundMaxHealth);
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

        /// <summary>
        /// 즉시 Destroy하는 대신 Collider2D부터 꺼서(더 이상 판정에 관여하지 않도록) 폭발
        /// 플립북을 재생하고, 스프라이트는 넉백(밀려남+회전+반투명+어두운 틴트) → 정지 유지 →
        /// 페이드아웃 순으로 진행한 뒤 파괴한다(DeathKnockbackSequencer, 사망 연출 고도화).
        /// 완전한 단색 실루엣 고정은 셰이더 없이는 못 만들어서(SpriteRenderer.color 곱연산으로는
        /// 원본 명암이 계속 비쳐 보임) 의도적으로 포기했다(VFX_전투_연출_설계안.md §4 —
        /// ProjectBloodmoon 실루엣 셰이더 이식 파기 결정에 따른 트레이드오프).
        /// </summary>
        private void HandleDied()
        {
            if (_isDying)
            {
                return;
            }

            _isDying = true;
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            // 죽는 순간 즉시 감춘다 — 안 감추면 넉백/회전 중에도 그대로 붙어 있다가(HealthBar는
            // UpdateHealthBar가 더 이상 안 불려서 마지막 수치에 얼어붙은 채로) 회전만 안 따라와
            // 어색해 보인다.
            if (healthBar != null)
            {
                healthBar.gameObject.SetActive(false);
            }

            SpriteFlipbook.Spawn(deathExplosionPrefab, transform.position);
            _deathSequencer.Begin(
                transform.position,
                _baseColor,
                deathKnockbackVisual,
                Instance.LastDamageSourcePosition,
                transform.eulerAngles.z);
        }

        /// <summary>거점 도달로 인한 제거는 처치가 아니므로 넉백/페이드 없이 기존처럼 즉시 사라진다.</summary>
        private void HandleReachedGoal()
        {
            Destroy(gameObject);
        }

        private void TickDeath()
        {
            _deathSequencer.Tick(Time.deltaTime);
            transform.position = _deathSequencer.Position;
            transform.rotation = Quaternion.Euler(0f, 0f, _deathSequencer.RotationDegrees);
            _spriteRenderer.color = _deathSequencer.TintColor;

            if (_deathSequencer.IsFinished)
            {
                Destroy(gameObject);
            }
        }
    }
}
