using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 미리 그려진 프레임 시퀀스를 지정 fps로 한 번 재생하고 풀에 반납되는 원샷 스프라이트
    /// 플립북. 사망 폭발(설계안 §4 고도화)이 첫 사용처 — 파티클 대신 실제 프레임 애니메이션
    /// 에셋(Assets/Art/VFX/explosion/Sprites)을 쓴다. 로직 없는 순수 렌더러 프리팹(4계층 3번),
    /// AttackFlash.cs와 같은 prefab별 static 풀링 패턴을 따른다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFlipbook : MonoBehaviour
    {
        [Tooltip("재생 순서대로 나열한 프레임들.")]
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float frameRate = 12f;
        [Tooltip("frames[0]의 가장 긴 축이 이 월드 크기가 되도록 자동 스케일(SpriteFit과 동일 계산).")]
        [SerializeField, Min(0.01f)] private float targetVisualSize = 1.5f;

        // AttackFlash/FakeProjectile/ParticleBurst와 같은 이유로 prefab별 풀을 분리한다.
        private static readonly Dictionary<GameObject, Queue<SpriteFlipbook>> _availablePool = new();

        private SpriteRenderer _spriteRenderer;
        private GameObject _sourcePrefab;
        private int _frameIndex;
        private float _frameTimer;
        private bool _isActive;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _spriteRenderer.enabled = false;
        }

        private void Update()
        {
            if (!_isActive || frames == null || frames.Length == 0)
            {
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            _frameTimer += Time.deltaTime;

            while (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _frameIndex++;
                if (_frameIndex >= frames.Length)
                {
                    Deactivate();
                    return;
                }

                _spriteRenderer.sprite = frames[_frameIndex];
            }
        }

        private void Play(Vector3 position)
        {
            transform.position = position;
            transform.rotation = Quaternion.identity;

            if (frames != null && frames.Length > 0 && frames[0] != null)
            {
                float scale = SpriteFit.CalculateUniformScale(frames[0], targetVisualSize);
                transform.localScale = new Vector3(scale, scale, 1f);
                _spriteRenderer.sprite = frames[0];
            }

            _frameIndex = 0;
            _frameTimer = 0f;
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

            if (!_availablePool.TryGetValue(_sourcePrefab, out Queue<SpriteFlipbook> queue))
            {
                queue = new Queue<SpriteFlipbook>();
                _availablePool[_sourcePrefab] = queue;
            }

            queue.Enqueue(this);
        }

        /// <summary>prefab을 아직 안 만들었으면 조용히 무시 — 시각 효과 미배치가 사망 처리를 막으면 안 된다.</summary>
        public static void Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                return;
            }

            GetOrCreate(prefab).Play(position);
        }

        private static SpriteFlipbook GetOrCreate(GameObject prefab)
        {
            if (_availablePool.TryGetValue(prefab, out Queue<SpriteFlipbook> queue) && queue.Count > 0)
            {
                return queue.Dequeue();
            }

            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            SpriteFlipbook flipbook = instance.GetComponent<SpriteFlipbook>();
            flipbook._sourcePrefab = prefab;
            return flipbook;
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
