using RCCom.Effects.Enemy;
using UnityEngine;
using UnityEngine.Rendering;

namespace RCCom.Runtime.Visuals
{
    /// <summary>
    /// EnemyInstance의 논리적 이동 방향을 따라 회전하는 월드 공간 반원 방어막. View 루트의
    /// 스프라이트 보정각·크기를 상속하지 않아 피해 방향 판정과 시각 방향이 항상 일치한다.
    /// </summary>
    public sealed class EnemyFrontShieldVisualRuntime
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int StrokeWidthId = Shader.PropertyToID("_StrokeWidth");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int FillOpacityId = Shader.PropertyToID("_FillOpacity");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private readonly IEnemyFrontShieldVisualEffect _definition;
        private readonly EnemyView _view;
        private readonly EnemyInstance _instance;
        private readonly GameObject _visualObject;
        private readonly MeshRenderer _renderer;
        private readonly MaterialPropertyBlock _properties = new();

        public EnemyFrontShieldVisualRuntime(
            IEnemyFrontShieldVisualEffect definition,
            EnemyView view,
            EnemyInstance instance,
            int sortingLayerId,
            int sortingOrder)
        {
            _definition = definition;
            _view = view;
            _instance = instance;
            _visualObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _visualObject.name = $"EnemyFrontShield_{view.GetInstanceID()}";
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
            _properties.SetColor(ColorId, definition.ShieldColor);
            _properties.SetFloat(StrokeWidthId, definition.StrokeWidth);
            _properties.SetFloat(GlowIntensityId, definition.GlowIntensity);
            _properties.SetFloat(FillOpacityId, definition.FillOpacity);
            _properties.SetFloat(OpacityId, definition.Opacity);
            _renderer.SetPropertyBlock(_properties);
            Tick();
        }

        public void Tick()
        {
            if (_visualObject == null || _renderer == null || _instance == null || !_instance.IsAlive)
            {
                if (_renderer != null)
                {
                    _renderer.enabled = false;
                }
                return;
            }

            Vector2 facing = _instance.FacingDirection;
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            Vector2 position = _instance.position;
            float z = _view != null ? _view.transform.position.z : 0f;
            _visualObject.transform.SetPositionAndRotation(
                new Vector3(position.x, position.y, z),
                Quaternion.Euler(0f, 0f, angle));

            // 셰이더의 외곽선 중심이 로컬 반경 0.92에 있으므로 실제 방어막 반경과 맞춘다.
            float diameter = _definition.ShieldRadius * 2f / 0.92f;
            _visualObject.transform.localScale = new Vector3(diameter, diameter, 1f);
            _renderer.enabled = true;
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
