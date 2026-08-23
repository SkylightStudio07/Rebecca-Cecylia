using RCCom.Effects.UnitVisual.Concrete;
using UnityEngine;
using UnityEngine.Rendering;

namespace RCCom.Runtime.Visuals
{
    /// <summary>
    /// 사거리 링 1개의 Renderer와 파동 진행도를 보유한다. View 루트는 스프라이트 맞춤 스케일과
    /// 방향 회전을 사용하므로 링은 자식이 아닌 월드 공간 오브젝트로 두어 실제 반경을 보존한다.
    /// </summary>
    public sealed class RangePulseVisualRuntime : IAllyUnitVisualRuntime
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int StrokeWidthId = Shader.PropertyToID("_StrokeWidth");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int GlossIntensityId = Shader.PropertyToID("_GlossIntensity");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private readonly RangePulseVisualEffect _definition;
        private readonly AllyUnitVisualContext _context;
        private readonly GameObject _visualObject;
        private readonly MeshRenderer _renderer;
        private readonly MaterialPropertyBlock _properties = new();
        private float _phase;
        private float _lastRange = -1f;

        public RangePulseVisualRuntime(
            RangePulseVisualEffect definition,
            AllyUnitVisualContext context)
        {
            _definition = definition;
            _context = context;
            _visualObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _visualObject.name = $"RangePulseVisual_{context.view.GetInstanceID()}";
            _visualObject.layer = context.view.gameObject.layer;

            Collider collider = _visualObject.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(collider);
                }
                else
                {
                    Object.DestroyImmediate(collider);
                }
            }

            _renderer = _visualObject.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = definition.Material;
            _renderer.sortingLayerID = context.sortingLayerId;
            _renderer.sortingOrder = context.sortingOrder;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.allowOcclusionWhenDynamic = false;

            ApplyStaticProperties();
            Tick(0f);
        }

        public void Tick(float deltaTime)
        {
            if (_visualObject == null || _renderer == null || _context.instance == null ||
                !_context.instance.IsAlive || _context.instance.Data == null)
            {
                if (_renderer != null)
                {
                    _renderer.enabled = false;
                }

                return;
            }

            Vector2 position = _context.instance.Position;
            float z = _context.view != null ? _context.view.transform.position.z : 0f;
            _visualObject.transform.SetPositionAndRotation(
                new Vector3(position.x, position.y, z),
                Quaternion.identity);

            float range = Mathf.Max(0f, _context.instance.Data.attackRange);
            if (!Mathf.Approximately(range, _lastRange))
            {
                float diameter = range * 2f;
                _visualObject.transform.localScale = new Vector3(diameter, diameter, 1f);
                _lastRange = range;
            }

            float duration = _definition.PulseDuration;
            float cycle = duration + _definition.PulseInterval;
            _phase = cycle > 0f ? Mathf.Repeat(_phase + Mathf.Max(0f, deltaTime), cycle) : 0f;
            bool isPulseVisible = range > 0f && _phase <= duration;
            _renderer.enabled = isPulseVisible;
            if (!isPulseVisible)
            {
                return;
            }

            _renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(ProgressId, Mathf.Clamp01(_phase / duration));
            _renderer.SetPropertyBlock(_properties);
        }

        public void Dispose()
        {
            if (_visualObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(_visualObject);
            }
            else
            {
                Object.DestroyImmediate(_visualObject);
            }
        }

        private void ApplyStaticProperties()
        {
            _renderer.GetPropertyBlock(_properties);
            _properties.SetColor(ColorId, _definition.AuraColor);
            _properties.SetFloat(StrokeWidthId, _definition.StrokeWidth);
            _properties.SetFloat(GlowIntensityId, _definition.GlowIntensity);
            _properties.SetFloat(GlossIntensityId, _definition.GlossIntensity);
            _properties.SetFloat(OpacityId, _definition.Opacity);
            _renderer.SetPropertyBlock(_properties);
        }
    }
}
