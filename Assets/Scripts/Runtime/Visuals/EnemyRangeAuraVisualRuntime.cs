using RCCom.Effects.Enemy;
using UnityEngine;
using UnityEngine.Rendering;

namespace RCCom.Runtime.Visuals
{
    /// <summary>
    /// 적 오라의 실제 판정 반경을 월드 공간의 지속 링으로 표시한다. EnemyView 루트의 회전과
    /// 스프라이트 맞춤 배율을 상속하지 않도록 별도 오브젝트로 생성한다.
    /// </summary>
    public sealed class EnemyRangeAuraVisualRuntime
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int StrokeWidthId = Shader.PropertyToID("_StrokeWidth");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private readonly EnemyView _view;
        private readonly EnemyInstance _instance;
        private readonly GameObject _visualObject;
        private readonly MeshRenderer _renderer;
        private readonly MaterialPropertyBlock _properties = new();
        private float _lastRange = -1f;

        public EnemyRangeAuraVisualRuntime(
            IEnemyRangeAuraVisualEffect definition,
            EnemyView view,
            EnemyInstance instance,
            int sortingLayerId,
            int sortingOrder)
        {
            _view = view;
            _instance = instance;
            _visualObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _visualObject.name = $"EnemyRangeAura_{view.GetInstanceID()}";
            _visualObject.layer = view.gameObject.layer;

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
            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.allowOcclusionWhenDynamic = false;

            _renderer.GetPropertyBlock(_properties);
            _properties.SetColor(ColorId, definition.AuraColor);
            _properties.SetFloat(StrokeWidthId, definition.StrokeWidth);
            _properties.SetFloat(GlowIntensityId, definition.GlowIntensity);
            _properties.SetFloat(OpacityId, definition.Opacity);
            _renderer.SetPropertyBlock(_properties);
            Tick();
        }

        public void Tick()
        {
            if (_visualObject == null || _renderer == null || _instance == null ||
                !_instance.IsAlive || _instance.Data == null)
            {
                if (_renderer != null)
                {
                    _renderer.enabled = false;
                }
                return;
            }

            float range = Mathf.Max(0f, _instance.Data.attackRange);
            _renderer.enabled = range > 0f;
            Vector2 position = _instance.position;
            float z = _view != null ? _view.transform.position.z : 0f;
            _visualObject.transform.SetPositionAndRotation(
                new Vector3(position.x, position.y, z),
                Quaternion.identity);

            if (!Mathf.Approximately(range, _lastRange))
            {
                // 셰이더 링 중심이 UV 반경 0.94에 있으므로 실제 월드 반경이 판정 range와
                // 정확히 맞도록 Quad 전체 크기를 그 비율만큼 보정한다.
                float diameter = range * 2f / 0.94f;
                _visualObject.transform.localScale = new Vector3(diameter, diameter, 1f);
                _lastRange = range;
            }
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
    }
}
