using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 원샷 파티클 버스트(히트 스파크 등)를 재생하는 풀링 컴포넌트. `AttackFlash`/`FakeProjectile`과
    /// 같은 static 풀(prefab별 <see cref="Queue{T}"/>) 패턴을 그대로 따르되, `ParticleSystem`은
    /// `Stop()` 직후에도 상태가 바로 정리되지 않으므로 반납 전 반드시
    /// `Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear)`로 파티클을 비운다
    /// (VFX_전투_연출_설계안.md §5). Shuriken의 Stop Action은 None으로 설정해 자동
    /// Destroy/Disable을 막고, 반납 타이밍을 이 컴포넌트가 직접 제어한다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleBurst : MonoBehaviour
    {
        // AttackFlash/FakeProjectile과 같은 이유로 prefab별 풀을 분리 — 히트 스파크/관통
        // 방전/폭발 파편/사망 버스트가 전부 이 클래스를 공유해도 서로 섞이지 않는다.
        private static readonly Dictionary<GameObject, Queue<ParticleBurst>> _availablePool = new();

        private ParticleSystem _particleSystem;
        private GameObject _sourcePrefab;
        private bool _isActive;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            // Stop Action = None이라 재생 종료를 코드가 직접 감시해야 한다. withChildren=true로
            // 자식 파티클 시스템(있다면)까지 함께 확인 — 버스트/랜덤 lifetime이 섞여 있어도
            // 정확한 종료 시점을 잡을 수 있다.
            if (_particleSystem.IsAlive(true))
            {
                return;
            }

            Deactivate();
        }

        private void Play(Vector3 position)
        {
            transform.position = position;
            _particleSystem.Play(true);
            _isActive = true;
        }

        private void Deactivate()
        {
            _isActive = false;

            // AttackFlash처럼 enabled = false만으로는 안 된다 — ParticleSystem은 Stop() 직후에도
            // 잔여 파티클이 남아 있을 수 있어 StopEmittingAndClear로 즉시 비운 뒤에만 반납한다.
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (_sourcePrefab == null)
            {
                return;
            }

            if (!_availablePool.TryGetValue(_sourcePrefab, out Queue<ParticleBurst> queue))
            {
                queue = new Queue<ParticleBurst>();
                _availablePool[_sourcePrefab] = queue;
            }

            queue.Enqueue(this);
        }

        /// <summary>prefab을 아직 안 만들었으면 조용히 무시 — 시각 효과 미배치가 공격 로직을 막으면 안 된다.</summary>
        public static void Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                return;
            }

            GetOrCreate(prefab).Play(position);
        }

        private static ParticleBurst GetOrCreate(GameObject prefab)
        {
            if (_availablePool.TryGetValue(prefab, out Queue<ParticleBurst> queue) && queue.Count > 0)
            {
                return queue.Dequeue();
            }

            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            ParticleBurst burst = instance.GetComponent<ParticleBurst>();
            burst._sourcePrefab = prefab;
            return burst;
        }

        /// <summary>
        /// 재시작(Retry, SceneManager.LoadScene) 시 GameManager가 최선행 Awake에서 호출 —
        /// 씬 재로드로 풀 안의 인스턴스들은 파괴되는데 이 정적 대기열은 그대로 남아 죽은
        /// 참조를 들고 있게 되므로(다음 Spawn에서 Dequeue하면 MissingReferenceException)
        /// 명시적으로 비워야 한다.
        /// </summary>
        public static void ClearPool()
        {
            _availablePool.Clear();
        }
    }
}
