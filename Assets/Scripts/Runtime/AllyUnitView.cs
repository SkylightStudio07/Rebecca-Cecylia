using System;
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

        [SerializeField] private float targetVisualSize = 0.9f;
        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField] private float hitFlashDuration = 0.1f;

        private SpriteRenderer _spriteRenderer;
        private Color _baseColor;
        private float _hitFlashRemaining;
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
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
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
