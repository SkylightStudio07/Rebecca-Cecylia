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
        [SerializeField] private float targetVisualSize = 0.9f;

        private SpriteRenderer _spriteRenderer;
        public AllyUnitInstance Instance { get; private set; }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Bind(AllyUnitInstance instance)
        {
            if (instance == null || !instance.IsSpawned)
            {
                throw new ArgumentException("스폰되지 않은 아군 유닛에는 View를 연결할 수 없습니다.", nameof(instance));
            }

            Instance = instance;
            Instance.Died += HandleDied;
            transform.position = Instance.Position;

            if (Instance.Definition.sprite != null)
            {
                _spriteRenderer.sprite = Instance.Definition.sprite;
                float scale = SpriteFit.CalculateUniformScale(Instance.Definition.sprite, targetVisualSize);
                transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void OnDestroy()
        {
            if (Instance != null)
            {
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
        }

        private void UpdateFacing(Vector2 position)
        {
            Vector2? target = Instance.CurrentTargetWaypoint;
            if (!target.HasValue)
            {
                return;
            }

            Vector2 direction = target.Value - position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg +
                          Instance.Definition.spriteForwardOffsetDegrees;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void HandleDied()
        {
            Destroy(gameObject);
        }
    }
}
