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

        [Header("회전 보간 (0 = 즉시 회전, 기존 동작)")]
        [Tooltip("목표 방향을 따라잡는 시간 상수(초). 0이면 기존처럼 즉시 스냅한다. 0.08~0.15 권장 — " +
                 "클수록 부드럽지만 코너에서 방향이 더 밀리고 조준도 함께 굼떠진다.")]
        [SerializeField] private float turnSmoothTime = 0f;

        private SpriteRenderer _spriteRenderer;
        private Color _baseColor;
        private float _hitFlashRemaining;
        private bool _hasFacing;
        private List<IAllyUnitVisualRuntime> _visualRuntimes;
        public AllyUnitInstance Instance { get; private set; }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
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

            Vector2 position = Instance.Position;
            transform.position = position;
            UpdateFacing(position);
            TickHitFlash();
            TickVisualEffects(Time.deltaTime);
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

        private void HandleDied()
        {
            Destroy(gameObject);
        }
    }
}
