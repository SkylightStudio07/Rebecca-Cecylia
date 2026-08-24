using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 스플래시 착탄 지점에서 한 번만 재생되고 풀에 반납되는 원샷 충격파 링(설계안 §3-③).
    /// 아군 유닛 하나에 계속 붙어 자기 사거리 기준으로 무한 반복 재생되는
    /// <see cref="Visuals.RangePulseVisualRuntime"/>(상시 오라, 유닛 생존 동안 파괴되지 않음)와는
    /// 성격이 달라 그대로 재사용할 수 없었다 — 이쪽은 위치 기반 원샷이라 AttackFlash.cs와 같은
    /// prefab별 static 풀링 패턴을 따른다. 셰이더/머티리얼은 새로 만들지 않고 기존
    /// RangePulseAura.shader 머티리얼을 MaterialPropertyBlock으로 공유한다(같은 머티리얼을
    /// 여러 인스턴스가 서로 다른 _Progress로 동시에 그릴 수 있는 이유는 RangePulseVisualRuntime과
    /// 동일 — 프로퍼티를 머티리얼이 아니라 렌더러별 블록에 저장하기 때문).
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class ShockwaveRing : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");

        private const float MinimumPlayDuration = 0.15f;
        private const float MaximumPlayDuration = 0.2f;

        [Tooltip("링이 반경 끝까지 확산하는 데 걸리는 시간(초). 0.15~0.2초로 제한된다.")]
        [SerializeField, Min(MinimumPlayDuration)] private float playDuration = 0.18f;

        // AttackFlash/FakeProjectile/ParticleBurst와 같은 이유로 prefab별 풀을 분리한다.
        private static readonly Dictionary<GameObject, Queue<ShockwaveRing>> _availablePool = new();

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _properties;
        private GameObject _sourcePrefab;
        private float _remaining;
        private float _duration;
        private bool _isActive;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _properties = new MaterialPropertyBlock();
            _renderer.enabled = false;
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            _remaining -= Time.deltaTime;
            float progress = _duration > 0f ? 1f - Mathf.Clamp01(_remaining / _duration) : 1f;

            // OutQuad 이징(설계안 §3-③): 초반에 빠르게 확산하다 끝에서 서서히 느려진다.
            float eased = 1f - (1f - progress) * (1f - progress);
            _renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(ProgressId, eased);
            _renderer.SetPropertyBlock(_properties);

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

            _duration = Mathf.Clamp(playDuration, MinimumPlayDuration, MaximumPlayDuration);
            _remaining = _duration;

            _renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(ProgressId, 0f);
            _renderer.SetPropertyBlock(_properties);

            _renderer.enabled = true;
            _isActive = true;
        }

        private void Deactivate()
        {
            _isActive = false;
            _renderer.enabled = false;

            if (_sourcePrefab == null)
            {
                return;
            }

            if (!_availablePool.TryGetValue(_sourcePrefab, out Queue<ShockwaveRing> queue))
            {
                queue = new Queue<ShockwaveRing>();
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

        private static ShockwaveRing GetOrCreate(GameObject prefab)
        {
            if (_availablePool.TryGetValue(prefab, out Queue<ShockwaveRing> queue) && queue.Count > 0)
            {
                return queue.Dequeue();
            }

            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            ShockwaveRing ring = instance.GetComponent<ShockwaveRing>();
            ring._sourcePrefab = prefab;
            return ring;
        }

        /// <summary>
        /// 재시작(Retry, SceneManager.LoadScene) 시 GameManager가 호출 — 씬 재로드로 풀 안의
        /// 인스턴스들은 파괴되는데, 이 정적 대기열은 그대로 남아 죽은 참조를 들고 있게 되므로
        /// 명시적으로 비워야 한다.
        /// </summary>
        public static void ClearPool()
        {
            _availablePool.Clear();
        }
    }
}
