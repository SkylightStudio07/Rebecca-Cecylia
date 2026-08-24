using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 스플래시 착탄 지점에 짧게 남는 바닥 그을림 자국(설계안 §3-③). 로직 없는 순수
    /// 렌더러 프리팹 — 판정과 완전히 분리돼 있어 늦게 나가거나 프리팹이 비어 있어도
    /// 전투에 영향이 없다. AttackFlash.cs와 같은 prefab별 static 풀링 패턴을 그대로 따른다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ScorchDecal : MonoBehaviour
    {
        [Tooltip("자국이 나타났다 사라지기까지 걸리는 시간(초).")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.3f;

        [Tooltip("최대 불투명도. 바닥 위에 살짝만 얹히도록 기본값을 낮게 둔다.")]
        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.6f;

        // AttackFlash/FakeProjectile/ParticleBurst/ShockwaveRing과 같은 이유로 prefab별 풀을 분리한다.
        private static readonly Dictionary<GameObject, Queue<ScorchDecal>> _availablePool = new();

        private SpriteRenderer _spriteRenderer;
        private GameObject _sourcePrefab;
        private Color _baseColor;
        private float _remaining;
        private float _duration;
        private bool _isActive;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _baseColor = _spriteRenderer.color;
            _spriteRenderer.enabled = false;
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            _remaining -= Time.deltaTime;
            // 등장 없이 바로 최대치로 찍힌 뒤 남은 수명 동안 페이드아웃 — 폭발 잔흔은 서서히
            // 나타날 이유가 없어 히트플래시류보다 단순하다.
            float alpha = _duration > 0f ? peakAlpha * Mathf.Clamp01(_remaining / _duration) : 0f;
            Color color = _baseColor;
            color.a = alpha;
            _spriteRenderer.color = color;

            if (_remaining <= 0f)
            {
                Deactivate();
            }
        }

        private void Play(Vector3 position, float radius)
        {
            transform.position = position;
            float diameter = Mathf.Max(0.01f, radius) * 2f;
            transform.localScale = new Vector3(diameter, diameter, 1f);

            _duration = Mathf.Max(0.05f, lifetime);
            _remaining = _duration;

            Color color = _baseColor;
            color.a = peakAlpha;
            _spriteRenderer.color = color;

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

            if (!_availablePool.TryGetValue(_sourcePrefab, out Queue<ScorchDecal> queue))
            {
                queue = new Queue<ScorchDecal>();
                _availablePool[_sourcePrefab] = queue;
            }

            queue.Enqueue(this);
        }

        /// <summary>prefab을 아직 안 만들었으면 조용히 무시 — 시각 효과 미배치가 공격 로직을 막으면 안 된다.</summary>
        public static void Spawn(GameObject prefab, Vector3 position, float radius)
        {
            if (prefab == null)
            {
                return;
            }

            GetOrCreate(prefab).Play(position, radius);
        }

        private static ScorchDecal GetOrCreate(GameObject prefab)
        {
            if (_availablePool.TryGetValue(prefab, out Queue<ScorchDecal> queue) && queue.Count > 0)
            {
                return queue.Dequeue();
            }

            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            ScorchDecal decal = instance.GetComponent<ScorchDecal>();
            decal._sourcePrefab = prefab;
            return decal;
        }

        /// <summary>재시작(Retry) 시 GameManager가 호출 — 씬 재로드로 죽은 참조가 남지 않게 비운다.</summary>
        public static void ClearPool()
        {
            _availablePool.Clear();
        }
    }
}
