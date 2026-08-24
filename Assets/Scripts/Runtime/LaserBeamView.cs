using System.Collections.Generic;
using RCCom.Runtime.Visuals;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 관통 사격 타워가 명중 즉시 잠깐 그리는 2-Layer 레이저 빔(설계안 §3-②). Inner Core(얇고
    /// 밝은 백색) + Outer Glow(넓고 반투명, 테마색) 두 개의 자식 LineRenderer로 구성되고,
    /// 둘 다 <see cref="LaserBeam"/> 셰이더(신규) 하나를 공유하되 MaterialPropertyBlock으로
    /// 레이어별 색/폭/글로우를 다르게 먹인다 — RangePulseVisualRuntime과 같은 이유로 같은
    /// 머티리얼을 여러 인스턴스/레이어가 서로 다른 값으로 동시에 그릴 수 있다.
    ///
    /// 요구 조건이 다중 LineRenderer/머티리얼 프로퍼티 구동이라 단일 LineRenderer 전제인
    /// <see cref="AttackFlash"/>와 책임이 달라 별도로 새로 짰다 — 풀링 패턴(prefab별 static
    /// Dictionary&lt;GameObject, Queue&lt;T&gt;&gt;)만 그대로 따른다. 판정(PierceDamageEffect의
    /// 원뿔각 판정)과는 완전히 분리된 순수 연출이라, 프리팹이 비어 있거나 이 컴포넌트가 늦게
    /// 나가도 전투에 영향이 없다.
    /// </summary>
    public class LaserBeamView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int SoftEdgeId = Shader.PropertyToID("_SoftEdge");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int LifeFadeId = Shader.PropertyToID("_LifeFade");

        private const float DefaultLifetime = 0.22f;
        private const float DefaultInnerWidth = 0.06f;
        private const float DefaultOuterWidth = 0.28f;
        private const float DefaultPinchStartMultiplier = 1.5f;
        private const float DefaultPinchDuration = 0.08f;

        [SerializeField] private LineRenderer innerCore;
        [SerializeField] private LineRenderer outerGlow;

        // AttackFlash/FakeProjectile/ParticleBurst/ShockwaveRing과 같은 이유로 prefab별 풀을 분리한다.
        private static readonly Dictionary<GameObject, Queue<LaserBeamView>> _availablePool = new();

        private MaterialPropertyBlock _innerProperties;
        private MaterialPropertyBlock _outerProperties;
        private GameObject _sourcePrefab;
        private float _innerBaseWidth;
        private float _outerBaseWidth;
        private float _pinchStartMultiplier;
        private float _pinchDuration;
        private float _remaining;
        private float _duration;
        private bool _isActive;

        private void Awake()
        {
            _innerProperties = new MaterialPropertyBlock();
            _outerProperties = new MaterialPropertyBlock();
            SetLinesEnabled(false);
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            Tick(Time.deltaTime);
        }

        private void Tick(float deltaTime)
        {
            _remaining -= deltaTime;
            float elapsed = Mathf.Max(0f, _duration - _remaining);

            // 발사 직후 폭 pinchStartMultiplier→100% 팬치 애니메이션(설계안 §3-②) — 셰이더가
            // 아니라 잔여시간 기반으로 절차적 계산(AttackFlash.lifetime과 같은 방식).
            float pinchT = _pinchDuration > 0f ? Mathf.Clamp01(elapsed / _pinchDuration) : 1f;
            float widthMultiplier = Mathf.Lerp(_pinchStartMultiplier, 1f, pinchT);
            if (innerCore != null)
            {
                innerCore.widthMultiplier = _innerBaseWidth * widthMultiplier;
            }

            if (outerGlow != null)
            {
                outerGlow.widthMultiplier = _outerBaseWidth * widthMultiplier;
            }

            float lifeFade = _duration > 0f ? Mathf.Clamp01(_remaining / _duration) : 0f;
            ApplyLifeFade(innerCore, _innerProperties, lifeFade);
            ApplyLifeFade(outerGlow, _outerProperties, lifeFade);

            if (_remaining <= 0f)
            {
                Deactivate();
            }
        }

        private void ApplyLifeFade(LineRenderer line, MaterialPropertyBlock properties, float lifeFade)
        {
            if (line == null)
            {
                return;
            }

            line.GetPropertyBlock(properties);
            properties.SetFloat(LifeFadeId, lifeFade);
            line.SetPropertyBlock(properties);
        }

        private void Play(Vector3 from, Vector3 to, LaserBeamVisualEffect visualEffect)
        {
            SetPositions(innerCore, from, to);
            SetPositions(outerGlow, from, to);

            _innerBaseWidth = visualEffect != null ? visualEffect.InnerWidth : DefaultInnerWidth;
            _outerBaseWidth = visualEffect != null ? visualEffect.OuterWidth : DefaultOuterWidth;
            _duration = visualEffect != null ? visualEffect.Lifetime : DefaultLifetime;
            _pinchStartMultiplier =
                visualEffect != null ? visualEffect.PinchStartMultiplier : DefaultPinchStartMultiplier;
            _pinchDuration = Mathf.Min(
                visualEffect != null ? visualEffect.PinchDuration : DefaultPinchDuration, _duration);
            _remaining = _duration;

            ApplyStaticProperties(visualEffect);
            SetLinesEnabled(true);
            _isActive = true;

            // 첫 프레임부터 팬치/페이드가 반영된 상태로 보이도록 즉시 한 번 계산
            // (RangePulseVisualRuntime 생성자가 Tick(0f)를 바로 호출하는 것과 같은 이유).
            Tick(0f);
        }

        private void ApplyStaticProperties(LaserBeamVisualEffect visualEffect)
        {
            if (visualEffect == null)
            {
                // 시각 SO 미배치는 안전한 실패로만 처리 — 머티리얼 원본 기본값으로 재생한다.
                return;
            }

            if (innerCore != null)
            {
                innerCore.GetPropertyBlock(_innerProperties);
                _innerProperties.SetColor(ColorId, visualEffect.InnerColor);
                _innerProperties.SetFloat(GlowIntensityId, visualEffect.InnerGlowIntensity);
                _innerProperties.SetFloat(SoftEdgeId, visualEffect.SoftEdge);
                _innerProperties.SetFloat(NoiseScaleId, visualEffect.NoiseScale);
                _innerProperties.SetFloat(ScrollSpeedId, visualEffect.ScrollSpeed);
                innerCore.SetPropertyBlock(_innerProperties);
            }

            if (outerGlow != null)
            {
                outerGlow.GetPropertyBlock(_outerProperties);
                _outerProperties.SetColor(ColorId, visualEffect.OuterColor);
                _outerProperties.SetFloat(GlowIntensityId, visualEffect.OuterGlowIntensity);
                _outerProperties.SetFloat(SoftEdgeId, visualEffect.SoftEdge);
                _outerProperties.SetFloat(NoiseScaleId, visualEffect.NoiseScale);
                _outerProperties.SetFloat(ScrollSpeedId, visualEffect.ScrollSpeed);
                outerGlow.SetPropertyBlock(_outerProperties);
            }
        }

        private static void SetPositions(LineRenderer line, Vector3 from, Vector3 to)
        {
            if (line == null)
            {
                return;
            }

            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        private void SetLinesEnabled(bool enabled)
        {
            if (innerCore != null)
            {
                innerCore.enabled = enabled;
            }

            if (outerGlow != null)
            {
                outerGlow.enabled = enabled;
            }
        }

        private void Deactivate()
        {
            _isActive = false;
            SetLinesEnabled(false);

            if (_sourcePrefab == null)
            {
                return;
            }

            if (!_availablePool.TryGetValue(_sourcePrefab, out Queue<LaserBeamView> queue))
            {
                queue = new Queue<LaserBeamView>();
                _availablePool[_sourcePrefab] = queue;
            }

            queue.Enqueue(this);
        }

        /// <summary>
        /// prefab을 아직 안 만들었으면 조용히 무시 — 시각 효과 미배치가 공격 로직을 막으면 안 된다.
        /// visualEffect도 같은 이유로 선택 인자다(null이면 머티리얼 기본값으로 재생).
        /// </summary>
        public static void Spawn(
            GameObject prefab, Vector3 from, Vector3 to, LaserBeamVisualEffect visualEffect = null)
        {
            if (prefab == null)
            {
                return;
            }

            GetOrCreate(prefab).Play(from, to, visualEffect);
        }

        private static LaserBeamView GetOrCreate(GameObject prefab)
        {
            if (_availablePool.TryGetValue(prefab, out Queue<LaserBeamView> queue) && queue.Count > 0)
            {
                return queue.Dequeue();
            }

            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            LaserBeamView view = instance.GetComponent<LaserBeamView>();
            view._sourcePrefab = prefab;
            return view;
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
