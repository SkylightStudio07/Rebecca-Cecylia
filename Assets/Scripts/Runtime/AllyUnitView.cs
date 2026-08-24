using System;
using System.Collections.Generic;
using RCCom.Effects.UnitVisual;
using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// AllyUnitInstance의 위치와 Definition 스프라이트만 표현하는 공용 View.
    /// 소환 직후 UnitDeployController가 Bind를 호출하며 게임 규칙은 이 MonoBehaviour에 두지 않는다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class AllyUnitView : MonoBehaviour
    {
        private static Sprite _fallbackSprite;

        [SerializeField] private float targetVisualSize = 2.0f;
        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField] private float hitFlashDuration = 0.1f;

        [Header("체력바")]
        [Tooltip("자식 오브젝트로 둔 체력바(선택). EnemyView와 같은 UnitHealthBar를 재사용한다.")]
        [SerializeField] private UnitHealthBar healthBar;
        [Tooltip("체력바 위치 — 유닛 스프라이트 기준 월드 단위 오프셋. 아군은 유닛별로 " +
                 "SpriteFit 스케일이 달라(EnemyView는 고정 스케일 1이라 이 보정이 필요 없었음) " +
                 "이 오프셋을 스케일 역보정해서 적용한다 — 유닛 크기와 무관하게 항상 같은 " +
                 "간격으로 보이게 하기 위함.")]
        [SerializeField] private Vector2 healthBarOffset = new(0f, -1.3f);

        [Header("사망 연출")]
        [Tooltip("사망 시 재생할 폭발 플립북(SpriteFlipbook, Assets/Art/VFX/explosion) 프리팹. 비워두면 넉백/페이드만 재생된다.")]
        [SerializeField] private GameObject deathExplosionPrefab;
        [Tooltip("폭발에 밀려나며 회전+반투명+어두운 틴트 → 정지 유지 → 페이드아웃하는 연출의 튜닝값. " +
                 "비워두면 DeathKnockbackSequencer의 기본값으로 재생된다. EnemyView와 SO를 공유한다.")]
        [SerializeField] private DeathKnockbackVisualEffect deathKnockbackVisual;

        [Header("회전 보간 (0 = 즉시 회전, 기존 동작)")]
        [Tooltip("목표 방향을 따라잡는 시간 상수(초). 0이면 기존처럼 즉시 스냅한다. 0.08~0.15 권장 — " +
                 "클수록 부드럽지만 코너에서 방향이 더 밀리고 조준도 함께 굼떠진다.")]
        [SerializeField] private float turnSmoothTime = 0f;

        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider;
        private Color _baseColor;
        private float _hitFlashRemaining;
        private bool _hasFacing;
        private bool _isDying;
        private readonly DeathKnockbackSequencer _deathSequencer = new();
        private List<IAllyUnitVisualRuntime> _visualRuntimes;
        public AllyUnitInstance Instance { get; private set; }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
            _baseColor = _spriteRenderer.color;
        }

        public void Bind(AllyUnitInstance instance)
        {
            if (instance == null || !instance.IsSpawned)
            {
                throw new ArgumentException("스폰되지 않은 아군 유닛에는 View를 연결할 수 없습니다.", nameof(instance));
            }

            if (Instance != null)
            {
                Instance.Damaged -= HandleDamaged;
                Instance.Died -= HandleDied;
            }

            DisposeVisualEffects();

            Instance = instance;
            Instance.Damaged += HandleDamaged;
            Instance.Died += HandleDied;
            transform.position = Instance.Position;

            // 재사용을 대비한 방어적 초기화 — 지금은 사망 시 실제로 Destroy까지 가지만,
            // 향후 풀링이 추가되더라도 이전 개체의 사망 연출 상태가 새 개체에 새어나가지 않게.
            _isDying = false;
            transform.rotation = Quaternion.identity;
            if (_collider != null)
            {
                _collider.enabled = true;
            }

            if (healthBar != null)
            {
                healthBar.gameObject.SetActive(true);
            }

            _baseColor = Instance.Definition.tint;
            _spriteRenderer.color = _baseColor;

            Sprite visualSprite = Instance.Definition.sprite != null
                ? Instance.Definition.sprite
                : GetFallbackSprite();
            if (visualSprite != null)
            {
                // 아트가 들어오기 전에도 회색상자 전투의 위치·전열을 눈으로 검증할 수 있게
                // 내장 흰 텍스처로 만든 사각형만 임시 사용한다. Definition 스프라이트가 들어오면
                // 이 경로는 자동으로 사라져 프리팹이나 유닛별 C# 타입을 추가할 필요가 없다.
                _spriteRenderer.sprite = visualSprite;
                float scale = SpriteFit.CalculateUniformScale(visualSprite, targetVisualSize);
                transform.localScale = new Vector3(scale, scale, 1f);
            }

            CreateVisualEffects();
        }

        private static Sprite GetFallbackSprite()
        {
            if (_fallbackSprite != null)
            {
                return _fallbackSprite;
            }

            _fallbackSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            return _fallbackSprite;
        }

        private void OnDestroy()
        {
            if (Instance != null)
            {
                Instance.Damaged -= HandleDamaged;
                Instance.Died -= HandleDied;
            }

            DisposeVisualEffects();
        }

        private void LateUpdate()
        {
            if (Instance == null)
            {
                return;
            }

            if (_isDying)
            {
                TickDeath();
                return;
            }

            Vector2 position = Instance.Position;
            transform.position = position;
            UpdateFacing(position);
            TickHitFlash();
            UpdateHealthBar();
            TickVisualEffects(Time.deltaTime);
        }

        /// <summary>
        /// healthBar는 프리팹상 이 오브젝트의 자식이지만, 위치/회전은 매 프레임 월드 좌표로
        /// 직접 계산해 SetPositionAndRotation으로 못박는다 — 로컬 좌표로 오프셋을 두면 부모
        /// 회전(이동/조준 추적)이 곱해져서, 유닛이 위를 보면 체력바가 반대로 튀어 오르는 버그가
        /// 났었다({{user}} 스크린샷으로 발견, 2026-08-24: 회전만 identity로 되돌려도 "이미
        /// 회전된 로컬 오프셋으로 계산된 위치" 자체는 못 되돌림 — Transform.rotation 세터는
        /// 위치엔 전혀 관여하지 않기 때문). 월드 좌표로 직접 계산하면 부모 스케일/회전과
        /// 완전히 무관해져 이 클래스 하나로 문제가 끝난다(EnemyView는 오프셋을 회전-보정된
        /// HealthBar의 "손자"에 둬서 우연히 문제가 없었던 것 — 그 구조를 따라할 수도 있었지만
        /// 이쪽이 훨씬 명시적이고 스케일 보정도 필요 없어져 더 단순하다).
        /// </summary>
        private void UpdateHealthBar()
        {
            if (healthBar == null)
            {
                return;
            }

            // 체력바가 자식이라 부모(이 오브젝트) 스케일을 그대로 물려받는데, 유닛마다 SpriteFit
            // 스케일이 달라 그대로 두면 체력바 크기도 유닛마다 들쭉날쭉해진다 — 역보정해서 항상
            // 같은 월드 크기로 보이게 한다(위치/회전과 달리 SetPositionAndRotation으로는 스케일을
            // 못 건드리므로 별도로 처리).
            Vector3 lossyScale = transform.lossyScale;
            float inverseX = lossyScale.x != 0f ? 1f / lossyScale.x : 1f;
            float inverseY = lossyScale.y != 0f ? 1f / lossyScale.y : 1f;
            healthBar.transform.localScale = new Vector3(inverseX, inverseY, 1f);

            Vector3 worldPosition = (Vector3)Instance.Position + new Vector3(healthBarOffset.x, healthBarOffset.y, 0f);
            healthBar.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            healthBar.SetHealthPercent(Instance.CurrentHealth / Mathf.Max(Instance.Data.maxHealth, Mathf.Epsilon));
        }

        private void CreateVisualEffects()
        {
            if (Instance.Definition.visualEffects == null ||
                Instance.Definition.visualEffects.Count == 0)
            {
                return;
            }

            _visualRuntimes ??= new List<IAllyUnitVisualRuntime>();
            var ctx = new AllyUnitVisualContext
            {
                view = this,
                instance = Instance,
                sortingLayerId = _spriteRenderer.sortingLayerID,
                sortingOrder = _spriteRenderer.sortingOrder - 1,
            };

            foreach (AllyUnitVisualEffectBase visualEffect in Instance.Definition.visualEffects)
            {
                if (visualEffect == null)
                {
                    continue;
                }

                IAllyUnitVisualRuntime runtime = visualEffect.CreateRuntime(ctx);
                if (runtime != null)
                {
                    _visualRuntimes.Add(runtime);
                }
            }
        }

        private void TickVisualEffects(float deltaTime)
        {
            if (_visualRuntimes == null)
            {
                return;
            }

            foreach (IAllyUnitVisualRuntime runtime in _visualRuntimes)
            {
                runtime.Tick(deltaTime);
            }
        }

        private void DisposeVisualEffects()
        {
            if (_visualRuntimes == null)
            {
                return;
            }

            foreach (IAllyUnitVisualRuntime runtime in _visualRuntimes)
            {
                runtime.Dispose();
            }

            _visualRuntimes.Clear();
        }

        private void UpdateFacing(Vector2 position)
        {
            Vector2 targetPosition;
            if (Instance.CurrentTarget != null && Instance.IsTargetInAttackRange(Instance.CurrentTarget))
            {
                targetPosition = Instance.CurrentTarget.position;
            }
            else
            {
                Vector2? waypoint = Instance.CurrentTargetWaypoint;
                if (!waypoint.HasValue)
                {
                    return;
                }

                targetPosition = waypoint.Value;
            }

            Vector2 direction = targetPosition - position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg +
                          Instance.Definition.spriteForwardOffsetDegrees;

            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);

            // 소환 직후 첫 프레임은 보간하지 않는다 — Instantiate가 준 회전에서 서서히
            // 돌아오면 등장하자마자 엉뚱한 방향을 보고 있게 된다.
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
            // 위 분기에서 공격 대상을 바라볼 때도 같은 보간이 걸린다 — 조준도 "방향 전환"이라
            // 동일하게 취급하며, 조준이 굼떠 보이면 이 값을 줄이거나 0으로 두면 된다.
            float t = 1f - Mathf.Exp(-Time.deltaTime / turnSmoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }

        /// <summary>
        /// 이벤트가 발생한 순간 즉시 틴트를 바꾸고, 코루틴 대신 잔여시간을 매 프레임
        /// 줄여 여러 아군 View가 동시에 피격되어도 인스턴스별로 독립적으로 되돌린다.
        /// </summary>
        private void TickHitFlash()
        {
            if (_hitFlashRemaining <= 0f)
            {
                _spriteRenderer.color = _baseColor;
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

        /// <summary>
        /// 즉시 Destroy하는 대신 Collider2D부터 꺼서(있다면) 폭발 플립북을 재생하고, 스프라이트는
        /// 넉백(밀려남+회전+반투명+어두운 틴트) → 정지 유지 → 페이드아웃 순으로 진행한 뒤
        /// 파괴한다(DeathKnockbackSequencer, EnemyView와 동일 — 사망 연출 고도화). 완전한 단색
        /// 실루엣 고정은 셰이더 없이는 못 만들어서 의도적으로 포기했다(VFX_전투_연출_설계안.md §4).
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

            if (healthBar != null)
            {
                healthBar.gameObject.SetActive(false);
            }

            // 사망한 유닛이 오라 버프 등을 계속 발산하면 안 되므로 스프라이트보다 먼저 끈다.
            DisposeVisualEffects();
            SpriteFlipbook.Spawn(deathExplosionPrefab, transform.position);
            _deathSequencer.Begin(
                transform.position,
                _baseColor,
                deathKnockbackVisual,
                Instance.LastDamageSourcePosition,
                transform.eulerAngles.z);
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
