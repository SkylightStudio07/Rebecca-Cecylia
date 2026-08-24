using System.Collections.Generic;
using RCCom.Data;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 이미 적용된 데미지를 따라가는 가짜 투사체. 공격 판정을 늦추지 않는 순수 렌더러라서
    /// 투사체가 늦게 도착하거나 프리팹이 비어 있어도 전투 결과에는 영향을 주지 않는다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FakeProjectile : MonoBehaviour
    {
        private const float MinimumTravelDuration = 0.05f;
        private const float MaximumTravelDuration = 0.1f;

        [Tooltip("호출부가 속도를 넘기지 않을 때 사용할 이동 시간. 실제 이동 시간은 0.05~0.1초로 제한된다.")]
        [SerializeField, Min(MinimumTravelDuration)] private float fallbackTravelDuration = 0.08f;

        [Tooltip("투사체가 목표 지점에 도착할 때 재생할 히트 스파크 ParticleBurst 프리팹")]
        [SerializeField] private GameObject hitSparkPrefab;

        // AttackFlash와 같은 이유로 프리팹별 풀을 분리한다. 서로 다른 오퍼레이터가 같은
        // 호출부를 공유해도 투사체의 스프라이트·파티클 조합이 섞이지 않아야 한다.
        private static readonly Dictionary<GameObject, Queue<FakeProjectile>> _availablePool = new();

        private SpriteRenderer _spriteRenderer;
        private GameObject _sourcePrefab;
        private Vector3 _from;
        private Vector3 _to;
        private float _remainingTravelTime;
        private float _travelDuration;
        private bool _isActive;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _spriteRenderer.enabled = false;
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            _remainingTravelTime -= Time.deltaTime;
            float progress = _travelDuration > 0f
                ? 1f - Mathf.Clamp01(_remainingTravelTime / _travelDuration)
                : 1f;
            transform.position = Vector3.Lerp(_from, _to, progress);

            if (_remainingTravelTime > 0f)
            {
                return;
            }

            // 도착 순간에만 히트 스파크를 띄운다. TakeDamage는 호출부에서 이미 끝났으므로
            // 이 시각 효과의 타이밍이 판정 타이밍을 뒤로 미루지 않는다.
            ParticleBurst.Spawn(hitSparkPrefab, _to);
            Deactivate();
        }

        private void Play(Vector3 from, Vector3 to, float projectileSpeed)
        {
            _from = from;
            _to = to;
            _travelDuration = CalculateTravelDuration(from, to, projectileSpeed);
            _remainingTravelTime = _travelDuration;
            transform.SetPositionAndRotation(from, CalculateRotation(from, to));
            _spriteRenderer.enabled = true;
            _isActive = true;
        }

        private void Deactivate()
        {
            _isActive = false;
            _spriteRenderer.enabled = false;

            if (_sourcePrefab == null)
            {
                return;
            }

            if (!_availablePool.TryGetValue(_sourcePrefab, out Queue<FakeProjectile> queue))
            {
                queue = new Queue<FakeProjectile>();
                _availablePool[_sourcePrefab] = queue;
            }

            queue.Enqueue(this);
        }

        /// <summary>
        /// 속도를 데이터에서 해석하지 않는 호출부용 진입점. 타워 효과처럼 현재 컨텍스트에
        /// 투사체 속도 필드가 없는 경우 프리팹의 0.08초 기본값을 사용한다.
        /// </summary>
        public static void Spawn(GameObject prefab, Vector3 from, Vector3 to)
        {
            Spawn(prefab, from, to, 0f);
        }

        /// <summary>플레이어 데이터의 실제 투사체 속도를 사용하는 진입점.</summary>
        public static void Spawn(GameObject prefab, Vector3 from, Vector3 to, PlayerData data)
        {
            Spawn(prefab, from, to, data != null ? data.projectileSpeed : 0f);
        }

        /// <summary>아군 유닛 데이터의 실제 투사체 속도를 사용하는 진입점.</summary>
        public static void Spawn(GameObject prefab, Vector3 from, Vector3 to, AllyUnitData data)
        {
            Spawn(prefab, from, to, data != null ? data.projectileSpeed : 0f);
        }

        /// <summary>
        /// 데이터 컨테이너에서 읽은 속도로 투사체 시간을 계산한다. 속도가 0 이하이면
        /// 프리팹의 기본 시간을 사용해 잘못된 데이터가 연출을 멈추게 하지 않는다.
        /// </summary>
        public static void Spawn(GameObject prefab, Vector3 from, Vector3 to, float projectileSpeed)
        {
            if (prefab == null)
            {
                return;
            }

            GetOrCreate(prefab).Play(from, to, projectileSpeed);
        }

        private float CalculateTravelDuration(Vector3 from, Vector3 to, float projectileSpeed)
        {
            if (projectileSpeed <= 0f)
            {
                return Mathf.Clamp(fallbackTravelDuration, MinimumTravelDuration, MaximumTravelDuration);
            }

            float duration = Vector3.Distance(from, to) / projectileSpeed;
            return Mathf.Clamp(duration, MinimumTravelDuration, MaximumTravelDuration);
        }

        private static Quaternion CalculateRotation(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            return direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(Vector3.forward, direction)
                : Quaternion.identity;
        }

        private static FakeProjectile GetOrCreate(GameObject prefab)
        {
            if (_availablePool.TryGetValue(prefab, out Queue<FakeProjectile> queue) && queue.Count > 0)
            {
                return queue.Dequeue();
            }

            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            FakeProjectile projectile = instance.GetComponent<FakeProjectile>();
            projectile._sourcePrefab = prefab;
            return projectile;
        }

        /// <summary>
        /// 씬 재로드 때 풀에 남은 파괴된 인스턴스 참조를 제거한다. GameManager가 다른
        /// static 세션 캐시와 같은 최선행 Awake에서 호출한다.
        /// </summary>
        public static void ClearPool()
        {
            _availablePool.Clear();
        }
    }
}
